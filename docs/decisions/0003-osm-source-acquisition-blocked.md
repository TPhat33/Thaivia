# ADR-0003: การดึงข้อมูล OSM จริงถูกบล็อกโดย egress policy — pipeline ทดสอบด้วย synthetic fixture ที่ label ชัด, G1 ยังไม่ผ่าน

- สถานะ: Accepted (blocked, ไม่ใช่ถอย)
- วันที่: 2026-09-20
- ผู้เกี่ยวข้อง: G0 bootstrap session

## บริบท

แผนกำหนดให้ใช้ Geofabrik Thailand `.osm.pbf` ที่ cache แล้ว หรือ bounded
Overpass query ที่ freeze เป็น fixture เป็นแหล่งข้อมูลจริง

## สิ่งที่ตรวจพบจริง

ทุก endpoint ที่แผนอนุญาตให้ใช้ถูกบล็อกโดย organization egress policy
ของสภาพแวดล้อมนี้ (403 บน CONNECT tunnel ยืนยันจาก proxy status
endpoint และจากการที่ `thaivia doctor` ยิง probe จริงแล้วได้ผลเดียวกัน):

- `download.geofabrik.de`
- `osm-internal.download.geofabrik.de`
- `overpass-api.de`
- `overpass.kumi.systems`
- `api.openstreetmap.org`
- `planet.openstreetmap.org`
- `download.openstreetmap.fr`

ดูหลักฐานเต็มใน `docs/environment.md`, `docs/evidence/g0-proxy-status.log`,
`docs/evidence/g0-doctor-run.log`

## การตัดสินใจ

1. **ไม่ใช้ synthetic data แทนข้อมูลจริง** `thaivia acquire` ไม่ทำอะไร
   นอกจาก exit ด้วยสถานะ "not implemented yet (G1)" ที่ระบุ blocker
   ชัดเจน (exit code `NOT_IMPLEMENTED = 3` ไม่ใช่ 0) — ดู
   `tools/map_pipeline/src/map_pipeline/commands/acquire.py`
2. Pipeline (parser, projection, normalization, graph builder เมื่อสร้าง
   ใน G1) จะถูกพัฒนาและทดสอบด้วย **synthetic fixture ที่ label ชัดเจน**
   ใต้ `tests/fixtures/synthetic/` เท่านั้น (`"synthetic": true` /
   header comment) ซึ่งทดสอบ parser/geometry logic ได้ แต่ **ไม่นับเป็น
   การผ่าน real-data gate**
3. `docs/data/pilot-audit.md` คง `status: blocked, no source data
   acquired` และทุก count เป็น `not_measured` จนกว่าจะมีข้อมูลจริง
4. **G1 (Real data) ยังคง FAILED/BLOCKED** ใน `TASKS.json` จนกว่ามนุษย์
   จะจัดหา snapshot ที่มีสิทธิ์ใช้จริง

## ขั้นตอนมนุษย์ที่เล็กที่สุดเพื่อปลดบล็อก (เลือกทางใดทางหนึ่ง)

1. **Allowlist `download.geofabrik.de`** (หรือ endpoint Overpass ที่
   เลือกใช้) ใน egress policy ของ container/organization แล้วให้ agent
   รัน `thaivia acquire` ใหม่ — pipeline จะดาวน์โหลดเฉพาะ bounded extract
   ตาม `configs/pilot-area.json` (budget ที่กำหนดไว้แล้ว ไม่ใช่ country
   ทั้งไฟล์)
2. **หรือวางไฟล์ snapshot ที่มีสิทธิ์ใช้เอง** (`.osm.pbf` หรือ
   `.osm.xml`) ที่ครอบคลุม bbox ใน `configs/pilot-area.json`
   (`editable_bbox_wgs84_lonlat` + `context_buffer_m`) ไว้ที่
   **`data/cache/`** ในชื่อไฟล์ที่สื่อความหมาย (เช่น
   `data/cache/th-bkk-pilot-001-source.osm.pbf`) แล้วแจ้งที่มา/license
   ของไฟล์นั้น (เพื่อบันทึก provenance ใน source lock ตามที่แผนกำหนด)
   — `thaivia doctor` จะเห็นไฟล์นี้ทันทีใน check `source_cache`

ไม่ต้องทำทั้งสองทาง เลือกทางที่สะดวกที่สุดสำหรับเจ้าของโครงการ

## ผลกระทบ

- G1/G2 (real-map viewer) ยังเริ่มไม่ได้จนกว่าจะมีข้อมูลจริง
- งานที่ไม่ต้องพึ่งข้อมูลจริง (schema, CLI scaffolding, parser logic ที่
  ทดสอบผ่าน synthetic fixture, C# contracts) เดินหน้าต่อได้ตามหลัก "ถ้า
  มี blocker ของด้านหนึ่ง ให้ทำอีกด้านต่อ"

## ทางเลือกที่ไม่เลือก

- ใช้ mirror หรือ VPN เพื่อเข้าถึง host ที่บล็อก — **ไม่เลือกเด็ดขาด**
  ตามคำสั่งของ supervisor และตาม README ของ proxy (`/root/.ccr/README.md`):
  policy denial ต้องรายงาน ไม่ใช่หาทางเลี่ยง
- สร้างแผนที่สมมติที่หน้าตาคล้ายกรุงเทพฯ — **ห้ามเด็ดขาด** ตามกติกาหลัก
  ของโครงการ (ห้ามปลอมภูมิศาสตร์ไทย)
