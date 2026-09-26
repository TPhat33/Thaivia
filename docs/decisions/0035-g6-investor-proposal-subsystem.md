# ADR-0035: Investor-proposal subsystem (หนึ่งระบบย่อ ตามที่ plan §16 กำหนด)

- สถานะ: Accepted
- วันที่: 2026-09-26
- ผู้เกี่ยวข้อง: wave 8 / G6-08 (Opus supervisor brief), spec §12/§16
- ไม่ supersede ADR ใด

## บริบท

Plan §16 (G6): "ข้อเสนอนักลงทุนหนึ่งระบบย่อ" — ระบบย่อยเดียว ไม่ใช่
เศรษฐกิจนักลงทุนเต็มรูป ต้องใช้วินัย transactional เดียวกับ
`PlanningEngine` (reserve/pay, ไม่ double-charge)

## การตัดสินใจ

### 1. Capped fixed-amount offer, เงื่อนไขเดียว

`InvestorProposal`: จำนวนเงินคงที่บวก (validate ที่ constructor, ห้าม
<=0 — "capped" คือจำนวนตายตัว ไม่ใช่ % หรือ open-ended) + เงื่อนไขเดียว
("commit โครงการประเภท X ภายใน N tick หลังรับข้อเสนอ") + offer expiry
tick ของตัวข้อเสนอเอง (แยกจาก condition deadline)

### 2. Accept เป็น atomic credit ครั้งเดียว, Decline/Expire ไม่แตะ ledger เลย

`InvestorProposalEngine.Accept` เรียก `MoneyLedger.Deposit` **ครั้งเดียว**
ต่อการ accept ที่สำเร็จหนึ่งครั้ง (accept ซ้ำถูก reject เพราะ status ไม่
ใช่ Offered แล้ว — พิสูจน์ด้วย `Accept_Twice_IsRejected_NeverDoubleCredits`)
`Decline`/offer หมดอายุแบบไม่เคย accept ไม่มี code path เรียก
`MoneyLedger` เมธอดใดเลย (พิสูจน์ด้วย
`Decline_HasNoLedgerEffectAtAll`/`UnacceptedOffer_PastExpiry_LapsesToExpired_NoLedgerEffect`)

### 3. เงื่อนไขตรวจจาก WorldState.Projects จริง ไม่ใช่ flag ปลอม

`CommittedProject` ไม่มี timestamp ว่า commit ตอน tick ไหน จึงตรวจเงื่อนไข
ด้วยวิธี snapshot **จำนวน** โครงการประเภทที่กำหนดตอนรับข้อเสนอ
(`BaselineRequiredKindCount`) แล้วเงื่อนไขผ่านเมื่อจำนวนปัจจุบัน
**มากกว่า** baseline เท่านั้น — ป้องกันโครงการที่ commit ไปแล้วก่อนรับ
ข้อเสนอจากมาช่วยผ่านเงื่อนไขย้อนหลัง พิสูจน์ด้วย
`PreexistingProjectOfRequiredKind_DoesNotRetroactivelySatisfyTheCondition`
(commit โครงการก่อน accept แล้วยืนยันว่ายัง Withdrawn อยู่ดี) ทางเลือกที่
พิจารณาแล้วไม่ทำ: เพิ่ม `CommittedAtTick` field ให้ `CommittedProject`
โดยตรง — ให้ผลแม่นกว่าเล็กน้อยแต่ผลกระทบกว้างกว่า (constructor,
SaveSerializer, WorldState.Restore) ไม่คุ้มกับความเสี่ยงเมื่อเทียบกับ
solution ปัจจุบันที่พิสูจน์แล้วว่าถูกต้องด้วย test ตรงๆ

### 4. Clawback ไม่มีทางทำให้เงินสดติดลบ — บันทึกจำนวนจริงที่ยึดได้

ถ้าเงื่อนไขไม่ผ่านภายใน deadline `EvaluateExpiryAndCondition` ยึดคืน
`Min(FundingAmountThb, ledger.Available)` ไม่ใช่เต็มจำนวนเสมอ — ถ้าผู้เล่น
ใช้เงินไปแล้วต่ำกว่าที่ต้องคืน ระบบยึดเท่าที่มีจริง (ไม่ force ติดลบ, ไม่
throw) และบันทึก `AmountClawedBack` เป็นจำนวนจริงที่ยึดได้ ไม่ใช่จำนวนที่
"ควรจะเป็น" พิสูจน์ด้วย
`ClawbackNeverForcesNegativeCash_RecordsTheActualAmountTakenBack` (ใช้เงิน
เกือบหมดเหลือ 50,000 จากที่ควรยึด 300,000 → ยึดได้แค่ 50,000, เงินสด
เหลือ 0 พอดี ไม่ติดลบ)

### 5. Reuse `LedgerAccountKind`/`PlayerDeltaKind` ที่มีอยู่ ไม่สร้าง ledger kind ใหม่

เงินทุนจากนักลงทุนใช้ `LedgerAccountKind` ที่ caller เลือก (ตัวอย่าง test
ใช้ `NonRecurring`) — ไม่เพิ่ม enum value ใหม่ เพิ่มแค่
`PlayerDeltaKind.InvestorProposalAcceptance` หนึ่งตัวสำหรับบันทึก delta
ทั้งตอน accept และตอน withdraw (ไม่ใช่ผูกกับ ProjectKind/CommittedProject
โดยตรง เพราะข้อเสนอนักลงทุนไม่ใช่ project ที่ต้อง milestone)

## Scope gap ที่ยังไม่ปิด (บันทึกตรงไปตรงมา)

- `InvestorProposal` list เป็น in-memory เท่านั้น ยังไม่ persist ใน
  SaveGame (เหตุผลเดียวกับ ADR-0033's utility-source gap: ลดความเสี่ยง
  เมื่อเทียบกับงานที่เหลือในรอบนี้)
- ไม่มี mechanism อัตโนมัติเรียก `EvaluateExpiryAndCondition` ทุก tick ใน
  `WorldState.SimulateTick` — ต้อง caller (เกม/G6-09's storyline) เรียก
  เองตามจังหวะที่เหมาะสม เพราะ "หนึ่งระบบย่อย" ตาม plan §16 ไม่ควรผูกเข้า
  tick loop หลักโดยไม่จำเป็นในรอบนี้

## ผลกระทบ

- 282 dotnet tests ผ่าน (267 เดิม + 15 ใหม่) —
  `docs/evidence/g6-08-dotnet-test.log`
