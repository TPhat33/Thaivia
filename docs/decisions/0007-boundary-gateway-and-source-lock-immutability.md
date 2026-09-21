# ADR-0007: รูปแบบ boundary gateway และความ immutable ของ source lock

- สถานะ: Accepted
- วันที่: 2026-09-21
- ผู้เกี่ยวข้อง: G1 pipeline session

## บริบท

แผนกำหนดสองเรื่องที่ต้องมี real decision ก่อนเริ่มใช้งาน: (1) การ crop
พื้นที่ต้องไม่ทำให้ถนนจริงกลายเป็นซอยตัน ต้องมี "editable interior +
read-only context buffer + external gateways" ที่มี "bounded simulated
inbound/outbound demand และ external capacity ไม่ใช่ทางออกดูดรถได้ไม่
จำกัด" (2) source lock ต้อง "immutable once written; เปลี่ยน settings
ต้องได้ lock ใหม่ ไม่ใช่ overwrite เงียบๆ"

## การตัดสินใจ: Gateway model

1. Node classification (`pipeline/boundary.py`) มี 3 ระดับ: `interior`
   (ในขอบ `editable_bbox_wgs84_lonlat`), `buffer` (นอก interior แต่ใน
   `context_buffer_m`), `outside` (พ้นทั้งสองชั้น) คำนวณผ่าน
   `Projector.lonlat_to_local` เทียบระยะเมตรจริง ไม่ใช่เทียบ degree ตรงๆ
2. Gateway ถูกสร้างที่ endpoint ของ road edge ใดๆ ที่ node นั้นจัดอยู่ใน
   `outside` — หมายความว่า way เส้นนั้นมีต่อไปนอก crop จริง (ไม่ใช่ปลาย
   ถนนจริงที่หยุดในโลกจริง) แต่ละ gateway มีค่า default ที่ประกาศชัดว่า
   เป็น `simulation_assumption` พร้อม `rule`:
   `inbound_demand_veh_per_hour=150`, `outbound_demand_veh_per_hour=150`,
   `external_capacity_veh_per_hour=400` — ทั้งสามค่ามี bound เป็นตัวเลข
   จำกัดเสมอ ไม่มี path ไหนใส่ infinity/unbounded
3. Gateway ไม่ได้เก็บใน namespace เดียวกับ tags จริงของถนน — มันเป็น
   object แยก (`Gateway` dataclass, `namespace="simulation_assumption"`)
   อยู่ใน `road_graph.gateways` ของ MapPack ไม่ใช่ผสมเข้า
   `geography_base`
4. ค่า default ทั้งสามยังไม่ได้ผ่าน calibration จริง (ไม่มี measured
   demand data ในสภาพแวดล้อมนี้) — นี่คือค่าเริ่มต้นที่เขียนไว้ให้แก้ได้
   ทีหลังเมื่อมี measured data ที่อนุมัติ ตามที่แผนระบุว่า "ทุกตัวเลข
   traffic ใน buffer เป็นสมมติจนกว่าจะมีแหล่ง measured demand ที่อนุมัติ"

## การตัดสินใจ: Source lock immutability

1. Lock แต่ละอันถูก content-address ด้วยชื่อไฟล์
   `<map_id>.<sha256[:16]>.<settings_hash[:12]>.sourcelock.json` ภายใต้
   `data/cache/` — sha256 ของ source bytes เปลี่ยน หรือ settings hash
   เปลี่ยน จะได้ชื่อไฟล์ใหม่เสมอ ไม่ชนกับของเดิม
2. `write_source_lock()` เขียนไฟล์ที่ content-addressed นี้ **เฉพาะตอนที่
   ยังไม่มีไฟล์นั้นอยู่** — ถ้ามีอยู่แล้ว (bytes+settings เดิมทุกอย่าง)
   จะไม่เขียนทับ (idempotent, ไม่ใช่ overwrite) ไฟล์ lock เก่าที่เคยเขียน
   ไว้จึงไม่ถูกแก้เนื้อหาโดยการรันซ้ำหรือรันด้วย settings อื่น — พิสูจน์
   ด้วย `test_write_source_lock_never_overwrites_an_existing_versioned_lock`
3. `<map_id>.sourcelock.json` เป็น **convenience pointer** เท่านั้น ชี้
   ไปที่ lock ล่าสุดเสมอ (`build`/`verify` อ่านจากตัวนี้เป็น default) —
   ตัวมันเองปรับปรุงได้ (เพื่อความสะดวก) แต่ไฟล์ versioned ข้างต้นที่มัน
   ชี้ไปไม่เคยถูกแก้

## ผลกระทบ

- ถ้าต้องการดู lock version เก่าที่เคยใช้ bake pack version หนึ่ง ยังหา
  ไฟล์นั้นได้เสมอใน `data/cache/` (ไม่ถูกลบ/ทับ) ตราบใดที่ยังไม่มีใครลบ
  ไฟล์ด้วยมือ (data/cache/ ทั้งโฟลเดอร์ไม่ commit เข้า git — เป็น local
  cache)
- Gateway demand/capacity ที่เป็นค่า default ต้องถูกตรวจสอบซ้ำก่อนใช้ใน
  G3+ เมื่อมี traffic model จริง ไม่ใช่ตัวเลขที่อนุมัติแล้วสำหรับ gameplay
  balance

## ทางเลือกที่ไม่เลือก

- ให้ `<map_id>.sourcelock.json` เป็นไฟล์ lock ตัวเดียวที่ถูก overwrite
  ตรงๆ ทุกครั้งที่ acquire ใหม่ — ไม่เลือกเพราะขัด requirement
  "immutable once written" ตรงๆ แม้จะง่ายกว่า
- Gateway capacity = infinity (ปล่อยรถเข้า/ออกได้ไม่จำกัดที่ขอบ crop) —
  ไม่เลือกเพราะแผนห้ามชัดเจน ("ไม่ดูดรถได้ไม่จำกัด")
