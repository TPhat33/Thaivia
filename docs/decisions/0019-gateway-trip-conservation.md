# ADR-0019: Gateway trip conservation -- integer ledger, ไม่มี tolerance

- สถานะ: Accepted
- วันที่: 2026-09-21
- ผู้เกี่ยวข้อง: wave 5 / G4 (Thaivia.Core.Simulation.Mobility.Gateways)

## บริบท

Spec §10 ระบุตรงๆ ว่า gateway "มี simulated inbound/outbound demand และ
external capacity; ไม่ดูดรถได้ไม่จำกัด" และ "การปิดประตูเข้าออกต้องกระทบ
การเดินทางและบริการ ไม่ทำให้รถหายจาก simulation" โจทย์ของ wave นี้ระบุว่า
supervisor จะตรวจส่วนนี้เข้มที่สุด และต้องการ integer-exact ledger test
(ไม่ใช่ tolerance) บวก control ที่พิสูจน์ว่า test จับบั๊กได้จริง

## การตัดสินใจ

### Invariant เดียว ที่ทุกอย่างต้อง hold ตลอดเวลา

`TripLedgerSnapshot.IsConserved` คือ `Generated == Completed + Queued`
เป๊ะ (long ทั้งหมด ไม่มี double เข้ามาปนในเส้นทางนี้เลย) `GatewayFlow`
ถูกออกแบบให้ invariant นี้เป็นจริง "โดยโครงสร้าง" ไม่ใช่แค่บังเอิญ:

- `GenerateOutboundDemand`/`GenerateInboundDemand` เพิ่ม `Generated*` และ
  `Pending*` พร้อมกันเสมอ (จำนวนเท่ากัน)
- `Step()` ย้ายจาก `Pending*` ไป `Completed*` พร้อมกันเสมอ (จำนวนเท่ากัน)
- **ไม่มี method ไหนใน `GatewayFlow` ที่แก้ `Pending*` โดยไม่แก้
  `Completed*` คู่กัน ยกเว้นสอง method ข้างบน** — `Open()`/`Close()` ไม่
  แตะ `Pending*`/`Completed*`/`Generated*` เลยแม้แต่บรรทัดเดียว มีผลแค่
  capacity ที่ `Step()` เห็นในรอบถัดไป

ผลคือ "ปิดประตู" ไม่ลบ demand — มันแค่ทำให้ effective capacity ของ
`Step()` เป็น 0 ชั่วคราว `Pending*` เลยพอกขึ้นแทนที่จะหาย ตรงตาม spec
เป๊ะ

### ทดสอบ: many-tick run + close/reopen + assert ทุก tick

`GatewayConservationTests.TotalTripsAcrossManyTicks_...` รัน 500 ticks
บนสอง gateway พร้อมกัน demand pattern ไม่คงที่ (กัน false-positive จาก
"ทุกอย่างเป็น 0 เสมอ"), ปิด gateway A ที่ tick 100 เปิดใหม่ที่ tick 220,
แล้ว assert invariant ทุก tick (ไม่ใช่แค่ท้ายสุด) รวมถึง assert ว่า
`Queued > 0` จริงหลังปิด — กัน test ที่ผ่านแบบ vacuous (เช่น ถ้า capacity
> demand เสมอ backlog จะเป็น 0 ตลอดและ test จะไม่พิสูจน์อะไรเกี่ยวกับ
"ปิดประตู" เลย)

ผลจริงที่วัดได้ (`docs/evidence/g4-gateway-conservation-measured.log`):
outbound generated=1750, completed=1507, queued=243 (1507+243=1750 ✓);
inbound generated=1000, completed=1000, queued=0 ✓ (inbound capacity ของ
ทั้งสอง gateway รวมกัน >= demand เสมอในโจทย์นี้ เลยไม่มี backlog inbound
เลย — เป็นผลจริงจากพารามิเตอร์ที่เลือก ไม่ใช่ค่า hardcode)

### Control: leaky variant (test-only) ต้องทำให้ assertion พัง

`LeakyGatewayFlow` (private class ใน test file เดียวกัน ไม่ใช่ production
code) ทำทุกอย่างเหมือน `GatewayFlow` ยกเว้น `Close()` ที่ set
`PendingOutbound = 0` ตรงๆ — จำลอง bug class ที่ spec ห้ามเป๊ะ ("ปิดประตู
ทำให้รถหาย") รันสถานการณ์เดียวกันผ่าน variant นี้แล้ว assert ว่า
`IsConserved` เป็น `false` จริง (generated=20, completed=4, queued=0 ->
4 != 20) และ assert ว่า `Assert.Equal` เดิมจะ throw
`Xunit.Sdk.EqualException` จริงถ้าใช้กับ variant นี้ — พิสูจน์ว่า
assertion หลักไม่ใช่ tautology แต่จับบั๊กได้จริงเมื่อมันเกิดขึ้นจริง

## ผลที่ตามมา

- `Thaivia.Core.Simulation.Mobility.Gateways.{TripLedgerSnapshot,
  GatewayFlow, GatewayNetwork}` — ยังไม่ได้ wire เข้ากับ
  `WorldState`/`MapPack.Gateway`'s demand/capacity fields จริงใน session
  นี้ (ดู progress.md "not_run/deferred") — โมดูลนี้ทดสอบและพิสูจน์ตัวเอง
  ได้สมบูรณ์แบบ standalone แต่การ derive `InboundCapacityPerTick`/
  `OutboundCapacityPerTick` จาก `MapPack.Gateway`'s
  `InboundDemandVehPerHour`/`ExternalCapacityVehPerHour` fields จริง และ
  การเรียก `GenerateOutboundDemand` จาก OD demand ที่ routing ผ่าน
  gateway node จริง ยังเป็นงานต่อยอดที่ไม่ได้ทำในรอบนี้
