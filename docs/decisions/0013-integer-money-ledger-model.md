# ADR-0013: เงินเป็น integer, reserve/charge แยกกัน, milestone-based payment

- สถานะ: Accepted
- วันที่: 2026-09-22
- ผู้เกี่ยวข้อง: wave 4 / G3 (Thaivia.Core.Simulation.Economy, .Planning)

## บริบท

IMPLEMENTATION_PLAN.th.md §12 กำหนดหลายข้อพร้อมกัน: เงินต้องเป็น integer
ไม่มี float ที่ไหนใน ledger เลย, แยก ledger เป็น recurring/opex/capex/
non-recurring, `available = cash - reserved`, accept project จองงบแต่
ไม่จ่ายซ้ำทั้งตอนรับและตอน milestone, cost ที่แน่นอนต้องแยกจาก predicted
impact ที่มี uncertainty, และ commit ต้อง revalidate geometry/conflict/
budget เป็น transaction เดียว

## การตัดสินใจ

1. **ทุก signature ใน `MoneyLedger` เป็น `long` เท่านั้น** ไม่มี
   `double`/`decimal` overload เลยแม้แต่ตัวเดียว — บังคับด้วย compiler
   ไม่ใช่แค่ convention ทดสอบซ้ำด้วย reflection ใน
   `MoneyLedgerTests.EveryPublicMoneyMember_UsesIntegerTypesOnly` (สแกน
   ทุก public property/method หา `float`/`double`/`decimal`)
2. **Reserve กับ Charge เป็นสอง operation แยกกันจริง**:
   `Reserve(kind, amount)` ย้ายจาก available ไป reserved โดยไม่แตะ cash
   เลย ส่วน `ChargeFromReservation(kind, amount)` หัก cash กับ reserved
   "พร้อมกันในเมธอดเดียว" เท่านั้น — ไม่มี code path ไหนหัก cash ของ
   โครงการที่ reserve ไว้โดยไม่ลด reservation คู่กัน ทำให้ "จองแล้วจ่าย
   เป็น milestone โดยไม่จ่ายซ้ำ" เป็นสมบัติเชิงโครงสร้างของ type ไม่ใช่
   วินัยที่ caller ต้องจำเอง
3. **`PlanningEngine.CommitRelocation`/`CommitNewRoadConnector`
   ตรวจทุกเงื่อนไข (geometry ปลายทางมีอยู่จริง, milestone รวมเท่า fixed
   cost, งบพอ) ก่อน mutate อะไรเลยสักบรรทัด** — ถ้ามี failure แม้แต่ข้อ
   เดียว คืน `CommitResult(Success=false, ...)` โดยไม่แตะ ledger/building
   state/revision เลย (ทดสอบ transactional rollback ทั้งกรณี budget ไม่
   พอและกรณี geometry ผิดใน `PlanningEngineTests`)
4. **Commit ซ้ำด้วย draft id เดิมเป็น idempotent no-op** — เช็ค
   `world.Projects` ก่อนทุกอย่าง ถ้ามี project ที่ id เดียวกันและยังไม่
   Cancelled คืนตัวเดิมทันทีโดยไม่ reserve ซ้ำ/ไม่ apply geometry ซ้ำ/ไม่
   เพิ่ม revision ซ้ำ (`ConfirmingTheSameDraftTwice_DoesNotDeductBudgetTwice`)
5. **`ImpactRange` (predicted impact) กับ `FixedCostThb` (cost แน่นอน)
   เป็นคนละ shape โดยเจตนา**: `ImpactRange` ไม่มี constructor แบบตัวเลข
   เดียว มีแต่ expected/min/max — UI ฝั่งไหนก็ตามที่อ่านค่านี้ถูกบังคับ
   ให้ตัดสินใจว่าจะแสดง range อย่างไร จะแสดงเป็นตัวเลขเดียวเหมือนแน่นอน
   ไม่ได้ ตรงข้ามกับ `FixedCostThb` ซึ่งเป็น `long` เปล่าๆ เพราะเป็นค่า
   แน่นอนจริง (spec §12: "cost ที่กำหนดแน่นอนแยกจาก predicted impacts ที่
   มี uncertainty")
6. **Draft ที่ยังไม่ commit ไม่เคย reserve อะไรเลย** — `Estimate*` เป็น
   pure read ล้วนๆ (ดู ADR ด้านล่างเรื่อง test coverage) ดังนั้น
   `CancelDraft()` จึงไม่ต้องรับ WorldState เลยด้วยซ้ำ ฟรีโดยไม่มีอะไร
   ต้อง cancel จริง

## ผลที่ตามมา

- ผลข้างเคียงของการแยก Reserve/Charge คือ CommittedProject ไม่จำเป็นต้อง
  เก็บ reference กลับไปยัง `ProjectDraft` เดิมหลัง commit แล้ว (geometry
  effect ถูก apply ทันทีตอน commit และเก็บใน `BuildingSimState`/
  `PlannedRoadSegment` แยกไปแล้ว) — ทำให้ save format ของ
  `CommittedProject` เล็กและ restore ได้ตรงไปตรงมา (ดู ADR-0014)
- Cancellation fee ของโครงการที่ commit แล้ว (`CancelProject`) ใช้
  `ChargeDirect` ไม่ใช่ `ChargeFromReservation` เพราะ fee ไม่เคยถูก
  reserve ไว้ล่วงหน้า เป็นค่าใหม่ที่เกิดตอนยกเลิกเท่านั้น
