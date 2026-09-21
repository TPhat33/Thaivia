# ADR-0009: `SourceValue<T>` — tri-state Known/Unknown/Assumed แทน nullable + flag

- สถานะ: Accepted
- วันที่: 2026-09-21
- ผู้เกี่ยวข้อง: wave 3 / G2 (Thaivia.Core)

## บริบท

AGENTS.md ข้อ 4 กำหนดว่า field ที่ source ไม่มีข้อมูล (height/use/lanes/
maxspeed ฯลฯ) ต้องเก็บเป็น `unknown` ไม่ใช่ default ที่อ่านแล้วดูเหมือน
ข้อเท็จจริง (`0`, `""`, `false`) และถ้ามี visual/simulation assumption
มาเติม ต้องเห็นได้ชัดว่าเป็นการเดา ไม่ใช่ source fact ทางเลือกที่ตรงไป
ตรงมาคือ `T? value` บวก `bool isAssumed` บวก `string? rule` แยกกัน 3
field — แต่แบบนั้นให้ caller ลืม check `isAssumed` ได้ง่าย (เช่น
`if (value != null) UseAsSourceFact(value.Value)` จะปน known กับ assumed
เข้าด้วยกันเงียบๆ)

## การตัดสินใจ

`Thaivia.Core.Values.SourceValue<T>` เป็น abstract class ที่มี subclass
`private` 3 ตัว (`KnownValue`/`UnknownValue`/`AssumedValue`) สร้างได้ผ่าน
factory method (`Known`/`Unknown`/`Assumed`) เท่านั้น — ไม่มี public
constructor คุณสมบัติสำคัญ 2 อย่างที่ enforce ด้วย type/runtime check
จริง ไม่ใช่ comment:

1. `AsSourceFact()` — accessor เดียวที่ตั้งใจให้ "อ่านเป็นข้อเท็จจริงจาก
   source" — throw `InvalidOperationException` ทั้งกรณี Unknown และ
   Assumed เสมอ ไม่มีทางที่ Assumed value จะไหลผ่าน accessor ตัวนี้ไป
   หลอกว่าเป็น source fact ได้เลย (ทดสอบใน
   `SourceValueTests.Assumed_AsSourceFact_Throws`)
2. `TryGetDisplayValue(out T)` — คืนค่าที่ "ควรแสดงผล/ใช้ simulation" ได้
   ทั้ง Known และ Assumed แต่คืน `false` เสมอสำหรับ Unknown (และไม่มี
   `GetValueOrDefault`-style member ให้ Unknown ไหลไปเป็น `default(T)`
   โดยไม่มี caller check ผลลัพธ์ก่อน — ทดสอบใน
   `Unknown_TryGetDisplayValue_ReturnsFalse_NeverADefaultThatLooksReal`)

`Match(onKnown, onUnknown, onAssumed)` เป็นทางเดียวที่บังคับให้ caller
เขียน branch ครบทั้ง 3 กรณี (compile-time) — ใช้จริงใน
`Thaivia.Runtime.UI.InspectorPanelController.AppendRow` เพื่อ render 3
สีที่ต่างกันสำหรับ source/unknown/assumption ตามที่ spec ของ G2-04
ต้องการ

`AssumptionKind` (`Visual`/`Simulation`) ติดมากับ `Assumed` เสมอ ตรงกับ
namespace 2 ชนิดฝั่ง Python (`visual_assumption`/`simulation_assumption`
ใน `map_pipeline.pipeline.tags`) — inspector แสดง kind ควบคู่กับ rule
string ได้โดยไม่ต้อง pattern-match บน concrete subclass

## ผลที่ตามมา

- `Thaivia.Core.Attributes.{Building,Road}Attributes` เป็นจุดเดียวที่
  แปลง `SourceTags` + `VisualAssumptions`/`SimulationAssumptions` ดิบๆ
  เป็น `SourceValue<T>` ต่อ field (height/building_use/width/lanes/
  maxspeed) — call site อื่นไม่ต้องเขียน "เช็ค tag ก่อน ถ้าไม่มีเช็ค
  assumption ก่อน ถ้าไม่มีค่อย unknown" ซ้ำเอง
- ข้อเสีย: ต้องเขียน `Match`/`TryGetDisplayValue` ทุกจุดที่ใช้ค่า แทนที่
  จะเขียน `value ?? 0` สั้นๆ — เป็นการแลก ergonomics เพื่อไม่ให้ unknown/
  assumed หลุดไปเป็น default โดยไม่ตั้งใจ ซึ่งตรงกับเจตนาของ AGENTS.md
  ข้อ 4 โดยตรง
