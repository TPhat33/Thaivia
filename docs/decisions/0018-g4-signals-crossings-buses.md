# ADR-0018: Fixed/adaptive signal trade-off, accessible crossings, bus scheduling

- สถานะ: Accepted
- วันที่: 2026-09-21
- ผู้เกี่ยวข้อง: wave 5 / G4 (Thaivia.Core.Simulation.Mobility)

## บริบท

Spec §9 ระบุ "fixed signals ก่อน adaptive" และโจทย์ของ wave นี้ระบุตรงๆ ว่า
"Adaptive ต้อง demonstrably different จาก fixed ในการทดสอบ ไม่ใช่ renamed
copy" และเรื่อง trade-off: "Demonstrate with measured numbers that a
mitigation genuinely trades off" — ห้าม tune ค่าคงที่จนกว่าจะ "เจอ"
trade-off ปลอมๆ

## การตัดสินใจ

### Fixed vs Adaptive: interface เดียว สอง implementation

`ISignalPlan.AllocateGreenTicks(queueA, queueB)` เป็น contract เดียวที่
`SignalIntersectionSimulator` เรียกทุกต้น cycle `FixedTimeSignalPlan`
ignore ทั้งสอง parameter (constant split เสมอ) `AdaptiveSignalPlan`
จัดสรร green ตามสัดส่วน queue จริง โดย clamp ที่
`[MinGreenTicks, CycleTicks-MinGreenTicks]` กันไม่ให้ฝั่งใดฝั่งหนึ่งอด
green เป็นศูนย์ ทั้งสอง class ทดสอบแยกกันตรงๆ ว่า Fixed ไม่ตอบสนอง
queue เลย (`AllocateGreenTicks` คืนค่าเดิมไม่ว่า queue เท่าไหร่) แต่
Adaptive ตอบสนองจริง (`SignalsTests.AdaptiveSignalPlan_ShiftsGreenToward...`)

### Trade-off ที่วัดได้จริง ไม่ใช่ tuned

รันสถานการณ์ over-saturated intersection จริง (arrivals รวม 7/tick >
discharge rate 4/tick ทุก tick ไม่ว่า split ไหน — งานตั้งใจให้ทั้งสอง
approach คับคั่งจริง ไม่ใช่โจทย์ที่ปลอดภัยเกินจนไม่มีอะไรให้ trade-off)
เทียบ Fixed(50/50) กับ Adaptive over 4000 ticks (200 cycles) ผลจริงที่วัด
ได้ (พิมพ์ผ่าน `ITestOutputHelper`, ดู
`docs/evidence/g4-signals-tradeoff-measured.log`):

| Metric (average queue length over the whole run) | Fixed | Adaptive |
|---|---|---|
| Approach A (busy, 4 arrivals/tick) | 3991.00 | 3440.37 (−13.8%, ดีขึ้น) |
| Approach B (light, 3 arrivals/tick) | 2010.50 | 2561.13 (+27.4%, แย่ลง) |

**นี่คือ trade-off จริง**: adaptive ลด delay ของฝั่งที่คับคั่งกว่าได้จริง
โดยแลกกับ delay ของฝั่งที่เบากว่าที่แย่ลงจริง — ไม่ใช่ pure win ทั้งสอง
ด้าน ตัวเลขทั้งสองแถวมาจากการรันจริงครั้งเดียวกัน ไม่ได้ tune parameter
จนกว่าจะได้ผลที่ "ดูดี" — เห็นได้จาก final green split ของ adaptive
(11/9, ห่างจาก fixed 10/10 ไม่มาก) ว่าอัลกอริทึมยังทำงานแบบ conservative
ตาม queue ratio จริง ไม่ใช่ maximize metric A โดยไม่สนใจ B

หมายเหตุ: ในสถานการณ์ over-saturated ทั้งคู่ (arrivals รวม > capacity
รวมเสมอ) ทั้งสอง queue โตไม่มีที่สิ้นสุดตามธรรมชาติของคิวแบบนี้ — สิ่งที่
adaptive ทำได้คือ "โตช้าลง" ที่ A แลกกับ "โตเร็วขึ้น" ที่ B ไม่ใช่ "แก้
ปัญหา" ทั้งระบบ (ระบบ over-saturated ไม่มีทาง signal timing แก้ทั้งหมดได้
โดยไม่เพิ่ม capacity จริง) — บันทึกไว้ตรงๆ ไม่ใช่ claim เกินจริง

### Accessible crossings เปลี่ยน route จริง ไม่ใช่แค่ label

`PedestrianCrossing.AccessibleFlag` ถูกอ่านโดย
`Routing.MobilityGraph`'s `requireAccessibleCrossings` parameter เท่านั้น
— crossing ที่ `AccessibleFlag=false` ถูกข้ามไปเลยเมื่อ build graph สำหรับ
traveller ที่ต้องการ accessible path จึงได้ผลลัพธ์ route ที่ต่างจาก
general walking graph จริง (ตัวเลขต่างกันจริงใน
`CrossingsTests.InaccessibleCrossing_...`) ไม่ใช่แค่ inspector แสดง badge
ต่างกัน

### Bus schedule มาจาก network distance จริง ไม่ใช่เลขตั้งเอง

`BusRouteScheduler.RoundTripTicks` เรียก `MobilityGraph.ShortestDistanceMeters`
(Vehicle mode, turn-aware) ระหว่างทุกคู่ป้ายถัดกัน + dwell time จริง —
เพิ่ม dwell time ต้องทำให้ round-trip ticks โตขึ้นจริงและ throughput/tick
ลดลงจริง (พิสูจน์ใน `BusRouteTests.IncreasingDwellTime_...`) Ridership มา
จาก cohort population batch (`HouseholdCohort.PopulationCount x mode
share`) ไม่ใช่การ spawn ผู้โดยสารทีละคน และถูก cap ด้วย fleet capacity
จริงเสมอ (overflow ถูกรายงาน ไม่ถูกทิ้งเงียบ)

## ผลที่ตามมา

- ทดสอบทั้งหมดอยู่ที่
  `game/Thaivia.Core.Tests/Simulation/Mobility/{SignalsTests,CrossingsTests,BusRouteTests}.cs`
- `docs/evidence/g4-signals-tradeoff-measured.log` เก็บ raw test output
  ของตัวเลขในตารางข้างบน
- ยังไม่ได้ wire adaptive signal เข้ากับ `LinkQueueSimulator`'s
  arrivals-from-OD-demand แบบเต็มระบบใน session นี้ — `SignalIntersectionSimulator`
  ทำงานอิสระด้วย arrivals ที่กำหนดเอง (เพียงพอสำหรับพิสูจน์ trade-off
  mechanically) การเชื่อมกับ demand ที่มาจาก MobilityGraph/cohorts จริง
  เป็นงานต่อยอดที่ยังไม่ทำใน wave นี้ — บันทึกไว้ตรงๆ ใน progress.md
