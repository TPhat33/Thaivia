# ADR-0010: `game/` layout — dotnet project และ Unity asmdef ใช้ source ชุดเดียวกัน

- สถานะ: Accepted
- วันที่: 2026-09-21
- ผู้เกี่ยวข้อง: wave 3 / G2

## บริบท

โจทย์ของ wave นี้บอกให้วาง `Thaivia.Core` "ที่ทั้ง `dotnet build` และ
Unity asmdef ในอนาคตใช้ร่วมกันได้" แต่ environment นี้ไม่มี Unity Editor
เลย (ADR-0002) — ต้องออกแบบ layout ที่ (ก) `dotnet build`/`dotnet test`
รันได้จริงตอนนี้ (ข) เมื่อมนุษย์เปิดโปรเจกต์นี้ใน Unity Editor จริงๆ
ไฟล์ชุดเดียวกันต้อง compile ได้โดยไม่ต้อง copy/fork source

## การตัดสินใจ

1. Source จริงของ `Thaivia.Core` อยู่ที่เดียว:
   `game/Assets/Scripts/Core/**/*.cs` — ตำแหน่งที่ Unity asmdef
   (`Thaivia.Core.asmdef` ในโฟลเดอร์เดียวกัน) จะมองเห็นเมื่อ Editor เปิด
   จริง
2. `game/Thaivia.Core.csproj` (นอก `Assets/` เพื่อไม่ให้ Unity สับสนกับ
   ไฟล์ที่ไม่ใช่ asset) ไม่ copy ไฟล์ แต่ compile ไฟล์ตรงจากตำแหน่งนั้น
   ด้วย `<Compile Include="Assets/Scripts/Core/**/*.cs" />` +
   `<EnableDefaultCompileItems>false</EnableDefaultCompileItems>`
   (ปิด default glob ของ SDK เพื่อไม่ให้หยิบไฟล์อื่นที่ไม่ตั้งใจ) ผลคือ
   `dotnet build` ที่นี่ กับ Unity Editor ในอนาคต compile ไฟล์ **ชุด
   เดียวกันเป๊ะ** ไม่ใช่ fork
3. UnityEngine-freedom ของ `Thaivia.Core` enforce 2 ชั้น ไม่ใช่ comment
   เดียว: (ก) `Thaivia.Core.csproj` ไม่มี PackageReference/
   ProjectReference ไปยังอะไรที่เกี่ยวกับ Unity เลย — `using
   UnityEngine;` จะทำให้ `dotnet build` fail ทันที (ข)
   `Thaivia.Core.asmdef` ตั้ง `"noEngineReferences": true` ซึ่งเป็น
   field จริงของ Unity ที่บังคับ compiler ของ Editor เองปฏิเสธ
   UnityEngine reference จาก assembly นี้ (ค)
   `Thaivia.Core.Tests.NoUnityEngineReferenceTests` scan source จริงหา
   `using UnityEngine` ทุกครั้งที่ `dotnet test` รัน — กันไม่ให้ PR ใน
   อนาคตแอบใส่กลับเข้ามาโดยไม่มีใครสังเกต
4. โค้ดที่ต้องพึ่ง `UnityEngine` จริงๆ (mesh building, camera, input, UI)
   อยู่แยกที่ `game/Assets/Scripts/Runtime/` พร้อม asmdef ของตัวเอง
   (`Thaivia.Runtime.asmdef`, reference `Thaivia.Core` +
   `Unity.InputSystem`) — ไม่มี `.csproj` ฝั่ง dotnet สำหรับโฟลเดอร์นี้
   เพราะ compile ไม่ได้จริงในนี้ (ไม่มี UnityEngine.dll ให้ resolve)
   ทุกไฟล์ขึ้นต้นด้วย `// UNCOMPILED` และมี test source-scan
   (`RuntimeSourceTree_IsMarkedAsUncompiledUnityCode`) คอยเช็คว่า marker
   ยังอยู่
5. `game/Thaivia.Core.Tests/` (โฟลเดอร์แยก นอก `Assets/`) ผูก xUnit +
   `Microsoft.NET.Test.Sdk` ผ่าน NuGet ตามปกติ — ไม่พยายามให้ Unity
   มองเห็น/compile โฟลเดอร์นี้เลย เพราะ Unity ไม่มี test framework แบบ
   xUnit ให้ใช้อยู่แล้ว การแยกโฟลเดอร์ทำให้ Unity asmdef ของ
   `Thaivia.Core`/`Thaivia.Runtime` (ที่อยู่ใต้ `Assets/`) ไม่มีวันเห็น
   หรือพยายาม compile ไฟล์ test พวกนี้

## ผลที่ตามมา

- `game/Thaivia.sln` รวมแค่ `Thaivia.Core.csproj` +
  `Thaivia.Core.Tests.csproj` — ไม่รวม Runtime (compile ไม่ได้จริง จึงไม่
  ใส่ปนใน solution ที่อ้างว่า build ได้)
- เมื่อมนุษย์เปิด `game/` ใน Unity Editor จริง คาดว่า Editor จะสร้าง
  `bin/`/`obj/` เพิ่มใต้ `Assets/Scripts/Core/` เอง (Unity's own scripting
  backend cache) — ไฟล์เหล่านั้นถูก `.gitignore` (`[Bb]in/`/`[Oo]bj/`)
  ครอบอยู่แล้วจาก G0 ไม่ชนกับ `game/bin/`/`game/obj/` ที่ `dotnet build`
  สร้างเองในโฟลเดอร์ root ของ `game/`
