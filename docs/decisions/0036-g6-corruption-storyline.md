# ADR-0036: คดีจัดซื้อ/ทุจริตแต่งขึ้นหนึ่งสาย ("the overpaid resurfacing milestone")

- สถานะ: Accepted
- วันที่: 2026-09-26
- ผู้เกี่ยวข้อง: wave 8 / G6-09 (Opus supervisor brief), plan §7/§12/§16, AGENTS.md ข้อ 9
- ไม่ supersede ADR ใด

## บริบท

Plan §16 (G6): "คดีจัดซื้อแต่งขึ้นหนึ่งสายเชื่อมกับ project ledger" plan §7
เตือนตรงๆ ว่า "เหตุการณ์อาชญากรรม/ทุจริตต้องตรวจการเชื่อมโยงกลับถึง
สถานที่จริง ไม่ใช่เชื่อว่ามี disclaimer แล้วทุกอย่างปลอดปัญหา" — เอกสารนี้
จึงต้องบันทึกการตรวจจริง ไม่ใช่แค่ข้อความปฏิเสธความรับผิดชอบ

## เนื้อเรื่อง (หนึ่งสายเดียว ตามที่กำหนด)

"The overpaid resurfacing milestone": milestone งบของโครงการถนนหนึ่งที่
ผู้เล่น commit และจ่ายเงินไปแล้ว ถูกกล่าวหาว่าจ่ายเกินจริง ผ่านผู้รับเหมา
ที่แต่งขึ้น ผู้เล่นเปิดคดี สืบสวน แล้วปิดคดีด้วยการกู้เงินคืนบางส่วนหรือ
ทั้งหมด (one-off credit) หรือยกฟ้อง (ไม่มีผลต่อ ledger)

## การตัดสินใจ

### 1. ชื่อที่ใช้เป็น placeholder ทั่วไป ไม่ผูกที่ตั้งเลย

- ผู้รับเหมา: `"Contractor Alpha"` — ชื่อ generic ไม่ใช่ชื่อบริษัทจริงใดๆ
- หน่วยงาน: `"a local public-works sub-office"` — **ตั้งใจไม่ระบุชื่อ
  หน่วยงานหรือสถานที่ใดเลย** (ไม่มีจังหวัด อำเภอ ตำบล หรือชื่อกรม/สำนักงาน
  จริงใดๆ ผูกอยู่) — ต่างจาก "district roads office of [จังหวัดจริง]" ที่
  จะเข้าข่ายกล่าวหาสถานที่จริงตาม plan §7

### 2. การตรวจที่ทำจริง ไม่ใช่แค่ disclaimer

ตามที่ plan §7 เตือนว่า disclaimer อย่างเดียวไม่พอ นี่คือสิ่งที่ตรวจจริง
ก่อนเลือกชื่อทั้งสอง:
- ไม่ได้ค้นจาก company registry, สารบบราชการ, หรือ `source_tags`
  (`name`, `addr:*`, `operator`) ของ MapPack ใดๆ เลย — สร้างจาก noun
  ภาษาอังกฤษทั่วไป ("contractor", "office") ไม่ใช่ชื่อเฉพาะ
- ทดสอบด้วย `ScriptedStoryline_LabelsCarryNoLocationMarker` ที่ grep หา
  marker สถานที่ (จังหวัด/อำเภอ/ตำบล/Province/Bangkok/Thailand) ใน label
  ทั้งสอง — ต้องไม่พบเลย
- `AllegedOverpaymentThb` ผูกกับ `CommittedProject.TotalPaid` ของโครงการ
  จริงที่ผู้เล่น commit เอง ไม่ใช่โครงการหรือสถานที่ที่ระบบแต่งขึ้นเอง —
  คดีจึง "เชื่อม" กับ project ที่ผู้เล่นเลือกเอง ไม่ใช่สถานที่ที่ระบบยัด
  เยียดให้

### 3. Machine-enforced control: storyline API รับ MapPack type ไม่ได้เลย

`CorruptionCaseEthicalControlTests` (ในไฟล์ `CorruptionCaseTests.cs`)
สแกนทุก type ใน namespace `Thaivia.Core.Simulation.Storyline` ด้วย
reflection แล้วปฏิเสธ method/property/constructor สาธารณะใดๆ ที่รับหรือ
คืนค่าเป็น type จาก namespace `Thaivia.Core.MapPack`
(`PolygonFeature`, `LineFeature`, `SourceTags`, `RoadEdge`,
`GeographyBase`, ...) — ทำให้ระบบนี้ **ไม่มีทางรับ source feature id/
name/address จริงได้เลยทางโครงสร้าง** ไม่ใช่แค่ "ไม่ได้ตั้งใจทำ" มี
positive control (`DetectorItself_FlagsARealMapPackType`) พิสูจน์ว่า
detector จับ type จริงได้ (mirror ของ G6-04's
`BuildingArchetypeEthicalControlTests` และ G4's
`IncidentEngineTests.EthicalControl_...`)

### 4. Asset recovery เป็น one-off เท่านั้น ไม่มีกฎหมายจริงถูกอ้างถึง

`CorruptionCaseEngine.ResolveWithRecovery` ทำได้ครั้งเดียวต่อคดี (status
ต้อง `UnderInvestigation` เท่านั้น เปลี่ยนเป็น `Resolved` ซึ่งเป็น terminal
ทันที) — เรียกซ้ำถูก reject เสมอ พิสูจน์ด้วย
`ResolvingAnAlreadyResolvedCase_IsRejected_NeverDoubleCredits` ไม่มี
มาตราหรือกฎหมายไทยจริงใดถูกอ้างในโค้ด/comment ที่ไหนเลย — recovery
อธิบายแค่ "a ledger credit representing recovered funds"

## ผลกระทบ

- 297 dotnet tests ผ่าน (282 เดิม + 15 ใหม่) —
  `docs/evidence/g6-09-dotnet-test.log`
- Scope gap เดียวกับ G6-06/G6-08: `CorruptionCase` ยัง in-memory เท่านั้น
  ไม่ persist ใน SaveGame รอบนี้ — บันทึกไว้ตรงๆ ไม่ใช่ปิดเงียบ
