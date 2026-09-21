# ADR-0020: Incident state machine — condition-driven, rate-bounded, ethically constrained

- สถานะ: Accepted
- วันที่: 2026-09-21
- ผู้เกี่ยวข้อง: wave 5 / G4 (Thaivia.Core.Simulation.Mobility.Incidents)

## บริบท

Spec ระบุสอง strand สมมติ (คืนไม่สงบ/แข่งรถบนถนน) ที่ต้อง "ผูกกับสภาพ
จำลอง" (activity clock, noise, traffic state) "ไม่ spawn ต่อ frame" และมี
"warnings/cooldown/response/prevention" AGENTS.md rule 9 ห้ามผูก
เหตุการณ์กับบุคคล/กิจการจริง และห้ามตั้งประเภทสถานที่เป็นปัญหาในตัวเอง

## การตัดสินใจ

### Risk function ไม่มีทาง "เห็น" ว่าสถานที่คืออะไร

`IncidentConditions.NightDisorderRisk`/`StreetRacingRisk` รับพารามิเตอร์
แค่ `hourOfDay`, `noiseIndex0To100`, `congestionRatio0To1` — ไม่มี
`BuildingArchetype`, source id, หรือ identifier อื่นใดเลยใน signature
นี่ไม่ใช่แค่ convention แต่เป็นข้อจำกัดเชิงโครงสร้าง: โค้ดที่อยากเขียน
"if archetype == Temple, risk += X" ทำไม่ได้เลยถ้าไม่เพิ่ม parameter ใหม่
เข้าไปก่อน — เป็น friction point ที่ตั้งใจ `IncidentEngineTests.EthicalControl_...`
ใช้ reflection สแกนทุก public method ใน namespace นี้ยืนยันว่าไม่มีตัว
ไหนรับ `BuildingArchetype` เลยจริง (เหมือนแนวทาง `ImmutabilityTests`/
`LayerSeparationTests` ที่มีอยู่แล้วในโค้ดเบส)

### State machine: Idle -> Warning -> Active -> Cooldown -> Idle

Transition แต่ละ phase ตัดสินจาก `risk` เทียบ `RiskThreshold` เท่านั้น —
`RandomStreamName.Incidents` stream ถูกดึงแค่ครั้งเดียว ตอน Warning
กลายเป็น Active เพื่อสุ่ม **severity** (1-100) เท่านั้น ไม่เคยมีผลต่อว่า
"จะเกิดหรือไม่เกิด" เลย พิสูจน์ด้วย
`Triggering_IsDrivenByConditions_NotByTheRngSeed_...`: รัน risk time
series เดียวกันผ่าน RNG seed สองค่าที่ต่างกันมาก (111 กับ 999) ได้ phase
sequence เหมือนกันเป๊ะทุก tick มีแค่ `LastSeverity` ที่ต่างกัน (19 vs 16)

Cooldown ไม่ consult risk เลย (เขียนไว้ตรงๆ ใน `IncidentEngine.Step`'s
comment) — เป็นกลไกที่ทำให้ rate bound เป็นจริงทางโครงสร้าง ไม่ใช่แค่
ความหวัง

### Rate bound วัดจริง

`IncidentThresholds.MinimumFullCycleTicks` (=WarningLeadTicks+
DurationTicks+CooldownTicks) คือ floor ทางคณิตศาสตร์ของ "เร็วที่สุดที่
site หนึ่งจะเกิดเหตุการณ์ซ้ำได้" ทดสอบด้วย risk=1.0 คงที่ตลอด 100,000
tick (worst case สำหรับ rate) วัดผลจริง:
**IncidentsTriggered = 2,778** เทียบ theoretical max 2,858
(`docs/evidence/g4-incident-rate-bound-measured.log`) — ไม่ใช่ "1 ครั้ง
ต่อ tick" (100,000 ครั้ง) เลยสักนิด

### Response/prevention levers ทดสอบแยกกันจริง

`IncidentLevers.ApplyPatrol` (prevention: ลด risk ก่อนถึง threshold,
สามารถยกเลิก warning ที่กำลังดำเนินอยู่ได้จริงก่อนกลายเป็น Active — ดู
`PreventionLever_CanCancelAWarningBeforeItBecomesActive`, ทั้งสอง site
เห็น risk เดียวกันตอนเริ่ม Warning ต่างกันแค่ lever ที่ใส่ทีหลัง พิสูจน์
ว่า lever ยกเลิก warning ที่เกิดแล้วจริง ไม่ใช่แค่ป้องกันไม่ให้เริ่ม)
`IncidentLevers.ApplyFasterResponse` (response: ลด `DurationTicks` ของ
incident ที่ Active อยู่แล้ว — ทดสอบว่า incident จบเร็วขึ้นจริงเป็น
จำนวน tick ที่คำนวณได้ตรงเป๊ะ)

## ผลที่ตามมา

- ยังไม่ได้ wire `IncidentEngine` เข้ากับ `WorldState`'s activity
  clock/noise index/queue occupancy จริง (ต้องแปลง tick -> hourOfDay,
  ดึง noise จาก `NoiseIndex.ComputeAt`, ดึง congestion จาก
  `LinkQueueSimulator` จริง) — โมดูลนี้ทดสอบและพิสูจน์ตัวเองสมบูรณ์แบบ
  standalone (pure function ของ risk inputs) แต่การต่อสายจริงเข้า world
  tick loop เป็นงานที่ deferred ไปเซสชันถัดไป บันทึกไว้ตรงๆ ใน
  progress.md
