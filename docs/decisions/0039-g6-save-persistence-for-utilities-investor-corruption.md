# ADR-0039: ปิดช่องว่าง save-completeness ของ UtilitySource/InvestorProposal/CorruptionCase

- สถานะ: Accepted — ไม่ supersede ADR ใด แต่ทำให้ scope gap ที่ ADR-0033
  (section เกี่ยวกับ persistence), ADR-0035, ADR-0036 บันทึกไว้ตรงๆ ปิดลง
- วันที่: 2026-09-26
- ผู้เกี่ยวข้อง: wave 9 / Task 1 (Opus supervisor brief), spec §13,
  AGENTS.md ข้อ 5

## บริบท

Wave ที่แล้ว (G6-06/G6-08/G6-09) บันทึกไว้ตรงๆ ในทั้งสาม ADR ว่า
`UtilitySource`, `InvestorProposal`, `CorruptionCase` เป็น **in-memory
เท่านั้น** — ไม่อยู่ใน `SaveGame`/`SaveSerializer` เลย spec §13 กำหนดว่า
save ต้องครอบคลุม "mutable state/PlayerDelta/RNG/queues/projects" ทั้งสาม
ระบบนี้เป็น live simulation state ที่ผู้เล่นสร้างขึ้นจริง (วาง utility,
รับข้อเสนอนักลงทุนแล้วเงินเข้าบัญชีจริง, เปิดคดีทุจริตแล้วสืบสวนอยู่) —
save/load หนึ่งรอบจะทำให้ state เหล่านี้หายไปทั้งหมดโดยไม่มีการแจ้งอะไร
เลย ตรงข้ามกับ ADR-0021 ที่ปิด gap แบบเดียวกันสำหรับ G4 mobility state ไป
แล้ว

## การตัดสินใจ

### 1. ต่อเข้า SaveGame + ComputeStructuralHash ในคอมมิตเดียวกัน (วินัยเดียวกับ ADR-0021)

`SaveGame` ได้ 3 collection ใหม่: `UtilitySources`, `InvestorProposals`,
`CorruptionCases` (DTO ใหม่ `SavedUtilitySource`/`SavedInvestorProposal`/
`SavedCorruptionCase` ใน `SavedRecords.cs`) `WorldState.CaptureSave` และ
`WorldState`'s restore constructor เติมทั้งสามพร้อมกัน และ
`ComputeStructuralHash` เพิ่ม field ทั้งสามในคอมมิตเดียวกันทันที (ไม่ใช่
เพิ่ม save ก่อนแล้วค่อยเพิ่ม hash ทีหลัง) — เหตุผลเดียวกับ ADR-0021: ถ้า
hash ไม่ครอบ state ใหม่ two-worlds-same-commands test จะ "ผ่านหลอก" แม้
utility/investor/corruption state ต่างกันจริง (พิสูจน์ตรงจุดนี้โดย
`StorylineAndUtilitySaveRoundTripTests.AllThreeMinorSystems_ContributeToComputeStructuralHash_SoLosingThemWouldBeDetected`)

### 2. Restore ตรงเข้า backing collection เหมือน subsystem อื่นที่มีอยู่แล้ว

`UtilitySource`/`InvestorProposal`/`CorruptionCase` ถูก restore ตรงเข้า
`_utilitySources`/`_investorProposals`/`_corruptionCases` แทนที่จะผ่าน
`AddUtilitySource`/`AddInvestorProposal`/`AddCorruptionCase` (validating
methods) — เหตุผลเดียวกับที่ `RoadWorksZone`/`BusRoute`/`SignalInstance`/
`IncidentSite` ทำอยู่แล้วใน restore constructor: ข้อมูลถูก validate ไปแล้ว
ครั้งหนึ่งตอนถูกเพิ่มเข้ามาจริงๆ ก่อนถูก capture เข้า `SaveGame`

### 3. `InvestorProposal`'s nullable fields ต้องการ JSON null ที่ชัดเจน ไม่ใช่ field หาย

`InvestorProposal.AcceptedAtTick`/`ConditionDeadlineTick`/
`BaselineRequiredKindCount` เป็น `long?`/`int?` ที่มีค่าเฉพาะตอน proposal
ผ่าน `Offered` ไปแล้ว — `JsonRequire` ได้ `NullableInt64`/`NullableInt32`
ใหม่ (คู่กับ `NullableString` ที่มีอยู่แล้วสำหรับ MapPack) ที่ต้องการ field
**อยู่จริงแต่เป็น JSON `null`** ไม่ใช่ field หายไปเฉยๆ — รักษาวินัยเดียวกับ
ทุก field อื่นใน save format: required เสมอ ไม่มี default เงียบๆ

### 4. Mid-state ที่ทดสอบ ไม่ใช่ trivial/starting state

`StorylineAndUtilitySaveRoundTripTests` capture ตอน utility source ถูกวาง
จริง, investor proposal อยู่ที่ `Accepted` (เงินเข้าบัญชีแล้ว, condition
window กำลังนับถอยหลัง, ทุก nullable field มีค่า) พร้อม required project
ที่ commit แล้วและมีเงิน**ทั้งจ่ายแล้วบางส่วนและยัง reserve บางส่วน**
(milestone สองก้อน จ่ายก้อนแรก) และ corruption case ที่ผ่าน
`BeginInvestigation` ไปแล้ว (ไม่ใช่ `Open` เฉยๆ) — ตาม pattern เดียวกับ
`MobilitySaveRoundTripTests` ที่ ADR-0021 บันทึกไว้ว่าต้อง capture ตอน
"state กึ่งกลาง" ไม่ใช่ state เริ่มต้น

`SaveLoadTests.RunPhaseOne`/`RunPhaseTwo` (canonical
`SaveThenRestore_ThenContinue_ProducesTheIdenticalHashAsNeverHavingSaved`
test) ก็ถูกขยายให้มี utility source + investor proposal (`RunPhaseOne`)
และ corruption case ที่เปิดหลัง milestone จ่ายจริงแล้ว (`RunPhaseTwo`) —
พิสูจน์ save → load → continue N ticks ≡ ไม่เคย save เลย ครอบคลุม state
ใหม่ทั้งสามด้วย ไม่ใช่แค่ test แยกต่างหาก

## ผลที่ตามมา

- `dotnet test`: 307 (หลัง Task Zero) → 310 (3 test ใหม่ของ
  `StorylineAndUtilitySaveRoundTripTests` — `SaveLoadTests` เดิม 7 ตัวถูก
  ขยาย ไม่ใช่เพิ่มตัวใหม่) ดู `docs/evidence/g8-dotnet-test.log`
- Invariant เดิมที่ยืนยันซ้ำหลังแก้ (ไม่มีตัวไหน regress):
  `WorldStateDeterminismTests.TwoWorlds_SameSeedSameCommands_ProduceIdenticalStructuralHash`,
  `GatewayConservationTests.TotalTripsAcrossManyTicks_WithAGatewayCloseAndReopen_ConserveExactly`,
  `PlanningEngineTests.Estimate_DoesNotMutateTheWorld_HashAndEveryRngStreamUnchanged`,
  `PlanningEngineTests.AcceptingThenPayingMilestones_NeverDoubleCharges_TotalDeductedEqualsFixedCostExactlyOnce`
- Scope gap ที่ ADR-0033/0035/0036 บันทึกไว้ ("in-memory เท่านั้น ยังไม่
  persist") **ปิดแล้ว** — ADR นี้ไม่ supersede เอกสารทั้งสามฉบับ (decision
  หลักของแต่ละฉบับเรื่อง model ของระบบยังถูกต้องเหมือนเดิม) แต่บันทึกไว้ที่
  นี่ว่า gap ที่ระบุไว้ปิดแล้วเมื่อไหร่และปิดอย่างไร
