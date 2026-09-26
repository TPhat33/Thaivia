# ADR-0033: Utilities เป็น capacity/reach ตาม network เหมือน accessibility

- สถานะ: Accepted
- วันที่: 2026-09-26
- ผู้เกี่ยวข้อง: wave 8 / G6-06 (Opus supervisor brief), spec §11/§16
- ไม่ supersede ADR ใด (ต่อยอด ADR-0015's accessibility-graph-metric decision)

## บริบท

Plan §16 (G6): "utilities แบบ capacity/reach" plan §11: "accessibility
คำนวณจาก network ไม่ใช่ radius ที่เดินข้ามคลองได้" — กติกาเดียวกับที่
`AccessibilityGraph`/`AccessibilityNeed` (ADR-0015) ใช้กับ job access ต้อง
ใช้กับ utilities ด้วย ห้ามมี radius fallback

## การตัดสินใจ

### 1. Reuse `AccessibilityGraph` ตรงๆ ไม่สร้าง graph ใหม่

`WorldState.ComputeUtilityCoverageScore` เรียก `BuildAccessibilityGraph()`
เดียวกับที่ `ComputeAccessibilityScore` ใช้ (รวม `PlannedRoadSegment` ที่
ผู้เล่นสร้างแล้วด้วย) — ไม่มี fallback แบบ Euclidean ที่ไหนเลยในเส้นทางนี้

### 2. Coverage = reach score x capacity penalty เท่านั้น ไม่มี "unlimited"

`UtilityCoverage.ComputeScore(networkDistance, demand, capacity)`:
- reach score: สูตรเดียวกับ `AccessibilityNeed.ComputeScore` เป๊ะ (null/
  unreachable = 0, ไม่ใช่ "โอเค") แต่ reference distance แยกกัน
  (`UtilityCoverage.ReferenceDistanceMeters=400m` vs
  `AccessibilityNeed.ReferenceDistanceMeters=500m`) เพราะเป็นปริมาณคนละ
  อย่าง (ระยะเดินของคน vs ระยะไกลสุดที่โครงสร้างสาธารณูปโภคยังให้บริการ
  ได้ดี) — ทั้งคู่เป็น documented game-design constant ไม่ใช่ค่าที่วัดจริง
- capacity penalty: `demand/capacity <= 1.0` → ไม่มี penalty เลย;
  `capacity <= 0` กับ demand ใดๆ > 0 → คะแนน 0 เสมอ (ไม่มีทางแทนด้วย
  "unlimited"); `demand/capacity > 1.0` → `reach / utilization` (ยิ่งเกิน
  ยิ่งแย่ลงต่อเนื่อง ไม่ใช่ threshold แข็ง)

### 3. Demand คำนวณจาก cohort จริง ไม่ใช่ตัวเลขสมมติ

`WorldState.ComputeUtilityDemandUnitsAtSource` รวม `HouseholdCount` ของ
ทุก cohort ที่ home อยู่ใกล้ source นี้ที่สุดทาง network (ในบรรดา source
ประเภทเดียวกันทั้งหมด) — 1 household = 1 demand unit/tick เป็น documented
simulation_assumption (บันทึกใน `UtilitySource`'s doc comment) พิสูจน์ด้วย
`CapacityExhaustion_DegradesCoverageComparedToAmpleCapacity_SameGeometryAndDemand`
ที่ fix จำนวนครัวเรือนเป็น 5 exact (`minHouseholdsPerResidentialBuilding =
maxHouseholdsPerResidentialBuilding = 5`) แล้วเทียบ capacity 1000 vs 1 บน
geometry เดียวกันเป๊ะ

### 4. Adversarial fixture: reuse SimulationFixtures.BuildDetourRoadGraph

ใช้ fixture เดียวกับที่ `AccessibilityGraphTests` ใช้พิสูจน์ network !=
straight-line (ช่องว่างตรง 2m แต่ network 502m ผ่านสะพานอ้อม) แทนที่จะสร้าง
fixture ใหม่ซ้ำซ้อน — วางอาคารที่ DetourBankANode, utility source ที่
DetourBankBNode วัดจริง:
- Euclidean stand-in (2m) → reach score > 90
- ค่าจริงจาก `world.ComputeUtilityCoverageScore` (network 502m) → **0**
  ต่างกันมากกว่า 50 แต้มเป๊ะ (ดู
  `AdversarialFixture_NetworkReachDiffersMateriallyFromStraightLineDistance`,
  ตัวเลขจริงใน `docs/evidence/g6-06-adversarial-and-capacity-measured.log`)

### 5. CohortNeedsCalculator ได้ overload ใหม่ ไม่กระทบของเดิม

`CohortNeeds` เพิ่ม field `Utilities` (default = 100, "documented
baseline" เหมือน `BaselineSafety` ที่มีมาก่อน G4) `CohortNeedsCalculator`
ได้ overload 5-arg ใหม่ที่รับ `utilitiesCoverage` ตรงๆ; overload 3/4-arg
เดิมยังคืนค่าเท่าเดิมทุกประการ (ทดสอบด้วย
`CohortNeedsCalculator_NewOverload_CarriesTheGivenUtilitiesScore_OldOverloadsKeepTheBaseline`)
— `WorldState.ComputeCohortNeeds()` (default pipeline) **ไม่ได้ต่อสาย
utilities เข้าไปอัตโนมัติ** ในรอบนี้ เพื่อไม่เสี่ยงเปลี่ยนพฤติกรรมที่มีอยู่
ของ G3-G5 tests ที่ assert ค่า CohortNeeds ที่แน่นอน — การต่อสายอัตโนมัติ
เข้ากับ default cohort-needs pipeline (ตัดสินใจว่า area ใดควรเริ่มมี
utility source ผูกกับ default scenario) เป็นงาน content-authoring แยก
(เกี่ยวข้องกับ G6-12 ที่ยัง blocked เพราะไม่มี real per-area road graph)

## Scope gap ที่ยังไม่ปิด (บันทึกตรงไปตรงมา)

`UtilitySource` list ยังเป็น **in-memory เท่านั้น** — ยังไม่ใช่ส่วนหนึ่งของ
`SaveGame`/`SaveSerializer` การ save/load รอบหนึ่งจะทำให้ registered
utility source หายไป (ต่างจาก `BusRoute`/`SignalInstance` ที่ persist
แล้ว) เหตุผลที่ตัดสินใจไม่ทำรอบนี้: acceptance criteria ของ G6-06 ไม่ได้
พูดถึง persistence และการเพิ่ม field ใหม่เข้า save format + structural
hash + restore constructor มีความเสี่ยงสูงกว่าที่ควรรับใน task เดียวเมื่อ
เทียบกับงานที่เหลืออีก 4 tasks ในรอบนี้ — บันทึกไว้ตรงๆ แบบเดียวกับที่
ADR-0031 บันทึกเรื่อง `NetworkDemandAssignment`'s scope gap ไม่ใช่ปิดแบบ
เงียบๆ

## ผลกระทบ

- 263 dotnet tests ผ่าน (246 เดิม + 17 ใหม่) —
  `docs/evidence/g6-06-dotnet-test.log`
