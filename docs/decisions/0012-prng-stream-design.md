# ADR-0012: Seeded PRNG แยก stream ด้วย splitmix64 ต่อ object แยก

- สถานะ: Accepted
- วันที่: 2026-09-22
- ผู้เกี่ยวข้อง: wave 4 / G3 (Thaivia.Core.Simulation.RandomStreams)

## บริบท

IMPLEMENTATION_PLAN.th.md §11 กำหนด "seeded PRNG แยก streams" (เช่น
traffic, incidents, cohort variation) โดยดึงจาก stream หนึ่งต้องไม่ทำให้
ลำดับของอีก stream เปลี่ยน และต้อง deterministic ข้าม session (save/load
ต้อง resume สตรีมแต่ละตัวได้ตรงจุดเดิม)

## ทางเลือกที่พิจารณา

1. **`System.Random` ตัวเดียว แชร์ทุก stream** — ตกทันทีเพราะดึงค่าจาก
   stream หนึ่งจะเลื่อน state ตัวเดียวที่ทุก stream อื่นใช้ร่วมกัน ขัดกับ
   ข้อกำหนดข้อแรกตรงๆ
2. **`System.Random` แยก instance ต่อ stream** — แก้ปัญหาข้อ 1 ได้ แต่
   .NET ไม่ garantee ว่า algorithm ภายในของ `System.Random` จะเสถียรข้าม
   .NET version (เอกสาร Microsoft ระบุว่า deterministic เฉพาะภายใน
   process/version เดียวกันเมื่อใช้ seed คงที่ ไม่รับประกันข้าม major
   version) ซึ่งเสี่ยงต่อ "save จาก build เก่าโหลดใน build ใหม่แล้ว RNG
   sequence เพี้ยน"
3. **splitmix64 เขียนเอง แยก instance ต่อ stream (เลือกใช้)** —
   algorithm public-domain ที่ระบุ step ชัดเจนตายตัว (Steele/Lea/Flood
   2014), state เป็น `ulong` เดียว, ไม่มี dependency บน runtime ใดๆ
   เขียนซ้ำได้เป๊ะในภาษาอื่นถ้าต้องทำ (สำคัญเพราะ pipeline ฝั่ง Python
   อาจต้องการ RNG ที่ reproducible แบบเดียวกันในอนาคต)

## การตัดสินใจ

- `Thaivia.Core.Simulation.RandomStreams.DeterministicRandom` implement
  splitmix64 ตรงตาม reference algorithm, expose `State` (ulong) และ
  `DrawCount` (long) สำหรับ save/restore แบบ bit-exact
- `NamedRandomStreams` เก็บ `Dictionary<RandomStreamName,
  DeterministicRandom>` — แต่ละชื่อ (`Traffic`/`Incidents`/
  `CohortVariation`) ได้ **object แยกกันจริง** ไม่ใช่ view เดียวกันของ
  state ก้อนเดียว ความเป็นอิสระจึงเป็นสมบัติเชิงโครงสร้าง (คนละ field
  ในหน่วยความจำ) ไม่ใช่แค่ผลลัพธ์ที่ test เจอโดยบังเอิญ — ดูการพิสูจน์ทั้ง
  เชิงโครงสร้างและเชิงพฤติกรรมใน
  `NamedRandomStreamsTests.DrawingFromOneStream_DoesNotShiftAnotherStreamsSequence`
- แต่ละ stream seed จาก `masterSeed XOR <salt คงที่เฉพาะ stream>` แล้วผ่าน
  splitmix64 หนึ่งรอบก่อนเริ่มใช้งานจริง (`NamedRandomStreams` constructor)
  เพื่อไม่ให้ seed คนละ stream ที่ใกล้กัน (เช่น masterSeed เดียวกันบวก
  salt ต่างกันแค่บิตเดียว) ให้ sequence ช่วงต้นที่สัมพันธ์กันสังเกตได้
- `DeterministicRandom.HashStep` (static, ไม่มี state) แยกออกจาก stream
  โดยเจตนา ใช้เฉพาะที่ต้องการ deterministic hash ที่ "ไม่กิน" ตำแหน่งของ
  stream ไหนเลย (ตัวอย่างจริง: `WorldState.AssignArchetype` ต้องเดา
  archetype จาก building source id แบบเดิมทุกครั้งไม่ว่าจะถูกเรียกก่อน/
  หลัง RNG อื่นแค่ไหน)

## ผลที่ตามมา

- ไม่ใช่ cryptographically secure — ไม่จำเป็น เพราะไม่ใช้ทำอะไรที่เกี่ยว
  กับความปลอดภัย
- Save file เก็บ `(RandomStreamName -> ulong state, long drawCount)` ต่อ
  stream ตรงๆ (ดู ADR-0014) — restore คือสร้าง `DeterministicRandom` ใหม่
  จาก state เดิมเป๊ะ ไม่ต้อง "replay" การสุ่มใหม่
