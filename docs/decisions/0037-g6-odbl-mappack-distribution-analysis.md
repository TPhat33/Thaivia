# ADR-0037: วิเคราะห์ ODbL สำหรับการแจกจ่าย MapPack (ต่อยอด ADR-0004 ข้อ 3)

- สถานะ: Proposed — **เป็น engineering analysis และข้อเสนอ ไม่ใช่ความเห็น
  ทางกฎหมาย (not legal advice)** ต้องให้เจ้าของโปรเจกต์และ/หรือ
  ที่ปรึกษากฎหมายตรวจก่อนแจกจ่ายจริงทุกกรณี
- วันที่: 2026-09-26
- ผู้เกี่ยวข้อง: wave 8 / G6-13 (Opus supervisor brief), plan §18, ต่อยอด ADR-0004 ข้อ 3
- **ไม่ supersede ADR-0004** — ADR-0004 ข้อ 1/2/4/5 ยังใช้ได้ทั้งหมด
  เอกสารนี้ทำสิ่งที่ ADR-0004 ข้อ 3 บอกไว้ว่า "ยังไม่เริ่มเพราะยังไม่มี
  ข้อมูลให้วิเคราะห์" — ตอนนี้เริ่มวิเคราะห์โครงสร้าง (structure) แล้ว
  แม้ยังไม่มีข้อมูลจริงให้แจกจ่าย (G1-10/G6-03 ยัง blocked) เพื่อให้พร้อม
  ทันทีที่ข้อมูลจริงมี

## สถานะที่ต้องระบุตรงๆ ก่อนอ่านต่อ

**ยังไม่มีการแจกจ่าย database หรือ app ใดๆ จริง** ทั้ง G1-10 (real
acquisition ของ th-bkk-pilot-001) และ G6-03 (พื้นที่ที่ 2/3) ยัง `blocked`
เพราะ network access ถูกบล็อก (ADR-0003) เอกสารนี้จึงเป็นการวิเคราะห์
**โครงสร้าง**ของ pipeline/MapPack ที่มีอยู่แล้ว เทียบกับข้อกำหนดของ ODbL
เพื่อเตรียมกลไกไว้ล่วงหน้า ไม่ใช่การประกาศว่าได้ตรวจสอบข้อมูลจริงชุดใด
ชุดหนึ่งแล้วผ่าน

## 1. Derivative Database vs Produced Work — จุดที่ต้องแยกให้ชัด

ODbL (Open Database License) แยกสองสิ่งที่ได้รับการปฏิบัติต่างกัน:

- **Derivative Database**: ฐานข้อมูลที่สร้างจากฐานข้อมูลต้นทาง (ODbL) โดย
  การเพิ่ม/ลบ/แก้ไข/จัดเรียงข้อมูลใหม่ ยังคง "เป็นฐานข้อมูล" อยู่ (ข้อมูล
  โครงสร้าง ไม่ใช่ผลลัพธ์ที่แปลงรูปจนดึงข้อมูลกลับคืนไม่ได้) — การแจกจ่าย
  Derivative Database ต้องคง attribution และ **share-alike ภายใต้
  ODbL** (มาตรา 4.4/4.5 ของ ODbL text) รวมถึงมาตรา 4.6 (Technological
  Protection Measures): ถ้าใส่มาตรการทางเทคนิคที่จำกัดสิทธิ์ตาม ODbL
  (เช่น encrypt/obfuscate) ต้องมีช่องทางให้ผู้รับสามารถเข้าถึงข้อมูลแบบ
  ไม่ถูกจำกัดได้ด้วย (เช่น เสนอ (offer) สำเนาที่ไม่ถูกจำกัดตามคำขอ)
- **Produced Work**: ผลงานที่ผลิตขึ้นโดยใช้ฐานข้อมูล แต่ไม่ได้รวมส่วนที่
  เป็นสาระสำคัญของโครงสร้างฐานข้อมูลไว้ในตัวมันเอง (ODbL ยกตัวอย่าง เช่น
  แผนที่ที่ render เป็นภาพ, รายงานสถิติที่สรุปจากฐานข้อมูล) — Produced
  Work ต้องมี **attribution เท่านั้น** ไม่ต้อง share-alike ทั้งฉบับ

### เมื่อนำมาใช้กับสถาปัตยกรรมของโปรเจกต์นี้

| Layer | คือ Derivative Database หรือ Produced Work? | เหตุผล |
|---|---|---|
| **MapPack JSON file** (`content/mappacks/<map_id>/<hash>.mappack.json` — `payload.geography_base`, `payload.road_graph`, `source_tags` ที่คัดลอกมาเกือบทั้งหมดจาก OSM entities) | **Derivative Database** | เป็นข้อมูลโครงสร้าง (coordinates, tags, node/way relationships) ที่ดึงและแปลงพิกัดมาจาก OSM โดยตรง ยังอยู่ในรูปแบบที่ query/สกัดกลับเป็นข้อมูลเชิงโครงสร้างได้ง่าย (เปิดไฟล์ JSON อ่านได้ตรงๆ) — ไม่ใช่ผลลัพธ์ที่ "แปรรูป" จนดึงกลับไม่ได้ |
| **การ render บนหน้าจอเกม** (extruded mesh, pixel texture, มุมกล้อง 2.5D) | **Produced Work** (ตามแนวทางที่ ODbL ยกตัวอย่างไว้ — ภาพที่ render จากฐานข้อมูล) | พิกเซลบนจอไม่ใช่โครงสร้างข้อมูลที่ query กลับเป็นตาราง/กราฟได้ตรงๆ แม้จะ "มาจาก" ฐานข้อมูลก็ตาม |
| **`SimulationInitialization`/`PlayerDelta`** (ประชากร งาน โครงการที่ผู้เล่นสร้าง) | ไม่ใช่ทั้งสอง — เป็นข้อมูลที่ทีมนี้แต่งขึ้นเองทั้งหมด ไม่ได้มาจาก OSM | AGENTS.md ข้อ 3 แยกชั้นนี้ออกจาก GeographyBase อยู่แล้ว — ไม่มีข้อผูกพัน ODbL กับชั้นนี้เลย |
| **โค้ด/art ต้นฉบับของเกม** (C#, pixel art, UI) | ไม่เกี่ยวกับ ODbL เลย | ไม่ได้มาจากฐานข้อมูล OSM |

**ข้อสรุปที่สำคัญที่สุดของการวิเคราะห์นี้**: การที่ MapPack JSON ถูก
bundle เข้าไปใน app ที่แจกจ่ายจริง (ไม่ว่าจะผ่าน Google Play/App Store
หรือช่องทางอื่น) **นับเป็นการแจกจ่าย Derivative Database** ไม่ใช่แค่
Produced Work — เพราะไฟล์ JSON ที่มีโครงสร้างข้อมูลเดิมอยู่ครบถูกส่งไปยัง
เครื่องผู้ใช้จริง ไม่ใช่แค่ภาพที่ render แล้วส่งออกไปดู ต้องปฏิบัติตาม
เงื่อนไข attribution + share-alike ของ ODbL กับไฟล์นี้โดยตรง (ไม่ใช่แค่
กับหน้าจอเกม) — **นี่คือการวิเคราะห์ทางวิศวกรรมของทีมนี้ ไม่ใช่ข้อสรุป
ทางกฎหมายที่ยืนยันได้ 100%** สถานการณ์แบบนี้ (game bundling extracted OSM
data as a structured file) เป็นรูปแบบที่ผู้พัฒนาโครงการโอเพนซอร์สจำนวน
มากถือปฏิบัติแบบเดียวกัน แต่การตีความ ODbL ที่แน่นอนสำหรับ use case
เฉพาะควรให้ผู้เชี่ยวชาญกฎหมายยืนยันก่อน distribute จริง

## 2. Attribution — สถานะปัจจุบัน (ครบตาม ADR-0004 แล้วในเชิงโครงสร้าง)

`Thaivia.Core.MapPack.Attribution` + `map_pipeline.pipeline.mappack`'s
`ATTRIBUTION_NOTICE`/`ODBL_LICENSE_URL`/`OSM_COPYRIGHT_URL` ฝัง
attribution ไว้ใน **ทุก MapPack ที่ build** อยู่แล้ว (`payload.provenance.attribution`)
และ ADR-0004 ข้อ 2 กำหนดให้ viewer แสดงผลได้จริง (ยัง blocked เพราะไม่มี
Unity Editor — G2/G6-11) ส่วนนี้ไม่มีอะไรต้องแก้เพิ่มจาก ADR-0004

## 3. ข้อเสนอ machine-readable release/source-offer mechanism

เนื่องจาก MapPack เป็น Derivative Database (ข้อ 1) การแจกจ่ายจริงควรมี
กลไกต่อไปนี้ — **นี่คือข้อเสนอสำหรับเจ้าของโปรเจกต์พิจารณา ยังไม่ได้ทำ
จริงในรอบนี้**:

### 3.1 เพิ่ม `license` block ใน MapPack manifest schema

เสนอเพิ่มใน `tools/map_pipeline/schemas/mappack.schema.json` (และ
`Thaivia.Core.MapPack.Manifest`/`Provenance` ฝั่ง C#) เป็น field ใหม่ที่
validate ได้จริงด้วย JSON Schema (ทดสอบได้ด้วย pytest/dotnet test ตรงๆ
ไม่ใช่แค่ prose):

```json
"license": {
  "database_license": "ODbL-1.0",
  "database_license_url": "https://opendatacommons.org/licenses/odbl/1-0/",
  "produced_work_notice": "In-game rendered map view requires attribution only (Produced Work); the bundled MapPack JSON file is a Derivative Database and is itself offered under ODbL-1.0 -- see source_offer below.",
  "source_offer": {
    "mechanism": "bundled_plaintext_no_tpm",
    "reproduction": {
      "pipeline_repo_url": "<repo url ตอน release>",
      "pipeline_commit": "<git commit ที่ build MapPack เวอร์ชันนี้>",
      "importer_version": "<manifest.importer_version เดิม>",
      "settings_hash": "<manifest.settings_hash เดิม>",
      "source_sha256": "<provenance.sha256 เดิม>"
    }
  }
}
```

หลักการ: **ไม่ใส่ technological protection measure (TPM) ใดๆ กับไฟล์
MapPack เลย** (ไม่ encrypt/obfuscate) — เพราะไฟล์เป็น JSON ธรรมดาอยู่แล้ว
ตอนนี้ ถ้ายังคงเป็นแบบนี้ต่อไปตอน distribute จริง ก็ไม่เข้าเงื่อนไข ODbL
มาตรา 4.6 ที่บังคับต้อง "เสนอสำเนาที่ไม่ถูกจำกัด" เพิ่มเติม เพราะไม่มี
การจำกัดตั้งแต่แรก — **นี่คือเหตุผลที่แนะนำให้คง MapPack เป็น
plaintext JSON ต่อไป แม้จะมี pressure ให้บีบอัด/เข้ารหัสเพื่อลดขนาด
app** ถ้าจะบีบอัด (เช่น gzip) ควรเป็นการบีบอัดที่ decompress กลับเป็น
JSON เดิมได้ตรงๆ (reversible, ไม่ใช่ lossy) ซึ่งไม่นับเป็น TPM ที่จำกัด
สิทธิ์

### 3.2 "Source offer" ผ่าน pipeline ที่ reproduce ได้จริงอยู่แล้ว

จุดแข็งของ pipeline ที่มีอยู่แล้ว (ADR-0006 mappack determinism):
`thaivia verify` พิสูจน์ว่า re-derive จาก source+settings เดิมได้ byte-
identical เป๊ะ ซึ่งแปลว่า **"source offer" ตาม ODbL ทำได้ง่ายกว่าปกติ
มาก** — แค่เผยแพร่ (ก) source snapshot (หรือ pointer+hash ไปยัง Geofabrik
snapshot วันที่ระบุ), (ข) commit ของ `tools/map_pipeline` ที่ใช้ bake, และ
(ค) `settings_hash` ที่บันทึกไว้แล้วในทุก manifest — ผู้รับสามารถ
re-derive MapPack ตัวเดียวกันได้เองโดยไม่ต้องได้ไฟล์ MapPack ที่ "แจก
พร้อมโค้ด" แยกกันเป็นคนละชุด นี่คือกลไกที่ **ทดสอบได้จริง** เพราะ
`test_pipeline_mappack_determinism.py`/`thaivia verify` มีอยู่แล้ว

### 3.3 Machine-readable "release manifest" แยกจาก MapPack เอง

เสนอ (ยังไม่ implement) ไฟล์ `content/mappacks/<map_id>/RELEASE.json`
ที่สร้างพร้อมกับ MapPack ตอน build ระดับ "release-ready" (ไม่ใช่ทุก dev
build) เก็บ: license block ข้างต้น + รายชื่อไฟล์ที่แจกจริง + hash ของแต่
ละไฟล์ + ลิงก์ source-offer จริงเมื่อ publish (URL หรือ contact) — เป็น
JSON Schema แยกที่ validate ได้เหมือน mappack.schema.json ปัจจุบัน

## 4. สิ่งที่ต้องทำก่อน distribute จริง (checklist ที่ยังไม่ผ่านสักข้อ)

- [ ] G1-10/G6-03: acquire ข้อมูลจริงอย่างน้อย 1 พื้นที่ (ยัง blocked, ADR-0003)
- [ ] Implement §3.1-3.3 ข้างต้น (schema + fields) — ยังไม่ทำ
- [ ] ตรวจซ้ำข้อความ attribution ที่ OSM Foundation กำหนด ณ เวลานั้นจริง
      (ADR-0004 ข้อ 1 เตือนไว้แล้วว่าอย่าคัดลอกจากความจำ)
- [ ] **ให้เจ้าของโปรเจกต์และ/หรือที่ปรึกษากฎหมายตรวจข้อสรุปข้อ 1
      ("MapPack = Derivative Database") ก่อน distribute จริงทุกครั้ง** —
      เอกสารนี้เป็นการวิเคราะห์ทางวิศวกรรมเพื่อเตรียมกลไก ไม่ใช่ความเห็น
      ทางกฎหมายที่ใช้แทนการตรวจสอบจริงได้
- [ ] ตรวจ Google Play/App Store's ข้อกำหนดเรื่อง third-party data
      licenses ในหน้า store listing (แยกจาก G6-14's mobile compliance
      checklist)

## ผลกระทบ

- ไม่มีโค้ด/schema เปลี่ยนแปลงในรอบนี้ — เอกสารนี้เป็น analysis + proposal
  เท่านั้นตามที่ acceptance criteria ของ G6-13 ระบุ
- G6-14 (mobile store compliance checklist, ยัง blocked) ควรอ้างอิงข้อ 4
  ของเอกสารนี้เป็นส่วนหนึ่งของ pre-distribution checklist
