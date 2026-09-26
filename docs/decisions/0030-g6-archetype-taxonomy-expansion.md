# ADR-0030: ขยาย BuildingArchetype จาก 8 เป็น 12 (หยุดที่ขอบล่างของ ceiling)

- สถานะ: Accepted — **section 4 ("ผลข้างเคียงที่ต้องแก้: hash modulus")
  ถูก superseded โดย ADR-0038** (wave 9, Task Zero): mechanism ที่ section
  4 บันทึกว่าต้องระวัง ("การขยาย enum ต้องตรวจ fixture ใหม่ทุกครั้ง")
  ถูกแทนที่ด้วย append-only versioned catalog ที่ไม่ reshuffle เมื่อ
  catalog โตอีกต่อไป — decision เรื่องจำนวน archetype (12, ไม่ใช่ 16) และ
  ethical review ใน section 1-3 ยังคง Accepted เหมือนเดิม ไม่ถูกแตะต้อง
- วันที่: 2026-09-26
- ผู้เกี่ยวข้อง: wave 8 / G6-04 (Opus supervisor brief), spec §4/§16, AGENTS.md ข้อ 9
- ไม่ supersede ADR ใด

## บริบท

Plan §16 กำหนด G6 archetype count เป็น "12-16" — เป็น scope **ceiling**
ไม่ใช่ quota (plan §4: "จำนวนพื้นที่และ archetypes เป็น scope ceiling
เบื้องต้น ไม่ใช่เงื่อนไขที่บังคับให้เพิ่มแม้คุณภาพยังไม่ผ่าน")

## การตัดสินใจ

### 1. หยุดที่ 12 ไม่ใช่ 16

เพิ่ม 4 archetype ใหม่: `Hospital`, `Hotel`, `Warehouse`,
`ConvenienceStore` — รวมเป็น 12 (ขอบล่างของ ceiling) เหตุผลที่ไม่เพิ่ม
ถึง 16: สอง candidate area (`th-bkk-candidate-002`/`003`, G6-02) ยัง
`UNVERIFIED` — ยังไม่รู้ land use จริงในนั้น การเพิ่ม archetype เกินกว่า
โจทย์ gameplay ที่ตั้งไว้แล้ว (office/residential mix, canal/old-town
access, arterial/market congestion — ดู
`docs/data/candidate-areas-rationale.md`) จะเป็นการเติมข้อมูลที่ไม่มี
โจทย์รองรับจริง ขัดกับหลักการ "ไม่ pad เพื่อให้ถึงเพดาน" ที่ TASKS.json
เขียนไว้ตรงๆ

เกณฑ์เลือกทั้ง 4: แต่ละตัวต้องมี **time-of-day shape ที่ต่างจาก 8
archetype เดิมจริง** ไม่ใช่แค่ชื่อใหม่กับ curve ซ้ำ:
- `Hospital`: near-flat ทั้งวัน (width=10) — ต่างจาก archetype อื่นทุกตัว
  ที่มี peak แคบ
- `Hotel`: peak ตอนเย็น (19:00) แต่ baseline ต่ำกว่า `Residential`
  (0.10 vs 0.15) และ peak แคบกว่า (3.5 vs 5 ชม.)
- `Warehouse`: peak เช้ามืด (06:00) — ไม่ทับกับ `SmallFactory` (11:00)
- `ConvenienceStore`: amplitude (peak-baseline) ต่ำสุดในบรรดา archetype
  เชิงพาณิชย์ทั้งหมด ตั้งใจให้เป็นแบบนี้เพื่อพิสูจน์ว่าไม่ใช่ทุก
  archetype เชิงพาณิชย์ "ดัง" เท่ากันหมด — พิสูจน์ด้วย
  `ConvenienceStore_HasLowerDailyAmplitudeThanOtherCommercialArchetypes`
  (theory เทียบกับ Market/Retail/LateNightFoodStreet/SmallFactory)

### 2. Ethical shape review แบบเดียวกับ Temple ทุกตัว

ไม่มีตัวใดใน 4 ตัวใหม่เป็น religious/ethnic/community land use แต่ยัง
ผ่าน review เดียวกัน (ไม่มี peak level/baseline ที่ทำให้ archetype
"ดังตลอดวัน" แบบที่ fixed negative score จะมีรูปร่างแบบนั้น) — unit test
ต่อ archetype ใน `ActivityClockCatalogTests`/`NoiseIndexAndActivityClockTests.cs`

### 3. Machine-enforced control ใหม่ (ไม่ใช่แค่ comment)

`BuildingArchetypeEthicalControlTests.
EthicalControl_NoArchetypeCarriesAStaticScalarScoreIndependentOfActivityClock`
สแกนทุก type ใน namespace `Thaivia.Core.Simulation.Archetypes` ด้วย
reflection แล้วปฏิเสธ field/property ใดๆ ที่ map `BuildingArchetype` ตรงๆ
ไปยัง scalar เปลือย (`double`/`float`/`int`/`long`/`decimal`) — รูปร่าง
เดียวที่ตาราง "archetype นี้แย่ -5 เสมอ" จะมี มี positive/negative
control สองตัว (`DetectorItself_FlagsAKnownBadShape`,
`DetectorItself_DoesNotFlagATimeDependentStructuredValue`) พิสูจน์ว่า
detector ทำงานจริง ไม่ผ่านแบบ vacuous (mirror ของ
`IncidentEngineTests.EthicalControl_NoIncidentsApiTakesABuildingArchetypeParameter`
ที่ G4 เขียนไว้)

### 4. ผลข้างเคียงที่ต้องแก้: `WorldState.AssignArchetype`'s hash modulus

`AssignArchetype` ใช้ `hash(sourceId) % Enum.GetValues(...).Length` — การ
เพิ่ม enum จาก 8 เป็น 12 เปลี่ยน divisor และเปลี่ยนผลลัพธ์ของ id ที่เคย
ถูกเลือกมาเจาะจงให้ hash ไปที่ archetype หนึ่งๆ ตรวจพบจริงว่า:
- `SimulationFixtures.ResidentialBuildingId = 1000` เปลี่ยนจาก
  `Residential` เป็น `Market` ภายใต้ modulus ใหม่ — ย้ายไปที่ `1012`
  (ยืนยันแล้วว่า hash ไปที่ `Residential` ภายใต้ modulus 12)
- `BudgetConstrainedTradeOffTests.CohortABuildingId = 3000` เปลี่ยนจาก
  `Residential` เป็น `Hospital` — ย้ายไปที่ `3013`
- `SimulationFixtures.OfficeBuildingId = 2003` ยัง hash ไปที่ `Office`
  เหมือนเดิม (ไม่ต้องแก้)
- `BudgetConstrainedTradeOffTests.OfficeBuildingId = 3009` เปลี่ยนจาก
  `Office` เป็น `Hotel` — แต่ test นั้นต้องการแค่ "ไม่ใช่ Residential"
  (มี jobs) ไม่ได้ assert ชนิดที่แน่นอน จึงไม่ต้องเปลี่ยนค่า แก้แค่
  comment

ยืนยันด้วย `dotnet test` ก่อน/หลังแก้ constant: **207/207 ผ่านเหมือนเดิม
ทุกตัว** หลังแก้ (ไม่ใช่ partial fix) — ดู
`docs/evidence/g6-04-dotnet-test.log` (221 = 207 เดิม + 14 ใหม่)

## บทเรียนสำหรับอนาคต

การขยาย enum ใดๆ ที่ `AssignArchetype`/similar hash-mod-length pattern
ใช้อยู่ **ต้อง** ตรวจ fixture ที่ hardcode "sourceId X hashes to
archetype Y" ใหม่ทุกครั้ง ก่อน merge — ไม่ใช่แค่ build ผ่านแล้วจบ เพราะ
การ build ผ่านไม่ตรวจ semantic ของ hash เปลี่ยน (สิ่งที่เกือบเป็น false
green ในรอบนี้ถ้าไม่ตรวจ `dotnet test` ก่อนเขียน test ใหม่)
