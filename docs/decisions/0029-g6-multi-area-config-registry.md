# ADR-0029: Multi-area config registry (`configs/areas/index.json`)

- สถานะ: Accepted
- วันที่: 2026-09-26
- ผู้เกี่ยวข้อง: wave 8 / G6-01 (Opus supervisor brief), spec §4/§5
- ไม่ supersede ADR ใด — เป็นการต่อยอด (generalize) กลไก config ที่มีอยู่
  เดิม ไม่เปลี่ยนข้อสรุปของ ADR ใดที่เขียนไว้ก่อนหน้านี้

## บริบท

ตั้งแต่ G1 ถึง G5 ทั้ง `map_pipeline` และ CLI (`thaivia acquire/build/
audit/verify`) ผูกกับ `configs/pilot-area.json` หนึ่งไฟล์ตรงๆ ผ่าน
`load_pilot_area()` — ไม่มีทางระบุพื้นที่อื่น G6 ต้องขยายไปสู่ scope
ceiling 3 พื้นที่จริง (spec §4/§16) จึงต้องมีกลไก "พื้นที่ที่ N" ก่อนจะ
เลือกพื้นที่ที่ 2/3 จริงได้ (G6-02)

ข้อจำกัดสำคัญ: ห้ามทำให้ test/behavior เดิมของ th-bkk-pilot-001 เปลี่ยน
(regression) และห้าม fixture/mechanism ใหม่ทำให้ดูเหมือนมีการ verify
coverage ของพื้นที่ที่ยังไม่ได้ acquire จริง

## การตัดสินใจ

### 1. Area registry แยกจาก area config

`configs/areas/index.json` (validate ด้วย schema ใหม่
`areas-index.schema.json`) เป็นเพียง **ดัชนี**: `{id, config_path,
notes?}` ต่อพื้นที่หนึ่ง ไม่ซ้ำ id ไฟล์ config จริงของแต่ละพื้นที่ (bbox,
CRS, driving_side, acquire_budget, coverage_status) ยังคง validate ด้วย
schema เดิม `pilot-area.schema.json` ซึ่งไม่มีอะไรเฉพาะ "pilot" อยู่แล้ว
(ชื่อไฟล์คงไว้เพื่อไม่ break `test_config_schemas.py` ที่มีอยู่เดิม แต่
ความหมายตอนนี้คือ "area config schema" ทั่วไป)

พื้นที่แรกที่ลงทะเบียนคือ `th-bkk-pilot-001` ชี้ไปที่
`configs/pilot-area.json` เดิมทุกประการ — ไฟล์นั้นไม่ถูกแก้แม้แต่บรรทัด
เดียว

### 2. Back-compat contract: ไม่ผ่าน `--area` = ไม่แตะ registry เลย

`thaivia acquire/build/audit` ได้ flag `--area <id>` ใหม่ (optional)
เมื่อ **ไม่ใส่** `--area`:
- โหลด config จาก `--config` ตรงๆ (default `configs/pilot-area.json`)
  เหมือนเดิมทุกประการ ไม่มีการอ่าน `configs/areas/index.json` เลย

เมื่อ **ใส่** `--area <id>`:
- resolve `<id>` ผ่าน `configs/areas/index.json` แล้วโหลด config ของ
  พื้นที่นั้นแทน (ไม่สนใจ `--config`)

เหตุผลที่แยกกันเด็ดขาดแบบนี้ (ไม่ merge เป็น "default area = pilot ผ่าน
registry เสมอ"): test suite เดิม (`test_pipeline_end_to_end.py`) สร้าง
fake repo root ที่มีแค่ `configs/pilot-area.json` + `sources.json` โดย
ไม่มี `configs/areas/` เลย — ถ้า default path ต้องพึ่ง registry
เสมอ test นั้นจะพังทันที การแยก contract ทำให้ regression suite เดิมผ่าน
โดยไม่ต้องแก้ fixture ของมันแม้แต่บรรทัดเดียว (ยืนยันด้วยผลรัน pytest
77 ผ่านทั้งหมด รวม 67 เดิม + 10 ใหม่)

`load_area(area_id, config_path)` ใน `map_pipeline.config` เป็น single
entry point ที่ CLI command ทั้งสาม (`acquire`, `build`, `audit`) เรียก
ใช้ร่วมกัน แทนที่ `load_pilot_area()` ตรงๆ ที่เคยเรียกแบบ hardcode
(`load_pilot_area()` เองยังอยู่ ไม่ลบ เผื่อโค้ดอื่นอ้างถึง)

### 3. Namespacing: ไม่ต้องแยก cache/output directory ต่อพื้นที่

`data/cache/<map_id>.sourcelock.json` และ
`content/mappacks/<map_id>/...` ใช้ `map_id` เป็นส่วนหนึ่งของชื่อไฟล์อยู่
แล้วตั้งแต่ G1 เพราะฉะนั้นสอง area ที่มี `map_id` ต่างกันจะไม่ชนกันแม้ใช้
`data/cache/` โฟลเดอร์เดียวกัน — พิสูจน์ด้วย
`test_two_registered_areas_do_not_collide_when_acquired_in_the_same_cache_dir`
(acquire+build สองพื้นที่ติดกัน แล้ว assert ว่า lock/mappack ของทั้งคู่
อยู่ครบแยกกัน)

### 4. ไม่สร้าง schema ใหม่สำหรับ area config เอง

พิจารณาแล้วว่าจะสร้าง `area.schema.json` แยกจาก `pilot-area.schema.json`
เพื่อความหมายที่ตรงกว่า แต่ตัดสินใจไม่ทำ เพราะ schema เดิมมีฟิลด์ที่
G6-01 ต้องการครบอยู่แล้ว (`map_id`, `editable_bbox_wgs84_lonlat`,
`candidate_projected_crs`, `driving_side`, `coverage_status`,
`acquire_budget`) การเพิ่ม schema ไฟล์ที่สองที่มีเนื้อหาเหมือนกันทุก
ประการจะเพิ่มความเสี่ยงที่สอง schema จะ drift ออกจากกันโดยไม่มีใครสังเกต
โดยไม่ได้ประโยชน์อะไรเพิ่ม

## ผลกระทบ

- G6-02 (พื้นที่ที่ 2/3 จริง) ทำได้แค่เพิ่มไฟล์ config ใต้
  `configs/areas/*.json` + entry ใน `configs/areas/index.json` โดยไม่ต้อง
  แตะ pipeline logic
- `th-bkk-pilot-001` behavior เดิมไม่เปลี่ยนแปลง — regression coverage:
  `docs/evidence/g6-01-pytest-run.log`

## ข้อจำกัดที่ยังไม่แก้

- `verify` command ยังไม่รับ `--area` (ไม่ได้อยู่ใน acceptance criteria
  ของ G6-01; รับ `--pack` ตรงๆ อยู่แล้วซึ่งไม่ผูกกับ area โดยตรง)
- Registry ยังไม่มี command `thaivia areas list` แยก — ตรวจผ่าน
  `configs/areas/index.json` ตรงๆ หรือ `registered_area_ids()` ใน Python
  ได้ทันที ยังไม่จำเป็นต้องมี subcommand ใหม่สำหรับ G6-01/G6-02
