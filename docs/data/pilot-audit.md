# Pilot area data audit — th-bkk-pilot-001

**status: blocked, no source data acquired**

วันที่: 2026-09-20 (G0 bootstrap session)

เอกสารนี้เป็น template ที่ `thaivia audit` จะ regenerate จริงในอนาคตเมื่อ
`thaivia build` ผลิต MapPack ได้ ตอนนี้ทุกค่าที่ต้องวัดถูกทำเครื่องหมาย
`not_measured` อย่างตรงไปตรงมา **ไม่มีการเดาหรือเติมตัวเลขใดๆ**

## เหตุผลที่ crop นี้ยังใช้ทดสอบไม่ได้

ไม่สามารถดึงข้อมูล OSM จริงได้ เพราะทุก endpoint ที่อนุญาต
(Geofabrik, Overpass) ถูกบล็อกโดย organization egress policy ของ
สภาพแวดล้อมที่ bootstrap repo นี้ ดูรายละเอียดเต็มใน
`docs/decisions/0003-osm-source-acquisition-blocked.md` และ
`docs/environment.md`

`configs/pilot-area.json` มี `coverage_status: "UNVERIFIED"` ตรงกับความ
จริงของ session นี้

## Source snapshot

- Source URL: `not_measured` (ไม่มีการดึงข้อมูลสำเร็จ)
- Retrieval time: `not_measured`
- Source snapshot time (หรือเหตุผลที่ไม่ทราบ): `not_measured`
- SHA-256: `not_measured`
- Importer/settings version: `not_measured`

## Feature inventory (ไม่ใช่ "coverage %" เพราะไม่มี ground truth)

| ประเภท | จำนวนที่พบ | หมายเหตุ |
|---|---|---|
| ถนน (ways ที่มี `highway=*`) | not_measured | — |
| ซอย/ทางเดิน | not_measured | — |
| คลอง/พื้นที่น้ำ | not_measured | — |
| อาคาร (footprint polygons) | not_measured | — |
| Relations (multipolygon, restriction) | not_measured | — |
| Grade separation (bridge/tunnel/layer) | not_measured | — |
| Barriers | not_measured | — |

## Known/unknown ของ attribute สำคัญ

| Attribute | สถานะ | หมายเหตุ |
|---|---|---|
| Building use | not_measured | — |
| Building height/levels | not_measured | — |
| Road access/modes | not_measured | — |
| Road widths/lanes | not_measured | — |
| Turn restrictions | not_measured | — |
| Oneway (รวม `oneway=-1`) | not_measured | — |

## Topology conflicts / clipping issues

not_measured — ยังไม่มีข้อมูลให้ตรวจ topology

## Retained vs rejected features

- Retained: not_measured
- Rejected (พร้อมเหตุผล): not_measured

## สรุปว่า crop นี้ใช้ทดสอบได้หรือไม่ได้

**ยังใช้ไม่ได้** ไม่ใช่เพราะพื้นที่มีปัญหา แต่เพราะยังไม่มีข้อมูลให้ตรวจ
เลย เมื่อ `docs/decisions/0003-osm-source-acquisition-blocked.md` ถูก
ปลดบล็อก (allowlist endpoint หรือวางไฟล์ snapshot ที่ `data/cache/`)
ให้รัน `thaivia acquire && thaivia build && thaivia audit` เพื่อ
regenerate เอกสารนี้ด้วยค่าจริง
