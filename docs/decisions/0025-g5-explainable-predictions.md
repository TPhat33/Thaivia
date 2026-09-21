# ADR-0025: Explainable predictions -- ReasoningInput/ExplainedImpact

- สถานะ: Accepted
- วันที่: 2026-09-21
- ผู้เกี่ยวข้อง: wave 6 / g5, spec §12

## บริบท

`ImpactRange` (จาก G3) มี uncertainty band อยู่แล้ว (ExpectedValue/
MinValue/MaxValue) แต่ไม่มี "เหตุผล" ว่าตัวเลขนั้นมาจากอะไร ผู้เล่นเห็น
แค่ตัวเลขกับช่วง ไม่เห็นว่า input ไหนที่ทำให้ได้ค่านั้น spec §12 สั่งให้
prediction ต้องพก "reasoning inputs" ควบคู่ uncertainty เสมอ

## การตัดสินใจ

### Type ใหม่ ไม่แก้ signature เดิม

`ReasoningInput` (Name, Value, Unit) และ `ExplainedImpact` (Impact:
`ImpactRange`, ReasoningInputs: list) เป็น type ใหม่ทั้งคู่
`PlanningEngine.Estimate*` เดิมสามตัว (`EstimateRelocationAccessImpact`,
`EstimateNewRoadAccessImpact`, `EstimateRoadWorksCapacityImpact`) **ไม่
แก้ signature เลย** — เพิ่ม `Explain*` คู่ขนาน
(`ExplainRelocationAccessImpact`, `ExplainNewRoadAccessImpact`,
`ExplainRoadWorksCapacityImpact`) ที่คำนวณ**ตัวเดียวกันเป๊ะ** (พิสูจน์ด้วย
`ExplainRelocationAccessImpact_MatchesThePlainEstimate_SameNumberDifferentShape`)
แล้วห่อด้วย reasoning inputs เพิ่มเติม เหตุผล: `ProjectDraft.PredictedImpact`
เป็น `ImpactRange` อยู่แล้วทั้งระบบ (BuildingRelocationDraft/
NewRoadConnectorDraft/RoadWorksDraft) การเปลี่ยน type ตรงนั้นจะกระทบ
constructor call site ที่ test เดิม (162+ ตัว) อ้างอิงอยู่ — เพิ่มแบบ
additive ปลอดภัยกว่า ไม่มีความเสี่ยงต่อ code ที่ผ่าน review มาแล้ว

### Reasoning input ต้อง "สอดคล้องจริง" ไม่ใช่แค่ "อยู่ข้างๆ"

`ExplainRelocationAccessImpact_CarriesUncertainty_AndRealReasoningInputs`
ไม่ได้แค่เช็คว่า list ไม่ว่าง — มัน assert ว่า
`projected_accessibility_score - current_accessibility_score ==
ExpectedValue` เป๊ะ (จาก reasoning input ตัวเดียวกับที่คำนวณ range) พิสูจน์
ว่า reasoning ที่คืนมาเป็นตัวที่**คำนวณ prediction จริง** ไม่ใช่ข้อความ
อธิบายที่แต่งขึ้นทีหลังแบบไม่ผูกกับตัวเลข

### Fixed cost แยกจาก prediction เสมอ (คงเดิมจาก G3)

`ProjectDraft.FixedCostThb` (long, แน่นอน) กับ `PredictedImpact`
(`ImpactRange`/`ExplainedImpact`, ไม่แน่นอน) เป็นคนละ field/type มาตั้งแต่
G3 — รอบนี้แค่ยืนยันซ้ำด้วย test
(`FixedCostAndPredictedImpact_AreDifferentFieldsOnTheDraft_NeverConflated`)
ไม่ได้เปลี่ยน design เดิม

## ผลกระทบ

- ไม่มี test เดิมแก้เลย (additive ล้วน) — 196 passed หลังเพิ่ม (190 + 6)
- `PlanningEngine.Explain*` ยัง pure เหมือน `Estimate*` เดิมทุกประการ
  (`ExplainMethods_DoNotMutateTheWorld` พิสูจน์)
