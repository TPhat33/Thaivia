# ADR-0031: Road preset catalog (2-3 presets) สำหรับ NewRoadConnector

- สถานะ: Accepted
- วันที่: 2026-09-26
- ผู้เกี่ยวข้อง: wave 8 / G6-05 (Opus supervisor brief), spec §9
- ไม่ supersede ADR ใด

## บริบท

Plan §9 กำหนดชัดว่าเครื่องมือถนนใหม่ของผู้เล่นเริ่มจาก "polyline ใหม่ 2-3
control points พร้อม preview ไม่เริ่ม arbitrary Bezier editor" — วินัย
เดียวกันนี้ควรใช้กับการเลือก **lane/capacity** ของถนนใหม่ด้วย ไม่ใช่ให้
ผู้เล่นพิมพ์ lane count เป็นตัวเลขอิสระ ก่อนหน้านี้
`NewRoadConnectorDraft`/`PlannedRoadSegment` ไม่มี field เรื่อง lane เลย —
ถนนใหม่มีแค่ node สองฝั่ง+ความยาว ไม่มีสมมติฐานเรื่อง capacity ใดๆ

## การตัดสินใจ

### 1. 3 preset ตรงตาม spec ตัวอย่าง

`RoadPreset` enum: `Soi` (1 เลน), `Local` (2 เลน, default), `Arterial`
(4 เลน) — ตรงกับตัวอย่างใน TASKS.json ("soi/alley 1-lane, local 2-lane,
arterial 4-lane") เก็บเป็น data ที่ inspect ได้ผ่าน `RoadPresetCatalog`
(`DisplayName`, `LaneCount`, `CapacityVehPerTick`, `AllPresets`)

### 2. Capacity คำนวณจาก `LinkCapacity.VehPerTickPerLane` ตัวเดียวกับที่ใช้กับ OSM edge จริง

`RoadPresetCatalog.CapacityVehPerTick(preset) = LaneCount(preset) *
LinkCapacity.VehPerTickPerLane` — สูตรเดียวกับ
`LinkCapacity.BaseCapacityVehPerTick` อ่าน `lanes` tag ของ way จริง
พิสูจน์ด้วย `CapacityUsesTheSamePerLaneConstantAsAnExistingOsmEdge` (สร้าง
`RoadEdge` จริงที่มี `lanes` tag เท่ากับ preset แล้วเทียบ capacity ตรงๆ)
— preset ไม่ใช่ตัวเลขที่คิดแยกออกมาเอง

### 3. Preset ใช้กับถนน "ใหม่" เท่านั้น ไม่แตะ tag ของ way ที่มีอยู่แล้ว

`RoadPreset` ไม่มีทางถูกเรียกใช้กับ `RoadEdge` ที่มาจาก MapPack จริงเลย —
`LinkCapacity.BaseCapacityVehPerTick(RoadEdge)` ยังอ่านจาก `edge.SourceTags`
เหมือนเดิมทุกประการ (ไม่ได้แก้ไฟล์นั้นเลยในรอบนี้) `RoadPreset` ถูกใช้ใน
`NewRoadConnectorDraft`/`PlannedRoadSegment` เท่านั้น ซึ่งเป็นชนิดข้อมูล
แยกจาก `RoadEdge` โดยสถาปัตยกรรมอยู่แล้ว (PlannedRoadSegment's doc
comment เดิม: "never merged into RoadGraph") — แยกกันทาง type ทำให้
"preset ทับ tag จริง" เป็นไปไม่ได้ทางโครงสร้าง ไม่ใช่แค่ระเบียบที่บอกไว้

### 4. Scope gap ที่ยังไม่ปิด (บันทึกตรงไปตรงมา)

`NetworkDemandAssignment.DeduplicateWayIds` กรอง synthetic wayId ที่ `< 0`
ทิ้งไปแล้วตั้งแต่ ADR-0022 — เส้นทางที่ผ่าน `PlannedRoadSegment`
(รวมถึง crossing) จึงไม่เคยถูกคิด congestion/queue capacity เลยในปัจจุบัน
ADR นี้ **ไม่ได้ปิด gap นั้น** เพียงทำให้ตัวเลข capacity ที่ควรใช้ (ถ้ามี
คนมาปิด gap นี้ในอนาคต) ถูกกำหนดไว้แล้วอย่าง inspectable และ consistent
กับสูตรเดิม ไม่ใช่เลขที่ประดิษฐ์เอาตอนนั้น — ดู
`RoadPresetCatalog`'s doc comment ที่ระบุเรื่องนี้ตรงๆ

### 5. Backward-compatible save field

`PlannedRoadSegment`/`SavedRoadSegment` เพิ่ม field `Preset`
(`RoadPreset`/string) เก็บ preset ที่ผู้เล่นเลือกตอน commit save เก่าก่อน
G6-05 ไม่มี field นี้ — `SaveSerializer.ReadOptionalPresetName` อ่านค่า
default เป็น `Local` เมื่อ field หาย (ระบุชัดว่าเป็นสมมติฐาน ไม่ใช่ข้อมูล
ที่กู้คืนได้จริง — AGENTS.md ข้อ 4) พิสูจน์ด้วย
`SaveMissingPresetField_LoadsWithDocumentedDefault_Local` ที่ตัด field
`"preset"` ออกจาก JSON จริงก่อนโหลด ไม่ใช่แค่สมมติว่า "ถ้าไม่ให้ก็คง
work"

## ผลกระทบ

- `NewRoadConnectorDraft` มี parameter ใหม่ `RoadPreset preset = RoadPreset.Local`
  (optional, default = เดิมทุกประการสำหรับ caller ที่ยังไม่ระบุ) — call
  site เดิมทั้งหมด (BudgetConstrainedTradeOffTests,
  TwoSolutionsIntegrationTests, PlanningEngineTests) compile และผ่านโดย
  ไม่ต้องแก้ เพราะ default ตรงกับพฤติกรรมเดิม (ไม่มี lane info)
- 236 dotnet tests ผ่าน (221 เดิม + 15 ใหม่) —
  `docs/evidence/g6-05-dotnet-test.log`
