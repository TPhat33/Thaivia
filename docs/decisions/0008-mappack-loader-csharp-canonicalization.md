# ADR-0008: MapPackLoader (C#) — canonical hash re-derivation แทนการเชื่อ content_hash เฉยๆ

- สถานะ: Accepted
- วันที่: 2026-09-21
- ผู้เกี่ยวข้อง: wave 3 / G2 (Thaivia.Core)

## บริบท

`Thaivia.Core.Serialization.MapPackLoader` (C#, ฝั่งเกม) ต้องรับ MapPack
JSON ที่ `thaivia build` (Python) bake ไว้ แล้วตรวจสอบว่าไฟล์ไม่ได้ถูก
แก้ไข/เสียหายหลัง bake — เหมือนที่ `thaivia verify` ทำฝั่ง Python
(`map_pipeline.pipeline.mappack.compute_content_hash` เทียบกับ
`content_hash` ที่ประกาศไว้ในไฟล์) โจทย์คือ: จะ re-implement
`json.dumps(payload, sort_keys=True, separators=(",", ":"),
ensure_ascii=True)` ของ Python ใน C# ให้ได้ byte เดียวกันเป๊ะได้อย่างไร
โดยไม่ต้อง reverse-engineer การ format float/escape string ของ Python
ทีละกรณี (ซึ่งเสี่ยงผิดเงียบๆ และ hash จะ mismatch กับไฟล์จริงที่ pipeline
ผลิต)

## การตัดสินใจ

`Thaivia.Core.Serialization.CanonicalJson` ไม่ reformat ตัวเลข/สตริงใหม่
จาก scratch เลย แต่ใช้ข้อเท็จจริงที่ว่า `tools/map_pipeline/.../commands/build.py`
เขียนไฟล์ทั้งไฟล์ด้วย `json.dumps(pack, indent=2, sort_keys=True)` ซึ่ง
`ensure_ascii=True` (default ของ Python) ตรงกับ canonical form อยู่แล้ว
— ตัวเลข/สตริงใน "content_hash" ฝั่งไฟล์ กับตัวเลข/สตริงที่ใช้คำนวณ
canonical hash จริงๆ จึงเป็น **token เดียวกันเป๊ะ** ต่างกันแค่ whitespace
คั่นระหว่าง token (ที่มาจาก `indent=2`) ดังนั้น `CanonicalJson.Write`
เดินซ้ำ (recursive) บน `JsonElement` ที่ parse แล้ว: สำหรับ object จะ
sort key (ด้วย `StringComparer.Ordinal` — ตรงกับ Python string `<` บน
ASCII) แล้วต่อกันแบบไม่มี whitespace; สำหรับ array ต่อกันตามลำดับเดิม;
สำหรับ leaf (string/number/true/false/null) ใช้
`JsonElement.GetRawText()` คัดลอก substring ต้นฉบับตรงๆ ไม่ format ใหม่
เลย ส่วน object key เองต้อง escape ใหม่ (เพราะ `System.Text.Json` คืนชื่อ
key แบบ unescape แล้ว) จึง implement escaping ตามกฎ Python's
`encode_basestring_ascii` เอง (backslash/quote/control-char escapes
มาตรฐาน + ทุกตัวอักษรนอกช่วง 0x20-0x7E เป็น `\uXXXX`) แทนที่จะพึ่ง
`System.Text.Json`'s default `JavaScriptEncoder` ซึ่ง escape กว้างกว่า
(HTML-safe) และจะให้ byte ต่างจาก Python

## ผลที่ตามมา

- ข้อจำกัด: ถ้า key ของ object ใน MapPack มีอักขระที่ escaping ทั้งสอง
  ภาษาต่างกันจริงๆ (ไม่เกิดกับ key ปัจจุบันทั้งหมด — เป็น property name
  คงที่ตาม schema หรือ OSM tag key ที่เป็น ASCII lowercase/underscore/colon
  เสมอ) hash อาจ mismatch ได้ในทางทฤษฎี ถ้าอนาคตมี key ที่ไม่ใช่ ASCII
  ต้อง revisit
- ข้อดี: ไม่ต้อง reimplement float formatting ของ Python (`repr()`-style
  shortest round-trip) เลย เพราะไม่เคย reformat ตัวเลขใหม่ — ทดสอบจริงด้วย
  `MapPackLoaderTests.UntamperedFixture_LoadsWithoutThrowing_ProvingTheTamperTestIsReal`
  (โหลดไฟล์ที่ `thaivia build` ผลิตจริงจาก synthetic fixture แล้วผ่าน)
  และ `TamperedPayloadByte_IsRejectedOnContentHashMismatch` (แก้ 1 หลัก
  ใน payload แล้วต้องถูกปฏิเสธ)
- `MapPackLoader.Load` ตรวจ `schema_version`/`importer_version`
  (เทียบกับ `MapPackVersions`) **ก่อน** คำนวณ hash เสมอ — mismatch เวอร์ชัน
  ต้องได้ error ที่บอกเวอร์ชันชัดเจน ไม่ใช่ error "tampered" ที่ทำให้
  เข้าใจผิดว่าไฟล์เสีย
