# ADR-0032: Country/area-select progress model เป็น pure C# แยกจาก UI

- สถานะ: Accepted
- วันที่: 2026-09-26
- ผู้เกี่ยวข้อง: wave 8 / G6-10 (Opus supervisor brief), spec §16, G6-11 (blocked)
- ไม่ supersede ADR ใด

## บริบท

Plan §16 (G6): "หน้าประเทศใช้เลือกฉากและติดตามความก้าวหน้า ไม่เปิดทุก
พื้นที่พร้อมกัน" หน้าจอจริง (G6-11) เป็น Unity UI ซึ่งยัง `blocked` (ไม่มี
Unity Editor ในสภาพแวดล้อมนี้ — ADR-0002) แต่ตัว **model** ของ "พื้นที่
ไหนล็อก/ปลดล็อก/กำลังเล่น/จบแล้ว" ไม่ต้องพึ่ง Unity เลย เหมือนที่
`ScenarioProgression` (G5-05) เป็น pure C# ไปแล้วก่อน UI ของมันจะมี

## การตัดสินใจ

### 1. Reuse ตรงๆ กับ ScenarioProgression's linear-sequencing model

`CountryProgression` มีรูปร่างเดียวกับ `ScenarioProgression`: list พื้นที่
เรียงลำดับ + `Evaluate(...)` ที่คืนค่า status ต่อรายการแบบ pure ไม่มี field
mutable ภายใน ไม่มี Unity/UI reference

### 2. Lock state เป็น pure function ของ recorded completion เท่านั้น — ไม่มี wall-clock

`Evaluate(completedAreaIds, startedAreaIds)` รับ **set ที่ผู้เรียกส่งมา
ตรงๆ** (จาก save file ของแต่ละพื้นที่จริง ไม่ใช่คำนวณเองในนี้) เป็น input
เดียว ไม่มี `DateTime`/clock API ใดๆ ในไฟล์นี้เลย พิสูจน์ด้วย 2 กลไก:
- `Evaluate_IsPureAndDeterministic`: เรียกด้วย input เดียวกัน (คนละ
  instance ของ `HashSet` แต่เนื้อหาเดียวกัน) สองครั้ง ต้องได้ผลเดียวกัน
  ทุกประการ
- `EthicalControl_TypeReferencesNoWallClockApi`: grep source file ของ
  `CountryProgression.cs` เองหาคำว่า `DateTime`/`DateTimeOffset`/
  `Environment.TickCount`/`Stopwatch` — ถ้าใครเพิ่มเข้ามาทีหลัง (แม้แค่
  บรรทัดเดียว) test นี้ fail ทันที เป็น machine-enforced control ไม่ใช่
  แค่ comment (mirror ของ control pattern ที่ G6-04/G4 ใช้ แต่ตรวจ source
  text แทน reflection เพราะสิ่งที่ห้ามคือ "การเรียก API" ไม่ใช่ "รูปร่าง
  ของ type")

### 3. Completion "นอกลำดับ" ไม่ทำให้พื้นที่หลังๆ เปิด

ถ้า area 3 ถูก mark ว่า `Completed` แต่ area 2 ไม่เคยเริ่มเลย
`CountryProgression` ยังคง lock area 2 และ area 3 (ไม่เชื่อ flag
completion ของ area ตัวเองว่าหมายถึงเคยเข้าถึง predecessor ได้จริง) —
ป้องกัน record ที่เสียหาย/แก้เองไม่ให้เปิดพื้นที่หลังข้ามลำดับ พิสูจน์ด้วย
`OutOfOrderCompletionRecord_StillLocksEverythingAfterTheGap`

## ผลกระทบ

- G6-11 (หน้าจอ Unity จริง) ยัง blocked เหมือนเดิม — ไม่ถูกปลดล็อกโดย ADR
  นี้ ยังต้องรอ Unity Editor ตาม ADR-0002
- 246 dotnet tests ผ่าน (236 เดิม + 10 ใหม่) —
  `docs/evidence/g6-10-dotnet-test.log`
