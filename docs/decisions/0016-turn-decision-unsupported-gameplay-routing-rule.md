# ADR-0016: กติกาชั่วคราวของ `TurnDecision.Unsupported` ใน gameplay routing (ADR ที่ wave ก่อนติดค้างไว้)

- สถานะ: Accepted
- วันที่: 2026-09-22
- ผู้เกี่ยวข้อง: wave 3 (G2, ผู้สร้าง `TurnDecision.Unsupported`) และ
  wave 4 / G3 (ผู้เขียน ADR นี้ตามที่ค้างไว้)

## บริบท

START_FROM_EMPTY_REPO.th.md ระบุตรงๆ ว่า "unsupported restrictions ต้อง
อยู่ใน report; viewer แสดง unknown ได้ แต่ gameplay routing ต้องมี
support หรือกติกาชั่วคราวที่เปิดเผย" wave 3 (G2) implement
`Thaivia.Core.Graph.RoadGraphIndex.EvaluateTurn` ที่คืน
`TurnDecision.Unsupported` เป็น state ที่สามจริง (ไม่ fallback เป็น
`Allowed` เงียบๆ) และบันทึกพฤติกรรมนี้ไว้ใน `docs/progress.md` Session 3
แต่ไม่เคยเขียน ADR อธิบาย **กติกาชั่วคราวที่เปิดเผย** ว่า gameplay
routing (ผู้บริโภค `TurnDecision` จริง ไม่ใช่แค่ viewer/inspector) ควรทำ
อย่างไรกับ state นี้ — เป็นช่องว่างที่ spec เรียกร้องชัดเจนแต่ไม่เคยปิด
ADR นี้ปิดช่องว่างนั้น

## การตัดสินใจ

`Thaivia.Core.Graph.GameplayTurnPolicy.IsAllowedForGameplayRouting`
เป็นจุดเดียวที่ตัดสินว่า `TurnDecision` แต่ละค่าควรถูกปฏิบัติอย่างไรโดย
gameplay routing (ตรงข้ามกับ viewer/inspector ที่อยากเห็น 3 สถานะแยกกัน
ตรงๆ):

- `Allowed` -> อนุญาตเลี้ยว
- `Denied` -> ห้ามเลี้ยว
- **`Unsupported` -> ห้ามเลี้ยว (fail-safe เหมือน `Denied`)**

นี่คือกติกาชั่วคราวที่เปิดเผย: **fail safe ไม่ใช่ fail open** — restriction
ที่ pipeline สร้าง graph ให้ไม่ครบ (เช่น via-way restriction หลายทาง)
gameplay routing จะปฏิเสธเส้นทางนั้นไปเลย แทนที่จะเสี่ยงพา virtual
vehicle/pedestrian ไปฝ่าฝืน restriction จริงที่โมเดลไม่รู้จัก ข้อเสียคือ
เส้นทางบางเส้นอาจถูกปิดเกินจำเป็น (over-restrictive) แต่ over-restrictive
ปลอดภัยกว่า under-restrictive สำหรับเกมที่อ้างว่าเคารพ traffic semantics
จริงจากแหล่งข้อมูล

กติกานี้ **ไม่ใช่คำตอบสุดท้าย** — เป็น "กติกาชั่วคราว" ตามที่ spec
อนุญาตให้มีได้ตราบใดที่เปิดเผย เมื่อมี router ที่ evaluate via-way
restriction ได้จริง (G4/G5 mobility scope) `GameplayTurnPolicy` ควรถูก
แทนที่ด้วย logic ที่ประเมิน `TurnRestrictionRecord` แบบเต็มรูปแบบ ไม่ใช่
fail-safe เหมารวม

## หลักฐาน

`GameplayTurnPolicyTests` (`game/Thaivia.Core.Tests/GameplayTurnPolicyTests.cs`)
ยืนยันทั้งสามกรณีตรงๆ: `Allowed`->true, `Denied`->false,
`Unsupported`->false (เหมือน `Denied` เป๊ะ ไม่ใช่เหมือน `Allowed`)

## ผลที่ตามมา

- G3 เองไม่มี vehicle/pedestrian routing จริง (accessibility metric ของ
  G3 ใช้ `AccessibilityGraph` ซึ่งไม่ประเมิน turn restriction เลย ดู
  ADR-0015) — `GameplayTurnPolicy` เขียนไว้ล่วงหน้าให้ G4 ใช้ทันทีที่มี
  router จริง ไม่ต้องออกแบบกติกานี้ใหม่ตอนนั้น
- ถ้าอนาคตมีคนอยากเปลี่ยนกติกาเป็น fail-open (Unsupported ->
  อนุญาต) ต้องเขียน ADR ใหม่พร้อมเหตุผลเฉพาะ ไม่ใช่แก้ enum หรือ default
  case เงียบๆ
