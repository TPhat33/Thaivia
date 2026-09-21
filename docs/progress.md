# Progress log

## Session 6 — 2026-09-21 (wave 6 / G4 tick-loop integration + G5-track work)

### Task Zero (ปิด gap ที่ supervisor ระบุก่อนงานอื่นทั้งหมด)

`WorldState.SimulateTick()` เดิม (wave 5) ทำแค่ `Clock.AdvanceTicks(1)` +
draw หนึ่งค่าจาก Traffic stream — ทุก G4 primitive implement+test แยก
เสร็จแล้วแต่ไม่มีอะไรเรียกมันโดยอัตโนมัติต่อ tick เลย (G4 เป็น library
ไม่ใช่ simulation ที่รันจริง) แก้แล้ว: `SimulateTick()` รันลำดับจริงตาม
ADR-0023 ทุก tick: hour-of-day จาก clock -> `MobilityGraph`(Vehicle) สด
-> OD demand batches จาก cohort activity clock -> all-or-nothing
assignment -> link queue step -> signal step (arrivals แบ่งตาม topology
จริงของ node) -> bus step (ridership จาก walking-reach จริง) -> gateway
step (demand จาก `Gateway.InboundDemandVehPerHour`/
`OutboundDemandVehPerHour` ของ MapPack จริง) -> incident evaluation
(risk จาก hour/noise/congestion จริงที่ node นั้น) ลำดับและเหตุผลเต็ม
อยู่ใน ADR-0023 และ `WorldState.SimulateTick`'s doc comment เอง

**Determinism คงอยู่กับ loop ที่ run จริง**: ไม่ต้องแก้
`ComputeStructuralHash()` เพิ่มเลย — state ที่ SimulateTick ทำให้มีค่า
จริง (queues/gateways/signals/incidents) ถูก hash ไว้แล้วตั้งแต่ ADR-0021
(G4 WorldState wiring, wave 5) Task Zero แค่ทำให้ field เดิมมีการ
เปลี่ยนแปลงจริงระหว่างเล่น ไม่ได้เพิ่ม persisted field ใหม่เลยสักตัว
`IntegratedTickLoopTests.TwoWorlds_SameSeedSameIntegratedTickRun_ProduceIdenticalStructuralHash`
พิสูจน์ด้วย fixture ที่เชื่อมต่อจริง (`IntegratedTickFixtures`, สี่แยก
จริงพร้อม lanes tag, gateway ที่มี demand จริง, signal/bus/incident
ลงทะเบียนไว้) รัน 5 ชั่วโมงเกมพร้อม commit road-works project กลางทาง
แล้ว hash ตรงกันเป๊ะระหว่างสองโลก และต่างกันเมื่อ seed ต่าง (control)

**Gateway conservation กับ loop ที่ run จริง**:
`GatewayConservation_HoldsAcrossThousandsOfTicks_WithTheFullLoopRunning_AndACloseReopenMidRun`
รัน 5,000 tick เต็ม loop (ไม่ใช่ unit test แยก) ปิด gateway ที่ tick 1500
เปิดใหม่ที่ tick 3200 assert `IsConserved` ทุก tick — ผลจริงที่วัดได้:
`Generated=25000, Completed=6600, Queued=18400` ทั้งขาเข้า/ออก
(`Generated == Completed + Queued` เป๊ะ)

**Incident rate ยัง bound ภายใต้เงื่อนไขจริง**:
`IncidentRate_StaysBoundedUnderLiveConditions_AcrossMultipleFullDays` รัน
3 วันเกมเต็ม (1,296,000 tick) ด้วย hour/noise/congestion ที่มาจาก loop
จริง (ไม่ใช่ risk=1.0 สังเคราะห์แบบ IncidentEngineTests เดิม) —
NightDisorder เกิดจริง 3,057 ครั้ง (hard max ตามทฤษฎี 12,343), StreetRacing
4,749 ครั้ง (hard max 14,401) ทั้งสองต่ำกว่า theoretical max มาก ไม่ใช่
เบียดขอบ

**Save round-trip ครอบ live state**:
`SaveThenRestore_ThenContinueTheIntegratedLoop_ProducesTheIdenticalHashAsNeverHavingSaved`
save ที่ 2 ชั่วโมงเกม, restore, เดินต่ออีก 3 ชั่วโมงเกม เทียบกับโลกที่ไม่
เคย save เลย — hash ตรงกันเป๊ะ (sanity check ยืนยันว่า run นี้มี live state
จริงไม่ใช่ trivial-empty)

**Estimate ยัง non-mutating หลัง loop run จริง**:
`EstimateStaysNonMutating_AfterTheIntegratedLoopHasProducedRealLiveMobilityState`
รัน loop 2 ชั่วโมงเกมให้มี queue/gateway backlog/signal state/incident
จริงก่อน แล้วเรียก Estimate ทั้งสามตัว (relocation/new-road/road-works)
assert hash+RNG state/draw count+cash ไม่เปลี่ยนแม้แต่บิตเดียว

**Cohort Safety ผูกกับ incident จริงแล้ว** (ปิด gap ที่ G3 บันทึกไว้ตรงๆ
ว่า "baseline คงที่ 70 เพราะยังไม่มีระบบ incident"):
`WorldState.ComputeSafetyScore` คำนวณจาก incident site ที่ Active จริง
ที่ reachable จาก home node ภายใน 500m (decay เชิงเส้น x severity, เอา
ค่าแย่ที่สุดไม่ใช่ sum) `CohortSafety_DropsBelowBaseline_...` พิสูจน์:
Safety วัดได้ 61 (ต่ำกว่า baseline 70) เมื่อมี incident Active จริงหนึ่ง
hop จากบ้าน — ถ้า wiring ไม่ทำงานจริง test นี้จะ fail

**บั๊ก/ข้อจำกัดจริงที่พบและบันทึกตรงๆ**: `NetworkDemandAssignment` ยัง
all-or-nothing (ADR-0022, wave 5) — `AllOrNothingAssignmentDistortionTests`
(ใหม่) พิสูจน์ด้วย fixture สองเส้นทางจริงว่า **นี่คือการบิดเบือนผลจริง
วัดได้จริง ไม่ใช่ edge case ที่ไม่สำคัญ**: 100% ของ demand ค้างอยู่บน
เส้นทางสั้น/capacity ต่ำตลอด 200 tick (backlog โต 800 คัน) ในขณะที่
เส้นทางอ้อม capacity สูงกว่าไม่เคยได้รับ arrival เลยแม้จะเหลือ capacity
มาก รายละเอียดเต็มและข้อเสนอแก้ (capacity-aware iterative assignment)
อยู่ใน ADR-0023

### Road works เป็น committable project (ปิด gap ที่สองที่ wave 5 deferred)

`ProjectKind.RoadWorks` + `RoadWorksDraft` + `PlanningEngine.
CommitRoadWorks`/`EstimateRoadWorksCapacityImpact` ใช้ discipline เดียวกับ
โครงการอื่นทุกประการ (reserve -> pay milestone -> cancel, idempotent
re-confirm, rollback เต็มเมื่อ validation ล้มเหลว) กติกาเฉพาะของ
RoadWorks: cancel กลางทางจะ truncate zone ให้จบที่ tick ปัจจุบัน (อนาคต
ไม่ถูก degrade อีก) แต่ไม่แก้ประวัติ tick ที่ผ่านไปแล้ว —
`RoadWorksProjectTests` (13 test) ครอบ: reserve ถูกจำนวน, idempotent
re-confirm, rollback เมื่องบไม่พอ/way ไม่มีจริง/multiplier นอกช่วง/
milestone ไม่ครบ, no-double-charge, และสอง cancellation-truncation test
(ก่อน/หลัง cancel ต้องต่างกันเฉพาะ tick ในอนาคต, zone ที่จบไปแล้วตาม
ธรรมชาติต้องไม่ถูกแตะ) `WorldState.AddRoadWorksZone` เดิมยังอยู่เป็น
direct-mutation escape hatch สำหรับ test/manual setup เท่านั้น (doc
comment อัปเดตให้ตรงความจริงใหม่)

### ผลการทดสอบ (Task Zero)

`dotnet build game/Thaivia.sln` → **0 warnings, 0 errors**
(`docs/evidence/g5-task0-dotnet-build.log`) `dotnet test
game/Thaivia.sln` → **183 passed** (162 เดิม + 21 ใหม่ — 7
IntegratedTickLoopTests + 13 RoadWorksProjectTests + 1
AllOrNothingAssignmentDistortionTests — ไม่มี test เดิมพังเลยสักตัว,
`docs/evidence/g5-task0-dotnet-test.log`) `./.venv/bin/pytest -q` ยัง
**67 passed** เหมือนเดิม (`docs/evidence/g5-task0-pytest.log`)

### G5-track (หลังจาก Task Zero) — งานที่ทำได้จริงโดยไม่ต้องมี Unity

Plan §16's G5 gate ต้องการ complete playable scenario + art/UI + tutorial
+ before/after + device testing — ส่วน art/UI/tutorial-UI/device ยัง
`blocked` เหมือนเดิม (ไม่มี Unity Editor/Android/iOS device) ส่วนที่เป็น
C# จริงและวัดผลได้ทำครบ 5 หัวข้อ (`G5-01`..`G5-05` ใน `TASKS.json`,
`G5-06` = ส่วน Unity/device ที่ยัง blocked):

**1. Performance budget วัดจริง (spec §15)**:
`PerformanceBenchmarkTests` (harness เป็น `dotnet test`, เหตุผลเลือกไว้
ใน ADR-0024) วัด `SimulateTick` จริง 9,000 tick (30 นาทีเกมที่ 5Hz) บน
grid สังเคราะห์ 8x8 (64 node, 32 อาคาร) — **ระบุตรงๆ ว่าไม่ใช่ pilot AOI
จริง** (ยังไม่มีข้อมูล OSM จริง ADR-0003) ผลจริง (Release build):
**p50 ~5.2-5.7ms, p95 ~6.1-6.5ms, max ~36-41ms, mean ~5.2-5.7ms — MISS
budget 5ms p95 ~24-30%** รายงานตรงๆ ไม่ปิดบัง ไม่ลด scenario จนผ่าน
วินิจฉัยสาเหตุจริง (Dijkstra ซ้ำไม่ cache ใน demand/bus-reach) และแก้
บางส่วนจริง (`WorldState` cache `MobilityGraph` ต่อ tick, invalidate
ด้วยจำนวน `PlannedRoadSegment`) — ช่วยได้แต่ไม่ปิด gap (p95 6.5->6.1ms)
รายละเอียดเต็มใน ADR-0024 พร้อมข้อเสนอ optimization ต่อที่ dependency-
ready สำหรับ session ถัดไป

**2. Trade-off จริง วัดสองฝั่ง**: `BudgetConstrainedTradeOffTests`
สร้างสถานการณ์ที่ต่างจาก G3's two-solutions gate โดยเจตนา (gate นั้น
baseline=0 เพราะ disconnected ทั้งคู่ ทำให้ "สองทางแก้" แค่คืน
connectivity ไม่ใช่ทางเลือกจริง) — ที่นี่สอง cohort ต่างกันต้องการถนน
ใหม่คนละเส้น งบเริ่มต้นพอสำหรับ**แค่เส้นเดียว** วัดจริง: commit ถนนของ
cohort A -> Access **0->40**, Economy **0->5** (cohort A) ในขณะที่ cohort
B ยังคง Access=0/Economy=0 เป๊ะ (ไม่ถูกแตะ) และพยายาม commit ถนนของ B
-> **fail จริง** ("insufficient available budget: need 250000, have
100000") สลับกัน (commit B ก่อน) ให้ผลสมมาตรตรงข้าม พร้อม control case
งบพอทั้งคู่ที่ไม่มีใครถูก foreclose

**3. Before/after เป็น first-class feature**:
`Thaivia.Core.Simulation.Reporting.BeforeAfterComparison.RebuildBaseline`
สร้างโลก baseline (zero-project) ใหม่จาก reference เดิมทุกตัว
(GeographyBase/SimulationInitialization/RoadGraph/ScenarioConfig) ได้ทุก
เมื่อในเซสชัน ไม่ต้อง snapshot ไว้ล่วงหน้า พิสูจน์ด้วย
`RebuildBaseline_IsAvailableAtAnyPoint_...`: rebuild ตอนเริ่มกับ rebuild
หลัง 40 tick + commit relocation แล้ว ได้ `ComputeStructuralHash()`
เท่ากันเป๊ะ และ `GeographyBase` เป็น object เดียวกัน (`Assert.Same`)
ตลอด

**4. Explainable predictions (spec §12)**: `ExplainedImpact`/
`ReasoningInput` ใหม่ (ไม่แก้ signature `Estimate*` เดิมเลย เพิ่ม
`Explain*` คู่ขนาน) ให้ prediction พก uncertainty (ของเดิม) **บวก**
reasoning input ที่คำนวณมันจริง (เช่น `projected_network_distance_to_
nearest_job`, `accessibility_reference_distance`) พิสูจน์ว่า reasoning
สอดคล้องกับตัวเลขจริง (`projected_score - current_score == ExpectedValue`
เป๊ะ) ไม่ใช่ข้อความแต่งทีหลัง `FixedCostThb` ยังแยกจาก prediction เสมอ
(คนละ field/type)

**5. Tutorial/scenario progression เป็น data+logic**:
`Thaivia.Core.Simulation.Progression` (`ObjectiveDefinition`,
`IObjectiveCheck`, `ScenarioProgression`) — objective ที่ยัง `Locked`
**ไม่ถูกเรียก check เลย** พิสูจน์ด้วย spy check ที่ throw ถ้าถูกเรียก
(`Evaluate_NeverEvaluatesALockedObjectivesCheck_SpyControlTest`) ขับ
objective ให้ผ่านด้วย `PlanningEngine.CommitRelocation` จริง (ตัวเลข
0->98 เดียวกับ G3's two-solutions gate) ไม่ mock world state

### ผลการทดสอบสุดท้าย (ทั้ง session)

`dotnet build game/Thaivia.sln` → **0 warnings, 0 errors**
(`docs/evidence/g5-final-dotnet-build.log`) `dotnet test
game/Thaivia.sln` → **202 passed** (162 ก่อน session นี้ + 40 ใหม่: 21
Task Zero + 1 benchmark + 3 trade-off + 3 before/after + 6 explainable +
6 progression, ไม่มี test เดิมพังเลยสักตัวตลอด session —
`docs/evidence/g5-final-dotnet-test.log`) `./.venv/bin/pytest -q` ยัง
**67 passed** เหมือนเดิม (`docs/evidence/g5-final-pytest.log`)

### สามทางแบ่งงาน (compiled+tested / written-but-not_run / deferred)

**Compiled + tested (dotnet test ผ่านจริง)**: ทุกอย่างข้างบนทั้งหมด
(Task Zero ทั้งสองส่วน + G5-01..G5-05 ทั้งห้า) — 40 test ใหม่ ทุกตัวรัน
จริงและผ่านจริง

**Written-but-not_run**: ไม่มีในรอบนี้ (session นี้ไม่ได้แตะ
`game/Assets/Scripts/Runtime/` เลย โค้ด Unity เดิมจาก wave 3 ยังอยู่ใน
สถานะเดิม uncompiled)

**Deferred (บันทึกเหตุผลตรงๆ)**:
- **`G5-06`** (Unity-side art/UI/tutorial-UI/before-after-view UI +
  device testing) — `blocked` เหมือนทุก wave (ADR-0002) ไม่มี Unity
  Editor/Android/iOS ในสภาพแวดล้อมนี้
- **Performance budget ยัง MISS** — บันทึกไว้ตรงๆ ข้างบน ไม่ใช่
  deferred แบบเงียบๆ แต่เป็น honest measured result พร้อม root-cause
  diagnosis และ optimization ต่อที่ dependency-ready
- **`NetworkDemandAssignment` all-or-nothing ยังไม่แก้เป็น
  capacity-aware equilibrium** — วัดผลกระทบจริงแล้ว
  (`AllOrNothingAssignmentDistortionTests`, ADR-0023) เป็นการบิดเบือนจริง
  ไม่ใช่ edge case เล็กน้อย dependency-ready สำหรับ session ถัดไป
- **แผนที่จริง** — ยังไม่ได้นำเข้า เหตุผลเดิมทุกประการ (ADR-0003)
  session นี้ไม่แตะ Python pipeline เลย (pytest 67 passed เหมือนเดิม)
- **Unity Editor/Android/iOS/phone/tablet** — ยัง `not_run` ทุกแถว
  เหมือนทุก session ก่อนหน้า (ADR-0002) ไม่เปลี่ยนแปลงใน session นี้

### Blockers และขั้นตอนมนุษย์ที่เล็กที่สุด

เหมือน session ก่อนหน้าทุกประการสำหรับ Unity/Android/iOS/OSM data (ดู
ADR-0002/0003) — session นี้ไม่มี blocker ใหม่ที่ agent เองแก้ไม่ได้

### Next dependency-ready tasks

- Deeper OD-route caching (`TripDemandGenerator`/`NetworkDemandAssignment`/
  `BusRidership`'s repeated Dijkstra calls) เพื่อปิด performance budget
  gap ต่อ — จุดเริ่มระบุไว้ใน ADR-0024
- `NetworkDemandAssignment` แบบ capacity-aware iterative assignment —
  จุดเริ่มระบุไว้ใน ADR-0023
- `G5-06` — ยังรอ Unity Editor/device เหมือนเดิม
- `G1-10` (real pilot audit) — ยัง blocked เหมือนเดิม รอข้อมูล OSM จริง

## Session 5 — 2026-09-21 (wave 5 / G4 mobility, queues, signals, incidents)

### Task Zero (แก้ gap ที่ supervisor ระบุไว้ก่อน G4 ทั้งหมด)

`AccessibilityGraphTests`'s bridge test เดิม (wave 4) พิสูจน์แค่ finite
vs null — network distance ที่คำนวณได้ (32m) บังเอิญเท่ากับ straight-line
distance เพราะ fixture เป็นเส้นตรง (collinear) ทำให้ mutant ที่แทน
Dijkstra ด้วย Euclidean distance + connectivity check จะผ่าน test เดิม
ทั้ง 113 ตัว เพิ่ม `SimulationFixtures.BuildDetourRoadGraph` (ช่องว่าง 2m
เส้นตรง เชื่อมกันจริงแค่ทาง detour ~502m ผ่านสะพาน) และสอง test:
`ConnectedGraphWithGrossDetour_NetworkDistance_MatchesTheDetourAndIsNotStraightLine`
(assert 502m, `Assert.NotEqual` กับ 2m) และ
`ConnectedGraphWithGrossDetour_NaiveEuclideanStandIn_WouldReportAMaterialityDifferentAnswer`
(mutation-style control: คำนวณคำตอบของ naive Euclidean stand-in บน
fixture เดียวกัน แล้ว assert ว่าต่างจากคำตอบจริง >100m — พิสูจน์ว่า
fixture discriminate ได้จริง ไม่ใช่แค่สมมติ) 115 passed (113+2), ไม่มี
test เดิมถูกอ่อนลง (`docs/evidence/g4-task0-dotnet-*.log`)

### Task IDs และไฟล์ที่เปลี่ยน

`G4-00`..`G4-10` เสร็จ (`done`), `G4-11` (auto tick-loop integration +
Unity UI) `blocked`/deferred ตามที่อธิบายด้านล่าง — ดู `TASKS.json`
สำหรับ acceptance criteria/evidence เต็มของแต่ละ task

โมดูลใหม่ทั้งหมดอยู่ใต้ `game/Assets/Scripts/Core/Simulation/Mobility/`
(namespace ใหม่ `Thaivia.Core.Simulation.Mobility.*`, pure C# เหมือนเดิม
ไม่มี `UnityEngine` แม้แต่บรรทัดเดียว — `NoUnityEngineReferenceTests`
สแกนซ้ำทุก `dotnet test` run เหมือนเดิม):

- `Routing/{TravelMode,ModeAccess,MobilityGraph}.cs` — directed
  multimodal graph (Walk/Vehicle/Freight), turn-aware Dijkstra บน state
  space `(node, arrivalWayId)`, ใช้ `RoadGraphIndex.EvaluateTurn` +
  `GameplayTurnPolicy` จริงเป็นครั้งแรก (G3 เขียนไว้ล่วงหน้าแต่ไม่เคยถูก
  consume) Walk mode ไม่ผูก oneway/turn restriction ของรถ
- `Queues/{LinkKey,LinkCapacity,LinkQueueSimulator}.cs` — finite-capacity
  queue ต่อ directed link, congestion เป็นผลลัพธ์ arithmetic ตรงจาก
  capacity เท่านั้น (`queue' = max(0, queue+arrivals-capacity)`) ไม่มี
  "traffic score" ที่ไหนเลย
- `RoadWorks/RoadWorksZone.cs` — ลด effective capacity เฉพาะช่วง
  `[StartTick, StartTick+DurationTicks)`
- `Crossings/PedestrianCrossing.cs` — `AccessibleFlag` เปลี่ยน walking
  route จริง (ผ่าน `MobilityGraph`'s `requireAccessibleCrossings`)
- `Signals/{ISignalPlan,FixedTimeSignalPlan,AdaptiveSignalPlan,
  SignalIntersectionSimulator,SignalInstance,SignalPlanKind,
  SignalApproach}.cs` — fixed ก่อน adaptive (ตามลำดับ spec), adaptive
  ตอบสนอง queue จริง วัด trade-off จริง (ดูหัวข้อ "ผลลัพธ์ trade-off"
  ด้านล่าง)
- `Transit/{BusRoute,BusRouteScheduler,BusRidership}.cs` — schedule จาก
  network distance จริง + dwell, ridership จาก cohort population batch,
  bounded ด้วย fleet capacity
- `Gateways/{TripLedgerSnapshot,GatewayFlow,GatewayNetwork}.cs` —
  integer-exact conservation ledger (`Generated == Completed + Queued`
  เป๊ะ), ปิด gateway ไม่ลบ demand แค่ทำให้ capacity เป็น 0 ชั่วคราว
- `Incidents/{IncidentStrand,IncidentPhase,IncidentConditions,
  IncidentThresholds,IncidentSite,IncidentEngine,IncidentLevers}.cs` —
  สอง strand สมมติ (NightDisorder/StreetRacing), risk function รับแค่
  (hour, noise, congestion) ไม่มีทาง "เห็น" archetype/place identity
  เลยเชิงโครงสร้าง, RNG ใช้แค่สุ่ม severity ไม่เคยกำหนดว่าจะเกิดหรือไม่
- `Demand/{OdBatch,TripDemandGenerator,NetworkDemandAssignment}.cs` — OD
  demand เป็น batch ต่อ cohort (ไม่ใช่ต่อคน), all-or-nothing assignment
  ลง way จริงตาม shortest path
- `game/Thaivia.Core.Tests/Simulation/Mobility/*.cs` (ใหม่ 11 ไฟล์, 49
  test ใหม่ในรอบ G4 หลัก + 2 test ของ Task Zero)
- `docs/decisions/0017..0022-*.md` — ADR ของ routing/capacity model,
  signals/crossings/buses, gateway conservation, incident system,
  WorldState wiring/save extension, OD demand
- `docs/evidence/g4-*.log`

### สิ่งที่ทำงานจริง (ยืนยันด้วยการรันจริง)

`dotnet build game/Thaivia.sln` → **0 warnings, 0 errors**
(`docs/evidence/g4-demand-dotnet-build.log`, ไฟล์ build log ล่าสุดของ
รอบนี้) `dotnet test game/Thaivia.sln` → **162 passed** (113 เดิม + 2
Task Zero + 47 G4 test ใหม่ ไม่มี test เดิมพังเลยสักตัวตลอดทั้ง session
— `docs/evidence/g4-demand-dotnet-test.log`) `./.venv/bin/pytest -q`
(Python pipeline) ยัง **67 passed** เหมือนเดิม (session นี้ไม่แตะ Python
เลย — `docs/evidence/g4-pytest-still-passing.log`)

Invariant ที่โจทย์ระบุ ↔ ชื่อ test ที่พิสูจน์:

| Invariant | Test |
|---|---|
| Turn restriction บังคับ vehicle routing จริง | `MobilityGraphTests.VehicleMode_RespectsTurnRestriction_TakesTheLegalDetour_WhereWalkModeIgnoresItAndGoesDirect` |
| Queue congestion เกิดจาก capacity ไม่ใช่ score | `LinkQueueSimulatorTests.SustainedArrivalsExceedingCapacity_GrowsTheQueueByExactlyTheShortfallPerTick` |
| Accessible crossing เปลี่ยน walking route จริง | `CrossingsTests.InaccessibleCrossing_ShortensGeneralWalkingRoute_ButAccessibleNeedTravellerStillDetours` |
| Adaptive ≠ fixed signal | `SignalsTests.AdaptiveSignal_ImprovesTheBusyApproach_ButMeasurablyWorsensTheLightApproach_ComparedToFixed` |
| Road works กระทบระหว่างสร้างเท่านั้น | `RoadWorksTests.RoadWorks_MeasurablyWorsensQueueingDuringConstruction_ComparedToBeforeAndAfter` |
| Gateway trip conservation (integer-exact) | `GatewayConservationTests.TotalTripsAcrossManyTicks_WithAGatewayCloseAndReopen_ConserveExactly` |
| ...+ leaky-variant control | `GatewayConservationTests.LeakyGatewayVariant_ThatDropsQueuedDemandOnClose_ViolatesConservation_ProvingTheAboveTestBites` |
| Incident rate มี hard bound | `IncidentEngineTests.IncidentRate_IsHardBoundedOverALongRun_NeverPerTickSpawning` |
| Incident เกิดจากเงื่อนไข ไม่ใช่ RNG | `IncidentEngineTests.Triggering_IsDrivenByConditions_NotByTheRngSeed_OnlySeverityDiffersBetweenSeeds` |
| Save round-trip ครอบ G4 state ทั้งหมด | `MobilitySaveRoundTripTests.AllG4MobilityState_SurvivesASaveRestoreRoundTrip_FieldByField` |
| Determinism คงอยู่กับ G4 state | `MobilitySaveRoundTripTests.TwoWorlds_SameSeedSameMobilityCommands_ProduceIdenticalStructuralHash` |
| Ethical constraint (ไม่ผูก archetype กับความเสี่ยง) | `IncidentEngineTests.EthicalControl_NoIncidentsApiTakesABuildingArchetypeParameter` |

### ผลลัพธ์ trade-off ที่วัดจริง (fixed vs adaptive signal)

รัน over-saturated intersection จริง 4000 ticks (arrivals รวม 7/tick >
discharge rate 4/tick ทุก tick ไม่ว่า split ไหน — ตั้งใจให้ทั้งสอง
approach คับคั่งจริง ไม่ใช่โจทย์ปลอดภัยที่ไม่มีอะไรให้ trade-off):

| Metric (average queue length ตลอด run) | Fixed (50/50) | Adaptive |
|---|---|---|
| Approach A (คับคั่งกว่า) | 3991.00 | 3440.37 (**ดีขึ้น** 13.8%) |
| Approach B (เบากว่า) | 2010.50 | 2561.13 (**แย่ลง** 27.4%) |

นี่คือ trade-off จริง ไม่ได้ tune ค่าคงที่จนกว่าจะ "เจอ" ผลที่ดูดี —
ตัวเลขทั้งสองแถวมาจากการรันจริงครั้งเดียวกัน (`docs/evidence/g4-signals-tradeoff-measured.log`)
รายละเอียดเหตุผลที่ทั้งสองฝั่งยังโตไม่มีที่สิ้นสุด (เพราะ intersection
over-saturated ทั้งระบบ) บันทึกไว้ตรงๆ ใน ADR-0018 ไม่ claim เกินจริงว่า
adaptive "แก้ปัญหา"

### Layer separation และ determinism (ตรวจซ้ำ)

โมดูล G4 ทั้งหมดอยู่ใต้ `Thaivia.Core.Simulation.Mobility.*` ไม่แตะ
`Thaivia.Core.MapPack` เลย (อ่านอย่างเดียวผ่าน `RoadGraph`/`RoadEdge`/
`Gateway` ที่มีอยู่แล้ว) `WorldState`'s constructor ปกติสร้าง
`GatewayFlow` จาก `RoadGraph.Gateways` จริง (capacity จาก
`Gateway.ExternalCapacityVehPerHour` ซึ่งเป็น `simulation_assumption`
เสมอตาม field เดิม) — ไม่มี gateway ไหนถูกสร้างจาก constant ลอยๆ
`ComputeStructuralHash` ขยายให้ครอบทุก field ใหม่ **ในคอมมิตเดียวกับ**
ที่เพิ่ม state นั้น (ไม่ใช่ add save ก่อนแล้วเพิ่ม hash ทีหลัง) —
`TwoWorlds_SameSeedSameMobilityCommands_ProduceIdenticalStructuralHash`
พิสูจน์ตรงนี้ ImmutabilityTests/NoUnityEngineReferenceTests/
LayerSeparationTests เดิมจาก wave 3-4 ยังผ่านทั้งหมดโดยไม่ต้องแก้

### บั๊กจริงที่พบและแก้ระหว่าง session นี้ (บันทึกตรงๆ)

`NetworkDemandAssignment.AssignToWays` เวอร์ชันแรก double-count arrivals
เมื่อ way เดียวมีหลาย polyline segment บนเส้นทางเดียวกัน (คาดหวัง 40,
ได้จริง 80) — test จับได้ทันทีตอนรันครั้งแรก แก้ด้วยการ de-duplicate
way_id ต่อ batch ก่อน accumulate (ดู ADR-0022 สำหรับรายละเอียด) — เป็น
ตัวอย่างจริงของ "test ที่เขียนก่อนแล้วจับบั๊กจริง" ไม่ใช่ test ที่เขียน
ตามโค้ดจนผ่านเสมอ

### สิ่งที่ยัง not_run / blocked / deferred (บอกตรงๆ ไม่ลดสโคปเงียบๆ)

- **`G4-11` (auto tick-loop integration + Unity UI)** — `blocked`/deferred
  โดยเจตนา: ทุก primitive ที่ต้องใช้ (`TripDemandGenerator`,
  `NetworkDemandAssignment`, `LinkQueueSimulator`, `SignalInstance`,
  `IncidentEngine`, `IncidentConditions`) implement+test เสร็จและเรียก
  จาก `WorldState` ได้แล้ว (G4-10) แต่ `WorldState.SimulateTick()` ยัง
  ทำแค่ clock + Traffic RNG draw เหมือน G3 เดิม **ไม่มี** gameplay loop
  ไหนเรียก demand generation -> link assignment -> queue step -> signal
  step -> incident risk evaluation โดยอัตโนมัติทุก tick จาก real
  noise/congestion/hour-of-day ยัง — เป็นการตัดสินใจจัดการความเสี่ยง
  (ต่อสายอัตโนมัติแบบเต็มระบบในเวลาจำกัดเสี่ยงบั๊ก integration ที่ตรวจ
  ไม่ทันในรอบนี้) ไม่ใช่ blocker ภายนอก — dependency-ready เต็มที่สำหรับ
  session ถัดไป
- **Road works ยังไม่ผูกกับ `PlanningEngine`'s budget flow** —
  `WorldState.AddRoadWorksZone` เป็น direct mutation ไม่ผ่าน
  reserve/pay-milestone เหมือนโครงการอื่น (ยังไม่มี
  `ProjectKind.RoadWorks`) — capacity degradation mechanism เองทำงาน
  และทดสอบสมบูรณ์แล้ว (G4-04) แค่ยังไม่ต่อกับระบบเงิน
- **Bus/signal/incident ยังไม่มี Unity-side UI** — เหมือนทุก session
  ก่อนหน้า ไม่มี Unity Editor ในสภาพแวดล้อมนี้ (ADR-0002)
- **`NetworkDemandAssignment` เป็น all-or-nothing assignment** —
  ไม่ใช่ capacity-aware equilibrium (ไม่มี rerouting ตาม congestion
  ภายใน pass เดียว) บันทึกไว้ตรงๆ ใน ADR-0022 เป็นข้อจำกัดที่ตั้งใจ
  ไม่ใช่ข้อบกพร่องที่ซ่อนไว้
- **Unity Editor/Android/iOS/phone/tablet** — ยัง `not_run` ทุกแถว
  เหมือนทุก session ก่อนหน้า (ADR-0002) ไม่เปลี่ยนแปลงใน session นี้
- **แผนที่จริง** — ยังไม่ได้นำเข้า เหตุผลเดิมทุกประการ (ADR-0003)
  session นี้ไม่แตะ Python pipeline เลย (pytest ยัง 67 passed เหมือนเดิม
  โดยไม่ได้รันซ้ำเพื่อพิสูจน์อะไรใหม่ นอกจาก "ไม่พัง")

### Blockers และขั้นตอนมนุษย์ที่เล็กที่สุด

เหมือน session ก่อนหน้าทุกประการสำหรับ Unity/Android/iOS/OSM data (ดู
ADR-0002/0003) — session นี้ไม่มี blocker ใหม่ที่ agent เองแก้ไม่ได้
`G4-11` ไม่ใช่ blocker ภายนอก เป็นงานที่เหลือให้ session ถัดไปทำต่อได้
ทันทีโดยไม่ต้องรอมนุษย์

### Next dependency-ready tasks

- `G4-11` — auto tick-loop integration (demand->assignment->queue->
  signal->incident ทุก tick) — dependency-ready ทันที ทุก primitive
  พร้อมแล้ว
- Road works เป็น committable `ProjectKind` ผ่าน `PlanningEngine` —
  dependency-ready ทันที
- `G3-10`/G2-0x — ยังรอ Unity Editor เหมือนเดิม
- `G1-10` (real pilot audit) — ยัง blocked เหมือนเดิม รอข้อมูล OSM จริง

## Session 4 — 2026-09-23 (wave 4 / G3 first-playable simulation core)

### Task IDs และไฟล์ที่เปลี่ยน

ทำ `G3-01` ถึง `G3-09` เสร็จ (สถานะ `done` — ดู `TASKS.json` สำหรับ
acceptance criteria/evidence เต็มของแต่ละ task) `G3-10` (Unity-side
rendering/UI ของระบบใหม่เหล่านี้) ยัง `blocked` ตามที่ scope ของรอบนี้
กำหนดไว้ชัดเจน ("simulation core only, pure C#, zero UnityEngine") —
ไม่ได้แตะ `game/Assets/Scripts/Runtime/` เลยใน session นี้

ไฟล์ใหม่หลัก (ทั้งหมดอยู่ใต้ `game/Assets/Scripts/Core/Simulation/` เว้น
แต่จะระบุไว้):

- `Simulation/Time/{TickConfig,SimClock}.cs` — fixed logical tick (5Hz
  constant เดียว), `AdvanceTicks`-only clock, `Resume()` ไม่มี parameter
  เลย (ไม่มีช่องให้ wall-clock catch-up)
- `Simulation/RandomStreams/{DeterministicRandom,RandomStreamName,
  NamedRandomStreams}.cs` — splitmix64 PRNG, stream ละ object แยกกันจริง
  (Traffic/Incidents/CohortVariation), `HashStep` แยกจาก stream สำหรับ
  deterministic hash ที่ไม่กิน RNG position (ใช้ทำ archetype assignment)
- `Simulation/Archetypes/{BuildingArchetype,HourlyActivity,
  ActivityClockCatalog}.cs` — 8 archetypes, activity clock ต่อชั่วโมง
  จาก peak-window shape (ไม่ใช่ตาราง magic number 8x24)
- `Simulation/Cohorts/{HouseholdCohort,CohortNeeds,
  CohortNeedsCalculator}.cs` — ประชากรเป็น household record,
  `SampleAgentCount` แยกจาก `PopulationCount` โดยโครงสร้าง
- `Simulation/Buildings/{BuildingSimState,VacatedLot}.cs` — simulation
  state ต่ออาคาร, immutable-replace style
- `Simulation/Noise/NoiseIndex.cs`, `Simulation/Accessibility/
  {AccessibilityGraph,AccessibilityNeed,PlannedRoadSegment}.cs` — noise
  0-100 ของเกม, accessibility จาก Dijkstra บน road graph เท่านั้น (ไม่มี
  radius helper เลยในโค้ดเบส)
- `Simulation/Economy/{LedgerAccountKind,MoneyLedger}.cs` — เงิน
  integer ล้วน, reserve/charge แยก operation
- `Simulation/Planning/{ImpactRange,ProjectKind,ProjectDraft,
  ProjectStatus,CommittedProject,PlanningEngine}.cs` — draft/estimate/
  commit/pay-milestone/cancel เต็มรูปแบบ, transactional commit
- `Simulation/Scenario/ScenarioConfig.cs` — scenario config สำหรับ seed
  household/jobs (SimulationInitialization layer, สมมติโดยเจตนา)
- `Simulation/Save/{SaveGame,SavedRecords,SaveSerializer,
  SaveGameStore,LoadResult}.cs` — save format เต็ม, latest/previous,
  map-version-mismatch เป็น result type ชัดเจน
- `WorldState.cs` (แก้ทั้งไฟล์) — ผูกทุกระบบข้างบนเข้าด้วยกัน, เพิ่ม
  `Revision`, `ComputeStructuralHash()`, `CaptureSave`/`Restore`,
  `BuildAccessibilityGraph`, `ComputeAccessibilityScore`,
  `ComputeNoiseIndexAt`, `ComputeCohortNeeds` — ยังคง `AddDelta`/
  `GeographyBase`/`SimulationInitialization` เป็น get-only เหมือนเดิม
  ไม่มี method ไหนเขียนกลับเข้า GeographyBase (ImmutabilityTests เดิม
  ยังผ่านทั้งหมด)
- `Graph/GameplayTurnPolicy.cs` — ADR ที่ wave 3 ติดค้างไว้:
  `TurnDecision.Unsupported` ถูกปฏิบัติเหมือน `Denied` (fail-safe) โดย
  gameplay routing
- `game/Thaivia.Core.Tests/Simulation/*.cs` (ใหม่ 11 ไฟล์),
  `game/Thaivia.Core.Tests/GameplayTurnPolicyTests.cs` — test suite เต็ม
  ของทุกระบบข้างบน (78 test ใหม่)
- `docs/decisions/0011..0016-*.md` — ADR ของ tick rate, PRNG stream
  design, ledger model, save format/versioning, accessibility metric,
  และ ADR ที่ค้างจาก wave 3 (`TurnDecision.Unsupported`)
- `docs/evidence/g3-*.log`

### สิ่งที่ทำงานจริง (ยืนยันด้วยการรันจริง)

`dotnet build game/Thaivia.sln` → **0 warnings, 0 errors**
(`docs/evidence/g3-dotnet-build.log`) `dotnet test game/Thaivia.sln` →
**113 passed** (35 เดิมจาก wave 3 + 78 ใหม่ ไม่มี test เดิมพัง เลยสักตัว
— `docs/evidence/g3-dotnet-test.log`) `./.venv/bin/pytest -q` (Python
pipeline) ยัง **67 passed** เหมือนเดิม (session นี้ไม่แตะโค้ด Python เลย
— `docs/evidence/g3-pytest-still-passing.log`)

Invariant ที่ supervisor ระบุไว้ในโจทย์ ↔ ชื่อ test ที่พิสูจน์:

| Invariant | Test |
|---|---|
| Determinism (seed+commands เดิม → hash เดิม) | `WorldStateDeterminismTests.TwoWorlds_SameSeedSameCommands_ProduceIdenticalStructuralHash` |
| RNG stream independence | `NamedRandomStreamsTests.DrawingFromOneStream_DoesNotShiftAnotherStreamsSequence` |
| ไม่มี wall-clock catch-up | `SimClockTests.PauseThenResume_AddsZeroTicksOnItsOwn` + `SimClockAndWorldStateSource_ContainNoWallClockRead` (พร้อม control test `Detector_FlagsASyntheticWallClockUsage_ControlCase` ที่พิสูจน์ว่า scanner จับได้จริง ไม่ใช่ no-op) |
| Estimate ไม่ mutate world | `PlanningEngineTests.Estimate_DoesNotMutateTheWorld_HashAndEveryRngStreamUnchanged` |
| Confirm ซ้ำไม่หักเงินซ้ำ | `PlanningEngineTests.ConfirmingTheSameDraftTwice_DoesNotDeductBudgetTwice` |
| Transactional commit rollback | `PlanningEngineTests.CommitRelocation_InsufficientBudget_AppliesNothing_TransactionalRollback` + `..._GeometryConflict_..._AppliesNothing_EvenWithSufficientBudget` |
| Population/jobs conserved เมื่อย้ายอาคาร | `PlanningEngineTests.Relocation_ConservesPopulationAndJobs_AndDoesNotMutateGeographyBase` |
| GeographyBase ไม่เปลี่ยนโดย PlayerDelta | test เดียวกันข้างบน (เช็ค `Assert.Same` + เนื้อหา polygon เดิม) |
| Save round-trip identity | `SaveLoadTests.SaveThenRestore_ThenContinue_ProducesTheIdenticalHashAsNeverHavingSaved` |
| Map-version mismatch ถูกปฏิเสธ | `SaveLoadTests.Store_LoadLatest_MapVersionMismatch_IsRejectedExplicitly_NotPartiallyLoaded` |
| Accessibility ตาม graph ไม่ใช่ radius | `AccessibilityGraphTests.StraightLineNearNodes_WithNoConnectingEdge_AreGraphUnreachable` (โหนดห่างกัน 2 เมตรเส้นตรง แต่คนละฝั่ง "คลอง" ไม่มี edge เชื่อม) |

### ผลลัพธ์ two-solutions (G3 gate)

`TwoSolutionsIntegrationTests` เริ่มจากโจทย์ accessibility ที่ seed ไว้
(residential cohort อยู่คนละฝั่งคลองจาก job-bearing building เดียวในโลก
— accessibility score เริ่มต้น **0** จริง ไม่ใช่โจทย์ปลอม) แล้วแก้ด้วย
สองวิธีที่ต่างกันจริง (คนละ `ProjectKind`, ผลต่อ world state ต่างกันจริง
— ดู `BothSolutions_AreMechanicallyDistinct_NotTheSameCommandRenamed`):

- **วิธี 1 — ย้ายอาคารที่อยู่อาศัย** เข้าไปใน cluster ที่เชื่อมกับงานได้:
  accessibility score **0 → 98** (Δ=98)
- **วิธี 2 — สร้างถนนเชื่อมข้ามคลอง** โดยไม่ย้ายอาคารเลย: accessibility
  score **0 → 94** (Δ=94), ยืนยันด้วย `Assert.False(...Relocated)` ว่า
  อาคารไม่ได้ขยับจริง

ตัวเลขจริงพิมพ์ออกทาง test output เสมอ (`docs/evidence/g3-two-solutions-measured.log`)
ไม่ได้ tune ให้ "มีสองวิธี" เป็นจริงแบบปลอมๆ — ทั้งสองใช้
`AccessibilityGraph`/`AccessibilityNeed` ตัวเดียวกับที่ test อื่นทั้งหมด
ในชุดใช้

### Layer separation (AGENTS.md rule 3)

`LayerSeparationTests` (ใหม่) พิสูจน์ด้วย reflection ว่าไม่มี type ใน
`Thaivia.Core.Simulation.*` (namespace ใหม่ทั้งหมดของ session นี้)
assignable ไป/มาจาก type ใน `Thaivia.Core.MapPack` เลยสักตัว, ว่า
`WorldState.GeographyBase`/`SimulationInitialization` เป็น get-only และ
ไม่มี method ชื่อแนว `MergeIntoGeographyBase`/`ApplyToSource`, และว่า
reference ของ `SimulationInitialization` ที่ WorldState ถืออยู่เป็น
instance เดิมตลอด session (`Assert.Same`) `ImmutabilityTests` เดิมจาก
wave 3 ก็ยังผ่านทั้งหมดโดยไม่ต้องแก้ (namespace ใหม่ไม่ได้แตะ
`Thaivia.Core.MapPack` เลย)

### สิ่งที่ยัง not_run / blocked / deferred (บอกตรงๆ ไม่ลดสโคปเงียบๆ)

- **`G3-10` (Unity-side UI ของระบบใหม่)** — `blocked`/not attempted
  โดยเจตนา: โจทย์ของ session นี้ระบุชัดว่า "Do NOT attempt renderer
  work, scenes, or prefabs" — ไม่มีไฟล์ใหม่ใต้
  `game/Assets/Scripts/Runtime/` เลยใน session นี้
- **Turn restrictions ไม่ถูกใช้ใน `AccessibilityGraph`** — G3's
  accessibility metric เป็น network-distance ล้วนๆ ไม่ได้ประเมิน
  `TurnRestrictionRecord`/`GameplayTurnPolicy` เลย (มันคือ scope ของ
  routing ที่แม่นกว่าใน G4) `GameplayTurnPolicy` เขียนไว้ล่วงหน้าให้ G4
  ใช้ได้ทันที ไม่ต้องออกแบบใหม่
- **"New road" command จำกัดแค่เชื่อมสอง node ที่มีอยู่แล้วในกราฟ** —
  ไม่รองรับ polyline ใหม่ที่มี control point อิสระ (2-3 จุด + preview
  ตาม plan §9) ในรอบนี้ — บันทึกไว้ตรงๆ ใน
  `PlannedRoadSegment`/`NewRoadConnectorDraft`'s doc comment ว่าเป็น
  G3 scope decision ที่ deferred ไป G4 ไม่ใช่ข้อจำกัดที่ซ่อนไว้
- **Safety need เป็น baseline คงที่ (70)** — G3 ไม่มี incident/night-
  disorder system (G4 scope ตาม plan §16) `CohortNeedsCalculator`
  บันทึกเหตุผลนี้ไว้ในโค้ดตรงๆ ไม่ใช่ตัวเลขที่ดูเหมือนวัดได้
- **Unity Editor/Android/iOS/phone/tablet** — ยัง `not_run` ทุกแถว
  เหมือนทุก session ก่อนหน้า (ADR-0002) ไม่เปลี่ยนแปลงใน session นี้
- **แผนที่จริง** — ยังไม่ได้นำเข้า เหตุผลเดิมทุกประการ (ADR-0003)
  session นี้ไม่แตะ Python pipeline เลย (`pytest` ยัง 67 passed เหมือน
  เดิมโดยไม่ได้รันซ้ำเพื่อพิสูจน์อะไรใหม่ นอกจาก "ไม่พัง")

### Blockers และขั้นตอนมนุษย์ที่เล็กที่สุด

เหมือน session ก่อนหน้าทุกประการสำหรับ Unity/Android/iOS/OSM data (ดู
ADR-0002/0003) — session นี้ไม่มี blocker ใหม่ที่ agent เองแก้ไม่ได้
ทุกอย่างที่ทำได้โดยไม่ต้องมี Unity/ข้อมูล OSM จริงถูกทำแล้ว

### Next dependency-ready tasks

- `G3-10` — รอ Unity Editor เหมือน `G2-01`..`G2-07`
- G4 mobility work (directed multimodal graph, queues, crossings, buses,
  signals) — dependency-ready แล้ว: `AccessibilityGraph`'s Dijkstra และ
  `GameplayTurnPolicy` เป็นจุดเริ่มที่มีอยู่แล้วให้ต่อยอด ไม่ต้องเริ่ม
  จาก 0
- `NewRoadConnectorDraft` แบบ polyline อิสระ (ไม่ใช่แค่ node-to-node) —
  dependency-ready ทันทีที่มีเวลา ไม่ติด blocker ภายนอกใดๆ
- `G1-10` (real pilot audit) — ยัง blocked เหมือนเดิม รอข้อมูล OSM จริง

## Session 3 — 2026-09-22 (wave 3 / G2 game-side layer — Thaivia.Core)

### Task IDs และไฟล์ที่เปลี่ยน

ทำ `W3-01` เสร็จ (สถานะ `done`) และ `G2-02` เปลี่ยนเป็น `partial` (ดู
`TASKS.json` สำหรับ acceptance criteria/evidence เต็ม) `G2-01`,
`G2-06`, `G2-07` ยัง `blocked` เหมือนเดิม (ไม่มี Unity Editor) `G2-03`,
`G2-04`, `G2-05` ยัง `blocked` แต่เพิ่ม notes ว่าโค้ดเขียนแล้ว (uncompiled)

ไฟล์ใหม่หลัก:

- `game/Thaivia.Core.csproj`, `game/Thaivia.sln` — pure C# library
  project, compile จาก `game/Assets/Scripts/Core/**/*.cs` โดยตรง (ไม่
  copy source)
- `game/Assets/Scripts/Core/` (ใหม่ทั้งโฟลเดอร์): `Values/SourceValue.cs`
  + `AssumptionKind.cs`, `MapPack/*.cs` (contract types ตรงกับ
  `tools/map_pipeline/schemas/mappack.schema.json`),
  `Serialization/{CanonicalJson,JsonRequire,MapPackLoader,MapPackLoadException}.cs`,
  `Coordinates/{Vec3F,CoordinateNarrowing}.cs`,
  `Graph/RoadGraphIndex.cs`, `Attributes/{Building,Road}Attributes.cs`,
  `Simulation/{PlayerDelta,WorldState}.cs`, `Thaivia.Core.asmdef`
- `game/Thaivia.Core.Tests/` (ใหม่ทั้งโฟลเดอร์): xUnit project +
  `SourceValueTests.cs`, `MapPackLoaderTests.cs`, `RoadGraphIndexTests.cs`,
  `CoordinateNarrowingTests.cs`, `ImmutabilityTests.cs`,
  `NoUnityEngineReferenceTests.cs`, `Fixtures/*.synthetic.mappack.json`
  (real `thaivia build` output จาก synthetic fixtures สอง fixture)
- `game/Assets/Scripts/Runtime/` (ใหม่ทั้งโฟลเดอร์, UnityEngine-dependent,
  **uncompiled** — ทุกไฟล์มี `// UNCOMPILED` marker):
  `MapPackLoaderBehaviour.cs`, `Rendering/{Road,Building,Water}MeshBuilder.cs`,
  `Camera/OrthoObliqueCameraRig.cs`, `Input/PanPinchController.cs`,
  `Selection/FeatureSelectionController.cs`,
  `UI/{InspectorPanelController,AttributionOverlay,SimulationDisclaimerLabel}.cs`,
  `Thaivia.Runtime.asmdef`
- `game/README.md`, `game/Assets/Scripts/Runtime/README.md`,
  `game/Packages/README.md` — คำอธิบาย compiled+tested vs
  written-but-not_run split และขั้นตอนมนุษย์ที่เหลือ
- `docs/decisions/0008..0010-*.md`, `docs/evidence/g2-*.log`

### สิ่งที่ทำงานจริง (ยืนยันด้วยการรันจริง)

`Thaivia.Core` compile ผ่าน `dotnet build` (0 warnings, 0 errors —
`docs/evidence/g2-dotnet-build.log`) และผ่าน `dotnet test` ที่
`game/Thaivia.Core.Tests`: **35 passed** (`docs/evidence/g2-dotnet-test.log`)
ครอบคลุม:

- MapPack round-trip จริง: โหลด MapPack ที่ `thaivia build` (Python)
  ผลิตจริงจาก `tests/fixtures/synthetic/road_graph_layers.synthetic.osm.xml`
  และ `multipart_building.synthetic.osm.xml` (คำสั่งที่ใช้สร้างไฟล์เหล่านี้
  บันทึกไว้ที่ `docs/evidence/g2-fixture-generation.log`) — ไฟล์ที่ commit
  ไว้ที่ `game/Thaivia.Core.Tests/Fixtures/*.synthetic.mappack.json` มี
  `provenance.synthetic: true` ชัดเจน
- Tampered pack (แก้ 1 หลักใน payload) → `MapPackTamperedException`
  จาก content_hash mismatch จริง (ไม่ใช่แค่ trust ค่าที่ประกาศไว้ — ดู
  ADR-0008 สำหรับวิธี re-derive canonical hash ใน C# ให้ตรงกับ Python)
- Version mismatch (schema_version/importer_version) →
  `MapPackVersionMismatchException` ที่ระบุ field/found/expected ชัดเจน
  ตรวจก่อน hash check เสมอ (ไม่ทำให้เข้าใจผิดว่าไฟล์ถูก tamper)
- `SourceValue<T>`: `Assumed`/`Unknown` อ่านผ่าน `AsSourceFact()` ไม่ได้
  (throw จริง); `Unknown` ไม่มีทาง degrade เป็น default ที่ดูเหมือนค่าจริง
- Grade separation: `RoadGraphIndex` สร้าง adjacency จาก shared node id
  เท่านั้น (ไม่แตะ geometry/layer เลย) — edge สอง way ที่ cross กันใน plan
  view แต่ไม่มี node ร่วมกันจะไม่มี adjacency เลย, edge ที่มี node ร่วมกัน
  มี adjacency เสมอไม่ว่า layer จะต่างกันแค่ไหน
- `oneway=-1` (Reversed) traverse ได้แค่ทิศย้อนกลับ; turn restriction
  ที่ supported ปิดกั้นทางที่ระบุจริง; restriction ที่ `unsupported`
  (เช่น via-way) คืน `TurnDecision.Unsupported` ไม่ใช่ `Allowed` เงียบๆ
- Coordinate narrowing: วัด error จริง (ไม่ assert away) — สูงสุดที่พบใน
  synthetic fixture จริงคือ ~3.4mm, ที่จุดสังเคราะห์ระยะ ~5000m จาก origin
  ได้ sub-millimetre — ดูรายละเอียดในหัวข้อ "float32 narrowing error"
  ด้านล่าง
- Determinism: โหลด MapPack เดิมสองครั้งได้ graph โครงสร้างเดียวกันเป๊ะ
  (`content_hash` เท่ากัน, node/edge ทุกตัวตรงกัน)
- Immutability enforced ในระดับ type: `ImmutabilityTests` ใช้ reflection
  เช็คว่าทุก public property ใน `Thaivia.Core.MapPack` เป็น get-only
  หรือ init-only เท่านั้น (เช็ค `IsExternalInit` modifier — สิ่งเดียวกับที่
  compiler เช็คจริง ไม่ใช่ proxy ที่อ่อนกว่า)
- `PlayerDelta`/`WorldState` (namespace `Thaivia.Core.Simulation`) เป็น
  type แยกจาก `GeographyBase`/`SimulationInitialization` โดยสิ้นเชิง —
  ไม่มี base type ร่วม ไม่มี field ที่รับอีกฝั่งได้ ตรวจด้วย
  `PlayerDelta_And_WorldState_ShareNoTypeWithGeographyBaseOrSimulationInitialization`
- UnityEngine-freedom: `NoUnityEngineReferenceTests` scan source จริง
  หา `using UnityEngine` (ยืนยันว่าไม่มี ทั้ง `Core/` เอง และ scan ซ้ำทุก
  `dotnet test` run); `Thaivia.Core.csproj` ไม่มี reference ไปยัง
  UnityEngine เลยด้วยตัวมันเอง — `using UnityEngine;` จะทำให้
  `dotnet build` fail ทันที (พิสูจน์เชิงโครงสร้าง ไม่ใช่แค่ scan)
- `./.venv/bin/pytest -q` (Python pipeline) ยัง **67 passed** เหมือนเดิม
  — session นี้ไม่แตะโค้ด Python เลย (`docs/evidence/g2-pytest-still-passing.log`)

### float32 narrowing error (วัดจริง)

`Thaivia.Core.Coordinates.CoordinateNarrowing.ToUnityGroundPlane` เป็น
จุดเดียวที่ narrow float64 → float32 ทั้งระบบ วัด error สองแบบใน
`CoordinateNarrowingTests`:

- จุดสังเคราะห์ที่ระยะ ~5000m จาก local origin (ลำดับขนาดของ pilot AOI
  + buffer ตาม `configs/pilot-area.json`): error วัดได้ sub-millimetre
  ต่อแกน (bound ที่ assert คือ < 1mm — generous ไม่ใช่ tuned ให้พอดี
  ค่าที่วัดได้)
- ทุก node coordinate จริงใน synthetic fixture (`road_graph_layers`,
  9 nodes): max |error| = **0.00339m (~3.4mm)** — ตัวเลขจริงพิมพ์ออกทาง
  test output เสมอ ไม่ถูก assert away

### สิ่งที่ยัง not_run / blocked

- ทุกอย่างที่ต้องพึ่ง Unity Editor จริง (compile ของ
  `game/Assets/Scripts/Runtime/`, scene/prefab, play-mode run, build ใดๆ)
  — **not_run** เหมือนเดิม (ADR-0002) โค้ด Runtime เขียนแล้วแต่ไม่เคยผ่าน
  compiler เลย ทุกไฟล์มี `// UNCOMPILED` marker ที่ตรวจซ้ำด้วย
  `NoUnityEngineReferenceTests.RuntimeSourceTree_IsMarkedAsUncompiledUnityCode`
- Android/iOS/phone/tablet — ไม่เปลี่ยนจาก session ก่อนหน้า ยัง `not_run`
  ทุกแถว (ไม่มี Android SDK/adb, ไม่มี macOS/Xcode)
- แผนที่จริงยังไม่ได้นำเข้า — เหตุผลเดิมทุกประการ (ADR-0003) session นี้
  ไม่แตะ Python pipeline หรือพยายามดึงข้อมูลจริงเลย

### Blockers และขั้นตอนมนุษย์ที่เล็กที่สุด

เหมือน G0/G1 ทุกประการสำหรับ Unity: ต้องมีมนุษย์ที่มี Unity license +
Editor install จริง (Unity 6.3 LTS, เช็ค patch ปัจจุบันตอนติดตั้ง) เปิด
`game/` เป็น root project แล้วแก้ compile error ที่ Editor Console รายงาน
(ไฟล์ `Runtime/` ไม่เคยผ่าน compiler เลย) ดู `game/README.md` สำหรับ
รายการขั้นตอนแบบเต็ม — Android ต้องมี Android SDK/adb + device จริง, iOS
ต้องมี macOS + Xcode

### Next dependency-ready tasks

- `G2-01` — ยังรอ Unity Editor เหมือนเดิม (ไม่เปลี่ยน)
- `G3` (real scenario seeding ใน `SimulationInitialization`, wiring
  `PlayerDelta` เข้ากับ MapPack ที่โหลดแล้วจริงๆ) — dependency-ready แล้ว
  ตอนนี้ที่ `Thaivia.Core`'s types มีครบ
- `G1-10` (real pilot audit) — ยัง blocked เหมือนเดิม รอข้อมูล OSM จริง

## Session 2 — 2026-09-21 (G1 Real-map data pipeline)

### Task IDs และไฟล์ที่เปลี่ยน

ทำ G1-01 ถึง G1-09, G1-11, G1-12 ครบ (สถานะ `done` — pipeline
implement+test จริงด้วย synthetic fixtures) G1-10 ยัง `blocked` เพราะยัง
ไม่มีข้อมูล OSM จริง (ดู ADR-0003) รายละเอียดแต่ละ task ดูใน
`TASKS.json`

ไฟล์ใหม่/แก้หลัก:

- `tools/map_pipeline/src/map_pipeline/pipeline/` (ใหม่ทั้งโฟลเดอร์):
  `settings.py`, `tags.py`, `geometry_utils.py`, `osm_parse.py`,
  `projection.py`, `normalize.py`, `quality.py`, `features.py`,
  `assumptions.py`, `graph.py`, `boundary.py`, `sourcelock.py`,
  `acquire_net.py`, `mappack.py`
- `tools/map_pipeline/schemas/mappack.schema.json` (ใหม่)
- `tools/map_pipeline/src/map_pipeline/commands/{acquire,build,audit,verify}.py`
  (เขียนใหม่ทั้งหมด จาก stub เป็น implementation จริง)
- `tools/map_pipeline/src/map_pipeline/doctor.py`,
  `tools/map_pipeline/src/map_pipeline/cli.py` (เพิ่ม `--probe-network`)
- `tests/fixtures/synthetic/{road_graph_layers,boundary_gateway,degenerate_hostile,multipart_building}.synthetic.osm.xml`
  (ใหม่ — ทุกไฟล์มี synthetic marker ผ่าน `test_synthetic_fixtures.py`)
- `tests/test_pipeline_{projection,graph,features,boundary,sourcelock,mappack_determinism,assumptions,degenerate,end_to_end}.py`
  (ใหม่ทั้งหมด) และ `tests/test_cli_smoke.py` (แก้ให้ตรงพฤติกรรมจริง)
- `docs/decisions/0005..0007-*.md` (ใหม่)
- `docs/evidence/g1-*.log`, `docs/evidence/g1-synthetic-e2e-audit-sample.md`
  (ใหม่)

### สิ่งที่ทำงานจริง (ยืนยันด้วยการรันจริง)

Pipeline เต็ม: `acquire -> source lock/hash -> buffered extract -> entity
parse -> project -> normalize -> graph -> boundary/gateways -> audit ->
MapPack bake -> verify` implement ครบตาม spec ของ
`REAL_MAP_PIPELINE`/`IMPLEMENTATION_PLAN.th.md` §6 (ดู module map เต็มใน
`docs/progress.md` ท้ายรายงานนี้/handback message) ทดสอบผ่าน pytest 67
tests (จาก 17 ที่ G0) ครอบคลุมทุก test case ที่ spec ระบุไว้ (lon/lat
round-trip, bridge-vs-layer ไม่กลายเป็น junction, oneway=-1, turn
restrictions, multipolygon holes/multipart/building:part,
unknown-ไม่ใช่-empty-land, buffered crop + gateways, deterministic bake,
assumption namespace isolation, degenerate/hostile input)

`thaivia acquire --from-url` ทดสอบจริงกับ
`https://download.geofabrik.de/asia/thailand-latest.osm.pbf` — fail
ทันทีด้วยข้อความบล็อกที่ชัดเจน (403 บน CONNECT) ไม่ retry ไม่มี
mirror/VPN (ดู `docs/evidence/g1-acquire-from-url-blocked.log`)

`thaivia acquire --from-local-file` → `build` → `audit` → `verify` รัน
สำเร็จครบทุกขั้นบน synthetic fixture จริง (exit 0 ทุกคำสั่ง) ดู
`docs/evidence/g1-e2e-local-file-acquire-build-audit-verify.log`
MapPack ที่ผลิตมี `provenance.synthetic: true` และ `synthetic_notice`
ชัดเจน ไม่มีทางเข้าใจผิดว่าเป็น pilot pack จริง

Determinism: รัน `thaivia build` สองครั้งอิสระ (ห่างกัน >1 วินาที) จาก
source+settings เดิม ได้ `content_hash` เดียวกันเป๊ะ เปลี่ยน
`context_buffer_m` แล้ว `settings_hash` เปลี่ยนตาม (ดู
`docs/evidence/g1-determinism-two-runs.log`)

`thaivia doctor` เปลี่ยนเป็น default = cached reachability (ไม่ยิง
network ซ้ำ), `--probe-network` เพื่อ live-probe ตามเดิม

### สิ่งที่ยัง not_run / blocked

- **ยังไม่มีการ import แผนที่จริง** — G1-10 (real
  `docs/data/pilot-audit.md` สำหรับ `th-bkk-pilot-001`) ยัง `blocked`
  เพราะทุก OSM endpoint ที่อนุญาตยังถูกบล็อกโดย organization egress
  policy (เหมือน G0 เป๊ะ ดู ADR-0003) `docs/data/pilot-audit.md`
  ตัวจริงยังคง `status: blocked, not_measured` ทุกฟิลด์ตามเดิม — sample
  ผลลัพธ์จาก synthetic fixture ถูกเก็บแยกไว้ที่
  `docs/evidence/g1-synthetic-e2e-audit-sample.md` เท่านั้น ไม่ทับของจริง
- G2 (Unity viewer) — ยัง not_run เหมือน G0 เพราะไม่มี Unity Editor ใน
  environment นี้ (ดู ADR-0002) ไม่ได้แตะในรอบนี้
- `dotnet test` บน pure C# core — ยัง not_run เหมือน G0 (W3-01 ยัง
  `pending`, out of scope ของรอบนี้)

### แผนที่จริงนำเข้าแล้วหรือยัง

**ยังไม่ได้นำเข้า** เหตุผลเดิมกับ G0 ทุกประการ (ดู ADR-0003) — สิ่งที่
เปลี่ยนในรอบนี้คือ pipeline ที่จะรับข้อมูลจริงตอนปลดบล็อกนั้น
**implement เสร็จและ test ครบแล้ว** (ไม่ใช่ stub อีกต่อไป) เมื่อมนุษย์วาง
ไฟล์ `.osm.pbf`/`.osm.xml` ที่มีสิทธิ์ใช้ไว้ที่ `data/cache/` แล้วรัน
`thaivia acquire --from-local-file <path> && thaivia build && thaivia
audit --pack <path>` จะได้ MapPack และ audit จริงทันที ไม่ต้องแก้โค้ด
เพิ่ม

### สถานะ Android/iOS/phone/tablet

ไม่เปลี่ยนจาก G0 — ยัง `not_run` ทุกแถว (ไม่ได้แตะ G2 ในรอบนี้)

### Blockers และขั้นตอนมนุษย์ที่เล็กที่สุด

เหมือน ADR-0003 ทุกประการ: allowlist `download.geofabrik.de` (หรือ
endpoint ที่เลือก) **หรือ** วางไฟล์ snapshot ที่มีสิทธิ์ใช้ไว้ที่
`data/cache/th-bkk-pilot-001-source.osm.pbf` (ชื่อไฟล์ไม่จำเป็นต้องตรง
เป๊ะ — `thaivia acquire --from-local-file <path จริง>` จะจัดการ hash/lock
เอง) แล้วรัน `thaivia acquire --from-local-file <path> && thaivia build
&& thaivia audit --pack <path จาก build> && thaivia verify --pack <path>`

### Next dependency-ready tasks

- G1-10 (real audit) — dependency-ready ทันทีที่มีไฟล์ OSM จริงที่ขอบ
  bbox ของ `configs/pilot-area.json`
- G2-01 (Unity bootstrap) — ยังรอ Unity Editor เหมือนเดิม
- W3-01 (pure C# core) — ยัง dependency-ready ไม่เปลี่ยนจาก G0

## Session 1 — 2026-09-20 (G0 Bootstrap)

### Task IDs และไฟล์ที่เปลี่ยน

ทำ G0-01 ถึง G0-13 ครบตาม `TASKS.json` (ดูรายละเอียด acceptance
criteria/evidence ในไฟล์นั้น) ไฟล์ที่สร้างใหม่ทั้งหมด (repo เพิ่งเริ่ม
จาก empty):

- Root: `.gitignore`, `.gitattributes`, `README.md`, `AGENTS.md`,
  `TASKS.json`, `pytest.ini`, `requirements.txt`
- `configs/pilot-area.json`, `configs/sources.json`
- `tools/map_pipeline/` — `pyproject.toml`, `README.md`,
  `schemas/{pilot-area,sources}.schema.json`,
  `src/map_pipeline/{__init__,__main__,cli,doctor,config,paths,exit_codes}.py`,
  `src/map_pipeline/commands/{__init__,not_implemented,acquire,build,audit,verify}.py`
- `tests/` — `conftest.py`, `test_config_schemas.py`, `test_cli_smoke.py`,
  `test_synthetic_fixtures.py`,
  `fixtures/synthetic/{tiny_intersection.geojson,tiny_multipolygon.synthetic.osm.xml}`
- `docs/environment.md`, `docs/progress.md` (ไฟล์นี้)
- `docs/decisions/0001..0004-*.md`
- `docs/data/pilot-audit.md`
- `docs/evidence/g0-*.log` (21 ไฟล์ evidence)
- `game/.gitkeep`, `content/.gitkeep`

### สิ่งที่ทำงานจริง (ยืนยันด้วยการรันจริง)

- `thaivia doctor` — ตรวจ Python version, 4 dependencies (osmium,
  pyproj, shapely, jsonschema), 2 configs, local cache, reachability ของ
  6 source endpoints — exit 0 (ไม่มี hard failure)
- `thaivia acquire` / `build` / `audit` / `verify` — parse argument ได้
  จริง, exit code 3 (`NOT_IMPLEMENTED`) พร้อมข้อความ blocker ที่เจาะจง
  ไม่มี exit 0 ปลอม
- `python -m map_pipeline doctor` — เทียบเท่า `thaivia doctor` ผ่าน
  module invocation
- pytest suite ทั้งหมด (17 tests): config-schema validation, CLI smoke
  (doctor=0, stub=3), synthetic-fixture labelling — **17 passed**
- Python venv + editable install ของ `map_pipeline` package ใช้งานได้
  จริง พร้อม dependency lock ที่ resolve จริงจาก PyPI (ไม่ใช่ freeze
  มือ)
- ติดตั้ง `dotnet-sdk-8.0` (8.0.131) สำเร็จผ่าน `apt-get` จาก Ubuntu
  archive (container-local เท่านั้น)

### Commands ที่รัน พร้อมผล (exit codes)

| คำสั่ง | exit code | log |
|---|---|---|
| `python3 -m venv .venv` | 0 | — |
| `./.venv/bin/pip install --upgrade pip` | 0 | — |
| `./.venv/bin/pip install pip-tools` | 0 | — |
| `./.venv/bin/pip-compile --generate-hashes --allow-unsafe ...` | 0 | (ดู `requirements.txt` header) |
| `./.venv/bin/pip install -r requirements.txt` | 0 | — |
| `./.venv/bin/pip install --no-deps -e tools/map_pipeline` | 0 | — |
| dependency import check (`osmium`, `pyproj`, `shapely`, `jsonschema`, `pytest`, `map_pipeline`) | 0 | `docs/evidence/g0-dep-import-check.log` |
| `./.venv/bin/thaivia doctor` | 0 | `docs/evidence/g0-doctor-run.log` |
| `./.venv/bin/thaivia acquire` | 3 | `docs/evidence/g0-cli-stub-commands.log` |
| `./.venv/bin/thaivia build` | 3 | `docs/evidence/g0-cli-stub-commands.log` |
| `./.venv/bin/thaivia audit` | 3 | `docs/evidence/g0-cli-stub-commands.log` |
| `./.venv/bin/thaivia verify` | 3 | `docs/evidence/g0-cli-stub-commands.log` |
| `python -m map_pipeline doctor` | 0 | `docs/evidence/g0-cli-stub-commands.log` |
| `./.venv/bin/pytest -v` (17 tests) | 0 | `docs/evidence/g0-pytest-run.log` |
| `curl $HTTPS_PROXY/__agentproxy/status` | 0 (แต่ระบุ blocked hosts ภายใน) | `docs/evidence/g0-proxy-status.log` |
| `sudo apt-get update` (มี PPA สอง repo ที่บล็อก, main archive สำเร็จ) | 100 | `docs/evidence/g0-dotnet-experiment.log` |
| `sudo apt-get install -y dotnet-sdk-8.0` | 0 | `docs/evidence/g0-dotnet-experiment.log` |
| `dotnet --version` | 0 (8.0.131) | `docs/evidence/g0-dotnet-experiment.log` |
| `git lfs version` | ล้มเหลว (ไม่มี git-lfs) | `docs/evidence/g0-git-lfs-check.log` |

### สิ่งที่ยัง not_run

- ทุกอย่างเกี่ยวกับ Unity: project creation, compile, PlayMode/EditMode
  tests, build ใดๆ — **not_run** (ไม่มี Unity Editor ในสภาพแวดล้อมนี้
  ดู ADR-0002)
- ทุกอย่างเกี่ยวกับ Android build/deploy — **not_run** (ไม่มี Android
  SDK/adb)
- ทุกอย่างเกี่ยวกับ iOS build/signing — **not_run** (ไม่มี macOS/Xcode;
  container นี้เป็น Linux)
- G1 pipeline stages ทั้งหมด (`acquire` ของจริง, source lock, entity
  parse, project/normalize, topology validate, road graph, buffered
  crop/gateways, MapPack bake) — **not_run** เพราะไม่มีข้อมูล OSM จริง
  (ดู ADR-0003) โค้ดยังไม่เขียนเกิน CLI stub ที่ตั้งใจให้ "not
  implemented yet"
- `dotnet test` บน pure C# simulation core — **not_run** เพราะยังไม่ได้
  เริ่มเขียน simulation core (นอก scope ของ G0 ตามคำสั่งงานนี้; ดู
  `TASKS.json` task `W3-01`)

### แผนที่จริงนำเข้าแล้วหรือยัง

**ยังไม่ได้นำเข้า** เหตุผล: ทุก OSM source endpoint ที่แผนอนุญาต
(`download.geofabrik.de`, `overpass-api.de`, `overpass.kumi.systems`,
`api.openstreetmap.org`, `osm-internal.download.geofabrik.de`,
`download.openstreetmap.fr`) ถูกบล็อกโดย organization egress policy
(403 บน CONNECT tunnel) ไม่มี source hash/snapshot ใดๆ เพราะไม่มีการ
ดาวน์โหลดสำเร็จแม้แต่ครั้งเดียว `data/cache/` ไม่มีอยู่ (ตรงกับที่
`thaivia doctor` รายงาน) ดู ADR-0003 สำหรับขั้นตอนปลดบล็อก

### สถานะ Android/iOS/phone/tablet (แยกกัน)

| Platform | สถานะ |
|---|---|
| Android — Editor build | not_run |
| Android — emulator | not_run |
| Android — physical phone | not_run |
| Android — physical tablet | not_run |
| iOS — Editor/simulator build | not_run |
| iOS — physical phone | not_run |
| iOS — physical tablet | not_run |

ทุกแถว not_run เพราะไม่มี Unity project ให้ build เลย (ดู ADR-0002)
ไม่ใช่เพราะ build ล้มเหลว

### Blockers และขั้นตอนมนุษย์ที่เล็กที่สุด

1. **OSM source (บล็อก G1)** — allowlist `download.geofabrik.de` (หรือ
   endpoint Overpass ที่เลือก) ใน egress policy **หรือ** วางไฟล์
   `.osm.pbf`/`.osm.xml` ที่มีสิทธิ์ใช้ไว้ที่ `data/cache/` ด้วยมือ
   (ดู ADR-0003)
2. **Unity (บล็อก G2)** — รันงานต่อบนเครื่อง/สภาพแวดล้อมที่มี Unity Hub
   + Unity 6.3 LTS ติดตั้งพร้อม license จริง **หรือ** allowlist
   `download.unity3d.com` ชั่วคราวพร้อมข้อมูล license (ดู ADR-0002)
3. **Android/iOS device evidence (บล็อก G2 ส่วน device)** — ต้องมี
   Android SDK/เครื่องจริง และเครื่อง Mac+Xcode ตามลำดับ ไม่มีในเครื่อง
   ปัจจุบัน

ไม่มี blocker ใดที่ agent เองมีสิทธิ์แก้ได้ต่อในสภาพแวดล้อมนี้โดยไม่ทำ
สิ่งที่ต้องห้าม (ปลอมข้อมูล/ปลอม metadata/เลี่ยง policy)

### Next dependency-ready tasks

- `W3-01` (pure C# simulation core scaffold, testable ด้วย `dotnet
  test`) — **dependency-ready ทันที** เพราะ `dotnet-sdk-8.0` ติดตั้งแล้ว
  ในสภาพแวดล้อมนี้ และงานนี้ไม่ต้องพึ่งทั้ง OSM data หรือ Unity เป็น
  candidate ที่ดีที่สุดสำหรับก้าวต่อไปที่ไม่ติด blocker ภายนอก
- งานอื่นทั้งหมดใน G1/G2 รอ human unblock ตามด้านบนก่อนจะ "dependency
  ready" จริง
