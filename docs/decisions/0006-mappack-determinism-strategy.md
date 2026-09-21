# ADR-0006: กลยุทธ์ byte-deterministic bake ของ MapPack

- สถานะ: Accepted
- วันที่: 2026-09-21
- ผู้เกี่ยวข้อง: G1 pipeline session

## บริบท

แผนกำหนดว่า MapPack ต้อง "byte-deterministic สำหรับ source + settings
เดิม (sorted keys, stable ID ordering, fixed float formatting, ไม่มี
timestamp อยู่ใน hashed payload)" และ `thaivia verify` ต้อง re-derive
แล้วเทียบ hash แบบ byte-identical

## การตัดสินใจ

1. MapPack ที่ bake แล้วมีโครงสร้าง top-level คงที่:
   ```json
   {"volatile": {...}, "content_hash": "sha256:...", "payload": {...}}
   ```
   `content_hash` คำนวณจาก SHA-256 ของ canonical JSON serialization ของ
   `payload` เท่านั้น (`json.dumps(payload, sort_keys=True,
   separators=(",", ":"), ensure_ascii=True)`) — ไม่รวม `volatile`
2. `volatile` เก็บทุกค่าที่เปลี่ยนตามเวลาที่รัน (bake timestamp, source
   retrieval timestamp) ไว้แยกออกจาก hashed region โดยเจตนา สอง bake
   ของ source+settings เดียวกันจึงได้ `content_hash` เท่ากันเป๊ะ แม้
   `volatile.baked_at` ต่างกัน — พิสูจน์แล้วด้วย
   `tests/test_pipeline_mappack_determinism.py` และ
   `docs/evidence/g1-determinism-two-runs.log` (สอง `thaivia build` รัน
   ห่างกัน >1 วินาที ได้ hash เดียวกัน)
3. Stable ID ordering: ทุก list ที่ bake เข้า payload ถูก sort ก่อนเสมอ
   (ways/nodes/relations by id, buildings/edges by source_id/way_id)
   ไม่พึ่ง iteration order ของ dict/set ที่ไม่รับประกัน
4. Fixed float formatting: canonical geometry ปัดเป็น 9 ตำแหน่งทศนิยม
   (`normalize.canonical_round`, sub-micron) ก่อน serialize เพื่อตัด
   floating-point noise จาก reordering ของ arithmetic ระหว่าง run เดียว
   ค่า render (1cm quantization) เป็นฟิลด์แยกที่คำนวณจาก canonical เสมอ
   ไม่เคยเขียนทับ canonical
5. `thaivia verify` ตรวจสองชั้น: (ก) self-consistency — hash ของ
   `payload` บนดิสก์ต้องตรงกับ `content_hash` ที่บันทึกไว้ (จับไฟล์ที่ถูก
   แก้/เสียหายหลัง bake) (ข) re-derivation — bake ใหม่จาก source lock +
   config เดิมแล้วต้องได้ `content_hash` เดียวกัน (จับ non-determinism
   หรือ settings ที่ drift) ทั้งสองต้องผ่านก่อน exit 0

## ผลกระทบ

- เปลี่ยน settings ใดๆ ที่อยู่ใน `pipeline.settings.effective_settings()`
  (bbox, context_buffer_m, candidate_projected_crs, driving_side,
  importer/schema version) จะเปลี่ยน `settings_hash` และทำให้
  `content_hash` เปลี่ยนตามเสมอ — พิสูจน์ด้วย
  `test_changed_settings_change_the_hash`
- `IMPORTER_VERSION`/`SCHEMA_VERSION` ใน `pipeline/settings.py` ต้อง bump
  ทุกครั้งที่ logic การ parse/project/normalize/graph/bake เปลี่ยนแบบที่
  อาจเปลี่ยนผลลัพธ์ เพื่อให้ source lock/MapPack เก่าไม่ถูกเข้าใจผิดว่า
  ตรงกับ pipeline เวอร์ชันใหม่

## ทางเลือกที่ไม่เลือก

- Hash ทั้งไฟล์ MapPack รวม `volatile` — ไม่เลือกเพราะจะทำให้ hash
  เปลี่ยนทุกครั้งที่ bake ใหม่แม้ source/settings เหมือนเดิมทุกอย่าง
  ขัดกับ requirement ตรงๆ
- ใช้ float ธรรมดาไม่ปัดทศนิยม แล้วหวังว่า IEEE-754 repr จะ deterministic
  เอง — ยังคง deterministic ภายใน Python run เดียวกันจริง แต่การปัดไว้
  ที่ 9 ตำแหน่งทำให้ diff อ่านง่ายขึ้นและกัน edge case จาก arithmetic
  order ที่ต่างกันเล็กน้อยระหว่าง code path
