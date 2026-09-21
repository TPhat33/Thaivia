# ADR-0023: G4 tick-loop integration (Task Zero), road works เป็น committable project

- สถานะ: Accepted
- วันที่: 2026-09-21
- ผู้เกี่ยวข้อง: wave 6 / g5 supervisor brief, Task Zero

## บริบท

Wave 5 (G4) implement ทุก mobility primitive เสร็จและ test แยกทีละตัว
ครบ แต่ `WorldState.SimulateTick()` ยังทำแค่ `Clock.AdvanceTicks(1)` +
draw หนึ่งค่าจาก Traffic stream — ไม่มี gameplay loop ไหนเรียก demand
generation -> assignment -> queue step -> signal step -> bus step ->
gateway step -> incident evaluation โดยอัตโนมัติทุก tick เลย (บันทึกไว้
ตรงๆ ใน `docs/progress.md` Session 5 ว่าเป็น `G4-11` ที่ deferred โดย
เจตนา) supervisor (Opus) สั่งให้ปิด gap นี้เป็นงานแรกของ wave นี้ ก่อน
งาน G5-track อื่นทั้งหมด

## การตัดสินใจ

### 1. ลำดับต่อ tick

`WorldState.SimulateTick()` รันตามลำดับนี้เสมอ (คงที่ ไม่สลับ):

1. Advance clock 1 tick; derive hour-of-day จาก tick ปัจจุบัน
   (`ComputeHourOfDay`, `secondsElapsed / 3600 % 24`)
2. สร้าง `MobilityGraph(Vehicle)` ใหม่ทุก tick จาก road_graph จริง +
   `PlannedRoadSegment` ที่ commit แล้วทั้งหมด — ถนนใหม่ที่ commit มีผล
   ตั้งแต่ tick ถัดไปทันที ไม่ต้องรอ "release" แยก
3. `TripDemandGenerator.GenerateCommuteBatches` จาก cohort population x
   activity clock ของชั่วโมงนั้น
4. `NetworkDemandAssignment.AssignToWays` (all-or-nothing, ดูหัวข้อ
   "ข้อจำกัดที่ยังไม่แก้" ด้านล่าง)
5. `LinkQueueSimulator.Step` ต่อทุก way ที่มี arrivals หรือ backlog ค้าง
   (capacity จาก `LinkCapacity.EffectiveCapacityVehPerTick` ซึ่งรวม
   `RoadWorksZone` ที่ active อยู่แล้ว)
6. `SignalInstance.Step` ต่อทุก signal ที่ลงทะเบียนไว้ — arrivals ของสอง
   approach มาจาก arrivals-by-way ของ tick นี้จริง แบ่งด้วย topology
   split จริง (ดูข้อ 2 ด้านล่าง) ไม่ใช่ 50/50 ที่แต่งขึ้น
7. Bus route step: ridership demand จาก cohort ที่อยู่ในระยะเดินจริง
   (Walk-mode `MobilityGraph`) ของ stop ใดก็ได้บน route, capped ด้วย
   fleet throughput จริง (`BusRouteScheduler`/`BusRidership`)
8. Gateway step: generate demand จาก `Gateway.InboundDemandVehPerHour`/
   `OutboundDemandVehPerHour` ของ MapPack จริง (แปลงเป็น per-tick ด้วย
   `VehPerHourToPerTick` ตัวเดียวกับที่ใช้แปลง capacity อยู่แล้วตอน
   construct) แล้ว `GatewayFlow.Step()` ทุกตัว
9. Incident evaluation: ต่อทุก `IncidentSite` ที่ลงทะเบียนไว้ คำนวณ risk
   จาก (hour, noise ที่ node นั้นจริง, congestion ratio ที่ node นั้นจริง)
   แล้วเรียก `IncidentEngine.Step` (RNG stream `Incidents` ถูก draw
   เฉพาะตอน severity เท่านั้น เหมือนเดิมทุกประการ ไม่กระทบ trigger)

เหตุผลของลำดับนี้: demand -> assignment -> queue step ต้องมาก่อนเสมอ
เพราะ signal/incident step ของ tick เดียวกันต้องการ arrivals/congestion
ที่ tick นั้น**จริง** ไม่ใช่ของ tick ก่อนหน้า Bus/gateway ไม่พึ่ง queue
state โดยตรง วางไว้หลัง queue step เพื่อให้ทุก "ผลลัพธ์เครือข่าย" ของ
tick นี้นิ่งก่อนที่จะประเมิน incident เป็นขั้นสุดท้าย (incident ต้องเห็น
ภาพ congestion ที่สมบูรณ์ที่สุดของ tick นั้น)

**Step "cohort needs/noise update"**: ตัดสินใจไม่ทำเป็น cache ที่ push
ทุก tick — `ComputeCohortNeeds`/`ComputeNoiseIndexAt`/`ComputeSafetyScore`
เป็น pure function ของ state ที่ SimulateTick อัปเดตอยู่แล้ว (cohorts,
buildings, incident sites, hour) การเก็บ cache ซ้ำจะเพิ่มพื้นที่
save/hash โดยไม่จำเป็น (ค่าที่ derive ได้เป๊ะจาก state ที่ hash อยู่แล้ว
ไม่ต้องมี state เพิ่มเพื่อความ determinism) ผลคือ caller ที่ query หลัง
`SimulateTick()` เห็นเงื่อนไขของ tick นั้นเสมอ (pull ไม่ใช่ push)

### 2. Signal approach split มาจาก topology จริง ไม่ใช่สมมติ

`SplitSignalApproachWays(nodeId)` เรียง way id ที่ incident กับ node นั้น
(จาก `RoadGraph.Edges` จริง) แล้วสลับ A/B (index คู่ -> A, คี่ -> B)
Deterministic และมาจาก topology จริง — ไม่ใช่ 50/50 คงที่ที่ไม่สนใจว่า
node นั้นมีกี่ทางจริง node ที่มีทางเดียว (dead end) จะได้ A=ทางนั้น,
B=ว่าง ซึ่งเป็นคำตอบที่ถูกต้องสำหรับ node แบบนั้น ไม่ใช่บั๊ก

### 3. Gateway demand มาจาก MapPack field ที่มีอยู่แล้ว

`Gateway.InboundDemandVehPerHour`/`OutboundDemandVehPerHour` มีอยู่แล้ว
ในสคีมา (เป็น `simulation_assumption` เสมอ ตามที่ `Gateway`'s doc comment
ระบุไว้) แต่ไม่เคยถูกใช้จริงจนถึง wave นี้ — SimulateTick อ่านค่านี้ตรงๆ
แล้วแปลงเป็น per-tick ด้วย conversion เดียวกับที่ constructor ใช้แปลง
capacity ไม่ได้ประดิษฐ์ตัวเลขใหม่จาก population/jobs อย่างที่พิจารณาไว้
ตอนแรก (ประหยัด complexity และตรงกับ field ที่มีอยู่แล้วมากกว่า)

### 4. Cohort Safety ผูกกับ incident จริง

G3 ปล่อย `Safety` เป็น baseline คงที่ (70) เพราะไม่มีระบบ incident
`WorldState.ComputeSafetyScore(homeRoadNodeId)` ตอนนี้คำนวณจาก incident
site ที่ `Phase == Active` จริง ที่ reachable (network distance) จาก home
node ภายใน `SafetyInfluenceRadiusMeters` (500m, simulation_assumption)
decay เชิงเส้นตามระยะ x severity ของ incident นั้น เอาค่า**แย่ที่สุด**
(ไม่ sum) เพื่อไม่ให้ incident เล็กๆ หลายอันไกลๆ รวมกันจนดัน safety เป็น 0
อย่างไม่สมเหตุผล ถ้าโลกนั้นไม่มี incident site ที่ลงทะเบียนเลย จะ fallback
เป็น baseline เดิม (ไม่ใช่ 0 หรือค่าที่ดูเหมือนวัดได้) — พฤติกรรม G3 เดิม
ยังคงอยู่สำหรับ scenario ที่ไม่ seed incident

Convention ใหม่ที่เพิ่ม: `IncidentSite.SiteId` เมื่อใช้ประเมินสดต่อ tick
ต้องเป็น string ของ road-graph node id ที่มีอยู่จริง (parse +
`_nodesById.ContainsKey`) — `IncidentSite` เองยังคง "caller-defined,
ไม่ตีความ" ตามเดิม (ดู doc comment เดิม) WorldState เป็นผู้กำหนด
ความหมายนี้ในฐานะ caller เท่านั้น ไม่ใช่การแก้ type

### 5. Road works เป็น committable project ผ่าน PlanningEngine

`ProjectKind.RoadWorks` ใหม่ + `RoadWorksDraft` + `PlanningEngine.
CommitRoadWorks`/`EstimateRoadWorksCapacityImpact` ใช้ discipline เดียวกับ
`BuildingRelocation`/`NewRoadConnector` ทุกประการ: idempotent re-confirm,
validate ทั้งหมดก่อน mutate (way ต้องมีจริง, duration > 0, multiplier ใน
(0,1], milestone รวมเท่า fixed cost, งบพอ), แล้วค่อย reserve + register
zone + register project + bump revision ในขั้นเดียว ไม่มี partial state

**กติกา cancellation เฉพาะของ RoadWorks**: `PlanningEngine.CancelProject`
เรียก `WorldState.TruncateActiveRoadWorksZone(projectId, currentTick)`
เพิ่มเติมจาก logic เดิม (release reservation + charge fee) —ตัด
`DurationTicks` ของ zone ให้จบที่ tick ปัจจุบัน ผล: tick ในอนาคตหลัง
cancel ไม่ถูก degrade อีกต่อไป แต่ tick ในอดีตก่อน cancel (backlog ที่
เกิดไปแล้วใน `LinkQueueSimulator`) ไม่ถูกเขียนทับ — history จริงไม่ถูก
แก้ย้อนหลัง ถ้า zone จบไปตามธรรมชาติแล้วก่อน cancel จะไม่ถูกแตะเลย
(no-op)

`WorldState.AddRoadWorksZone` (direct mutation เดิม) ยังอยู่ — เปลี่ยน
doc comment ให้ตรงกับความจริงใหม่: มันคือ escape hatch สำหรับ
test/manual setup ที่ตั้งใจข้ามระบบเงิน ไม่ใช่ทางเดียวอีกต่อไป

## ข้อจำกัดที่ยังไม่แก้ (บันทึกตรงๆ ตามที่ supervisor สั่ง)

### All-or-nothing assignment บิดเบือนผลจริงเมื่อมีเส้นทางสำรอง

`NetworkDemandAssignment` ยังเป็น all-or-nothing (ADR-0022) —
`AllOrNothingAssignmentDistortionTests` (ใหม่ใน wave นี้) พิสูจน์ด้วย
fixture ที่มีสองเส้นทางจริง (เส้นตรง 200m capacity ต่ำ vs อ้อม ~360m
capacity สูงกว่ามาก) ว่า **100% ของ demand ค้างอยู่บนเส้นทางสั้น/
capacity ต่ำตลอด 200 tick แม้ backlog จะโตไม่มีที่สิ้นสุด** ในขณะที่
เส้นทางอ้อมซึ่งมี capacity เหลือเฟือไม่เคยได้รับ arrival แม้แต่คันเดียว
— นี่คือการบิดเบือนจริง วัดได้จริง ไม่ใช่ edge case หายาก เกิดขึ้นทุกที่
ที่เครือข่ายมีมากกว่าหนึ่งเส้นทางและเส้นหนึ่งสั้นกว่าอีกเส้นแม้เพียง
เล็กน้อย ผลคือ **ตัวเลข queue/congestion ที่ integrated loop รายงานตอนนี้
เป็น upper-bound ของความคับคั่งจริง ไม่ใช่ค่าประมาณการณ์ของระบบที่มี
driver ฉลาดพอจะเปลี่ยนเส้นทางเมื่อติด** — ผลกระทบนี้จะยิ่งชัดขึ้นเมื่อ
pilot AOI จริงมีโครงข่ายถนนหนาแน่นกว่า fixture สังเคราะห์นี้มาก (มีเส้น
ทางเลือกมากกว่า) แก้ด้วย capacity-aware iterative assignment (เช่น
incremental/successive-average assignment) เป็นงานที่ dependency-ready
แล้วสำหรับ session ถัดไป ไม่ติด blocker ภายนอกใดๆ

## ผลกระทบ

- `WorldState.SimulateTick()` เปลี่ยนพฤติกรรมโดยสิ้นเชิง (จาก stub เป็น
  loop จริง) — `ComputeStructuralHash()` ไม่ต้องแก้เพิ่ม เพราะ state ใหม่
  ที่ SimulateTick แก้ (queues/signals/gateways/incidents) ถูก hash ไว้
  แล้วตั้งแต่ ADR-0021 (G4 WorldState wiring) — Task Zero ไม่ได้เพิ่ม
  field ที่ persist ใหม่เลย มีแต่ทำให้ field เดิมมีค่าจริงระหว่างเล่น
- `CohortNeedsCalculator.Compute` มี overload ใหม่ (4 args พร้อม safety)
  overload เดิม (3 args) ยังอยู่ ไม่มี test เดิมอ้างอิง `BaselineSafety`
  โดยตรงมาก่อน (ตรวจแล้ว) จึงไม่ทำให้ test เดิมพัง
- Test ใหม่: `IntegratedTickLoopTests` (7), `RoadWorksProjectTests` (13),
  `AllOrNothingAssignmentDistortionTests` (1) — รวม 21 test ใหม่ ไม่มี
  test เดิมถูกแก้ให้อ่อนลงแม้แต่ตัวเดียว (162 เดิมผ่านหมดโดยไม่แตะ)
