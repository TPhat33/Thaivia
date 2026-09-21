# ADR-0021: WorldState wiring และ save format extension สำหรับ G4 mobility state

- สถานะ: Accepted
- วันที่: 2026-09-21
- ผู้เกี่ยวข้อง: wave 5 / G4 (Thaivia.Core.Simulation.WorldState, .Save)

## บริบท

AGENTS.md rule 5 / spec §13 ระบุว่า save ต้องครอบคลุม "state/delta/RNG/
queues/projects" โจทย์ของ wave นี้ระบุตรงๆ ว่าต้อง extend save format ให้
ครอบ queues/routes/signal state/incident state และ extend save round-trip
test ให้พิสูจน์ด้วย ทุกโมดูล G4 (routing/queues/crossings/signals/buses/
roadworks/gateways/incidents) ที่ทำใน commit ก่อนหน้าล้วนเป็น standalone
module ที่ยังไม่ได้ต่อเข้า `WorldState`

## การตัดสินใจ

### Exposure pattern เดียวกับ Clock/RandomStreams ไม่ใช่แบบ MoneyLedger

`WorldState` เพิ่ม property สาธารณะ: `LinkQueues` (mutable object, เรียก
`.Step` ตรงได้เลย), `GatewayFlows` (dictionary อ่านได้ ตัว `GatewayFlow`
แต่ละตัว mutable เรียก `.Step()`/`.Close()`/`.Open()`/`.Generate*Demand()`
ตรงได้), `RoadWorksZones`/`BusRoutes`/`Signals`/`IncidentSites` (list
อ่านได้ + `Add*` method) — **ไม่ผ่าน `PlanningEngine`** ต่างจาก
`MoneyLedger.Reserve`/`ChargeFromReservation` ที่ถูกจำกัดด้วย `internal`
เพราะ subsystem เหล่านี้ไม่มี "budget/transactional" concern แบบเดียวกับ
เงิน (ไม่มีความเสี่ยง double-charge) — เหตุผลเดียวกับที่ `Clock`/
`RandomStreams` เปิดเป็น public อยู่แล้ว

### Gateway capacity มาจาก MapPack.Gateway จริง ไม่ใช่ hardcode

`WorldState`'s constructor ปกติสร้าง `GatewayFlow` หนึ่งตัวต่อ
`RoadGraph.Gateways` entry จริง โดยแปลง
`Gateway.ExternalCapacityVehPerHour` (ซึ่งมี `Namespace ==
"simulation_assumption"` เสมอตาม `Gateway`'s doc comment) เป็น per-tick
ผ่าน `TickConfig.TicksPerSecond` — ไม่มี gateway ไหนถูกสร้างจาก
constant ตายตัว `SimulationFixtures.BuildRoadGraph()` เดิม (G3) ไม่มี
gateway เลย (`new List<Gateway>()`) จึงไม่กระทบ test เดิมแม้แต่ตัวเดียว —
`MobilitySaveRoundTripTests` มี fixture ใหม่
(`BuildRoadGraphWithGateway`) ที่เพิ่ม gateway จริงเข้าไปเพื่อทดสอบ
เส้นทางนี้โดยเฉพาะ

### ComputeStructuralHash และ CaptureSave ขยายพร้อมกันเสมอ

ทุก field ใหม่ใน `GatewayFlow`/`LinkQueueSimulator`/`RoadWorksZone`/
`BusRoute`+ridership/`SignalInstance`/`IncidentSite` ถูกเพิ่มเข้า
`ComputeStructuralHash` และ `CaptureSave`/restore **พร้อมกันในคอมมิตเดียว**
(ไม่ใช่ add save ก่อนแล้วค่อยเพิ่ม hash ทีหลัง) เพราะถ้า hash ไม่ครอบ
state ใหม่ two-worlds-same-commands test จะ "ผ่านหลอก" แม้ mobility
state ต่างกันจริง — `TwoWorlds_SameSeedSameMobilityCommands_ProduceIdenticalStructuralHash`
พิสูจน์ตรงนี้โดยตรง

### Save round-trip พิสูจน์ทีละ field ไม่ใช่แค่ hash เดียว

`MobilitySaveRoundTripTests.AllG4MobilityState_SurvivesASaveRestoreRoundTrip_FieldByField`
ตั้งใจ assert ทีละ field ของทุก subsystem (ไม่ใช่แค่เทียบ
`ComputeStructuralHash` ท้ายสุด ซึ่งอาจซ่อน bug ถ้า hash function เอง
เขียนผิดสมมาตรกัน) และตั้งใจ capture ตอน incident site อยู่กลาง
`Warning` phase (ไม่ใช่ `Idle` ที่ trivial) กับ gateway ที่ปิดอยู่และมี
backlog ค้าง (ไม่ใช่ gateway ว่างเปล่า) — เพื่อพิสูจน์ว่า "state
กึ่งกลาง" รอดจริง ไม่ใช่แค่ state เริ่มต้น

## ผลที่ตามมา

- Test suite รวม G4: **158 passed** (จาก 113 เดิม + 2 Task Zero + G4 43
  test ใหม่)
- ยังไม่มี gameplay loop ใดเรียก `WorldState.LinkQueues`/`GatewayFlows`/
  `Signals`/`IncidentSites` โดยอัตโนมัติทุก tick (`SimulateTick` ยังทำแค่
  clock + Traffic RNG draw เหมือน G3) — การต่อ OD demand generation จริง
  เข้ากับ tick loop อัตโนมัติเป็นงาน deferred (บันทึกใน progress.md)
  สิ่งที่ session นี้พิสูจน์คือ state เหล่านี้ **persist ถูกต้อง** และ
  **participate ใน determinism hash ถูกต้อง** เมื่อถูกเรียกใช้ตรงๆ
  (โดย gameplay code หรือ test) ไม่ใช่ว่าระบบขับเคลื่อนตัวเองอัตโนมัติ
  แล้วในรอบนี้
