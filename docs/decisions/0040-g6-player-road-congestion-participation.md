# ADR-0040: ถนนที่ผู้เล่นสร้าง (PlannedRoadSegment) เข้าร่วม congestion accounting เหมือนถนนจริง

- สถานะ: Accepted — ไม่ supersede ADR ใด แต่ปิด scope gap ที่ ADR-0022
  (การ dedupe way id) และ ADR-0031 (section 4, "Scope gap ที่ยังไม่ปิด")
  บันทึกไว้ตรงๆ
- วันที่: 2026-09-26
- ผู้เกี่ยวข้อง: wave 9 / Task 2 (Opus supervisor brief), spec §9/§11/§15

## บริบท

`NetworkDemandAssignment.DeduplicateWayIds` กรอง way id ที่ `< 0` ทิ้งมา
ตั้งแต่ ADR-0022 — id ลบเป็น synthetic id ที่ `MobilityGraph` สร้างให้
`PlannedRoadSegment` (ถนนใหม่ที่ผู้เล่น commit ผ่าน
`PlanningEngine.CommitNewRoadConnector`) และ pedestrian crossing เมื่อ
built graph ผลคือ: เส้นทางที่ผ่านถนนใหม่ของผู้เล่น **ไม่เคยถูกนับเข้า
`arrivalsByWay`** เลย — แปลว่า
1. `NetworkDemandAssignment.AssignToWaysCongestionAware`'s congestion cost
   function ไม่เคยเห็น load บนถนนนั้น (เพราะ `cumulativeVolumeByWay` ไม่ถูก
   update ให้มันเลย)
2. `WorldState.StepLinkQueues` ไม่เคยเรียก `LinkQueues.Step` ให้มันเลย
   (เพราะไม่มี capacity entry ใน `capacityByWayId`)

ผลลัพธ์ที่ผู้เล่นเห็น: ถนนที่ตัวเองสร้างดูเหมือนมี capacity ไม่จำกัด ไม่
เคยติด ไม่ว่าจะส่ง traffic ไปเท่าไหร่ — ตรงข้ามกับ spec §9/§11 ที่กำหนดว่า
"congestion ต้องเกิดจาก capacity" และ "road works ต้องกระทบการเข้าถึง" (ถ้า
congestion ไม่เกิดกับถนนใหม่เลย ก็ไม่มีทาง demonstrate trade-off ที่ผู้เล่น
สร้างถนนแล้วต้องบริหาร capacity ต่อ)

ADR-0031 (section 4) ให้ตัวเลข capacity ที่ควรใช้ไว้แล้ว
(`RoadPresetCatalog.CapacityVehPerTick`) แต่บันทึกตรงๆ ว่ายังไม่ได้เอาไป
ใช้จริง — ADR นี้ปิด gap นั้น

## การตัดสินใจ

### 1. `MobilityGraph` เปิดเผย mapping synthetic id → PlannedRoadSegment

`MobilityGraph.PlannedRoadSegmentWayIds` (property ใหม่, อ่านอย่างเดียว)
เก็บ synthetic way id ที่ constructor กำหนดให้แต่ละ
`PlannedRoadSegment` ระหว่างสร้าง graph — ไม่ต้อง re-derive scheme การนับ
เลขจากภายนอก (ซึ่งจะเปราะบางถ้า constructor เปลี่ยนลำดับการกำหนดในอนาคต)
คุณสมบัตินี้ว่างเปล่าเสมอสำหรับ `TravelMode.Walk` graph เพราะ crossing
(ไม่ใช่ PlannedRoadSegment) เป็นเจ้าของ negative id ใน mode นั้นแทน — และ
`WorldState` สร้าง vehicle graph โดยไม่ส่ง `crossings` พารามิเตอร์เลย จึง
รับประกันว่า negative id ทุกตัวที่ vehicle graph เห็นมาจาก
`PlannedRoadSegment` เท่านั้น ไม่ปนกับ crossing

### 2. `WorldState.ComputeCapacityByWayId` เพิ่ม capacity entry ให้ทุก PlannedRoadSegment

ใช้ `RoadPresetCatalog.CapacityVehPerTick(segment.Preset)` ตรงๆ — เลข
เดียวกับที่ ADR-0031 นิยามไว้แล้ว ไม่ใช่ตัวเลขใหม่ที่คิดเอง **ไม่ใช้**
road-works capacity reduction กับ segment ผู้เล่น (ไม่มี `RoadEdge` ให้
`LinkCapacity.EffectiveCapacityVehPerTick` ตรวจสอบ) — เป็น scope ที่แคบกว่า
เส้นทางถนนจริงโดยตั้งใจและบันทึกไว้ตรงๆ ใน doc comment ไม่ใช่ gap เงียบ

### 3. `NetworkDemandAssignment.DeduplicateWayIds` เลิกกรอง negative id ทิ้ง

De-duplicate เหมือนเดิมทุกประการ (ป้องกัน double-count เมื่อ route ข้าม
way เดียวกันหลาย segment) แต่ไม่ตัดทิ้งอีกต่อไป — เพราะ method นี้ถูกเรียก
กับ vehicle graph เท่านั้น (ยืนยันด้วยการอ่าน call site ทั้งหมดใน
production code และ test) จึง negative id ที่เจอในนี้เป็น
`PlannedRoadSegment` เสมอ ไม่ใช่ crossing

## ผลที่ตามมา — ทดสอบอะไรพิสูจน์อะไร

- `TripDemandGeneratorTests.NetworkDemandAssignment_AddsTheBatchsFullVehicleCount_ToEveryWayOnItsShortestPath`
  แก้จาก `Assert.DoesNotContain(... k < 0)` เป็น `Assert.Contains(...)` —
  พฤติกรรมเปลี่ยนจริง ไม่ใช่ test เดิมบังเอิญยังผ่าน
- `PlayerBuiltConnectorCongestionTests` (ใหม่, สอง test) ทำซ้ำรูปทรงเดียวกับ
  `CongestionAwareAssignmentTests` (ถนนสั้น capacity ต่ำ vs ทางเลือก
  capacity สูง) แต่ทางเลือกที่ capacity สูงเป็น **PlannedRoadSegment ของ
  ผู้เล่น** (RoadPreset.Arterial, 8 veh/tick) ไม่ใช่ real way:
  - `PlayerBuiltConnector_NowParticipatesInAssignmentAndCapacity_NotExcludedAsSynthetic`:
    connector ได้ arrivals จริง ไม่ถูก drop
  - `PlayerBuiltConnector_CarriesAMeaningfulShareOfDemand_AndQueuesWhenOverloaded_LikeASourceWayWould`:
    demand split ที่วัดจริง (12 veh/tick x 200 ticks, ดู
    `docs/evidence/g8-task2-demand-split-measured.log`):
    **connector รับ 1,665/2,400 (69.4%)**, ending backlog **65** (>0 —
    พิสูจน์ตรงว่าไม่ใช่ capacity ไม่จำกัดอีกต่อไป); short way รับ 735
    (30.6%), ending backlog 335 — conservation ผ่าน (735+1665=2400)
- Perf benchmark (`SimulateTick_PerTickCost_OnAPilotSizedSyntheticGrid_...`,
  ดู `docs/evidence/g8-task2-benchmark-after.log`): **p95 = 0.8142ms**
  (เดิม 0.7712ms) ยังอยู่ใต้ budget 5ms มาก — synthetic grid fixture นี้ไม่
  มี committed `PlannedRoadSegment` เลย ดังนั้นโค้ดที่เพิ่ม (วน
  `PlannedRoadSegmentWayIds` ที่ว่างเปล่า) มีต้นทุนเท่ากับ O(0) ในการวัดนี้
  — ความต่าง 0.043ms อยู่ในช่วง noise ปกติของการวัดซ้ำ ไม่ใช่ regression
  ที่ attribute ให้โค้ดนี้ได้จริง

## ทดสอบเดิมที่ยัง reuse ได้ ไม่ต้องเขียนใหม่

`GatewayConservationTests`, `WorldStateDeterminismTests`,
`PlanningEngineTests` (double-charge/non-mutating estimate) ยังผ่านทั้งหมด
ไม่มีตัวใดถูกแตะ — การเปลี่ยนนี้จำกัดอยู่ที่ `NetworkDemandAssignment`,
`MobilityGraph`, `WorldState.ComputeCapacityByWayId`/`StepLinkQueues` เท่านั้น
