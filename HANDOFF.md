# HANDOFF.md — สรุปสถานะเพื่อส่งงานต่อให้ agent ตัวถัดไป

อัปเดตล่าสุด: 2026-09-27 · branch ที่ให้ทำงานต่อ: **`main`** (default branch ของ repo)

เอกสารนี้เขียนให้ **coding agent ที่ทำงานต่อบนเครื่อง local** อ่านก่อนเริ่ม
ถ้าขัดกับความเคยชินทั่วไปของ AI assistant ให้ยึด `AGENTS.md` เป็นใหญ่ที่สุด
แล้วรองมาคือเอกสารนี้

---

## 1. อ่านอะไรก่อน (ตามลำดับ)

1. **`AGENTS.md`** — กติกาที่ห้ามละเมิด (ห้ามปลอมแผนที่/หลักฐาน, แยกสามชั้น
   ข้อมูล, unknown ≠ ที่ว่าง, แหล่งข้อมูลที่อนุญาต, ข้อจริยธรรม)
2. **`docs/progress.md`** — บันทึก 9 sessions ที่ผ่านมา ทำอะไรไปแล้วบ้าง
   commands ที่รันพร้อม exit code และอะไรที่ยัง `not_run`
3. **`TASKS.json`** — backlog จริง 80 tasks พร้อม dependencies และ
   acceptance criteria (ดูข้อ 5 ของเอกสารนี้ประกอบ)
4. **`docs/decisions/0001`–`0040`** — ADR ทุกการตัดสินใจ อ่านตัวที่เกี่ยวกับ
   งานที่จะทำก่อนแก้โค้ดส่วนนั้น
5. `docs/environment.md` — สภาพแวดล้อมที่ตรวจจริง (ของ cloud container เดิม
   ไม่ใช่เครื่องคุณ — ต้องตรวจเครื่องตัวเองใหม่)

spec ต้นฉบับ 2 ไฟล์ (`START_FROM_EMPTY_REPO.th.md`,
`IMPLEMENTATION_PLAN.th.md`) **ไม่ได้อยู่ใน repo** เป็น source of truth ที่
เจ้าของโครงการถืออยู่ — ถ้าต้องอ้างอิงข้อความสเปกตรงๆ ให้ขอจากเจ้าของ
`AGENTS.md` กลั่นข้อสำคัญมาแล้ว

---

## 2. สถานะจริง ณ commit นี้

| ด้าน | สถานะ | หลักฐาน |
|---|---|---|
| Python map pipeline | ครบวงจร ใช้งานได้ | `pytest` **77 passed** |
| C# simulation core | ครบและเดิน tick จริง | `dotnet test` **312 passed** |
| Unity | **ยังไม่เคย compile** | ไม่มี Editor — ADR-0002 |
| แผนที่ไทยจริง | **ยังไม่เคยนำเข้า** | ทุก endpoint ถูกบล็อก — ADR-0003 |
| Android / iOS / phone / tablet | `not_run` ทั้งหมด แยกรายการ | `docs/progress.md` |

สะสม: 55 commits · 40 ADRs · 111 evidence logs · C# 312 tests · Python 77 tests

**ยังไม่มี MapPack ของพื้นที่จริงแม้แต่ไฟล์เดียว** ทุก pack ที่มีมาจาก
synthetic fixture และติด `provenance.synthetic: true` ไว้ชัด

---

## 3. ตั้งเครื่องให้รันได้ (venv/bin/obj ไม่ได้ commit)

```bash
# Python — ต้อง 3.11+ (baseline ที่ใช้จริงคือ 3.11.15, ADR-0001)
python3 -m venv .venv
./.venv/bin/pip install -r requirements.txt          # hash-pinned lock
./.venv/bin/pip install --no-deps -e tools/map_pipeline
./.venv/bin/thaivia doctor      # ต้อง exit 0
./.venv/bin/pytest -q           # ต้องได้ 77 passed

# C# — ต้อง .NET SDK 8  (macOS: brew install --cask dotnet-sdk)
cd game && dotnet build Thaivia.sln && dotnet test Thaivia.sln   # 312 passed
```

CLI ที่ใช้งานได้จริง: `thaivia doctor | acquire | build | audit | verify`
รองรับหลายพื้นที่ด้วย `--area <id>` (ทะเบียนพื้นที่อยู่ที่ `configs/areas/`)
ละ `--area` ไว้ = ใช้ `configs/pilot-area.json` แบบเดิม (ADR-0029)

---

## 4. สถาปัตยกรรมย่อ

```
tools/map_pipeline/          Python: acquire → source lock/hash → parse →
                             project → normalize → graph → boundary → bake →
                             audit → verify   (deterministic bake)
game/Assets/Scripts/Core/    C# บริสุทธิ์ ไม่แตะ UnityEngine  ← งานหลักอยู่ที่นี่
game/Assets/Scripts/Runtime/ MonoBehaviour/renderer/camera/UI — เขียนแล้วแต่
                             ยังไม่เคย compile ทุกไฟล์ติดป้าย // UNCOMPILED
game/Thaivia.Core.Tests/     xUnit — ช่องทางหลักฐานเดียวที่มีตอนนี้
```

`Thaivia.Core` ถูกกันไม่ให้แตะ UnityEngine **3 ชั้นอิสระกัน** ห้ามรื้อ:
1. `Thaivia.Core.csproj` ไม่มี reference ไป Unity เลย
2. `Thaivia.Core.asmdef` ตั้ง `"noEngineReferences": true`
3. test สแกน source หา `using UnityEngine`

---

## 5. งานที่เหลือ — 15 items ทั้งหมด**บล็อกด้วยมนุษย์** ไม่มีงานที่ทำได้ทันที

```
80 tasks:  done 65 · blocked 14 · partial 1  →  unblocked ที่ยังไม่ทำ = 0
```

**บล็อกเพราะไม่มีข้อมูล OSM จริง**
`G1-10` (pilot audit ตัวจริง) · `G6-03` (พื้นที่ 2-3) · `G6-12` (content ต่อพื้นที่)

**บล็อกเพราะไม่มี Unity Editor / เครื่องจริง / owner sign-off**
`G2-01`…`G2-07` · `G2-02` (partial) · `G3-10` · `G5-06` · `G6-11` · `G6-14` · `G6-15`

**ถ้าเครื่อง local มี Unity แล้ว** งานแรกคือ `G2-01` (bootstrap project จริงผ่าน
Editor/Hub ด้วย script ที่ idempotent) แล้วไล่ `G2-02` → `G2-03` → `G2-04`
→ `G2-05` → `G2-06` → `G2-07` ตาม dependencies ใน `TASKS.json`

**ถ้าได้ไฟล์ OSM มาแล้ว** วางที่ `data/cache/` (gitignored) แล้ว:
```bash
./.venv/bin/thaivia acquire --from-local-file data/cache/<file>.osm
./.venv/bin/thaivia build && ./.venv/bin/thaivia audit && ./.venv/bin/thaivia verify
```
ไม่ต้องเขียนโค้ดเพิ่มเลย pipeline รออยู่แล้ว วิธีละเอียด + bbox ที่คำนวณไว้
อยู่ใน `docs/decisions/0003-osm-source-acquisition-blocked.md`

---

## 6. Invariant ที่ห้ามทำพัง — มี test คุมทุกข้อ

ถ้าแก้อะไรแล้ว test เหล่านี้แดง **ให้ถือว่าโค้ดคุณผิด ไม่ใช่ test ผิด**
ก่อนแก้ test ให้อ่าน ADR ที่เกี่ยวข้องและอธิบายเหตุผลใน ADR ใหม่

| Invariant | test ที่พิสูจน์ |
|---|---|
| seed เดิม + คำสั่งเดิม → world hash เท่ากัน | `TwoWorlds_SameSeedSameIntegratedTickRun_ProduceIdenticalStructuralHash` |
| RNG แต่ละ stream ไม่กวนกัน | `DrawingFromOneStream_DoesNotShiftAnotherStreamsSequence` |
| ไม่มี wall-clock catch-up | `PauseThenResume_AddsZeroTicksOnItsOwn` |
| estimate ไม่แตะ world/RNG | `Estimate_DoesNotMutateTheWorld_HashAndEveryRngStreamUnchanged` |
| confirm ซ้ำไม่หักเงินซ้ำ | `ConfirmingTheSameDraftTwice_DoesNotDeductBudgetTwice` |
| commit ล้ม → rollback ทั้งก้อน | `CommitRelocation_InsufficientBudget_AppliesNothing_TransactionalRollback` |
| ย้ายอาคาร คน/งานคงจำนวน | `Relocation_ConservesPopulationAndJobs_AndDoesNotMutateGeographyBase` |
| trip ข้ามขอบแผนที่ไม่หาย (integer-exact) | `GatewayConservation_HoldsAcrossThousandsOfTicks...` + control `LeakyGatewayVariant_...ProvingTheAboveTestBites` |
| save → load → tick ต่อ ≡ ไม่เคย save | `SaveThenRestore_ThenContinue...ProducesTheIdenticalHashAsNeverHavingSaved` |
| map/catalog version ไม่ตรง → error ชัด ไม่ load ครึ่งๆ | `RestoringASave_WithAnArchetypeCatalogVersionNewerThanThisBuildKnows_ThrowsExplicitly_NotAPartialLoad` |
| เพิ่ม archetype ไม่สลับของเดิม | `AppendOnlyCatalogAssignment_AddingACategory_ChangesNoIdThatWasAssignedAgainstTheOldCatalog` |
| accessibility/utilities เดินบน graph ไม่ใช่รัศมี | `ConnectedGraphWithGrossDetour_NetworkDistance_MatchesTheDetourAndIsNotStraightLine`, `AdversarialFixture_NetworkReachDiffersMateriallyFromStraightLineDistance` |
| เส้นตัดกันในภาพ ≠ เชื่อมกัน | `test_only_the_genuinely_shared_node_is_a_junction` (pytest) |
| pack ถูกแก้ 1 ไบต์ → ปฏิเสธ | `TamperedPayloadByte_IsRejectedOnContentHashMismatch` + control |
| bake ได้ hash เดิมทุกครั้ง | `thaivia verify` + pytest determinism tests |

**กฎจริยธรรม 2 ข้อบังคับด้วยเครื่อง ห้ามปิด:**
- `BuildingArchetypeEthicalControlTests` — ไม่มี archetype ไหนมีคะแนนลบคงที่
  ได้ ผลกระทบต้องมาจาก activity × time × location
- `CorruptionCaseTests.EthicalControl_NoStorylineApiTakesOrReturnsAMapPackType`
  — storyline ผูกคดีกับ feature/ชื่อ/ที่อยู่จริงไม่ได้เลย

ทั้งสองมี positive control พิสูจน์ว่า detector ยิงโดนของเสียจริง

---

## 7. หนี้ที่รู้อยู่ (ไม่ใช่บั๊กซ่อน — บันทึกไว้แล้ว)

- `UtilityCoverage` ยังไม่คิด turn restriction (เป็น vehicle routing = G4+ scope)
- `NetworkDemandAssignment` เป็น incremental assignment 4 slices ไม่ใช่
  equilibrium เต็มรูป (ADR-0028)
- `CohortNeeds.Safety` คำนวณจาก incident จริงแล้ว แต่สูตร decay ยังหยาบ
- performance วัดบน **8×8 synthetic grid ไม่ใช่พื้นที่จริง** — p50 0.13ms /
  p95 0.81ms / max 22ms เทียบ budget p95 ≤ 5ms ผ่าน **แต่ตัวเลขจะเปลี่ยน
  เมื่อมีข้อมูลจริง ต้องวัดใหม่** (ADR-0024)
- G6-13 (ODbL) เป็น **analysis + proposal เท่านั้น ไม่ใช่คำแนะนำทางกฎหมาย**
  ต้องให้เจ้าของ/ทนายตรวจก่อนแจกจ่ายจริง (ADR-0037)

---

## 8. วิธีทำงานที่โครงการนี้ใช้ (ขอให้ทำต่อ)

- **ห้ามอ้างว่า build/test ผ่านโดยไม่มีผลรัน** ทุกตัวเลขที่รายงานต้องมี log
  ใน `docs/evidence/` — ที่ผ่านมาเคยพลาดข้อนี้ 1 ครั้งและถูกจับได้
- **ห้ามสร้าง Unity metadata ปลอม** (`ProjectVersion.txt`, `packages-lock.json`,
  `.meta`) ให้ดูเหมือน Editor เคยเปิด
- **subcommand ที่ยังไม่ทำต้อง exit non-zero** อย่าง honest ห้าม exit 0 หลอก
- เขียน ADR ทุกการตัดสินใจจริง และถ้าขัด ADR เก่าให้ **supersede อย่างชัดเจน**
  ห้ามขัดแบบเงียบ
- อัปเดต `TASKS.json` + `docs/progress.md` ทุก session
- เอกสาร prose เป็นภาษาไทย / โค้ด identifier comment commit message เป็นอังกฤษ
- commit ทีละก้อนเล็ก push บ่อย (session ที่ผ่านมามี agent ตาย 3 ตัวกลางทาง
  ตัวที่ push เป็นระยะไม่เสียงานเลย)
- เทสต์ต้อง **กัดได้จริง** ถ้าเขียนเทสต์ที่ผ่านเพราะ fixture อ่อน ให้เพิ่ม
  control ที่พิสูจน์ว่า implementation ผิดจะทำให้เทสต์แดง — แพตเทิร์นนี้ใช้
  ทั่ว repo แล้ว (`DetectorItself_Flags...`, `...ProvingTheAboveTestBites`)

---

## 9. หมายเหตุเรื่อง branch

`main` เป็น **default branch** ของ repo แล้ว และเป็น branch ที่ให้ทำงานต่อ
`claude/sonnet-5-implementation-pcqfib` เป็น branch ประวัติของ 9 sessions แรก
ชี้ commit เดียวกัน (`884046a`) เก็บไว้อ้างอิง ไม่ต้องพัฒนาต่อบนนั้น

```bash
git clone https://github.com/TPhat33/Thaivia.git   # ได้ main มาเลย
```
