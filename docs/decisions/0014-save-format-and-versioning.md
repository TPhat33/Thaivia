# ADR-0014: Save format — latest/previous, explicit map-version mismatch, ไม่มี wall-clock catch-up

- สถานะ: Accepted
- วันที่: 2026-09-22
- ผู้เกี่ยวข้อง: wave 4 / G3 (Thaivia.Core.Simulation.Save)

## บริบท

IMPLEMENTATION_PLAN.th.md §13 กำหนดว่า save ต้องมี mapId/mapVersion/
payload hashes, simulation/content version, mutable state, PlayerDelta,
RNG stream positions, queues/projects, ต้องเก็บ latest+previous พร้อม
recover จากไฟล์เสียแล้วบอกผู้เล่น, และ "OSM snapshot ใหม่ห้ามทับ save ที่
กำลังเล่น" (โหลด save ที่ mapVersion/hash ไม่ตรงกับ pack ที่ติดตั้งต้อง
error ชัดเจน ไม่ใช่โหลดครึ่งๆ กลางๆ)

## การตัดสินใจ

1. **`SaveGame` ไม่เก็บ `GeographyBase`/`SimulationInitialization` เลย**
   — เก็บแค่ `MapId` + `MapContentHash` (content hash ที่
   `MapPackLoader` verify แล้วตอนโหลด ซึ่งเข้มกว่า version string เฉยๆ
   เพราะเปลี่ยนแม้แต่ byte เดียวก็เปลี่ยน hash) โครงสร้างภูมิศาสตร์โหลด
   ใหม่จาก MapPack ที่ติดตั้งอยู่เสมอ — นี่คือสิ่งที่ทำให้
   "GeographyBase byte-identical ก่อน/หลัง" เป็นจริงโดยอัตโนมัติ (save
   ไม่มีทางพก state ที่ overwrite มันได้ตั้งแต่แรก)
2. **`LoadResult` เป็น abstract type ปิด (sealed nested classes)** 4 แบบ:
   `Loaded` (พร้อม `FellBackToPrevious` flag), `MapVersionMismatch`,
   `Corrupt`, `NotFound` — caller ถูกบังคับให้ pattern-match แยกกรณี
   ไม่มีทาง "โหลดสำเร็จ" แบบเงียบๆ เมื่อจริงๆ แล้ว fallback หรือ mismatch
   เกิดขึ้น (spec: "ต้อง...แจ้งผู้เล่น" — ที่นี่ทำเป็น result type ตามที่
   spec อนุญาตไว้ "ไม่ต้องมี UI" ในรอบนี้)
3. **`SaveGameStore.LoadLatest` เช็ค map version หลังอ่านไฟล์สำเร็จ
   เท่านั้น แต่ก่อน return `Loaded` เสมอ** — ไฟล์ latest ที่ parse ได้
   แต่ mapId/mapContentHash ไม่ตรงกับที่ติดตั้งอยู่ คืน
   `MapVersionMismatch` ทันที ไม่ตกไปอ่าน previous ต่อ (เพราะ mismatch
   ไม่ใช่ "เสีย" — ไฟล์ยังถูกต้องสมบูรณ์ แค่เป็นของแผนที่คนละเวอร์ชัน)
   ส่วน parse ไม่ได้เลย (corrupt) ถึงจะ fallback ไป previous
4. **Rotate-then-write-atomic**: `Save()` copy ไฟล์ latest เดิมไปเป็น
   previous ก่อน (ถ้ามี) แล้วเขียน latest ใหม่ผ่าน temp-file +
   `File.Move(overwrite: true)` — crash กลาง `Save()` เขียนไม่เสร็จ
   ทำให้ temp file ค้างแต่ latest เดิมยังอ่านได้ปกติ (atomic move ไม่ใช่
   partial write) ความเสียหายที่ recovery ต้องรับมือคือไฟล์เสียจากสาเหตุ
   อื่น (ดิสก์, sync, manual edit) ไม่ใช่จาก store เองเขียนไม่จบ
5. **RNG state เก็บเป็น `ulong` ต่อ stream ตรงๆ (ไม่ใช่ replay จาก seed +
   draw count)** — ดู ADR-0012 เหตุผลเรื่อง `DeterministicRandom.State`
6. **JSON เขียน/อ่านมือด้วย `Utf8JsonWriter`/`JsonRequire` แบบเดียวกับ
   `MapPackLoader`** แทนที่จะใช้ `JsonSerializer.Serialize/Deserialize`
   ตรงๆ กับ `SaveGame` เพราะทุก type ในโค้ดนี้เป็น get-only (ไม่มี
   parameterless constructor/settable property ที่ attribute-based
   serializer ต้องการ) — เขียนมือรักษาความสม่ำเสมอของโค้ดเบสไว้ และทำให้
   field ที่ขาด/type ผิดใน save file ที่เสียหาย throw
   `MapPackFieldException` ที่เจาะจง ไม่ใช่แค่ throw generic parse
   exception

## ผลที่ตามมา — ทดสอบอะไรพิสูจน์อะไร

- `SaveLoadTests.SaveThenRestore_ThenContinue_ProducesTheIdenticalHashAsNeverHavingSaved`
  คือ test ที่สอง supervisor บอกว่าจะตรวจเข้มที่สุด: รัน world สองตัวจาก
  seed เดียวกัน คำสั่งเดียวกัน ตัวหนึ่งไม่เคย save เลย อีกตัว save
  กลางทางแล้ว restore แล้วรันคำสั่งที่เหลือต่อ — เทียบ
  `ComputeStructuralHash()` ท้ายสุดต้องเท่ากันเป๊ะ
- `Store_CorruptLatest_FallsBackToPrevious_AndSaysSo` /
  `Store_BothSlotsCorrupt_ReturnsCorrupt_NeverAPartialLoad` /
  `Store_LoadLatest_MapVersionMismatch_IsRejectedExplicitly_NotPartiallyLoaded`
  ครอบคลุมทั้งสามเส้นทางความล้มเหลวที่ spec ระบุ
