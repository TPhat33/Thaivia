# ADR-0027: Route-cache invalidation contract (OD route caching, closes the ADR-0024 p95 miss)

- สถานะ: Accepted
- วันที่: 2026-09-26
- ผู้เกี่ยวข้อง: wave 7 / G6 debt-paydown (Opus supervisor brief), spec §15
- Supersedes: ADR-0024's "deferred to session ถัดไป" note on route-result caching (การวินิจฉัย root cause ของ ADR-0024 ยังถูกต้องทั้งหมด — ADR นี้แค่ปิด gap ที่ ADR-0024 บอกไว้ตรงๆ ว่ายังไม่ได้แก้)

## บริบท

ADR-0024 วัด p95 MISS จริง (~6.1-6.5ms บน budget 5ms) และวินิจฉัยสาเหตุตรงๆ
ว่ามาจาก **>180 การเรียก Dijkstra ที่ไม่มี cache ต่อ tick**:
`TripDemandGenerator.FindNearestJobNode` (cohort x job-building ทุกคู่) และ
`WorldState.StepBusRoutes`'s walking-reach check (cohort x bus stop ทุกคู่)
เรียก `MobilityGraph.ShortestDistanceMeters` ใหม่ทุกครั้งแม้คำตอบจะไม่
เปลี่ยนเลยตราบใดที่ topology เดิม ADR-0024 บันทึกไว้ตรงๆ ว่า "ยังไม่ได้แก้
ในรอบนี้" และระบุจุดเริ่มต้นที่แนะนำไว้ล่วงหน้า

## การตัดสินใจ

### 1. Cache อยู่ที่ตัว MobilityGraph instance เอง ไม่ใช่ WorldState-level dictionary แยก

`MobilityGraph` เพิ่ม `_staticRouteCache: Dictionary<(long From, long To),
Route?>` เก็บผลลัพธ์ของ `ShortestRoute(from, to)` แบบไม่มี cost multiplier
(การเรียกแบบเดิมทุกที่ก่อนหน้านี้) เหตุผลที่วางไว้ตรงนี้แทนที่จะเป็น
dictionary แยกใน `WorldState`:

- `MobilityGraph` เป็น **immutable หลังสร้างเสร็จ** (adjacency list สร้าง
  ใน constructor ครั้งเดียว ไม่มี method ไหน mutate มันอีก) ดังนั้น route
  ที่ resolve จาก instance หนึ่งจะไม่มีทาง stale ตราบใดที่ยังใช้ instance
  เดิมอยู่ — invalidation contract ทั้งหมดจึงเท่ากับ "ใช้ instance ใหม่เมื่อ
  ไหร่" ไม่ต้องมี flag/timestamp/epoch แยกที่อาจลืม sync
- ผลคือ cache ไม่มีทาง stale โดยโครงสร้าง (structurally impossible) ไม่ใช่
  แค่ "พยายาม invalidate ให้ถูกทุกจุด" — คำสั่งจาก supervisor ที่ว่า "a
  stale-route bug would be worse than the perf miss" นี่คือคำตอบต่อคำสั่ง
  นั้นโดยตรง: ไม่มี invalidation logic ให้พลาดเลยที่ระดับ cache เอง
  ภาระทั้งหมดตกไปที่ "WorldState เรียก GetOrBuildVehicleGraph/
  GetOrBuildWalkGraph ถูกจังหวะหรือเปล่า" (ข้อ 2 ด้านล่าง)

Route ที่หาไม่เจอ (unreachable) ก็ cache เป็น `null` เหมือนกัน (แยกจาก
"ยังไม่เคยถาม" ด้วย `TryGetValue`'s bool) ไม่งั้นคู่ปลายทางที่ไปไม่ถึงจะ
ถูกค้นหาซ้ำทุก tick เหมือนเดิม

### 2. WorldState's invalidation signal ขยายจาก "นับ segment" เป็น "segment count + closed-way set"

ของเดิม (ADR-0024) ใช้ `_plannedRoadSegments.Count` อย่างเดียวเป็น
invalidation signal (นับได้เพิ่มขึ้นอย่างเดียว ไม่เคยถูกแทนที่/ลบ) ADR
นี้เพิ่มสัญญาณที่สอง: **ชุด way id ที่ถูกปิดสนิท (hard closure)** จาก
`RoadWorksZone` ที่ `CapacityMultiplierDuringConstruction <= 0` และ
active อยู่ที่ tick ปัจจุบัน (`RoadWorksZone`'s doc comment เดิมพูดไว้
ตรงๆ อยู่แล้วว่า "callers that truly need a hard closure can still pass
0.0" — ADR นี้แค่ทำให้ multiplier 0 มีผลจริงต่อการ routing ไม่ใช่แค่ต่อ
capacity เหมือนก่อนหน้านี้)

`WorldState.ComputeClosedWayIds(tick)` คำนวณชุดนี้ใหม่ **ทุก tick**
(O(active zones) เท่านั้น ไม่ใช่ O(cohorts) หรือ Dijkstra — เข้ากับสิ่งที่
ADR-0024 วินิจฉัยไว้ว่าไม่ใช่ bottleneck) เหตุผลที่ต้องคำนวณใหม่ทุก tick
แทนที่จะเป็น monotonic counter เหมือน segment: zone หนึ่ง "จบ" ได้เองจาก
เวลาผ่านไปเฉยๆ (`IsActiveAt(tick)` กลายเป็น false) โดยไม่มี method call
ไหนถูกเรียกเลย — ถ้าใช้ counter ที่เพิ่มเฉพาะตอน `AddRoadWorksZone` cache
จะไม่มีทาง invalidate ตอน zone หมดอายุตามธรรมชาติ

`MobilityGraph`'s constructor รับ `closedWayIds` เพิ่มเป็น optional
parameter: way ที่อยู่ใน set นี้จะไม่ถูกใส่ใน adjacency list เลย (ไม่ใช่
"ทำให้แพง" แบบ Debt 2's congestion cost — เป็นการตัดออกจริง เพราะ
capacity 0 แปลว่ารถ 0 คันผ่านได้จริง) **ใช้เฉพาะ
Vehicle/Freight** — Walk ไม่ถูกกระทบ เพราะถนนที่ปิดสำหรับรถไม่ได้แปลว่า
ทางเท้า/ไหล่ทางข้างๆ ก็ปิดด้วย (ไม่มีข้อมูลรองรับสมมติฐานนั้น — AGENTS.md
ข้อ 4)

`RoadWorksHardClosure_StopsAndThenResumesRouting_ExactlyAcrossTheZonesActiveWindow`
(Thaivia.Core.Tests) คือ test ที่ supervisor ขอตรงๆ ("one that would fail
if the cache were never invalidated"): ปิดถนนเส้นเดียวที่เชื่อม cohort
กับอาคารมีงานเส้นเดียวในโลกทดสอบ ยืนยันว่า arrivals **นิ่งสนิท**ตลอด
ช่วง active window (ถ้า cache ไม่ invalidate ตอน zone เริ่ม รถจะยังวิ่งผ่าน
เส้นทางเดิมที่ cache ไว้ก่อนปิด) และ**กลับมาวิ่งใหม่**หลัง window
หมดอายุตามธรรมชาติ (ถ้า cache invalidate ครั้งเดียวตอนปิดแต่ไม่กลับมา
rebuild อีกตอนเปิด จะนิ่งตลอดไป — พิสูจน์ทั้งสองทิศทางของบั๊ก)
`RoadWorksSoftDegradation_NeverRemovesTheWayFromRouting_OnlyAHardZeroMultiplierDoes`
เป็น negative control: zone ที่ multiplier > 0 (ทางเดียวที่
`PlanningEngine.CommitRoadWorks` ยอมให้ผ่านจริง — validate เป็น (0,1])
ต้องไม่ตัดถนนออกจาก routing เลย มีแค่ capacity ลดเท่านั้น

### 3. Gateway open/close ไม่อยู่ใน invalidation signal — ตั้งใจ ไม่ใช่ช่องโหว่

ตรวจสอบแล้วตรงๆ: `Gateway`/`GatewayFlow` ไม่เคยถูกอ่านจาก
`MobilityGraph`'s constructor เลย และ `WorldState.StepGateways` ไม่แตะ
`LinkQueueSimulator`/arrivalsByWay เลย (มัน generate/step demand ของ
`GatewayFlow` แยกบัญชีต่างหากทั้งหมด) — การปิด/เปิด gateway จึงไม่มีทาง
เปลี่ยนคำตอบของ `ShortestRoute` คู่ไหนเลย ไม่ใช่เพราะไม่ได้ใส่ใจ
`ClosingAGateway_NeverChangesVehicleRoutingOrLinkArrivals`
(Thaivia.Core.Tests) พิสูจน์ตรงนี้ตรงๆ: สอง WorldState seed เดียวกัน โลก
หนึ่งปิด gateway ตั้งแต่ tick 0 อีกโลกเปิดตลอด รัน 200 tick แล้ว
`LinkQueues.TotalArrived` ต้องเท่ากันทุก key เป๊ะ — ถ้าการปิด gateway
เคยมีผลต่อ routing จริง test นี้จะ fail ทันที

ถ้าในอนาคตมี feature ที่ทำให้ gateway ปิดแล้วบล็อกการเดินทางภายในจริง
(เช่น cross-boundary routing ผ่าน gateway node) invalidation signal นี้
ต้องขยายตอนนั้น — ไม่ใช่ตอนนี้ ที่ยังไม่มี behavior แบบนั้นอยู่จริง

### 4. Dynamic (congestion-aware) route query ไม่ cache เลย

`MobilityGraph.ShortestRoute(from, to, wayCostMultiplier)` เมื่อ
`wayCostMultiplier` ไม่ใช่ null (ใช้เฉพาะโดย
`NetworkDemandAssignment.AssignToWaysCongestionAware` — ดู ADR-0028) จะ
**ไม่แตะ `_staticRouteCache` เลย ทั้งอ่านและเขียน** เพราะคำตอบตั้งใจให้
เปลี่ยนได้ทุกครั้งที่ congestion เปลี่ยน (ทั้งข้าม slice ภายใน tick เดียว
และข้าม tick) — cache มันจะเป็นบั๊ก stale ตัวเดียวกับที่ ADR นี้ทั้งฉบับ
ตั้งใจปิดกั้น ไม่ใช่ optimization ที่ถูกต้อง `Route.DistanceMeters` ที่คืน
มาจาก path นี้ยังเป็นระยะทางจริง (metres จริง ไม่ใช่ weighted cost) เสมอ
เพราะ caller อื่น (safety proximity, accessibility, bus walking reach)
อ่านค่านี้เป็นระยะทางจริงอยู่แล้ว — ดู `MobilityGraph.ComputeShortestRoute`'s
doc comment สำหรับกลไกที่แยก cost (ใช้หา path) ออกจาก physical distance
(ใช้รายงาน) อย่างชัดเจน

## ผลที่วัดได้ (ก่อน Debt 2's congestion cost, caching อย่างเดียว)

Release build, scenario เดียวกับ ADR-0024 เป๊ะ (8x8 grid, 9,000 tick,
100-tick warm-up — ดู `docs/evidence/g6-benchmark-after-debt1.log`):

```
ก่อน (ADR-0024): p50: ~5.2-5.7 ms | p95: ~6.1-6.5 ms | max: ~36-41 ms | mean: ~5.2-5.7 ms -- MISS
หลัง (caching, ADR นี้): p50: 0.1569 ms | p95: 0.2846 ms | max: 11.9563 ms | mean: 0.1583 ms -- PASS
```

p95 ลดลง ~95% ยืนยันการวินิจฉัยของ ADR-0024 ว่าการเรียก Dijkstra ซ้ำที่ไม่
มี cache คือ bottleneck จริง ไม่ใช่การสร้าง adjacency list (ที่ ADR-0024
เองก็เคย cache แล้วได้ผลแค่ ~5-6%) `max` ที่ยังเหลืออยู่ (~12-22ms
ในการรันหลายรอบ) มาจาก GC gen0/gen1 pause เป็นหลัก — วัดตรงด้วย
`GC.CollectionCount` ต่อ tick แล้วพบว่า tick ที่ช้าที่สุดสองอันดับ
(18.65ms, 13.55ms) ตรงกับ tick ที่เกิด GC collection พอดี ส่วน tick ที่
ช้ารองลงมา (3-6ms) กระจุกตัวอยู่ในช่วง ~250 tick แรกหลัง warm-up
สอดคล้องกับ JIT tier-up ที่ยังไม่จบภายใน 100-tick warm-up ของ harness นี้
(ดู `docs/evidence/g6-benchmark-max-spike-diagnosis.log`) — ไม่ใช่ปัญหา
เชิง algorithm

ตัวเลขหลัง Debt 2 (congestion-aware assignment) รวมด้วยแล้ว อยู่ใน
ADR-0028

## ผลกระทบ

- `MobilityGraph` constructor มี parameter ใหม่ `closedWayIds` (optional,
  default null) — ทุก call site เดิมไม่ต้องแก้ (unaffected)
- `MobilityGraph.ShortestRoute` เปลี่ยนจาก 2-arg เป็น 3-arg โดย arg ที่ 3
  เป็น optional (`Func<long, double>? wayCostMultiplier = null`) — call
  site เดิมทั้งหมด (`ShortestDistanceMeters`, `PlanningEngine`,
  `BusRouteScheduler`) ไม่ต้องแก้
- `WorldState.StepLinkQueues`/`GetOrBuildVehicleGraph` เปลี่ยน signature
  ภายใน (private) — ไม่กระทบ public API
- Test ใหม่: `RouteCacheInvalidationTests` (3 test) พิสูจน์ invalidation
  contract ทั้งสามกรณี (road connector — ยืนยันซ้ำผ่าน test เดิมที่ยังผ่าน,
  road-works hard/soft closure, gateway open/close) ไม่มี test เดิมถูกแก้
  ให้อ่อนลง
