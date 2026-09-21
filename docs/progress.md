# Progress log

## Session 3 — 2026-09-22 (wave 3 / G2 game-side layer — Thaivia.Core)

### Task IDs และไฟล์ที่เปลี่ยน

ทำ `W3-01` เสร็จ (สถานะ `done`) และ `G2-02` เปลี่ยนเป็น `partial` (ดู
`TASKS.json` สำหรับ acceptance criteria/evidence เต็ม) `G2-01`,
`G2-06`, `G2-07` ยัง `blocked` เหมือนเดิม (ไม่มี Unity Editor) `G2-03`,
`G2-04`, `G2-05` ยัง `blocked` แต่เพิ่ม notes ว่าโค้ดเขียนแล้ว (uncompiled)

ไฟล์ใหม่หลัก:

- `game/Thaivia.Core.csproj`, `game/Thaivia.sln` — pure C# library
  project, compile จาก `game/Assets/Scripts/Core/**/*.cs` โดยตรง (ไม่
  copy source)
- `game/Assets/Scripts/Core/` (ใหม่ทั้งโฟลเดอร์): `Values/SourceValue.cs`
  + `AssumptionKind.cs`, `MapPack/*.cs` (contract types ตรงกับ
  `tools/map_pipeline/schemas/mappack.schema.json`),
  `Serialization/{CanonicalJson,JsonRequire,MapPackLoader,MapPackLoadException}.cs`,
  `Coordinates/{Vec3F,CoordinateNarrowing}.cs`,
  `Graph/RoadGraphIndex.cs`, `Attributes/{Building,Road}Attributes.cs`,
  `Simulation/{PlayerDelta,WorldState}.cs`, `Thaivia.Core.asmdef`
- `game/Thaivia.Core.Tests/` (ใหม่ทั้งโฟลเดอร์): xUnit project +
  `SourceValueTests.cs`, `MapPackLoaderTests.cs`, `RoadGraphIndexTests.cs`,
  `CoordinateNarrowingTests.cs`, `ImmutabilityTests.cs`,
  `NoUnityEngineReferenceTests.cs`, `Fixtures/*.synthetic.mappack.json`
  (real `thaivia build` output จาก synthetic fixtures สอง fixture)
- `game/Assets/Scripts/Runtime/` (ใหม่ทั้งโฟลเดอร์, UnityEngine-dependent,
  **uncompiled** — ทุกไฟล์มี `// UNCOMPILED` marker):
  `MapPackLoaderBehaviour.cs`, `Rendering/{Road,Building,Water}MeshBuilder.cs`,
  `Camera/OrthoObliqueCameraRig.cs`, `Input/PanPinchController.cs`,
  `Selection/FeatureSelectionController.cs`,
  `UI/{InspectorPanelController,AttributionOverlay,SimulationDisclaimerLabel}.cs`,
  `Thaivia.Runtime.asmdef`
- `game/README.md`, `game/Assets/Scripts/Runtime/README.md`,
  `game/Packages/README.md` — คำอธิบาย compiled+tested vs
  written-but-not_run split และขั้นตอนมนุษย์ที่เหลือ
- `docs/decisions/0008..0010-*.md`, `docs/evidence/g2-*.log`

### สิ่งที่ทำงานจริง (ยืนยันด้วยการรันจริง)

`Thaivia.Core` compile ผ่าน `dotnet build` (0 warnings, 0 errors —
`docs/evidence/g2-dotnet-build.log`) และผ่าน `dotnet test` ที่
`game/Thaivia.Core.Tests`: **35 passed** (`docs/evidence/g2-dotnet-test.log`)
ครอบคลุม:

- MapPack round-trip จริง: โหลด MapPack ที่ `thaivia build` (Python)
  ผลิตจริงจาก `tests/fixtures/synthetic/road_graph_layers.synthetic.osm.xml`
  และ `multipart_building.synthetic.osm.xml` (คำสั่งที่ใช้สร้างไฟล์เหล่านี้
  บันทึกไว้ที่ `docs/evidence/g2-fixture-generation.log`) — ไฟล์ที่ commit
  ไว้ที่ `game/Thaivia.Core.Tests/Fixtures/*.synthetic.mappack.json` มี
  `provenance.synthetic: true` ชัดเจน
- Tampered pack (แก้ 1 หลักใน payload) → `MapPackTamperedException`
  จาก content_hash mismatch จริง (ไม่ใช่แค่ trust ค่าที่ประกาศไว้ — ดู
  ADR-0008 สำหรับวิธี re-derive canonical hash ใน C# ให้ตรงกับ Python)
- Version mismatch (schema_version/importer_version) →
  `MapPackVersionMismatchException` ที่ระบุ field/found/expected ชัดเจน
  ตรวจก่อน hash check เสมอ (ไม่ทำให้เข้าใจผิดว่าไฟล์ถูก tamper)
- `SourceValue<T>`: `Assumed`/`Unknown` อ่านผ่าน `AsSourceFact()` ไม่ได้
  (throw จริง); `Unknown` ไม่มีทาง degrade เป็น default ที่ดูเหมือนค่าจริง
- Grade separation: `RoadGraphIndex` สร้าง adjacency จาก shared node id
  เท่านั้น (ไม่แตะ geometry/layer เลย) — edge สอง way ที่ cross กันใน plan
  view แต่ไม่มี node ร่วมกันจะไม่มี adjacency เลย, edge ที่มี node ร่วมกัน
  มี adjacency เสมอไม่ว่า layer จะต่างกันแค่ไหน
- `oneway=-1` (Reversed) traverse ได้แค่ทิศย้อนกลับ; turn restriction
  ที่ supported ปิดกั้นทางที่ระบุจริง; restriction ที่ `unsupported`
  (เช่น via-way) คืน `TurnDecision.Unsupported` ไม่ใช่ `Allowed` เงียบๆ
- Coordinate narrowing: วัด error จริง (ไม่ assert away) — สูงสุดที่พบใน
  synthetic fixture จริงคือ ~3.4mm, ที่จุดสังเคราะห์ระยะ ~5000m จาก origin
  ได้ sub-millimetre — ดูรายละเอียดในหัวข้อ "float32 narrowing error"
  ด้านล่าง
- Determinism: โหลด MapPack เดิมสองครั้งได้ graph โครงสร้างเดียวกันเป๊ะ
  (`content_hash` เท่ากัน, node/edge ทุกตัวตรงกัน)
- Immutability enforced ในระดับ type: `ImmutabilityTests` ใช้ reflection
  เช็คว่าทุก public property ใน `Thaivia.Core.MapPack` เป็น get-only
  หรือ init-only เท่านั้น (เช็ค `IsExternalInit` modifier — สิ่งเดียวกับที่
  compiler เช็คจริง ไม่ใช่ proxy ที่อ่อนกว่า)
- `PlayerDelta`/`WorldState` (namespace `Thaivia.Core.Simulation`) เป็น
  type แยกจาก `GeographyBase`/`SimulationInitialization` โดยสิ้นเชิง —
  ไม่มี base type ร่วม ไม่มี field ที่รับอีกฝั่งได้ ตรวจด้วย
  `PlayerDelta_And_WorldState_ShareNoTypeWithGeographyBaseOrSimulationInitialization`
- UnityEngine-freedom: `NoUnityEngineReferenceTests` scan source จริง
  หา `using UnityEngine` (ยืนยันว่าไม่มี ทั้ง `Core/` เอง และ scan ซ้ำทุก
  `dotnet test` run); `Thaivia.Core.csproj` ไม่มี reference ไปยัง
  UnityEngine เลยด้วยตัวมันเอง — `using UnityEngine;` จะทำให้
  `dotnet build` fail ทันที (พิสูจน์เชิงโครงสร้าง ไม่ใช่แค่ scan)
- `./.venv/bin/pytest -q` (Python pipeline) ยัง **67 passed** เหมือนเดิม
  — session นี้ไม่แตะโค้ด Python เลย (`docs/evidence/g2-pytest-still-passing.log`)

### float32 narrowing error (วัดจริง)

`Thaivia.Core.Coordinates.CoordinateNarrowing.ToUnityGroundPlane` เป็น
จุดเดียวที่ narrow float64 → float32 ทั้งระบบ วัด error สองแบบใน
`CoordinateNarrowingTests`:

- จุดสังเคราะห์ที่ระยะ ~5000m จาก local origin (ลำดับขนาดของ pilot AOI
  + buffer ตาม `configs/pilot-area.json`): error วัดได้ sub-millimetre
  ต่อแกน (bound ที่ assert คือ < 1mm — generous ไม่ใช่ tuned ให้พอดี
  ค่าที่วัดได้)
- ทุก node coordinate จริงใน synthetic fixture (`road_graph_layers`,
  9 nodes): max |error| = **0.00339m (~3.4mm)** — ตัวเลขจริงพิมพ์ออกทาง
  test output เสมอ ไม่ถูก assert away

### สิ่งที่ยัง not_run / blocked

- ทุกอย่างที่ต้องพึ่ง Unity Editor จริง (compile ของ
  `game/Assets/Scripts/Runtime/`, scene/prefab, play-mode run, build ใดๆ)
  — **not_run** เหมือนเดิม (ADR-0002) โค้ด Runtime เขียนแล้วแต่ไม่เคยผ่าน
  compiler เลย ทุกไฟล์มี `// UNCOMPILED` marker ที่ตรวจซ้ำด้วย
  `NoUnityEngineReferenceTests.RuntimeSourceTree_IsMarkedAsUncompiledUnityCode`
- Android/iOS/phone/tablet — ไม่เปลี่ยนจาก session ก่อนหน้า ยัง `not_run`
  ทุกแถว (ไม่มี Android SDK/adb, ไม่มี macOS/Xcode)
- แผนที่จริงยังไม่ได้นำเข้า — เหตุผลเดิมทุกประการ (ADR-0003) session นี้
  ไม่แตะ Python pipeline หรือพยายามดึงข้อมูลจริงเลย

### Blockers และขั้นตอนมนุษย์ที่เล็กที่สุด

เหมือน G0/G1 ทุกประการสำหรับ Unity: ต้องมีมนุษย์ที่มี Unity license +
Editor install จริง (Unity 6.3 LTS, เช็ค patch ปัจจุบันตอนติดตั้ง) เปิด
`game/` เป็น root project แล้วแก้ compile error ที่ Editor Console รายงาน
(ไฟล์ `Runtime/` ไม่เคยผ่าน compiler เลย) ดู `game/README.md` สำหรับ
รายการขั้นตอนแบบเต็ม — Android ต้องมี Android SDK/adb + device จริง, iOS
ต้องมี macOS + Xcode

### Next dependency-ready tasks

- `G2-01` — ยังรอ Unity Editor เหมือนเดิม (ไม่เปลี่ยน)
- `G3` (real scenario seeding ใน `SimulationInitialization`, wiring
  `PlayerDelta` เข้ากับ MapPack ที่โหลดแล้วจริงๆ) — dependency-ready แล้ว
  ตอนนี้ที่ `Thaivia.Core`'s types มีครบ
- `G1-10` (real pilot audit) — ยัง blocked เหมือนเดิม รอข้อมูล OSM จริง

## Session 2 — 2026-09-21 (G1 Real-map data pipeline)

### Task IDs และไฟล์ที่เปลี่ยน

ทำ G1-01 ถึง G1-09, G1-11, G1-12 ครบ (สถานะ `done` — pipeline
implement+test จริงด้วย synthetic fixtures) G1-10 ยัง `blocked` เพราะยัง
ไม่มีข้อมูล OSM จริง (ดู ADR-0003) รายละเอียดแต่ละ task ดูใน
`TASKS.json`

ไฟล์ใหม่/แก้หลัก:

- `tools/map_pipeline/src/map_pipeline/pipeline/` (ใหม่ทั้งโฟลเดอร์):
  `settings.py`, `tags.py`, `geometry_utils.py`, `osm_parse.py`,
  `projection.py`, `normalize.py`, `quality.py`, `features.py`,
  `assumptions.py`, `graph.py`, `boundary.py`, `sourcelock.py`,
  `acquire_net.py`, `mappack.py`
- `tools/map_pipeline/schemas/mappack.schema.json` (ใหม่)
- `tools/map_pipeline/src/map_pipeline/commands/{acquire,build,audit,verify}.py`
  (เขียนใหม่ทั้งหมด จาก stub เป็น implementation จริง)
- `tools/map_pipeline/src/map_pipeline/doctor.py`,
  `tools/map_pipeline/src/map_pipeline/cli.py` (เพิ่ม `--probe-network`)
- `tests/fixtures/synthetic/{road_graph_layers,boundary_gateway,degenerate_hostile,multipart_building}.synthetic.osm.xml`
  (ใหม่ — ทุกไฟล์มี synthetic marker ผ่าน `test_synthetic_fixtures.py`)
- `tests/test_pipeline_{projection,graph,features,boundary,sourcelock,mappack_determinism,assumptions,degenerate,end_to_end}.py`
  (ใหม่ทั้งหมด) และ `tests/test_cli_smoke.py` (แก้ให้ตรงพฤติกรรมจริง)
- `docs/decisions/0005..0007-*.md` (ใหม่)
- `docs/evidence/g1-*.log`, `docs/evidence/g1-synthetic-e2e-audit-sample.md`
  (ใหม่)

### สิ่งที่ทำงานจริง (ยืนยันด้วยการรันจริง)

Pipeline เต็ม: `acquire -> source lock/hash -> buffered extract -> entity
parse -> project -> normalize -> graph -> boundary/gateways -> audit ->
MapPack bake -> verify` implement ครบตาม spec ของ
`REAL_MAP_PIPELINE`/`IMPLEMENTATION_PLAN.th.md` §6 (ดู module map เต็มใน
`docs/progress.md` ท้ายรายงานนี้/handback message) ทดสอบผ่าน pytest 67
tests (จาก 17 ที่ G0) ครอบคลุมทุก test case ที่ spec ระบุไว้ (lon/lat
round-trip, bridge-vs-layer ไม่กลายเป็น junction, oneway=-1, turn
restrictions, multipolygon holes/multipart/building:part,
unknown-ไม่ใช่-empty-land, buffered crop + gateways, deterministic bake,
assumption namespace isolation, degenerate/hostile input)

`thaivia acquire --from-url` ทดสอบจริงกับ
`https://download.geofabrik.de/asia/thailand-latest.osm.pbf` — fail
ทันทีด้วยข้อความบล็อกที่ชัดเจน (403 บน CONNECT) ไม่ retry ไม่มี
mirror/VPN (ดู `docs/evidence/g1-acquire-from-url-blocked.log`)

`thaivia acquire --from-local-file` → `build` → `audit` → `verify` รัน
สำเร็จครบทุกขั้นบน synthetic fixture จริง (exit 0 ทุกคำสั่ง) ดู
`docs/evidence/g1-e2e-local-file-acquire-build-audit-verify.log`
MapPack ที่ผลิตมี `provenance.synthetic: true` และ `synthetic_notice`
ชัดเจน ไม่มีทางเข้าใจผิดว่าเป็น pilot pack จริง

Determinism: รัน `thaivia build` สองครั้งอิสระ (ห่างกัน >1 วินาที) จาก
source+settings เดิม ได้ `content_hash` เดียวกันเป๊ะ เปลี่ยน
`context_buffer_m` แล้ว `settings_hash` เปลี่ยนตาม (ดู
`docs/evidence/g1-determinism-two-runs.log`)

`thaivia doctor` เปลี่ยนเป็น default = cached reachability (ไม่ยิง
network ซ้ำ), `--probe-network` เพื่อ live-probe ตามเดิม

### สิ่งที่ยัง not_run / blocked

- **ยังไม่มีการ import แผนที่จริง** — G1-10 (real
  `docs/data/pilot-audit.md` สำหรับ `th-bkk-pilot-001`) ยัง `blocked`
  เพราะทุก OSM endpoint ที่อนุญาตยังถูกบล็อกโดย organization egress
  policy (เหมือน G0 เป๊ะ ดู ADR-0003) `docs/data/pilot-audit.md`
  ตัวจริงยังคง `status: blocked, not_measured` ทุกฟิลด์ตามเดิม — sample
  ผลลัพธ์จาก synthetic fixture ถูกเก็บแยกไว้ที่
  `docs/evidence/g1-synthetic-e2e-audit-sample.md` เท่านั้น ไม่ทับของจริง
- G2 (Unity viewer) — ยัง not_run เหมือน G0 เพราะไม่มี Unity Editor ใน
  environment นี้ (ดู ADR-0002) ไม่ได้แตะในรอบนี้
- `dotnet test` บน pure C# core — ยัง not_run เหมือน G0 (W3-01 ยัง
  `pending`, out of scope ของรอบนี้)

### แผนที่จริงนำเข้าแล้วหรือยัง

**ยังไม่ได้นำเข้า** เหตุผลเดิมกับ G0 ทุกประการ (ดู ADR-0003) — สิ่งที่
เปลี่ยนในรอบนี้คือ pipeline ที่จะรับข้อมูลจริงตอนปลดบล็อกนั้น
**implement เสร็จและ test ครบแล้ว** (ไม่ใช่ stub อีกต่อไป) เมื่อมนุษย์วาง
ไฟล์ `.osm.pbf`/`.osm.xml` ที่มีสิทธิ์ใช้ไว้ที่ `data/cache/` แล้วรัน
`thaivia acquire --from-local-file <path> && thaivia build && thaivia
audit --pack <path>` จะได้ MapPack และ audit จริงทันที ไม่ต้องแก้โค้ด
เพิ่ม

### สถานะ Android/iOS/phone/tablet

ไม่เปลี่ยนจาก G0 — ยัง `not_run` ทุกแถว (ไม่ได้แตะ G2 ในรอบนี้)

### Blockers และขั้นตอนมนุษย์ที่เล็กที่สุด

เหมือน ADR-0003 ทุกประการ: allowlist `download.geofabrik.de` (หรือ
endpoint ที่เลือก) **หรือ** วางไฟล์ snapshot ที่มีสิทธิ์ใช้ไว้ที่
`data/cache/th-bkk-pilot-001-source.osm.pbf` (ชื่อไฟล์ไม่จำเป็นต้องตรง
เป๊ะ — `thaivia acquire --from-local-file <path จริง>` จะจัดการ hash/lock
เอง) แล้วรัน `thaivia acquire --from-local-file <path> && thaivia build
&& thaivia audit --pack <path จาก build> && thaivia verify --pack <path>`

### Next dependency-ready tasks

- G1-10 (real audit) — dependency-ready ทันทีที่มีไฟล์ OSM จริงที่ขอบ
  bbox ของ `configs/pilot-area.json`
- G2-01 (Unity bootstrap) — ยังรอ Unity Editor เหมือนเดิม
- W3-01 (pure C# core) — ยัง dependency-ready ไม่เปลี่ยนจาก G0

## Session 1 — 2026-09-20 (G0 Bootstrap)

### Task IDs และไฟล์ที่เปลี่ยน

ทำ G0-01 ถึง G0-13 ครบตาม `TASKS.json` (ดูรายละเอียด acceptance
criteria/evidence ในไฟล์นั้น) ไฟล์ที่สร้างใหม่ทั้งหมด (repo เพิ่งเริ่ม
จาก empty):

- Root: `.gitignore`, `.gitattributes`, `README.md`, `AGENTS.md`,
  `TASKS.json`, `pytest.ini`, `requirements.txt`
- `configs/pilot-area.json`, `configs/sources.json`
- `tools/map_pipeline/` — `pyproject.toml`, `README.md`,
  `schemas/{pilot-area,sources}.schema.json`,
  `src/map_pipeline/{__init__,__main__,cli,doctor,config,paths,exit_codes}.py`,
  `src/map_pipeline/commands/{__init__,not_implemented,acquire,build,audit,verify}.py`
- `tests/` — `conftest.py`, `test_config_schemas.py`, `test_cli_smoke.py`,
  `test_synthetic_fixtures.py`,
  `fixtures/synthetic/{tiny_intersection.geojson,tiny_multipolygon.synthetic.osm.xml}`
- `docs/environment.md`, `docs/progress.md` (ไฟล์นี้)
- `docs/decisions/0001..0004-*.md`
- `docs/data/pilot-audit.md`
- `docs/evidence/g0-*.log` (21 ไฟล์ evidence)
- `game/.gitkeep`, `content/.gitkeep`

### สิ่งที่ทำงานจริง (ยืนยันด้วยการรันจริง)

- `thaivia doctor` — ตรวจ Python version, 4 dependencies (osmium,
  pyproj, shapely, jsonschema), 2 configs, local cache, reachability ของ
  6 source endpoints — exit 0 (ไม่มี hard failure)
- `thaivia acquire` / `build` / `audit` / `verify` — parse argument ได้
  จริง, exit code 3 (`NOT_IMPLEMENTED`) พร้อมข้อความ blocker ที่เจาะจง
  ไม่มี exit 0 ปลอม
- `python -m map_pipeline doctor` — เทียบเท่า `thaivia doctor` ผ่าน
  module invocation
- pytest suite ทั้งหมด (17 tests): config-schema validation, CLI smoke
  (doctor=0, stub=3), synthetic-fixture labelling — **17 passed**
- Python venv + editable install ของ `map_pipeline` package ใช้งานได้
  จริง พร้อม dependency lock ที่ resolve จริงจาก PyPI (ไม่ใช่ freeze
  มือ)
- ติดตั้ง `dotnet-sdk-8.0` (8.0.131) สำเร็จผ่าน `apt-get` จาก Ubuntu
  archive (container-local เท่านั้น)

### Commands ที่รัน พร้อมผล (exit codes)

| คำสั่ง | exit code | log |
|---|---|---|
| `python3 -m venv .venv` | 0 | — |
| `./.venv/bin/pip install --upgrade pip` | 0 | — |
| `./.venv/bin/pip install pip-tools` | 0 | — |
| `./.venv/bin/pip-compile --generate-hashes --allow-unsafe ...` | 0 | (ดู `requirements.txt` header) |
| `./.venv/bin/pip install -r requirements.txt` | 0 | — |
| `./.venv/bin/pip install --no-deps -e tools/map_pipeline` | 0 | — |
| dependency import check (`osmium`, `pyproj`, `shapely`, `jsonschema`, `pytest`, `map_pipeline`) | 0 | `docs/evidence/g0-dep-import-check.log` |
| `./.venv/bin/thaivia doctor` | 0 | `docs/evidence/g0-doctor-run.log` |
| `./.venv/bin/thaivia acquire` | 3 | `docs/evidence/g0-cli-stub-commands.log` |
| `./.venv/bin/thaivia build` | 3 | `docs/evidence/g0-cli-stub-commands.log` |
| `./.venv/bin/thaivia audit` | 3 | `docs/evidence/g0-cli-stub-commands.log` |
| `./.venv/bin/thaivia verify` | 3 | `docs/evidence/g0-cli-stub-commands.log` |
| `python -m map_pipeline doctor` | 0 | `docs/evidence/g0-cli-stub-commands.log` |
| `./.venv/bin/pytest -v` (17 tests) | 0 | `docs/evidence/g0-pytest-run.log` |
| `curl $HTTPS_PROXY/__agentproxy/status` | 0 (แต่ระบุ blocked hosts ภายใน) | `docs/evidence/g0-proxy-status.log` |
| `sudo apt-get update` (มี PPA สอง repo ที่บล็อก, main archive สำเร็จ) | 100 | `docs/evidence/g0-dotnet-experiment.log` |
| `sudo apt-get install -y dotnet-sdk-8.0` | 0 | `docs/evidence/g0-dotnet-experiment.log` |
| `dotnet --version` | 0 (8.0.131) | `docs/evidence/g0-dotnet-experiment.log` |
| `git lfs version` | ล้มเหลว (ไม่มี git-lfs) | `docs/evidence/g0-git-lfs-check.log` |

### สิ่งที่ยัง not_run

- ทุกอย่างเกี่ยวกับ Unity: project creation, compile, PlayMode/EditMode
  tests, build ใดๆ — **not_run** (ไม่มี Unity Editor ในสภาพแวดล้อมนี้
  ดู ADR-0002)
- ทุกอย่างเกี่ยวกับ Android build/deploy — **not_run** (ไม่มี Android
  SDK/adb)
- ทุกอย่างเกี่ยวกับ iOS build/signing — **not_run** (ไม่มี macOS/Xcode;
  container นี้เป็น Linux)
- G1 pipeline stages ทั้งหมด (`acquire` ของจริง, source lock, entity
  parse, project/normalize, topology validate, road graph, buffered
  crop/gateways, MapPack bake) — **not_run** เพราะไม่มีข้อมูล OSM จริง
  (ดู ADR-0003) โค้ดยังไม่เขียนเกิน CLI stub ที่ตั้งใจให้ "not
  implemented yet"
- `dotnet test` บน pure C# simulation core — **not_run** เพราะยังไม่ได้
  เริ่มเขียน simulation core (นอก scope ของ G0 ตามคำสั่งงานนี้; ดู
  `TASKS.json` task `W3-01`)

### แผนที่จริงนำเข้าแล้วหรือยัง

**ยังไม่ได้นำเข้า** เหตุผล: ทุก OSM source endpoint ที่แผนอนุญาต
(`download.geofabrik.de`, `overpass-api.de`, `overpass.kumi.systems`,
`api.openstreetmap.org`, `osm-internal.download.geofabrik.de`,
`download.openstreetmap.fr`) ถูกบล็อกโดย organization egress policy
(403 บน CONNECT tunnel) ไม่มี source hash/snapshot ใดๆ เพราะไม่มีการ
ดาวน์โหลดสำเร็จแม้แต่ครั้งเดียว `data/cache/` ไม่มีอยู่ (ตรงกับที่
`thaivia doctor` รายงาน) ดู ADR-0003 สำหรับขั้นตอนปลดบล็อก

### สถานะ Android/iOS/phone/tablet (แยกกัน)

| Platform | สถานะ |
|---|---|
| Android — Editor build | not_run |
| Android — emulator | not_run |
| Android — physical phone | not_run |
| Android — physical tablet | not_run |
| iOS — Editor/simulator build | not_run |
| iOS — physical phone | not_run |
| iOS — physical tablet | not_run |

ทุกแถว not_run เพราะไม่มี Unity project ให้ build เลย (ดู ADR-0002)
ไม่ใช่เพราะ build ล้มเหลว

### Blockers และขั้นตอนมนุษย์ที่เล็กที่สุด

1. **OSM source (บล็อก G1)** — allowlist `download.geofabrik.de` (หรือ
   endpoint Overpass ที่เลือก) ใน egress policy **หรือ** วางไฟล์
   `.osm.pbf`/`.osm.xml` ที่มีสิทธิ์ใช้ไว้ที่ `data/cache/` ด้วยมือ
   (ดู ADR-0003)
2. **Unity (บล็อก G2)** — รันงานต่อบนเครื่อง/สภาพแวดล้อมที่มี Unity Hub
   + Unity 6.3 LTS ติดตั้งพร้อม license จริง **หรือ** allowlist
   `download.unity3d.com` ชั่วคราวพร้อมข้อมูล license (ดู ADR-0002)
3. **Android/iOS device evidence (บล็อก G2 ส่วน device)** — ต้องมี
   Android SDK/เครื่องจริง และเครื่อง Mac+Xcode ตามลำดับ ไม่มีในเครื่อง
   ปัจจุบัน

ไม่มี blocker ใดที่ agent เองมีสิทธิ์แก้ได้ต่อในสภาพแวดล้อมนี้โดยไม่ทำ
สิ่งที่ต้องห้าม (ปลอมข้อมูล/ปลอม metadata/เลี่ยง policy)

### Next dependency-ready tasks

- `W3-01` (pure C# simulation core scaffold, testable ด้วย `dotnet
  test`) — **dependency-ready ทันที** เพราะ `dotnet-sdk-8.0` ติดตั้งแล้ว
  ในสภาพแวดล้อมนี้ และงานนี้ไม่ต้องพึ่งทั้ง OSM data หรือ Unity เป็น
  candidate ที่ดีที่สุดสำหรับก้าวต่อไปที่ไม่ติด blocker ภายนอก
- งานอื่นทั้งหมดใน G1/G2 รอ human unblock ตามด้านบนก่อนจะ "dependency
  ready" จริง
