# ADR-0002: Unity 6.3 LTS เป็น proposed baseline แต่ตรวจไม่ได้ในสภาพแวดล้อมนี้

- สถานะ: Accepted
- วันที่: 2026-09-20
- ผู้เกี่ยวข้อง: G0 bootstrap session

## บริบท

แผนเสนอ Unity 6.3 LTS เป็น engine baseline (patch จริงต้องเลือกหลังตรวจ
official docs และ lock หลังจากนั้น) ต้องพิสูจน์ด้วยการเปิด clean
project และ build ได้จริงผ่าน Editor/Hub

## สิ่งที่ตรวจพบจริง

ในสภาพแวดล้อมที่ bootstrap repo นี้:

- ไม่มี Unity Editor หรือ Unity Hub ติดตั้งอยู่ (`which unity-editor
  unityhub` ว่าง, ไม่มี `/opt/Unity*`)
- ไม่มี Unity license ให้ตรวจ (ตรวจไม่ได้เพราะไม่มี Editor)
- host ดาวน์โหลด Unity (`download.unity3d.com`) ถูกบล็อกโดย egress
  policy (403 บน CONNECT, ยืนยันจาก proxy status endpoint)
- ไม่มีบัญชี Unity/license/ค่าใช้จ่ายที่ agent มีสิทธิ์จัดหาเองได้

ดูหลักฐานเต็มใน `docs/environment.md` และ `docs/evidence/g0-unity-check.log`,
`docs/evidence/g0-proxy-status.log`

## การตัดสินใจ

1. **ไม่สร้าง Unity project ปลอม** ไม่มีไฟล์ `ProjectVersion.txt`,
   `Packages/packages-lock.json`, `.meta` หรือ scene ใดๆ ถูก commit ใน
   `game/` จนกว่าจะมี Unity Editor จริงมาสร้างมันขึ้นผ่าน Editor/Hub
   ตามที่แผนกำหนด (idempotent Editor bootstrap script)
2. `game/` ใน repo นี้มีเพียง `.gitkeep` และหมายเหตุอธิบายสถานะ
3. `.gitattributes` เตรียม policy สำหรับ Unity text serialization และ
   Git LFS ไว้ล่วงหน้า (ดู ADR ด้าน binary policy ใน `.gitattributes`
   เอง) เพื่อไม่ต้อง migrate ทีหลังเมื่อมี Unity project จริง
4. G2 (Unity viewer) และ compile/device evidence ทั้งหมดมีสถานะ
   `not_run` ใน `TASKS.json`/`docs/progress.md` — ไม่ใช่ `failed` (ไม่มี
   เครื่องมือให้รันตั้งแต่แรก ไม่ใช่รันแล้วพัง) และไม่ใช่ `passed`
   (ไม่มีการยืนยันด้วยการรันจริง)
5. C# simulation core (wave 3) จะเขียนเป็น pure C# assembly ที่ไม่ import
   `UnityEngine` ตั้งแต่ต้น เพื่อให้ compile/test ได้ด้วย `dotnet test`
   ผ่าน .NET SDK ที่ติดตั้งแบบทดลองในสภาพแวดล้อมนี้ (ดู
   `docs/environment.md` ส่วน .NET) โดยไม่ต้องรอ Unity Editor

## ผลกระทบ

- G2 acceptance gate (Unity app ดูแผนที่จริงได้) **ยังไม่ผ่านและผ่านไม่ได้
  ในสภาพแวดล้อมปัจจุบัน** จนกว่าจะมี Unity Editor/license/เครื่องจริง
- งานที่ไม่ต้องพึ่ง Unity (Python pipeline, C# pure simulation contracts,
  JSON Schema, tests) เดินหน้าต่อได้เต็มที่โดยไม่ถูกบล็อกตาม platform
  หนึ่งบล็อกทั้งโครงการ

## ขั้นตอนมนุษย์ที่เล็กที่สุดเพื่อปลดบล็อก

ตัวเลือกใดตัวเลือกหนึ่ง (ไม่ต้องทำทั้งหมด):

1. รัน bootstrap นี้ต่อบนเครื่องที่มี Unity Hub + Unity 6.3 LTS ติดตั้ง
   อยู่แล้ว (Editor license ที่ถูกต้องตามบัญชีของเจ้าของโครงการ) แล้วให้
   agent ใช้ Editor นั้นสร้าง project จริงผ่าน command line
   (`-createProject`) และ idempotent Editor bootstrap script
2. หรือ allowlist `download.unity3d.com` (และ endpoint license ที่
   Unity Hub ต้องใช้) ใน egress policy ของ container นี้ชั่วคราว แล้ว
   แจ้ง license key/seat ที่จะใช้

## ทางเลือกที่ไม่เลือก

- ปลอม/handcraft ไฟล์ Unity metadata ให้ดูเหมือน Editor เคยเปิด —
  **ห้ามเด็ดขาด** ตามกติกาใน `AGENTS.md`
