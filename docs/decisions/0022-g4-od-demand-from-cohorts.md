# ADR-0022: OD demand เป็น batch จาก cohort ไม่ใช่ per-agent spawning

- สถานะ: Accepted
- วันที่: 2026-09-21
- ผู้เกี่ยวข้อง: wave 5 / G4 (Thaivia.Core.Simulation.Mobility.Demand)

## บริบท

Spec §9/§11 ระบุ "OD batches" จาก cohort และห้าม spawn ต่อ agent
`TripDemandGenerator`/`NetworkDemandAssignment` เป็นสะพานเชื่อมระหว่าง
`HouseholdCohort` (G3, population truth) กับ `MobilityGraph`/
`LinkQueueSimulator` (G4, routing/congestion) ที่ยังไม่มีมาก่อน

## การตัดสินใจ

### หนึ่ง cohort = หนึ่ง batch เสมอ

`TripDemandGenerator.GenerateCommuteBatches` วนลูปต่อ **cohort** (ไม่ใช่
ต่อคน) และคำนวณ `VehicleCount` จาก
`cohort.PopulationCount x ActivityClockCatalog.At(Residential,
hour).TripGeneration x modeShare` — cohort ประชากร 2,000 คนได้ record
เดียว (`TripDemandGeneratorTests.OneCohort_ProducesExactlyOneBatch_NotOnePerPerson`
ยืนยันด้วย `Assert.Single(batches)`) ปลายทางหา job node ที่ใกล้ที่สุด
จริงผ่าน `MobilityGraph.ShortestDistanceMeters` (Vehicle mode, turn-aware)
— ไม่มี route ก็ไม่มี batch เลย (ไม่เดา distance เป็น 0)

### All-or-nothing assignment: ทางเลือกที่ตรงไปตรงมา ไม่ใช่ equilibrium จริง

`NetworkDemandAssignment.AssignToWays` route แต่ละ batch ครั้งเดียว
(shortest path ปัจจุบัน) แล้วเติม `VehicleCount` เต็มจำนวนลงทุก way บน
เส้นทางนั้น — **ไม่ใช่** capacity-aware equilibrium assignment (ไม่มี
rerouting ตาม congestion ภายใน pass เดียว) นี่เป็นข้อจำกัดที่ตั้งใจ
บันทึกไว้ตรงๆ ไม่ใช่ข้อบกพร่องที่ซ่อนไว้ — mechanism ที่ถูกต้องกว่า
(iterative equilibrium, หรือผูกกับ `LinkQueueSimulator` แบบ feedback
จริงทุก tick) เป็นงานต่อยอด

### บั๊กจริงที่พบระหว่างเขียน test (บันทึกไว้ตรงๆ)

`AccessibilityGraph`/`MobilityGraph`'s `WayIdsInOrder` มี way_id ซ้ำได้
เมื่อ way เดียวมีหลาย segment ที่เส้นทางเดินผ่านมากกว่าหนึ่ง segment
(เช่น `102: node3->node4->node5` สอง segment) — เวอร์ชันแรกของ
`AssignToWays` เอา arrivals ไปบวกซ้ำ (40+40=80 แทนที่จะเป็น 40) ทดสอบ
จับได้ทันทีตอนรัน (`Expected: 40, Actual: 80`) แก้ด้วยการ de-duplicate
way_id **ต่อ batch** ก่อน accumulate — เป็นตัวอย่างจริงของ test ที่จับ
บั๊กจริงในรอบนี้ ไม่ใช่แค่ assertion ที่ผ่านเพราะเขียนตามโค้ด

## ผลที่ตามมา

- ยังไม่ได้ต่อ `TripDemandGenerator`/`NetworkDemandAssignment` เข้ากับ
  `WorldState`'s tick loop อัตโนมัติ (ยังต้องเรียกจาก caller ตรงๆ เหมือน
  `BuildAccessibilityGraph`) — เป็น pure/read-only query ที่ไม่แตะ world
  state เลย (ไม่ reserve budget ไม่ mutate RNG) จึงปลอดภัยเรียกจาก
  Estimate-style code path ได้เหมือน `PlanningEngine.Estimate*`
- Direction (`Forward`/`Reversed`) ของ arrivals ต่อ
  `Queues.LinkKey` ยังไม่ได้ตัดสินใจจาก assignment นี้โดยตรง (มันคืน
  arrivals ต่อ way แบบไม่แยกทิศทาง) — caller ที่จะป้อนเข้า
  `LinkQueueSimulator.Step` ต้องเลือกทิศทางเอง documented เป็นข้อจำกัด
  ตรงๆ ใน `NetworkDemandAssignment`'s doc comment
