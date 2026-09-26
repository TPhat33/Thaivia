# ADR-0028: Congestion-aware demand assignment (replaces all-or-nothing, closes the ADR-0022/0023 distortion)

- สถานะ: Accepted
- วันที่: 2026-09-26
- ผู้เกี่ยวข้อง: wave 7 / G6 debt-paydown (Opus supervisor brief), spec §9/§11
- Supersedes: ADR-0022's all-or-nothing assignment เป็นโมเดลที่ใช้จริงใน
  `WorldState.SimulateTick` (ADR-0022's document ของ behavior เดิมยังคง
  ถูกต้อง — เก็บไว้เป็น "before" baseline ผ่าน
  `NetworkDemandAssignment.AssignToWaysAllOrNothing`, ไม่ลบทิ้ง); ปิด
  ข้อจำกัดที่ ADR-0023 บันทึกไว้ตรงๆ ในหัวข้อ "ข้อจำกัดที่ยังไม่แก้"

## บริบท

`AllOrNothingAssignmentDistortionTests` (เขียนไว้ตั้งแต่ ADR-0023) พิสูจน์
ด้วยตัวเลขจริงว่า all-or-nothing assignment บิดเบือนผลอย่างมีนัยสำคัญเมื่อ
มีเส้นทางสำรอง: เส้นสั้น/capacity ต่ำ (2 คัน/tick) กับเส้นอ้อม/capacity สูง
(20 คัน/tick) ที่มี demand 6 คัน/tick คงที่ — all-or-nothing ให้ backlog
บนเส้นสั้นโต **ไม่มีขอบเขต** (แน่นอน (6-2)x200=800 หลัง 200 tick) ในขณะที่
เส้นอ้อมไม่เคยได้รับ arrival แม้แต่คันเดียว ตัวเลข congestion ที่ integrated
loop รายงานจนถึงตอนนี้จึงเป็น **upper bound** ไม่ใช่สิ่งที่ router ที่มี
ความฉลาดพอจะเปลี่ยนเส้นทางเมื่อติดจะให้ผลจริง

## การตัดสินใจ

### โมเดลที่เลือก: Incremental assignment ด้วย BPR-style volume-delay cost, แบ่งเป็น slice

`NetworkDemandAssignment.AssignToWaysCongestionAware` แทนที่
`AssignToWaysAllOrNothing` ใน `WorldState.SimulateTick`:

1. แต่ละ `OdBatch.VehicleCount` ถูกแบ่งเป็น `DefaultSliceCount` (=4) ส่วน
   แบบ **integer-exact** (`SliceShare`: base = total/sliceCount, ส่วนที่
   เหลือ (remainder) แจกให้ slice แรกๆ ทีละ 1 — ผลรวมของทุก slice เท่ากับ
   total เป๊ะเสมอ ไม่มีปัดเศษหาย)
2. Batch ถูกประมวลผลตามลำดับ **`CohortId` แบบ ordinal เสมอ** (ไม่ใช่ลำดับ
   ของ dictionary/HashSet enumeration ที่ไม่รับประกัน) — determinism
3. ในแต่ละ slice, batch แต่ละตัวถูก route ด้วย cost ที่ way หนึ่งมี weight
   `length x (1 + Alpha x (load/capacity)^Beta)` (BPR — Bureau of Public
   Roads volume-delay function, ค่าคงที่ตำรามาตรฐาน Alpha=0.15, Beta=4 —
   **ไม่ใช่ค่าที่ fit จากข้อมูลจราจรไทยจริง เพราะยังไม่มี — เป็น
   simulation_assumption ที่ inspect/แก้ได้ตรงๆ ผ่าน
   `NetworkDemandAssignment.CongestionAlpha`/`CongestionBeta`**) `load`
   คือ backlog ที่ค้างจริงจากปลาย tick ก่อนหน้า
   (`LinkQueueSimulator.QueueLengthOf`) **บวก** ปริมาณที่ assignment ของ
   tick นี้เองได้ใส่ลงบน way นั้นไปแล้วใน slice ก่อนหน้า — สองส่วนนี้รวม
   กันทำให้ route เปลี่ยนได้ทั้งข้าม tick (จาก backlog จริง) และภายใน tick
   เดียว (จาก successive slice ที่เห็นว่า way ก่อนหน้าเริ่มแน่นแล้ว)
4. Way ที่ route ใช้ถูก de-duplicate ต่อ route ก่อนบวก arrivals (บั๊กเดิม
   ที่ ADR-0022 เจอ — de-duplication ยังคงอยู่เหมือนเดิมทุกประการ)

**Congestion ยังคงเกิดจาก capacity เท่านั้น**: `LinkQueueSimulator.Step`
(enqueue arrivals, dequeue เท่า capacity) **ไม่ถูกแก้เลย** — สิ่งที่
เปลี่ยนมีแค่ "รถไปลงที่ way ไหน" (routing CHOICE) ไม่ใช่ "queue โตยังไง"
(queue MECHANICS) BPR cost ใช้ capacity/backlog จริงเป็น input เสมอ ไม่มี
ตัวเลข "traffic score" ที่ไม่ผูกกับ capacity ที่ไหนเลย (spec: "congestion
ต้องเกิดจาก capacity ไม่ใช่ hand-tuned traffic score" — ยังคงจริงเป๊ะ)

### ทำไมไม่ใช่ full iterative equilibrium (Frank-Wolfe ฯลฯ)

Incremental assignment แบบนี้ถูกตำราจัดเป็น "successive averages ที่
ง่ายลง" — ไม่ใช่ equilibrium ที่ลู่เข้าสู่ user-equilibrium อย่างเข้มงวด
ทางคณิตศาสตร์ แต่ (ก) ให้พฤติกรรมเชิงคุณภาพที่ถูกต้อง (เส้นทางสำรองที่มี
capacity เหลือได้รับ demand จริง เส้นทางที่แน่นแล้วถูกหลีกเลี่ยงมากขึ้น
เรื่อยๆ) และ (ข) ต้นทุนต่อ tick คาดเดาได้ตรงไปตรงมา: `sliceCount x
cohortCount` การเรียก Dijkstra **ที่ตั้งใจไม่ cache** (เพราะ cost
เปลี่ยนทุก slice) เทียบกับ full equilibrium ที่มักต้องวนหลาย iteration
จนลู่เข้า (ต้นทุนต่อ tick ไม่แน่นอน ขึ้นกับ scenario) — ภายใต้ time budget
ของ wave นี้และ p95 budget ที่ 5ms เลือกโมเดลที่ต้นทุนคาดเดาได้แน่นอนก่อน
เพื่อให้วัด/รายงานตัวเลขได้ตรงไปตรงมา ถ้า pilot AOI จริง (เมื่อมีข้อมูล
OSM แล้ว — ADR-0003) แสดงว่า 4 slice ไม่พอสมจริง ค่านี้ (`DefaultSliceCount`)
ปรับได้ตรงๆ โดยไม่ต้องเปลี่ยน algorithm

### Determinism: double ใช้แค่เลือก route, ไม่เคยถูก persist/hash

`CongestionMultiplier`/BPR cost เป็น `double` (จำเป็นสำหรับ `Math.Pow`)
แต่ค่านี้ใช้แค่ **จัดลำดับ edge ใน Dijkstra ภายในการเรียกครั้งเดียว** —
ไม่เคยถูกเขียนลง `WorldState`, ไม่เคยถูก serialize ลง save file, ไม่เคย
เข้า `ComputeStructuralHash` เลย ผลลัพธ์ที่ persist มีแค่ `long` (arrivals
ต่อ way, VehicleCount ต่อ slice ที่คำนวณด้วย integer division/modulo
เท่านั้น) การเรียก `Math.Pow`/floating-point arithmetic ของ .NET ให้ผล
เดิมเป๊ะเมื่อ input เดิมเป๊ะในกระบวนการเดียวกัน (class เดียวกับที่ใช้อยู่
แล้วใน `MobilityGraph.Distance()`'s `Math.Sqrt` โดยไม่มีปัญหามาก่อน) —
`CongestionAwareAssignmentTests.CongestionAwareAssignment_IsFullyDeterministic_AcrossIndependentRunsOfTheSameScenario`
พิสูจน์ตรงนี้ที่ระดับ method เดี่ยว และ
`IntegratedTickLoopTests.TwoWorlds_SameSeedSameIntegratedTickRun_ProduceIdenticalStructuralHash`
(เดิมของ wave ก่อน ไม่ได้แก้ assertion ใดๆ) ยังคงผ่านที่ระดับ
`WorldState.SimulateTick` เต็มรูปแบบ ยืนยันว่า `ComputeStructuralHash`
เหมือนกันเป๊ะระหว่างสอง world seed เดียวกันที่รัน congestion-aware
assignment จริงตลอด 5 ชั่วโมงเกม + commit road-works project กลางทาง

### Gateway conservation ยังคง exact

`IntegratedTickLoopTests.GatewayConservation_HoldsAcrossThousandsOfTicks_WithTheFullLoopRunning_AndACloseReopenMidRun`
(เดิม ไม่ได้แก้) รันผ่านด้วย assignment ใหม่: 5,000 tick integrated,
gateway ปิด/เปิดกลางทาง `Generated == Completed + Queued` ตรงเป๊ะทั้ง
inbound/outbound ทุก tick (`docs/evidence/g6-gateway-conservation-post-debt2.log`)
เหตุผลที่ยังคง exact: `NetworkDemandAssignment` ไม่แตะ `GatewayFlow`/
`GatewayNetwork` เลย (คนละ subsystem, ดู ADR-0027 ข้อ 3) — multi-slice
assignment ไม่มีทางไปสูญเสีย/ซ้ำ trip ของ gateway ได้เลยเพราะไม่เคย
touch มันตั้งแต่แรก การ "ระวังไม่ให้ multi-slice assignment ทำให้ trip
หาย/ซ้ำ" ที่แท้จริงอยู่ที่ `NetworkDemandAssignment` เอง:
`CongestionAwareAssignment_DivertsAMeaningfulShareOfDemandOntoTheHighCapacityAlternative`
assert ตรงๆ ว่า `shortArrived + detourArrived1 == totalDemand` ทุกครั้ง
(ผลรวม arrivals ข้าม way เท่ากับ demand ทั้งหมดเป๊ะ ไม่มี trip หาย/ซ้ำจาก
การแบ่ง slice)

### Companion test: การเบี่ยงเบนจริงที่วัดได้ (ย้อนกลับจาก distortion test)

Scenario เดียวกันกับ `AllOrNothingAssignmentDistortionTests` เป๊ะ (demand
6 คัน/tick, เส้นสั้น 2 คัน/tick capacity, เส้นอ้อม 20 คัน/tick capacity x2
way, 200 tick):

| | เส้นสั้น (arrivals) | เส้นสั้น backlog สุดท้าย | เส้นอ้อม (arrivals ต่อ way) | ส่วนแบ่งเส้นอ้อม |
|---|---|---|---|---|
| All-or-nothing (ADR-0022, เดิม) | 1,200 | **800** (ไม่มีขอบเขต) | **0** | **0%** |
| Congestion-aware (ADR นี้) | 402 | **2** (คงที่/stabilize) | **798** | **66.5%** |

(ตัวเลขจริงจาก `CongestionAwareAssignmentTests`, ดู test output ใน
`docs/evidence/g6-dotnet-test-full.log`) — เส้นอ้อมได้รับส่วนแบ่ง demand
จริง 66.5% (เกิน 25% ที่ test assert ไว้อย่างมาก) และ backlog เส้นสั้น
เข้าสู่จุดสมดุล (2 คัน ไม่ใช่ 0 เพราะ 6 คัน/tick หารด้วย slice ไม่ลงตัว
พอดีกับ capacity 2 ทุกครั้ง — เป็นพฤติกรรมที่ถูกต้องของโมเดล incremental
ไม่ใช่บั๊ก) แทนที่จะโตไม่มีที่สิ้นสุด

## ผลที่วัดได้ (หลัง Debt 1 + Debt 2 รวมกัน, scenario เดียวกับ ADR-0024/0027 เป๊ะ)

Release build, 8x8 grid synthetic, 9,000 tick, 100-tick warm-up
(`docs/evidence/g6-benchmark-after-debt2.log`):

```
p50: 0.1259 ms | p95: 0.7712 ms | max: 22.4025 ms | mean: 0.2584 ms
Budget (spec §15, experimental): p95 <= 5 ms -- PASS
```

เทียบกับหลัง Debt 1 อย่างเดียว (ADR-0027): p95 ขยับจาก 0.2846ms เป็น
0.7712ms (เพิ่มขึ้นจริง ~2.7x จากการเพิ่ม `sliceCount x cohortCount`
Dijkstra ที่ตั้งใจไม่ cache ต่อ tick — ตรงกับที่ ADR-0027 ทำนายไว้ว่าจะ
"เพิ่มกลับมาบ้าง แต่ไม่กลับไปเท่าปัญหาเดิม") ยังคงต่ำกว่า budget 5ms มาก
(เหลือ headroom ~6.5x) — **ไม่ใช่ MISS ครั้งที่สองที่ gap แคบลง แต่เป็น
PASS ที่มี margin ชัดเจน**, รายงานตรงไปตรงมาตามที่ ADR-0024/supervisor
ขอไว้ทั้งสองกรณี (จะ pass หรือ miss ก็ตาม)

`max` ที่ยังสูงกว่า `p95` มาก (22ms) มาจากสาเหตุเดียวกับที่ ADR-0027
วินิจฉัยไว้ (GC gen0/gen1 pause บาง tick, JIT tier-up jitter ในช่วงต้น
ของ soak) — ไม่ใช่ปัญหาใหม่จาก Debt 2's algorithm, ยืนยันว่า slice
count=4 ไม่ได้สร้าง allocation pathology ใหม่

## ผลกระทบ

- `NetworkDemandAssignment.AssignToWays` (2-arg) เปลี่ยนชื่อเป็น
  `AssignToWaysAllOrNothing` — call site ที่เหลือ (test เดิม 3 จุด) แก้
  เป็นชื่อใหม่ตรงๆ ไม่มี behavior เปลี่ยน
- `WorldState.SimulateTick` เรียก `AssignToWaysCongestionAware` แทน — นี่
  คือการเปลี่ยน production behavior จริง (ตัวเลข congestion ที่ผู้เล่นเห็น
  จะเปลี่ยนไปจากนี้ ใกล้เคียงความเป็นจริงมากขึ้น ไม่ใช่ upper-bound เดิม)
- `WorldState.ComputeCapacityByWayId`/`PriorQueueLengthByWayId` ใหม่
  (private helper, ใช้ร่วมกันระหว่าง assignment กับ `StepLinkQueues` เพื่อ
  ไม่ให้ทั้งสองมี capacity เพี้ยนจากกัน)
- Test ใหม่: `TwoRouteAssignmentFixtures` (shared fixture, refactor จาก
  `AllOrNothingAssignmentDistortionTests`'s เดิม ไม่เปลี่ยน assertion),
  `CongestionAwareAssignmentTests` (2 test) — ไม่มี test เดิมถูกลบหรือแก้
  ให้อ่อนลง (`AllOrNothingAssignmentDistortionTests` ยังคง assertion เดิม
  เป๊ะ เปลี่ยนแค่ชื่อ method ที่เรียก)
