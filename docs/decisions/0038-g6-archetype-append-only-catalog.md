# ADR-0038: Archetype assignment เป็น append-only versioned catalog แทน hash-mod-enum-length (supersedes ADR-0030 §4)

- สถานะ: Accepted — **supersedes ADR-0030 section "4. ผลข้างเคียงที่ต้อง
  แก้: `WorldState.AssignArchetype`'s hash modulus"**
- วันที่: 2026-09-26
- ผู้เกี่ยวข้อง: wave 9 / Task Zero (Opus supervisor brief), spec §13/§16,
  AGENTS.md ข้อ 3/4

## บริบท

ADR-0030 (wave 8) บันทึกไว้ตรงๆ ว่า `WorldState.AssignArchetype` ใช้
`hash(sourceId) % Enum.GetValues(typeof(BuildingArchetype)).Length` — และ
การเพิ่ม `BuildingArchetype` จาก 8 เป็น 12 สมาชิกเปลี่ยน divisor นั้น จึง
เปลี่ยนผลลัพธ์ของ **ทุก** sourceId ที่เคย hash ไปยัง archetype หนึ่งๆ
ADR-0030 "แก้" ปัญหานี้ด้วยการ renumber fixture ids
(`SimulationFixtures.ResidentialBuildingId` จาก 1000 → 1012) — เป็นการแก้
symptom ที่จุดที่ปัญหาแสดงออก (test fixture) ไม่ใช่แก้ที่ต้นเหตุ
(mechanism เอง)

ผลกระทบจริงของ mechanism เดิม ไม่ใช่แค่เรื่อง fixture: spec §13 กำหนดว่า
save ต้องมี content version และ "OSM snapshot ใหม่ห้ามทับ save ที่กำลัง
เล่น" — โดยหลักการเดียวกัน การเพิ่ม archetype (เนื้อหาเกม ไม่ใช่ geography)
ก็ไม่ควรเปลี่ยนความหมายของ world ที่มีอยู่แล้วอย่างเงียบๆ และ plan §16
กำหนดว่า G6 จะโตจาก 12 ไปถึง ceiling 16 — ทุกครั้งที่โต mechanism เดิมจะ
reshuffle ทุก building ในทุกพื้นที่อีกครั้ง (ยืนยันด้วยการทดลองจริงใน
`ArchetypeCatalogGrowthTests.OldHashModCatalogLengthScheme_ReshufflesManyExistingAssignments_WhenTheCatalogGrows`
— ส่วนใหญ่ของ sample ids เปลี่ยนเมื่อ divisor โตจาก 8 เป็น 12)

## การตัดสินใจ

### 1. Append-only versioned catalog (`ArchetypeCatalog`) แทน `Enum.GetValues(...).Length`

`Thaivia.Core.Simulation.Archetypes.ArchetypeCatalog` เก็บ ordered list
ของ `BuildingArchetype` **ต่อ version** โดย version แต่ละอันถูก freeze
ทันทีที่ ship แล้วไม่แก้อีก:

- `V1` (version 1): 8 archetype เดิมของ G3
- `V2` (version 2, `CurrentVersion` วันนี้): `V1` เดิมทุกตัว **เรียงลำดับ
  เดิมทุกประการ** บวก 4 ตัวใหม่ของ G6-04 ต่อท้าย

การเพิ่ม archetype ในอนาคต (ตาม plan §16 ไปถึง 16) หมายถึงสร้าง `V3` ใหม่
= `V2` + สมาชิกใหม่ต่อท้าย แล้ว bump `CurrentVersion` — **ห้ามแก้ `V1`/`V2`
ที่มีอยู่แล้ว** ตาม doc comment ของ type นี้

### 2. `AssignArchetype` รับ version explicit เสมอ ไม่มี "current" แบบซ่อน

```csharp
public static BuildingArchetype AssignArchetype(long sourceId, int archetypeCatalogVersion) =>
    AppendOnlyCatalogAssignment.Assign(sourceId, ArchetypeCatalog.ForVersion(archetypeCatalogVersion));
```

Mechanism การ hash (`AppendOnlyCatalogAssignment.Assign`) เองไม่เปลี่ยน —
ยังเป็น `hash(sourceId) % catalog.Count` เหมือนเดิมทุกประการ (ดู
`ArchetypeCatalogGrowthTests.V2_MatchesBuildingArchetypesDeclarationOrder_...`
ที่พิสูจน์ว่า `ForVersion(2)` ตรงกับลำดับ enum วันนี้เป๊ะ ดังนั้นโค้ดที่มี
อยู่แล้วทั้งหมด — รวม fixture ids 1012/2003/3013/3009 ที่ ADR-0030 บันทึก
ไว้ — **ไม่ต้องแก้อีก**) สิ่งที่เปลี่ยนคือ **divisor ไม่ใช่
`Enum.GetValues(...).Length` ที่โตตามจำนวน archetype ปัจจุบันอีกต่อไป**
แต่เป็น `ArchetypeCatalog.ForVersion(N).Count` ของ version ที่ถูก **pin**
ไว้กับ world นั้นๆ ตั้งแต่สร้าง — เพราะ version ที่ pin ไว้ frozen ตลอดไป
`AssignArchetype(id, N)` จึงให้คำตอบเดิมเสมอไม่ว่าจะมี version ใหม่กว่า
ship ไปแล้วกี่ตัว

`WorldState.ArchetypeCatalogVersion` (property ใหม่) เก็บ version ที่ world
นั้นถูกสร้างขึ้น: world ใหม่ = `ArchetypeCatalog.CurrentVersion`, world ที่
restore จาก save = version ที่ save บันทึกไว้ (ไม่ใช่ current เสมอ)
`SeedFromGeography` เรียก `AssignArchetype(sourceId, ArchetypeCatalogVersion)`
ที่ pin ไว้นี้เท่านั้น ไม่เคยเรียก overload ที่ default เป็น current

### 3. Generic seam (`AppendOnlyCatalogAssignment`) เพื่อทดสอบ growth ได้จริง

แยก mechanism การ hash-then-index ออกเป็น type generic
(`AppendOnlyCatalogAssignment.Assign<T>(sourceId, IReadOnlyList<T>)`) ที่ไม่
ผูกกับ `BuildingArchetype` โดยตรง เพื่อให้ทดสอบ "การโตของ catalog ไม่
เปลี่ยน assignment เดิม" ได้แบบ dynamic จริง (เพิ่มสมาชิกเข้า list แล้ว
เทียบผลลัพธ์) โดยไม่ต้องเติมสมาชิกปลอมเข้า enum จริงที่ผ่าน ethical review
แล้ว — การทดสอบ pure algorithm ด้วย local test enum เป็นคนละเรื่องกับ
synthetic geography fixture ที่ AGENTS.md ข้อ 2 ห้าม (ข้อนั้นพูดถึงข้อมูล
แผนที่จริง ไม่ใช่ unit test ของ algorithm ล้วนๆ)

### 4. Content-version handling บน save (spec §13)

`SaveGame.ArchetypeCatalogVersion` (int, required field ใหม่ใน JSON:
`archetype_catalog_version`) บันทึก version ที่ world ตอน save ใช้อยู่
`WorldState.Restore` เช็ค `ArchetypeCatalog.ForVersion(save.ArchetypeCatalogVersion)`
เป็นบรรทัดแรกๆ ก่อนแตะ state อื่นใดเลย:

- **Version ใหม่กว่าที่ build นี้รู้จัก** (save มาจาก build ใหม่กว่า) →
  throw `ArchetypeCatalogVersionUnknownException` ทันที (มี
  `RequestedVersion`/`HighestKnownVersion` ให้ debug) — ปฏิเสธชัดเจน ไม่มี
  world ครึ่งๆ กลางๆ ถูกสร้างขึ้นมาเลย ตาม pattern เดียวกับ
  `MapPackVersionMismatchException` ที่ `MapPackLoader` ใช้อยู่แล้ว
- **Version เก่ากว่าที่ยังรู้จัก** (เช่น 1) → โหลดผ่านตรงๆ ไม่มี migration
  logic ใดๆ เพราะ archetype ของแต่ละอาคารถูกเก็บเป็น**ชื่อ** (string) ใน
  `SavedBuilding.Archetype` อยู่แล้วตั้งแต่ ADR-0014 — ไม่ใช่ index/modulus
  — ชื่อจาก version เก่าทุกชื่อยังมีอยู่ใน enum ปัจจุบันเสมอ (catalog โต
  แบบ append-only เท่านั้น) migration ที่ถูกต้องจึงเป็น **identity**
  พิสูจน์จริงด้วย
  `RestoringASave_FromArchetypeCatalogVersion1_LoadsCleanly_TestedMigrationIsTheIdentity`
  ไม่ใช่แค่ยืนยันด้วย comment

## ผลที่ตามมา

- `dotnet test`: 297 (baseline ก่อน wave นี้) → 307 หลัง Task Zero (10 test
  ใหม่: `ArchetypeCatalogGrowthTests` 7 ตัว, `ArchetypeCatalogSaveVersioningTests`
  3 ตัว) — ดู `docs/evidence/g8-dotnet-test.log` สำหรับตัวเลขรวมสุดท้าย
  ของทั้ง wave (รวม Task 1 ด้วย)
- Fixture constants ทั้งหมดที่ ADR-0030 บันทึกไว้ (1012/2003/3013/3009)
  **ไม่ต้องแก้อีก** — พิสูจน์โดย `dotnet build`+`dotnet test` ผ่านทั้งชุด
  โดยไม่แตะไฟล์ fixture เลย
- ทั้ง `BuildingArchetypeEthicalControlTests` และ
  `CorruptionCaseTests.EthicalControl_NoStorylineApiTakesOrReturnsAMapPackType`
  ยังผ่านเหมือนเดิม (ไม่ถูกแตะต้อง) — `ArchetypeCatalog.ByVersion` เป็น
  `Dictionary<int, IReadOnlyList<BuildingArchetype>>` ซึ่ง key เป็น `int`
  ไม่ใช่ `BuildingArchetype` จึงไม่ตรงกับ shape ที่ detector สแกนหา
- บทเรียนของ ADR-0030 ("การขยาย enum ใดๆ ที่ hash-mod-length pattern ใช้
  อยู่ต้องตรวจ fixture ใหม่ทุกครั้ง") **ไม่จำเป็นอีกต่อไป** สำหรับ
  archetype โดยเฉพาะ — การเพิ่ม archetype ใน `V3` ในอนาคตจะไม่กระทบ fixture
  ใดๆ ที่อ้างอิง `V1`/`V2` เพราะ mechanism ใหม่ pin version ไว้แล้วไม่โต
  ตาม enum อีกต่อไป
