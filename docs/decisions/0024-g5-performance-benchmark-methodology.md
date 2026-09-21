# ADR-0024: Performance benchmark methodology, MobilityGraph caching, honest p95 miss

- สถานะ: Accepted
- วันที่: 2026-09-21
- ผู้เกี่ยวข้อง: wave 6 / g5, spec §15

## บริบท

Plan §15 ระบุ target แบบ "experimental" (fixed tick 5Hz, p95 work per tick
<= 5ms) แต่ระบุตรงๆ ว่าเป็น target ไม่ใช่ค่าที่วัดจริง จนถึง wave นี้
ไม่เคยมีการวัดจริงเลยเพราะ `SimulateTick` เป็น stub (ดู ADR-0023) ตอนนี้
loop รันจริงแล้ว จึงวัดได้จริงเป็นครั้งแรก

## การตัดสินใจ

### Harness: `dotnet test`, ไม่ใช่ console project แยก

`PerformanceBenchmarkTests` อยู่ใน `Thaivia.Core.Tests` เดียวกับ test อื่น
ทั้งหมด เหตุผล: (1) วัดจาก build เดียวกับที่ test อื่นพิสูจน์ความถูกต้อง
ไว้แล้ว ไม่ต้อง maintain artifact แยกที่อาจ drift, (2) benchmark นี้เป็น
fixed-tick-rate simulation loop ไม่ใช่ workload ที่ต้องการ profiler/GUI
พิเศษ ITestOutputHelper พอสำหรับ raw output แล้ว, (3) `dotnet test -c
Release` ให้ผลที่ representative ของ build ที่จะ ship จริงมากกว่า Debug

### ไม่ hard-assert 5ms budget เป็น xUnit assertion

Test พิมพ์ p50/p95/max/mean ผ่าน `_output.WriteLine` เสมอ และ assert
เฉพาะ regression guard หยาบๆ (`max < 2000ms`, จับเฉพาะ blow-up ระดับ
pathological) — **ไม่** assert `p95 <= 5` เพราะโจทย์สั่งชัดว่า budget ที่
พลาดต้องถูก**รายงานพร้อมตัวเลขจริง** ไม่ใช่ทำให้ `dotnet test` แดงทั้งชุด
บน target ที่ spec เองระบุว่า "experimental" และไม่ทำให้ scenario เล็กลง
จนผ่านแล้วเรียกว่า pass — scenario size (`PerformanceBenchmarkFixtures.
GridSize = 8`) ถูก fix ไว้ก่อนอ่านผลครั้งแรก ไม่เคยถูกปรับลดหลังเห็นผล

### Scenario: 8x8 synthetic grid — ไม่ใช่ pilot AOI จริง

ยังไม่มีข้อมูล OSM จริง (ADR-0003) จึงไม่มีทาง benchmark บน "pilot-sized"
scenario จริงได้ `PerformanceBenchmarkFixtures` สร้าง grid สังเคราะห์
8x8 (64 node, 112 edge, 32 อาคาร, cohort residential ~6 ตัว, signal 4
ตัว, bus route 1 สาย, incident site 3 จุด, gateway 1 จุด) — ระบุไว้ตรงๆ
ใน test output ทุกครั้งที่รันว่า "THIS IS A SYNTHETIC STAND-IN, NOT THE
REAL PILOT AOI" ตัวเลขที่วัดได้ผูกกับขนาด scenario นี้เท่านั้น ไม่ใช่
prediction ของ pilot AOI จริง

### ผลที่วัดได้ (Release build, 9,000 tick = 30 นาทีเกมที่ 5Hz, 100-tick warm-up)

```
p50: ~5.2-5.7 ms | p95: ~6.1-6.5 ms | max: ~36-41 ms | mean: ~5.2-5.7 ms
Budget (p95 <= 5ms): MISS
```

(ตัวเลขจริงจาก run ล่าสุด: `docs/evidence/g5-benchmark-simulatetick.log`)
**MISS จริง รายงานตรงๆ ไม่ปิดบัง** — p95 เกิน budget ~24-30%

### MobilityGraph caching — optimization จริงที่ทำ ไม่ใช่การ "โกง" benchmark

ก่อนวัดครั้งแรก `SimulateTick` สร้าง `MobilityGraph` ใหม่ (Vehicle +
Walk) ทุก tick แม้ topology จะไม่เปลี่ยนเลยระหว่าง tick ส่วนใหญ่ เพิ่ม
`WorldState.GetOrBuildVehicleGraph`/`GetOrBuildWalkGraph` cache ที่ keyed
ด้วยจำนวน `_plannedRoadSegments` (นับได้เพิ่มขึ้นอย่างเดียว ไม่เคยถูก
แทนที่/ลบ — นับจึงเป็น invalidation signal ที่ถูกต้อง 100%, ไม่ใช่
heuristic) ผลลัพธ์การ routing เหมือนเดิมทุกประการ (ไม่ใช่ approximation)
วัดผล: p95 ลดจาก ~6.5ms เหลือ ~6.1-6.2ms — ปรับปรุงจริงแต่เล็กน้อย
(~5-6%) เพราะ**การสร้าง graph ไม่ใช่ต้นทุนหลัก**

### สาเหตุที่แท้จริงของ p95 miss (วินิจฉัยตรงๆ ไม่เดา)

ต้นทุนหลักต่อ tick คือจำนวนครั้งที่เรียก Dijkstra (ผ่าน
`MobilityGraph.ShortestRoute`/`ShortestDistanceMeters`) ซ้ำๆ โดยไม่มี
cache: `TripDemandGenerator.FindNearestJobNode` เรียก Dijkstra แยกต่างหาก
ต่อ (cohort x job-bearing building) คู่ ทุก tick แม้คำตอบจะไม่เปลี่ยนเลย
ตราบใดที่ topology เดิม (`NetworkDemandAssignment` ก็ re-run Dijkstra ซ้ำ
อีกรอบสำหรับเส้นทางเดียวกัน), และ `StepBusRoutes`'s walking-reach check
เรียก Dijkstra ต่อ (cohort x stop) คู่เช่นกัน บน scenario นี้ (6 cohort,
~26 อาคารมีงาน, 1 bus route 4 stop) รวมแล้วมากกว่า 180 ครั้ง/tick แต่ละ
ครั้งจัดสรร `Dictionary`/`HashSet`/`PriorityQueue` ใหม่ — นี่คือ GC/CPU
cost ตัวจริง ไม่ใช่การสร้าง adjacency list

**ไม่ได้แก้ในรอบนี้**: การ cache ผลลัพธ์ routing ระดับ
(cohort-destination)/(cohort-stop) แทนที่จะ cache แค่ graph object เอง
เป็น optimization ที่ถูกต้องและมีโอกาสสูงจะปิด gap นี้ได้ (คำตอบไม่ขึ้นกับ
อะไรนอกจาก topology ซึ่งเปลี่ยนไม่บ่อย) แต่แตะ logic ของ G4 code ที่
review ผ่านมาแล้วจากรอบก่อน (`TripDemandGenerator`/
`NetworkDemandAssignment`/`BusRidership`) ซึ่งมี test suite ของตัวเองอยู่
แล้ว — ตัดสินใจไม่รีบแก้ algorithm หลักภายใต้ time budget ของ wave นี้
เพื่อลดความเสี่ยง integration bug ที่ตรวจไม่ทันในรอบเดียวกับที่เพิ่ง
เปลี่ยน dependency-ready สำหรับ session ถัดไป (จุดเริ่ม: cache
`Dictionary<string /*cohortId*/, long? /*nearestJobNode*/>` +
`Dictionary<(string, long), double?> /*route distance*/` ใน WorldState,
invalidate ด้วย signal เดียวกับ `_cachedGraphSegmentCount`)

## ผลกระทบ

- ไม่มี behavior ใดเปลี่ยนจาก caching (routing ผลลัพธ์เดิมทุกประการ
  ยืนยันด้วย test suite เดิม 183 ตัวยังผ่านหมดหลังเพิ่ม cache)
- Performance budget ยัง MISS อย่างตรงไปตรงมา — บันทึกไว้ใน
  `docs/progress.md` และรายงานนี้ ไม่ tune scenario ให้ดูผ่าน
