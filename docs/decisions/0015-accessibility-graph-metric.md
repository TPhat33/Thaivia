# ADR-0015: Accessibility จากระยะทางบน road graph เท่านั้น ไม่ใช้รัศมีเส้นตรง

- สถานะ: Accepted
- วันที่: 2026-09-22
- ผู้เกี่ยวข้อง: wave 4 / G3 (Thaivia.Core.Simulation.Accessibility)

## บริบท

IMPLEMENTATION_PLAN.th.md §11 ระบุตรงๆ ว่า accessibility ต้องคำนวณจาก
network "ไม่ใช่ radius ที่เดินข้ามคลองได้" นี่เป็นข้อกำหนดที่ supervisor
ระบุว่าจะตรวจเข้มที่สุดในรอบนี้ (ต้องมี fixture ที่อาคารใกล้กันแบบเส้นตรง
แต่ไกลกันบน graph จริงๆ เช่น อยู่คนละฝั่งคลอง)

## การตัดสินใจ

1. **`Thaivia.Core.Simulation.Accessibility.AccessibilityGraph` เป็น
   type เดียวในโค้ดเบสทั้งหมดที่ตอบคำถาม "ระยะทางจากจุด A ถึงจุด B"
   สำหรับ gameplay** — ไม่มี helper แบบ straight-line/Euclidean-distance
   accessibility อยู่ที่ไหนเลยใน `Thaivia.Core.Simulation` (ตรวจได้จาก
   `grep` ว่าไม่มี method อื่นชื่อคล้าย `AccessibilityScore`/
   `DistanceTo` ที่ไม่ผ่าน `AccessibilityGraph`)
2. **Adjacency สร้างจาก `RoadEdge.NodeRefs` (shared node id) เท่านั้น**
   — สืบทอด "วินัย" เดียวกับ `Thaivia.Core.Graph.RoadGraphIndex` (G2
   wave): สอง node ที่บังเอิญพิกัดใกล้กันแต่ไม่มี edge ร่วม ไม่มีทาง
   ปรากฏเป็น neighbor ของกันในกราฟนี้เลย ไม่ว่าระยะจะสั้นแค่ไหน
3. **Edge weight = ระยะ Euclidean ระหว่างสอง node ที่ edge นั้น "เชื่อม
   จริง"** (จาก `RoadGraphNode.LocalX/LocalZ`) ไม่ใช่ความยาว polyline
   สะสมทุกจุด (simplification ที่ยอมรับได้สำหรับ pilot scale — ระยะทาง
   ระหว่างสอง node ติดกันของถนนจริงมักสั้นพอที่ chord กับ arc ต่างกันไม่
   มาก) — ผลลัพธ์จึงเป็นผลรวมของ "ระยะที่ edge จริงพาไปได้" เท่านั้น ไม่
   เคยเป็นเส้นตรงระหว่างจุดเริ่ม/จุดจบที่ไม่มี edge เชื่อมกัน
4. **`PlannedRoadSegment` (player-committed connector) รวมเข้ากราฟแบบ
   "ข้างๆ" ผ่าน constructor parameter เท่านั้น ไม่เคย merge เข้า
   `RoadGraph`/`RoadEdge` ของ GeographyBase** — รักษาการแยกชั้นข้อมูล
   (AGENTS.md rule 3) พร้อมกันไปกับ metric นี้: MapPack ที่โหลดมาไม่เคย
   ถูกแก้เพื่อให้ accessibility คำนวณง่ายขึ้น
5. **`AccessibilityNeed.ComputeScore(null) == 0`** ไม่ใช่ "ไม่รู้จึงถือว่า
   ปกติ" — สอดคล้องกับวินัยของ `SourceValue<T>.Unknown` (ADR-0009) ที่ไม่
   ให้ "ไม่มีข้อมูล/ไปไม่ถึง" แปลว่า "โอเค"

## Fixture ที่ใช้พิสูจน์ (adversarial ตามที่ supervisor ขอ)

`SimulationFixtures` สร้าง node 2 ที่ (10,0) กับ node 3 ที่ (12,0) —
**ห่างกันเพียง 2 เมตรแบบเส้นตรง** (สั้นกว่าฟุตพริ้นท์อาคารทั่วไป) แต่คนละ
cluster กันโดยไม่มี edge เชื่อมเลย (เปรียบเป็นคนละฝั่งคลองที่ไม่มีสะพาน)
`AccessibilityGraphTests.StraightLineNearNodes_WithNoConnectingEdge_AreGraphUnreachable`
ยืนยันว่า `ShortestDistanceMeters(node2, node3)` คืน `null` (ไปไม่ถึง)
ทั้งที่ตรงข้ามกับที่ radius-based metric ใดๆ จะรายงานว่า "ใกล้มาก" หลังจาก
เพิ่ม `PlannedRoadSegment` เชื่อมสองจุดนี้ (สร้างสะพาน) ระยะทางจาก node 1
ถึง node 5 เปลี่ยนจาก unreachable เป็น 32 เมตรทันที (ผลรวม edge จริงตาม
เส้นทางเดียวที่มีอยู่ ไม่ใช่เส้นตรง)

## ผลที่ตามมา

- `AccessibilityNeed.ReferenceDistanceMeters` (500m) เป็นค่าออกแบบเกม
  ระบุชัดว่าไม่ใช่ผลสำรวจการเดินจริง — ต้อง revisit เมื่อมี playtest
- G4 (mobility/queues เต็มรูปแบบ) จะแทนที่ Dijkstra ธรรมดานี้ด้วย
  time-weighted routing (คิว, สัญญาณไฟ) — ADR นี้ครอบคลุมเฉพาะ "ต้องเป็น
  graph ไม่ใช่ radius" ซึ่งเป็น invariant ที่จะยังจริงต่อไปแม้ edge weight
  จะซับซ้อนขึ้น
