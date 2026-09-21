# ADR-0026: Tutorial/scenario progression -- ObjectiveDefinition/ScenarioProgression

- สถานะ: Accepted
- วันที่: 2026-09-21
- ผู้เกี่ยวข้อง: wave 6 / g5, spec §19

## บริบท

Plan §19 ต้องการ tutorial/scenario progression ก่อน G5 gate จะสมบูรณ์
แต่ art/UI/tutorial-UI ทำไม่ได้ในสภาพแวดล้อมนี้ (ไม่มี Unity) ส่วนที่
เป็น data + logic (objective definition, completion condition, check ที่
ตัดสินว่าผ่านหรือยัง) ทำได้และ test แบบ headless ได้เต็มที่

## การตัดสินใจ

### แยก "data" (`ObjectiveDefinition`) จาก "logic" (`IObjectiveCheck`)

`ObjectiveDefinition` มีแค่ id/title (string) + `IObjectiveCheck` object
— ไม่มี behavior ของตัวเอง `IObjectiveCheck` เป็น interface เดียว
(`IsSatisfied(WorldState) -> bool`) implementation ทุกตัวใน `Checks.cs`
เป็น wrapper บาง (thin) รอบ query ที่ `WorldState` มีอยู่แล้ว
(`ComputeAccessibilityScore`, `ComputeCohortNeeds`, `Ledger.Available`,
`Clock.CurrentTick`, `Projects`, `IncidentSites`) — namespace นี้ไม่สร้าง
ตัวเลขจำลองใหม่เอง อ่านอย่างเดียวแล้วเทียบ

### Sequencing: locked objective's check ไม่ถูกเรียกเลย

`ScenarioProgression.Evaluate` วนตามลำดับ, objective แรกที่ยังไม่ผ่านจะ
เป็น `InProgress` และทุกตัวหลังจากนั้น**ไม่ถูกประเมินเลย** (`Locked`
ทันทีโดยไม่เรียก `IsSatisfied`) — พิสูจน์ด้วย spy check ที่ throw ถ้าถูก
เรียก (`Evaluate_NeverEvaluatesALockedObjectivesCheck_SpyControlTest`)
เป็น mutation-style control test แบบเดียวกับที่ supervisor เคยขอใน wave
ก่อนๆ (proving the assertion actually bites, ไม่ใช่ vacuous)

### "Unlock" ไม่เท่ากับ "complete"

Objective ที่ unlock แล้ว (ประเมินจริง) อาจยัง `InProgress` ได้ ไม่ต้อง
`Completed` ทันที — `UnlockingAnObjective_IsNotTheSameAsCompletingIt`
พิสูจน์ด้วย fixture ที่ตั้งใจให้ objective ที่สามยัง fail (งบเหลือ
50,000 < เกณฑ์ 100,000) หลังจากสอง objective แรกผ่านแล้ว

### ทดสอบด้วย commit จริงผ่าน PlanningEngine เสมอ ไม่ mock world state

ทุก test ขับเคลื่อน objective ด้วยการเรียก `PlanningEngine.CommitRelocation`
จริง (ตัวเลข accessibility 0->98 เดียวกับที่ G3's two-solutions gate ใช้)
ไม่ได้ตั้งค่า `WorldState` ปลอมให้ผ่าน objective — เพื่อให้ progression
system พิสูจน์ตัวเองด้วยผลลัพธ์จริงของ simulation ไม่ใช่แค่ mock

## ผลกระทบ

- ไม่มี test เดิมแก้เลย (feature ใหม่ทั้งหมด, namespace ใหม่
  `Thaivia.Core.Simulation.Progression`) — 202 passed หลังเพิ่ม (196 + 6)
- `Evaluate`/`IsScenarioComplete` ไม่ mutate world (พิสูจน์ด้วย
  `Evaluate_DoesNotMutateTheWorld`)
- ยังไม่มี Unity-side UI ที่ render objective list — เหมือนเดิมทุก wave
  (ADR-0002) ส่วนนี้ dependency-ready รอ Unity Editor
