# ADR-0034: ขยาย incident strand จาก 2 เป็น 4 (ยังจำกัดจำนวนแน่นอน)

- สถานะ: Accepted
- วันที่: 2026-09-26
- ผู้เกี่ยวข้อง: wave 8 / G6-07 (Opus supervisor brief), ต่อยอด ADR-0020 (G4 incident system)
- ไม่ supersede ADR-0020 (ข้อสรุปเดิมเรื่อง rate-bounding/ethical constraint ยังใช้ได้ทั้งหมด)

## บริบท

G4 (ADR-0020) วาง incident system ไว้สองสาย: `NightDisorder`,
`StreetRacing` พร้อมวินัย rate-bounding ผ่าน `IncidentThresholds.
MinimumFullCycleTicks` G6-07 ขยายเป็น 4 สาย ตาม acceptance ที่ต้อง "reuse
IncidentEngine/IncidentThresholdCatalog's existing rate-bounding
discipline" และ "stay a small, named, finite list"

## การตัดสินใจ

### 1. สองสายใหม่: `RoadworksGridlock`, `IllegalWasteDumping`

เลือกจาก signal ที่ **มีอยู่แล้ว** ใน simulation (hour, noise index,
congestion ratio) ไม่เพิ่ม dependency ใหม่ และเลือกให้ **polarity/peak
hour ต่างจากสายเดิมจริง** ไม่ใช่แค่เปลี่ยนชื่อ:
- `RoadworksGridlock`: risk สูงเมื่อ congestion **สูง** (ตรงข้ามกับ
  `StreetRacing` ที่ต้องการถนนโล่ง) peak ตอนกลางวัน (hour ~12, width 5)
  ไม่ใช่กลางคืน — พิสูจน์ด้วย
  `RoadworksGridlockRisk_IsHigherWhenCongested_UnlikeStreetRacing`
- `IllegalWasteDumping`: peak ที่ hour ~3.5 (ลึกกว่า `NightDisorder`'s
  ~1.5) — พิสูจน์ด้วย
  `IllegalWasteDumpingRisk_PeaksAtADifferentHourThanNightDisorder` ว่าที่
  hour 4 สายนี้ยังไต่ขึ้นในขณะที่ NightDisorder เริ่มลงแล้ว

### 2. ผ่าน ethical structural control เดิมโดยไม่ต้องเขียนใหม่

`IncidentEngineTests.EthicalControl_NoIncidentsApiTakesABuildingArchetypeParameter`
สแกน**ทั้ง namespace** `Thaivia.Core.Simulation.Mobility.Incidents` ด้วย
reflection — เมธอดใหม่ทั้งสอง (`RoadworksGridlockRisk`,
`IllegalWasteDumpingRisk`) ถูกตรวจครอบคลุมโดยอัตโนมัติ ไม่ต้องเขียน test
ซ้ำ (พิสูจน์แล้วว่า pass หลังเพิ่มโค้ด — ดู
`docs/evidence/g6-07-dotnet-test.log`)

### 3. รายการยังจำกัดแน่นอน (ไม่ใช่ config เปิดกว้าง)

`IncidentStrand` ยังเป็น enum ที่ตายตัว (ไม่ใช่ list ที่โหลดจาก config/
data ที่ใครก็เพิ่มได้โดยไม่ผ่าน review) — doc comment ของ enum ระบุตรงๆ
ว่าห้ามกลายเป็น "add more later without a cap"

### 4. Re-prove rate bound ด้วยทั้ง 4 สายพร้อมกัน ไม่ใช่แยกทีละสาย

`IncidentRate_IsHardBoundedOverALongRun_AllFourStrandsLiveSimultaneously`:
รัน 4 site (สายละ 1) พร้อมกัน risk=1.0 (worst case) 100,000 tick โดยใช้
threshold จริงจาก `IncidentThresholdCatalog` (ไม่ใช่ threshold สังเคราะห์
ที่ unit test อื่นใช้) ตัวเลขวัดจริง
(`docs/evidence/g6-07-incident-rate-bound-4strands-measured.log`):

| Strand | IncidentsTriggered / 100,000 ticks | MinimumFullCycleTicks | Theoretical max |
|---|---|---|---|
| NightDisorder | 944 | 105 | 953 |
| StreetRacing | 1099 | 90 | 1112 |
| RoadworksGridlock | 1563 | 63 | 1588 |
| IllegalWasteDumping | 848 | 117 | 855 |

ทุกสายอยู่ภายใน theoretical max เป๊ะ และต่ำกว่า 5% ของจำนวน tick ทั้งหมด
มาก (สูงสุด 1.6% สำหรับ RoadworksGridlock) — ไม่มี per-tick spawning

## ผลกระทบ

- `WorldState.StepIncidents`'s switch เพิ่ม 2 case; ไม่มี exhaustive
  switch อื่นใน production code ที่ต้องแก้ (ตรวจแล้วด้วย grep)
- 267 dotnet tests ผ่าน (263 เดิม + 4 ใหม่) —
  `docs/evidence/g6-07-dotnet-test.log`
