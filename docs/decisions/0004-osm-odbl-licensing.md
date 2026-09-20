# ADR-0004: ข้อผูกพัน OSM/ODbL ตั้งแต่ G1 และ attribution artifact ที่ repo นี้จะถือ

- สถานะ: Accepted (นโยบาย, รอบังคับใช้จริงเมื่อมีข้อมูล)
- วันที่: 2026-09-20
- ผู้เกี่ยวข้อง: G0 bootstrap session

## บริบท

แผนระบุชัดว่า OSM attribution และ ODbL obligations อยู่ใน scope ตั้งแต่
G1 ไม่ใช่งานหลังเกมเสร็จ ข้อมูล OSM อยู่ภายใต้ Open Database License
(ODbL) ซึ่งกำหนดเงื่อนไข attribution และ share-alike สำหรับ derivative
database/Produced Work

## การตัดสินใจ (นโยบายที่ผูกไว้ล่วงหน้า แม้ยังไม่มีข้อมูลจริง)

1. ทุก MapPack ที่ `thaivia build` จะสร้างใน G1 ต้องมี attribution/ODbL
   notice ฝังอยู่ในตัว manifest ของมันเอง (ไม่ใช่แค่ในเอกสารแยก) พร้อม:
   - ข้อความ attribution มาตรฐาน "© OpenStreetMap contributors" (หรือ
     ข้อความที่ OSM Foundation กำหนด ณ เวลานั้น — ต้องตรวจซ้ำก่อนใช้จริง
     ไม่ใช่คัดลอกจากความจำ)
   - ลิงก์ไปยัง ODbL license text
   - source URL, retrieval time, snapshot time (หรือเหตุผลที่ไม่ทราบ)
     ตาม source lock ที่แผนกำหนด
2. Unity viewer (G2) ต้องแสดง attribution ที่อ่านได้จริงในหน้าจอ ไม่ใช่
   ซ่อนอยู่ใน credits ที่เข้าถึงยาก ตามที่แผนกำหนดไว้ใน "Unity viewer ที่
   ต้องได้ในรอบนี้"
3. ก่อนแจกจ่าย database หรือ app จริงใดๆ (นอก scope ของ G0-G2) ต้อง
   วิเคราะห์ว่าสิ่งที่แจกเป็น "derivative database" หรือ "Produced Work"
   ตามนิยามของ ODbL แล้วเลือก machine-readable release/source offer ที่
   เหมาะสม — งานนี้ยังไม่เริ่มเพราะยังไม่มีข้อมูลให้วิเคราะห์ (ดู
   ADR-0003)
4. การแยก GeographyBase/SimulationInitialization/PlayerDelta ช่วยให้
   ตรวจที่มาของแต่ละชั้นได้ง่ายขึ้น แต่ **ไม่ได้ยกเว้นข้อผูกพันใบอนุญาต
   โดยอัตโนมัติ** — ต้องตรวจแยกอีกครั้งว่าชั้นไหนต้อง share-alike เมื่อ
   ถึงเวลาแจกจ่ายจริง
5. `configs/sources.json` เก็บ `license_notice_url` ของแต่ละ endpoint
   ไว้แล้วตั้งแต่ G0 เพื่อให้ G1 อ้างอิงได้ทันทีโดยไม่ต้องค้นใหม่

## สิ่งที่ยังไม่ทำใน G0 (ตรงไปตรงมา)

- ยังไม่มี MapPack ให้ใส่ attribution เพราะยังไม่มีข้อมูลจริง (ADR-0003)
- ยังไม่มีการวิเคราะห์ derivative-database/Produced Work อย่างเป็น
  ทางการ เพราะยังไม่รู้ขอบเขตข้อมูลที่จะแจกจริง
- ยังไม่เผยแพร่ฐานข้อมูลหรือแอปใดๆ (README ระบุชัดว่ายังไม่มีแผนที่ import)

## ทางเลือกที่ไม่เลือก

- เลื่อนเรื่อง license ไปคิดตอนใกล้ release — **ไม่เลือก** เพราะแผนระบุ
  ชัดว่าต้องอยู่ใน scope ตั้งแต่ G1 การเขียน manifest/attribution
  contract ไว้ล่วงหน้าใน G0 ทำให้ G1 ไม่ลืมใส่
