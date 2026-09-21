# ADR-0005: นโยบายเลือกและตรวจสอบ CRS ต่อ AOI (ไม่ hardcode ทั้งประเทศ)

- สถานะ: Accepted
- วันที่: 2026-09-21
- ผู้เกี่ยวข้อง: G1 pipeline session

## บริบท

แผนกำหนดชัดว่า "ไม่ใช้ raw degrees เป็น meters และไม่ force CRS เดียวให้
ทุกจังหวัด CRS choice ต้องเป็น per-AOI config value โดย pilot เสนอ
EPSG:32647 แต่โค้ดต้อง select/validate มันเทียบกับ AOI extent" ประเทศไทย
กินพื้นที่ข้าม UTM zone 47N และ 48N อย่างน้อย ดังนั้นค่า CRS เดียวใช้ไม่ได้
กับทุกพื้นที่

## การตัดสินใจ

1. `pipeline/projection.py` ไม่มีค่าคงที่ CRS ระดับ module ที่ใช้ทุก AOI
   `candidate_projected_crs` มาจาก `configs/pilot-area.json` เสมอ (per-AOI
   config value)
2. `validate_crs_for_aoi(candidate_crs, bbox_wgs84_lonlat)` คำนวณ UTM zone
   ที่ "ควรจะเป็น" จาก longitude กึ่งกลาง bbox ด้วยสูตรมาตรฐาน
   `floor((lon+180)/6)+1` แล้วเทียบกับ zone ที่ EPSG code ที่ config เลือก
   สื่อถึง (326xx = north, 327xx = south) ถ้าไม่ตรงกัน (zone ผิด หรือ
   hemisphere ผิด) จะคืน warning string ที่อธิบายปัญหา
3. **โค้ดไม่ auto-correct CRS ให้เอง** — เมื่อ warning เกิดขึ้น ค่า config
   ที่ตั้งไว้ยังถูกใช้ต่อ (เพราะการเปลี่ยน CRS มาตรฐานเป็นการเปลี่ยน
   baseline สำคัญที่ต้องให้มนุษย์ตัดสินใจตาม AGENTS.md) แต่ warning ถูกเก็บ
   ไว้ใน MapPack manifest (`manifest.crs_validation_warnings`) ให้ทุกคนที่
   อ่าน pack เห็นปัญหาทันที ไม่ใช่ซ่อนไว้
4. สำหรับ `th-bkk-pilot-001` (bbox กรุงเทพฯ) EPSG:32647 (UTM 47N) ตรวจแล้ว
   validate สะอาด ไม่มี warning (ดู `tests/test_pipeline_projection.py`)
5. ทุก transform ผ่าน `pyproj.Transformer.from_crs(..., always_xy=True)`
   เท่านั้น ไม่มี path ไหนสลับ lon/lat เป็น lat/lon ด้วยมือ

## ผลกระทบ

- ถ้าในอนาคตขยายไปพื้นที่อื่นของไทยที่ตกใน UTM zone 48N (เช่นภาคอีสาน/
  ภาคใต้บางส่วน) `configs/pilot-area.json` (หรือ config ของ AOI ใหม่) ต้อง
  ระบุ `candidate_projected_crs` ที่ถูกต้องเอง — pipeline จะเตือนถ้าเลือก
  ผิด แต่จะไม่หยุดหรือแก้ให้อัตโนมัติ
- CRS ที่ไม่ใช่ WGS84/UTM (326xx/327xx) ยังใช้ได้ (pyproj รองรับ) แต่
  automatic zone validation จะข้ามไปพร้อม warning ว่า "verify manually"

## ทางเลือกที่ไม่เลือก

- Hardcode EPSG:32647 เป็นค่าคงที่ใน `projection.py` — ไม่เลือกเพราะขัด
  กติกาข้อ 8 ของ AGENTS.md โดยตรง (ห้าม force CRS เดียวทุกจังหวัด)
- Auto-correct CRS ตาม zone ที่คำนวณได้เมื่อ config ผิด — ไม่เลือกเพราะ
  การเปลี่ยน CRS มาตรฐานเป็น "เปลี่ยน baseline สำคัญ" ที่ต้องหยุดถามมนุษย์
  ตาม AGENTS.md ไม่ใช่ให้โค้ดตัดสินใจเงียบๆ
