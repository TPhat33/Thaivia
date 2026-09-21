# ADR-0011: Logical fixed tick ที่ไม่ขึ้นกับ wall clock/FPS

- สถานะ: Accepted
- วันที่: 2026-09-22
- ผู้เกี่ยวข้อง: wave 4 / G3 (Thaivia.Core.Simulation)

## บริบท

IMPLEMENTATION_PLAN.th.md §11 กำหนดว่า simulation ต้องใช้ "logical tick
ที่ไม่ขึ้นกับ FPS" และ §13/§15 กำหนดว่าต้อง "ไม่มี wall-clock catch-up"
(แอปที่ resume หลัง background นานๆ ต้องไม่ fast-forward เมือง) และเสนอ
5Hz เป็นเป้าทดลองเริ่มต้น ไม่ใช่ผลวัด

## การตัดสินใจ

1. `Thaivia.Core.Simulation.Time.TickConfig.TicksPerSecond` เป็น `const
   double = 5.0` จุดเดียวที่ค่านี้ถูกเขียน — ไม่มี magic number `5`/`5.0`
   กระจายอยู่ที่อื่น ค่านี้เป็นเป้าทดลองตามแผน ยังไม่ผ่านการวัด soak จริง
   (ยังไม่มี Unity ให้วัด frame pacing — ดู ADR-0002)
2. `SimClock` มีเมธอดเดียวที่เปลี่ยน `CurrentTick` คือ `AdvanceTicks(long
   tickCount)` — รับจำนวน tick ตรงๆ จาก fixed-step loop ของเกม ไม่มีการ
   อ่าน `DateTime.Now`/`DateTime.UtcNow`/`Environment.TickCount`/
   `Stopwatch` ที่ไหนในคลาสนี้หรือใน `WorldState` เลย (ตรวจด้วย
   source-scan test จริง ไม่ใช่แค่ comment — ดู
   `SimClockTests.SimClockAndWorldStateSource_ContainNoWallClockRead`
   พร้อม control test
   `Detector_FlagsASyntheticWallClockUsage_ControlCase` ที่พิสูจน์ว่า
   scanner เองตรวจจับได้จริง ไม่ใช่ no-op)
3. `Resume()` ไม่รับ parameter ใดๆ เลย (ตรวจด้วย reflection ใน
   `Resume_TakesNoParameters`) — ไม่มีช่องให้ elapsed-wall-time หลุดเข้า
   มาคำนวณ catch-up ได้แม้จะพยายามแก้ในอนาคตโดยไม่เปลี่ยน signature นี้
4. `AdvanceTicks` throw ถ้าเรียกตอน paused — บังคับให้ caller เรียก
   `Resume()` ก่อนเสมอ ทำให้ "pause บน background" เป็นจริงในระดับ type
   ไม่ใช่แค่ convention

## ผลที่ตามมา

- Renderer/UI ฝั่ง Unity (ยังไม่ compile ได้ในสภาพแวดล้อมนี้ — ดู
  ADR-0002) เป็นผู้ตัดสินใจว่าจะเรียก `AdvanceTicks` กี่ครั้งต่อ frame
  ตาม fixed-step accumulator ของตัวเอง `SimClock` เองไม่รู้จัก frame
  rate เลย
- เมื่อมี Unity Editor จริงและวัด soak 30 นาทีตาม plan §15 ได้ ตัวเลข
  5Hz นี้อาจถูกปรับ — เป็นค่าที่แก้ได้จุดเดียว (`TickConfig`) ไม่ต้องไล่
  แก้ทุกจุดที่ใช้
