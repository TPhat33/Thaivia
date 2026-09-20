# Progress log

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
