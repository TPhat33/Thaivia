# ADR-0001: ใช้ Python 3.11 แทน 3.12 ที่เสนอไว้ในแผนเดิม

- สถานะ: Accepted
- วันที่: 2026-09-20
- ผู้เกี่ยวข้อง: G0 bootstrap session

## บริบท

`IMPLEMENTATION_PLAN.th.md` เสนอ Python 3.12 เป็น baseline สำหรับ
`tools/map_pipeline/` (pyosmium/osmium, pyproj, Shapely, pytest)

## สิ่งที่ตรวจพบจริง

สภาพแวดล้อมที่ bootstrap repo นี้มี **Python 3.11.15** ที่
`/usr/local/bin/python3` เท่านั้น ไม่มี `python3.12` binary ให้ใช้ และ
ไม่มีสิทธิ์ compile Python จาก source หรือติดตั้งระดับระบบเพิ่มเติมนอก
เหนือขอบเขตงาน G0 (ดู `docs/environment.md`,
`docs/evidence/g0-python-version.log`)

## การตัดสินใจ

ใช้ **Python 3.11.15** เป็น baseline จริงของ `tools/map_pipeline/`
(`pyproject.toml` กำหนด `requires-python = ">=3.11"`) แทนที่จะรอหรือ
เดาว่า 3.12 มีอยู่

## ผลกระทบ

- ทุก dependency (`osmium`, `pyproj`, `shapely`, `jsonschema`, `pytest`)
  ตรวจแล้วว่ามี prebuilt wheel รองรับ cp311 บน Linux x86_64 จริง ไม่ต้อง
  build จาก source (ดู `docs/evidence/g0-dep-import-check.log`)
- ไม่มีการเปลี่ยนพฤติกรรม syntax/feature ที่โครงการนี้ใช้ในระดับที่ต่าง
  กันระหว่าง 3.11 กับ 3.12 อย่างมีนัยสำคัญ ณ จุดนี้ (โค้ดยังเล็กมาก)
- เมื่อเครื่องจริงของเจ้าของโครงการมี Python 3.12 ให้ทดสอบ compatibility
  อีกครั้งก่อนขยับ `requires-python` แต่ไม่บังคับต้องเปลี่ยนถ้า 3.11
  ทำงานได้ครบ
- นี่คือ deviation จากแผนเดิมที่บันทึกไว้อย่างตรงไปตรงมา ไม่ใช่การเสแสร้ง
  ว่ามี 3.12

## ทางเลือกที่ไม่เลือก

- ติดตั้ง Python 3.12 ผ่าน `deadsnakes` PPA — **ไม่เลือก** เพราะ
  `ppa.launchpadcontent.net` ถูกบล็อกโดย egress policy (ยืนยันจาก proxy
  status endpoint และจากการทดลอง `apt-get update` จริง ดู
  `docs/evidence/g0-dotnet-experiment.log`) และนโยบาย session นี้คือไม่
  retry host ที่บล็อกแล้ว
- Build Python 3.12 จาก source — ไม่เลือกเพราะเกินขอบเขต G0 และไม่มีเหตุ
  ผลเพียงพอ (3.11 ใช้งานได้ครบ)
