# ADR-0017: G4 multimodal routing (turn-aware) และ capacity-based queue model

- สถานะ: Accepted
- วันที่: 2026-09-21
- ผู้เกี่ยวข้อง: wave 5 / G4 (Thaivia.Core.Simulation.Mobility)

## บริบท

G3's `AccessibilityGraph` จงใจไม่ประเมิน oneway/turn restriction เลย
(ดู ADR-0015 และ docs/progress.md Session 4) — มันตอบแค่ "ระยะทางบน
graph" สำหรับ accessibility score หยาบๆ `IMPLEMENTATION_PLAN.th.md` §9
ระบุตรงๆ ว่า vehicle routing ของ G4 ต้อง honor oneway/turn restriction
จริง (ไม่ใช่แค่ viewer แสดง unknown ได้) และ §11 ระบุ "directed
multimodal graph" (เดิน/รถส่วนตัว/รถขนส่ง อย่างน้อย)

โจทย์ยังระบุด้วยว่า "congestion ต้องเกิดจาก capacity ไม่ใช่ hand-tuned
traffic score" และ "road works ต้องกระทบการเข้าถึงระหว่างสร้าง"

## การตัดสินใจ

### Routing: state-space Dijkstra ไม่ใช่ node-Dijkstra ธรรมดา

`Thaivia.Core.Simulation.Mobility.Routing.MobilityGraph` ทำ Dijkstra บน
state `(NodeId, ArrivalWayId)` แทนที่จะเป็น node เฉย ๆ เพราะ
`RoadGraphIndex.EvaluateTurn` ต้องรู้ว่ามาจาก way ไหนถึงจะประเมิน
`type=restriction` relation ได้ถูกต้อง (fromWay/viaNode/toWay) — ถ้าใช้
node-Dijkstra ธรรมดาเหมือน `AccessibilityGraph` จะไม่มีทางรู้ "มาจากทาง
ไหน" ที่ node นั้น การขยาย state space เป็นทางเดียวที่ให้ turn
restriction ถูกต้องแบบ structural ไม่ใช่ heuristic

`Thaivia.Core.Graph.GameplayTurnPolicy` (เขียนไว้ล่วงหน้าตั้งแต่ G2/G3,
ADR-0016) ถูกใช้จริงเป็นครั้งแรกใน `MobilityGraph` สำหรับ
Vehicle/Freight mode — ปิดช่องว่างที่ G3's progress.md ระบุไว้ตรงๆ ว่า
"Turn restrictions ไม่ถูกใช้ใน AccessibilityGraph ... มันคือ scope ของ
routing ที่แม่นกว่าใน G4"

### Per-mode ความแตกต่าง: Walk ไม่ผูกกับ oneway/turn restriction ของรถ

`Walk` mode treat ถนนทุกเส้นเป็น bidirectional เสมอ (ไม่สน `oneway` tag)
และไม่ประเมิน `type=restriction` เลย เพราะ relation เหล่านี้ใน OSM
เกือบทั้งหมดหมายถึงยานยนต์ — การให้มันผูกกับคนเดินเท้าด้วยจะเป็น
assumption ที่ไม่มีหลักฐานรองรับ (AGENTS.md rule 4) `Vehicle`/`Freight`
สอง mode ผูกทั้ง oneway และ turn restriction เหมือนกันทุกประการ
(ไม่มี tag ที่แยกพฤติกรรม lane-level ระหว่างสองโหมดนี้ในโครงสร้างข้อมูล
ปัจจุบัน) ส่วน access ต่อ mode (`foot`/`motor_vehicle`/`hgv`) อ่านจาก
`RoadEdge.AccessModes` โดย key ที่ไม่มีอยู่เลยถือว่า "ไม่ระบุ" และ
default เป็นเปิด (documented `simulation_assumption`) ไม่ใช่ข้อเท็จจริง
จาก source

### Capacity-based congestion, ไม่มี "traffic score"

`Thaivia.Core.Simulation.Mobility.Queues.LinkQueueSimulator` เป็น
deterministic queue ต่อ `LinkKey` (way + ทิศทาง): ทุก tick
`queue' = max(0, queue + arrivals - capacity)`, `completed += min(queue+
arrivals, capacity)` — ไม่มีตัวแปรอื่นเข้ามาปนเลย congestion เป็นผลลัพธ์
ทางคณิตศาสตร์ตรงจาก capacity เท่านั้น `LinkCapacity.BaseCapacityVehPerTick`
อ่าน `lanes` tag ถ้ามี (ไม่มี = default 1 lane, เป็น assumption ที่บันทึก
ไว้ตรงๆ ไม่ใช่ fact) `RoadWorksZone` ลด capacity เฉพาะช่วง
`[StartTick, StartTick+DurationTicks)` เท่านั้น — ก่อน/หลังช่วงนั้น
capacity เต็ม 100% ทำให้ "road works กระทบระหว่างสร้าง ไม่ใช่หลังเสร็จ"
เป็นจริงเชิงกลไก ไม่ใช่แค่คำอธิบาย

## ผลที่ตามมา

- `MobilityGraph` ไม่ reuse `RoadGraphIndex`'s adjacency ตรงๆ (มัน re-build
  adjacency ของตัวเองที่พก `WayId` ต่อ step) แต่ reuse
  `RoadGraphIndex.EvaluateTurn`/`GameplayTurnPolicy` สำหรับ turn logic —
  หลีกเลี่ยงการ fork กติกา turn restriction เป็นสองที่
- Player-committed connector (`PlannedRoadSegment`) ได้ synthetic
  negative `WayId` เมื่อป้อนเข้า `MobilityGraph` — ไม่มีทางชนกับ
  OSM way id จริง (เสมอ >= 1) และไม่มี turn restriction ไหนอ้างถึงมันได้
  (ไม่มี relation ไหนมี fromWay เป็นเลขติดลบ)
- ทดสอบ: `Thaivia.Core.Tests.Simulation.Mobility.MobilityGraphTests`
  (turn restriction บังคับ Vehicle/Freight อ้อม ขณะ Walk ไม่อ้อม, oneway
  ผูก Vehicle ไม่ผูก Walk, access mode ต่อ mode),
  `LinkQueueSimulatorTests` (backlog โตเท่า shortfall เป๊ะ, conservation
  arrived==completed+queued), `RoadWorksTests` (capacity ลดเฉพาะช่วง
  construction, ผลกระทบต่อ queue วัดได้จริงเทียบก่อน/หลัง)
