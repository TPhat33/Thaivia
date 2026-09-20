# AGENTS.md — กติกาการทำงานของ Thailand Urban Repair

เอกสารนี้กลั่นจาก `START_FROM_EMPTY_REPO.th.md` และ `IMPLEMENTATION_PLAN.th.md`
(ทั้งสองไฟล์ไม่ได้อยู่ใน repo นี้ แต่เป็น source of truth ที่ใช้ตอนเริ่มโครงการ)
เป็นกติกาที่ agent ทุกตัว (Claude หรืออื่น) ที่ทำงานใน repo นี้ต้องยึดถือ
ถ้ากติกาในไฟล์นี้ขัดกับสามัญสำนึกทั่วไปของ AI assistant ให้ยึดไฟล์นี้ก่อน

## หลักการที่ห้ามละเมิด

1. **ห้ามปลอมแผนที่**: ห้าม snap ถนนเข้ากริด 4/8 ทิศ ห้ามทำถนนคดให้ตรง
   ห้ามย้าย/ลบอาคารเพื่อความสวยงาม ห้ามเติมตึก/คลอง/แยกที่เดาแล้วติด
   label `source=OSM` หรือ tag ว่ามาจากแหล่งข้อมูลจริงทั้งที่ไม่ใช่
2. **ห้ามใช้ synthetic fixture แทน source ข้อมูลจริง**: synthetic fixture
   ใช้ทดสอบ parser/geometry ได้เท่านั้น ต้องอยู่ใต้
   `tests/fixtures/synthetic/` และมี marker ชัดเจน (`"synthetic": true`
   ใน JSON หรือ header comment ใน XML) ไฟล์ synthetic ห้ามอ้าง domain
   ของแหล่งข้อมูลจริง (เช่น openstreetmap.org, geofabrik.de) เป็นของตน
   Real-data gate (G1) ผ่านได้ด้วยข้อมูลจริงเท่านั้น
3. **แยกสามชั้นข้อมูลเสมอ**:
   - `GeographyBase` — vectors/tags/provenance จาก source snapshot จริง
     เปลี่ยนเฉพาะตอน release ของ map version ใหม่
   - `SimulationInitialization` — ประชากร งาน รายได้ ความสุข เสียง
     demand และ scenario สมมติ ไม่อ้างเป็นสถิติจริง
   - `PlayerDelta`/`WorldState` — โครงการ ถนนใหม่ การย้ายอาคาร ผล
     simulation ที่ผู้เล่นสร้างขึ้นเอง
   ห้ามผสมสามชั้นนี้ในโครงสร้างข้อมูลเดียวกัน และห้ามส่ง PlayerDelta
   กลับไปแก้ OSM
4. **ไม่มีข้อมูล = unknown ไม่ใช่พื้นที่ว่าง**: ฟีเจอร์ที่ source ไม่มี
   height/use/lanes/entrance ต้องเก็บเป็น `unknown` หรือทำ
   `visual_assumption`/`simulation_assumption` ที่ inspect/แก้ได้ ห้าม
   เขียนว่าเป็น source fact
5. **ห้ามปลอมหลักฐาน**: ห้ามแต่ง Unity metadata (เช่น
   `ProjectVersion.txt`, package lock) ให้ดูเหมือน Editor เคยเปิดทั้งที่
   ไม่มี Unity ในเครื่อง ห้ามอ้างว่า build/test ผ่านโดยไม่มีผลรันจริง
   ทุก command ที่อ้างว่าใช้ได้ต้องมี log จริงใน `docs/evidence/`
6. **CLI ต้องไม่ปลอมความสำเร็จ**: subcommand ที่ยังไม่ implement ต้อง
   exit code ที่ชัดว่า "not implemented yet" ห้าม exit 0 หลอกว่า
   ทำงานสำเร็จ
7. **แหล่งข้อมูลที่อนุญาต**: Geofabrik Thailand `.osm.pbf` หรือ bounded
   Overpass query ที่ freeze เป็น fixture หลังสำเร็จเท่านั้น ห้าม Google
   Maps/Street View tracing, screenshots-as-database, bulk public tile
   download ตรวจ budget เครือข่าย/ดิสก์ก่อนดาวน์โหลดเสมอ
   country-sized download ต้องขออนุมัติมนุษย์
8. **coordinate handling**: ใช้ `always_xy`, lon/lat WGS84 →
   AOI-appropriate projected meters → local origin → Unity X/Z ground.
   ห้ามใช้ raw degrees เป็นเมตร ห้าม force CRS เดียวทุกจังหวัด ห้าม
   quantization/simplification เปลี่ยน canonical source geometry
9. **ห้ามผูกเหตุการณ์สมมติกับตัวจริง**: storyline อาชญากรรม/ทุจริตห้าม
   ใช้ชื่อบุคคล/กิจการจริงเป็นผู้ผิด และห้ามเชื่อมโยงกลับสถานที่จริงแบบ
   กล่าวหา

## สิ่งที่ต้องหยุดถามมนุษย์เสมอ

- ติดตั้งระดับระบบที่มีค่าใช้จ่าย/license (เช่น Unity seat, Apple
  Developer account, Google Play Console)
- signing/production upload/publish จริง
- เปลี่ยน baseline สำคัญ (เช่น เปลี่ยน engine, เปลี่ยน CRS มาตรฐาน)
- action ที่ทำลายข้อมูลที่กู้คืนไม่ได้
- ทุกกรณีที่ agent ตรวจเองไม่ได้ (เช่น ไม่มีเครื่อง Mac สำหรับ iOS)

Agent ไม่ต้องหยุดถามอนุมัติทีละไฟล์ระหว่างทำงานปกติ

## นิยาม "จบงาน" (Definition of Done ย่อ)

งานจบเมื่อมีครบ: logic จริง + provenance ของข้อมูล + validation/tests ที่
รันจริงแล้ว + evidence ใน `docs/evidence/` การมีปุ่มหรือ mock result ไม่
เท่ากับระบบทำงาน ทุก session ต้องอัปเดต `TASKS.json` และ
`docs/progress.md`

## ภาษา

เอกสาร prose (README, ADR, progress, audit) เป็นภาษาไทย ศัพท์เทคนิค
คงภาษาอังกฤษได้ โค้ด, identifiers, code comments, commit message และ
CLI output เป็นภาษาอังกฤษเสมอ
