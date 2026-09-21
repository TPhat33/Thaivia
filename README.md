# Thailand Urban Repair (ชื่อทำงาน)

เกมบริหาร/ฟื้นฟูเมืองที่ใช้ geometry ถนน ซอย คลอง พื้นที่น้ำ และ footprint
อาคาร**จริง**ของประเทศไทยจากข้อมูล OpenStreetMap ที่มีสิทธิ์ใช้ ผู้เล่น
สำรวจย่านจริง วางแผนแก้ปัญหาจำลอง (เสียง การเข้าถึง ความแออัด ฯลฯ) แล้ว
ดูผลก่อน/หลัง บน base map ที่ไม่ถูกบิดให้เข้ากริดหรือแต่งให้สวยขึ้น

โครงการนี้แยกข้อมูลออกเป็นสามชั้นเสมอ: **GeographyBase** (source vectors
จริง, immutable), **SimulationInitialization** (ประชากร/งาน/demand
สมมติ) และ **PlayerDelta** (สิ่งที่ผู้เล่นเปลี่ยนในเซฟของตัวเอง) ดู
รายละเอียดกติกาทั้งหมดใน [`AGENTS.md`](./AGENTS.md)

## สถานะปัจจุบัน (G1 — Real-map data pipeline)

**ยังไม่มีแผนที่จริงถูก import เข้าระบบ** แหล่งข้อมูล OSM ที่อนุญาต
(Geofabrik, Overpass) ยังถูกบล็อกโดยนโยบาย network ขององค์กรใน
สภาพแวดล้อมที่พัฒนา repo นี้ (ดูรายละเอียดใน
[`docs/decisions/0003-osm-source-acquisition-blocked.md`](./docs/decisions/0003-osm-source-acquisition-blocked.md))
`configs/pilot-area.json` ยังมีแค่ bbox เป้าหมายเริ่มต้นที่
`coverage_status: "UNVERIFIED"` และ `docs/data/pilot-audit.md`
ยังเป็น placeholder `not_measured` ตามเดิม — **สิ่งที่เปลี่ยนในรอบนี้คือ
pipeline ที่จะรับข้อมูลจริงเมื่อปลดบล็อก implement และ test ครบแล้ว**
ไม่ใช่ stub อีกต่อไป: `acquire -> source lock/hash -> buffered extract ->
entity parse -> project -> normalize -> graph -> boundary/gateways ->
audit -> MapPack bake -> verify` ทำงานจริงทุกขั้น ทดสอบผ่าน
`tests/fixtures/synthetic/` (67 pytest tests) และรันจบ end-to-end จริง
ผ่าน `thaivia acquire --from-local-file` (ดู
[`docs/progress.md`](./docs/progress.md) session 2 และ
`docs/evidence/g1-*.log`) เมื่อมนุษย์วางไฟล์ `.osm.pbf`/`.osm.xml` ที่มี
สิทธิ์ใช้ไว้ที่ `data/cache/` ก็ใช้ pipeline นี้ได้ทันทีโดยไม่ต้องแก้โค้ด

**ยังไม่มี Unity project หรือ build ใดๆ ในสภาพแวดล้อมนี้** ไม่มี Unity
Editor, license หรือ build tool ติดตั้งอยู่ ดูเหตุผลและแผนถัดไปใน
[`docs/decisions/0002-unity-unverifiable-in-this-environment.md`](./docs/decisions/0002-unity-unverifiable-in-this-environment.md)
ไม่มี fake `ProjectVersion.txt` หรือ package lock ใดๆ ถูก commit ไว้ใน
repo นี้

**สิ่งที่มีจริงคือ `Thaivia.Core`** — pure C# library (ไม่มี
`UnityEngine` reference เลย) ที่ compile และ test ได้จริงด้วย
`dotnet build`/`dotnet test` (113 passed ณ wave 4 / G3: 35 จาก wave 3 +
78 ใหม่) ครอบคลุม MapPack contract types, strict loader (content_hash
re-derivation, version mismatch check), `SourceValue<T>` tri-state,
road-graph traversal primitives, coordinate narrowing ที่วัด error จริง,
และตั้งแต่ wave 4: deterministic tick/RNG streams, household/cohort
population, 8 building archetypes + activity clock, noise index,
graph-based accessibility (ไม่ใช่ radius), integer money ledger +
planning mode (draft/estimate/commit/pay/cancel), building relocation
เป็นโครงการ, และ save/load เต็มรูปแบบ — ดู
[`docs/progress.md`](./docs/progress.md) Session 4 และ
[`docs/decisions/`](./docs/decisions/) 0011-0016 โค้ดฝั่ง Unity
(`game/Assets/Scripts/Runtime/`) เขียนแล้วแต่**ไม่เคยถูก compile**
(ทุกไฟล์มี `// UNCOMPILED` marker) เพราะยังไม่มี Editor ในสภาพแวดล้อมนี้
รายละเอียดเต็มดู [`game/README.md`](./game/README.md)

`thaivia` CLI มี subcommand `doctor` (ตรวจ environment; default = cached
reachability, `--probe-network` เพื่อ live-probe), `acquire`
(`--from-url` หรือ `--from-local-file`), `build`, `audit`, `verify` —
ทุกตัวทำงานจริง ไม่มี stub เหลืออยู่

ดู [`docs/environment.md`](./docs/environment.md) สำหรับตาราง
available/missing/blocked/unverified แบบเต็ม และ
[`docs/progress.md`](./docs/progress.md) สำหรับ session log ล่าสุด

## โครงสร้าง repo

```
game/                 Unity project. Thaivia.Core (pure C#, dotnet build/test) is real;
                       Assets/Scripts/Runtime is written but uncompiled (no Unity Editor here).
tools/map_pipeline/   Python package: acquire -> build -> audit -> verify pipeline
content/              MapPack output และ content ที่ generate แล้ว (ยังว่าง)
configs/              pilot-area.json, sources.json + JSON Schemas
tests/                pytest ของ pipeline, synthetic fixtures ที่ label ชัดเจน
docs/                 environment audit, ADRs, evidence, data audit, progress log
```

## เริ่มต้นใช้งาน (Python toolchain)

```sh
python3 -m venv .venv
./.venv/bin/pip install -r requirements.txt
./.venv/bin/pip install --no-deps -e tools/map_pipeline
./.venv/bin/thaivia doctor
./.venv/bin/pytest
```

`doctor` ตรวจ Python version, dependencies, config schema, local cache
และ reachability ของแหล่งข้อมูลที่กำหนดค่าไว้ (จาก cache; ใช้
`--probe-network` เพื่อ live-probe) แล้ว exit non-zero เมื่อเจอปัญหาที่
block งานจริงๆ (ไม่ใช่แค่ "ยังไม่มีข้อมูล" ซึ่งเป็นสถานะปกติจนกว่าจะมี
ไฟล์ OSM จริง)

เมื่อมีไฟล์ `.osm.pbf`/`.osm.xml` ที่มีสิทธิ์ใช้ (ดู
[ADR-0003](./docs/decisions/0003-osm-source-acquisition-blocked.md)):

```sh
./.venv/bin/thaivia acquire --from-local-file /path/to/snapshot.osm.pbf
./.venv/bin/thaivia build
PACK=$(find content/mappacks -name '*.mappack.json')
./.venv/bin/thaivia audit --pack "$PACK"
./.venv/bin/thaivia verify --pack "$PACK"
```

## เริ่มต้นใช้งาน (Thaivia.Core, .NET toolchain)

```sh
cd game
dotnet build Thaivia.Core.csproj
dotnet test Thaivia.Core.Tests/Thaivia.Core.Tests.csproj
```

ทั้งสองคำสั่งรันได้จริงด้วย `dotnet-sdk-8.0` ที่ติดตั้งไว้แล้ว
(container-local) ไม่ต้องมี Unity ใดๆ — ดู
[`game/README.md`](./game/README.md) สำหรับขอบเขตที่ชัดเจนของสิ่งที่
compile+test ได้จริง เทียบกับโค้ด Unity ที่เขียนแล้วแต่ยังไม่เคย compile

## License / ODbL

โครงการนี้ยังไม่ได้แจกจ่ายฐานข้อมูลแผนที่หรือแอปใดๆ ข้อผูกพัน OSM/ODbL
(attribution, share-alike ของ derivative database) อยู่ใน scope ตั้งแต่
G1 ดู [`docs/decisions/0004-osm-odbl-licensing.md`](./docs/decisions/0004-osm-odbl-licensing.md)
