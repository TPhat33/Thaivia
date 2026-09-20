# Environment audit (G0)

บันทึกวันที่ 2026-09-20 ตรวจจากสภาพแวดล้อมที่ bootstrap repo นี้จริง
(container Linux) ไม่ใช่เครื่องของเจ้าของโครงการ ทุกแถวมี evidence เป็น
คำสั่งจริงที่รันแล้ว ผลลัพธ์เต็มอยู่ใน `docs/evidence/*.log`
status ใช้ 4 ระดับ: `available` (มีและใช้ได้จริง), `missing` (ตรวจแล้วไม่มี),
`blocked` (มีแต่ endpoint/policy บล็อกไม่ให้เข้าถึง), `unverified`
(ตรวจไม่ได้ในสภาพแวดล้อมนี้ เช่น ต้องมี hardware อื่น)

## ระบบปฏิบัติการและฮาร์ดแวร์

| รายการ | สถานะ | evidence |
|---|---|---|
| OS/สถาปัตยกรรม | available — Ubuntu 24.04.4 LTS (noble), kernel 6.18.44, x86_64 | `uname -a` → `docs/evidence/g0-os-uname.log` |
| CPU | available — 4 cores | `nproc` → `docs/evidence/g0-cpu-nproc.log` |
| RAM | available — 15Gi total, ~14Gi free | `free -h` → `docs/evidence/g0-mem-free.log` |
| Disk ว่าง | available — ~30GB avail บน `/` (252G filesystem, 7.1G used) | `df -h /` → `docs/evidence/g0-disk-df.log` |

## Git และ toolchain พื้นฐาน

| รายการ | สถานะ | evidence |
|---|---|---|
| Git | available — git version 2.43.0 | `git --version` → `docs/evidence/g0-git-version.log` |
| Git LFS | missing — `git: 'lfs' is not a git command` | `git lfs version` → `docs/evidence/g0-git-lfs-check.log` |
| Java (JDK) | available — OpenJDK 21.0.10 (Ubuntu 24.04) | `java -version` → `docs/evidence/g0-java-version.log` |
| Repo ก่อนเริ่มงาน | available — empty repo, branch `claude/sonnet-5-implementation-pcqfib`, no commits, remote `origin` = https://github.com/TPhat33/Thaivia | `git status`, `git branch -a && git remote -v` → `docs/evidence/g0-git-status.log`, `docs/evidence/g0-git-branch-remote.log` |

## Python toolchain (GIS pipeline)

| รายการ | สถานะ | evidence |
|---|---|---|
| Python interpreter | available — Python 3.11.15 ที่ `/usr/local/bin/python3` | `python3 --version` → `docs/evidence/g0-python-version.log` |
| Python 3.12 (proposed baseline ในแผนเดิม) | **missing** — ไม่มีในเครื่องนี้ ใช้ 3.11.15 แทนจริง; ดู ADR-0001 | เดียวกับข้างบน — ไม่มี python3.12 binary ให้ตรวจ |
| venv | available — สร้างที่ `.venv/` (gitignored) ด้วย `python3 -m venv .venv` สำเร็จ | `docs/evidence/g0-dep-import-check.log` (รันจาก `.venv/bin/python`) |
| `osmium` (เดิมชื่อ `pyosmium` บน PyPI) | available — ติดตั้งจาก wheel จริง `osmium==4.3.1` (cp311-manylinux_2_27-x86_64), import สำเร็จ | `docs/evidence/g0-pip-resolve-pyosmium.log` (การค้นพบว่าเปลี่ยนชื่อ), `docs/evidence/g0-dep-import-check.log` (import จริง) |
| `pyproj` | available — `3.7.2`, wheel, import สำเร็จ | `docs/evidence/g0-dep-import-check.log` |
| `shapely` | available — `2.1.2`, wheel, import สำเร็จ | `docs/evidence/g0-dep-import-check.log` |
| `jsonschema` | available — `4.26.0`, wheel, import สำเร็จ | `docs/evidence/g0-dep-import-check.log` |
| `pytest` | available — `8.4.2` | `docs/evidence/g0-dep-import-check.log`, `docs/evidence/g0-pytest-run.log` |
| `pip-tools` (สำหรับ lock file) | available — `7.6.1` ติดตั้งและใช้ compile lock ได้จริง | `docs/evidence/pip-install log ระหว่าง session (ดู requirements.txt header)` |
| `osmium-tool` (C++ CLI, optional per plan) | missing — ไม่มี binary `osmium` ในเครื่อง | `which osmium` → `docs/evidence/g0-osmium-check.log` |
| `pyosmium`/`osmium` wheel build from source | ไม่จำเป็น — มี prebuilt cp311 manylinux wheel บน PyPI จริง ไม่ต้อง build จาก source | `docs/evidence/g0-dep-import-check.log` |

## Network / proxy / แหล่งข้อมูล OSM

| รายการ | สถานะ | evidence |
|---|---|---|
| Outbound HTTPS ผ่าน policy proxy | available (proxy เอง enabled, ทำงานปกติ) | `curl $HTTPS_PROXY/__agentproxy/status` → `docs/evidence/g0-proxy-status.log` |
| pypi.org / files.pythonhosted.org | available — ใช้ resolve/ติดตั้ง dependency จริงสำเร็จทั้งหมด | `docs/evidence/g0-pip-resolve-pyosmium.log`, `requirements.txt` |
| github.com | available (ใช้ push ปลายงาน) | ดูผล `git push` ท้าย session |
| download.geofabrik.de | **blocked** — 403 บน CONNECT tunnel (policy denial ที่ proxy) | `docs/evidence/g0-proxy-status.log`, `docs/evidence/g0-doctor-run.log` |
| overpass-api.de | **blocked** — 403 บน CONNECT tunnel | เดียวกับข้างบน |
| overpass.kumi.systems | **blocked** — 403 บน CONNECT tunnel | เดียวกับข้างบน |
| api.openstreetmap.org | **blocked** — 403 บน CONNECT tunnel | เดียวกับข้างบน |
| planet.openstreetmap.org | **blocked** — 403 บน CONNECT tunnel (ปรากฏใน `recentRelayFailures` ของ proxy status) | `docs/evidence/g0-proxy-status.log` |
| download.openstreetmap.fr | **blocked** — 403 บน CONNECT tunnel | `docs/evidence/g0-proxy-status.log`, `docs/evidence/g0-doctor-run.log` |
| osm-internal.download.geofabrik.de | **blocked** — 403 บน CONNECT tunnel | เดียวกับข้างบน |
| ppa.launchpadcontent.net | **blocked** — 403 บน CONNECT tunnel (ยืนยันซ้ำตอนพยายาม `apt-get update`) | `docs/evidence/g0-proxy-status.log`, `docs/evidence/g0-dotnet-experiment.log` |
| download.unity3d.com | **blocked** — 403 บน CONNECT tunnel | `docs/evidence/g0-proxy-status.log` |
| dotnetcli.azureedge.net / builds.dotnet.microsoft.com | **blocked** — 403 บน CONNECT tunnel (ไม่ได้ใช้จริงเพราะ apt ใช้ archive.ubuntu.com แทนได้) | `docs/evidence/g0-proxy-status.log` |
| archive.ubuntu.com / security.ubuntu.com | available — `apt-get update`/`install` ผ่าน mirror นี้สำเร็จ | `docs/evidence/g0-dotnet-experiment.log` |
| packages.microsoft.com, nuget.org | unverified — ไม่ได้ใช้จริงใน session นี้ (ไม่จำเป็น เพราะติดตั้ง dotnet ผ่าน archive.ubuntu.com ได้) | ไม่มี log แยก |

**นโยบายของ session นี้ (ตามคำสั่ง supervisor): ไม่ retry host ที่ยืนยันว่าบล็อกแล้วซ้ำ**
รายการ blocked ด้านบนคือผลตรวจครั้งเดียวจาก proxy status endpoint (local,
เรียกได้) และผลจาก `thaivia doctor` เอง (ซึ่งเป็นเครื่องมือตรวจสภาพจริง
ไม่ใช่การพยายามข้ามนโยบาย)

## Unity / เกม

| รายการ | สถานะ | evidence |
|---|---|---|
| Unity Editor (ทุก patch) | missing — ไม่พบ `unity-editor`, `unityhub`, หรือ `/opt/Unity*` | `docs/evidence/g0-unity-check.log` |
| Unity license/build modules | unverified — ตรวจไม่ได้เพราะไม่มี Editor ให้ตรวจ license attach | เดียวกับข้างบน |
| Android SDK / adb | missing — ไม่พบ `adb`, `sdkmanager`, ไม่มี `ANDROID_HOME`/`ANDROID_SDK_ROOT` | `docs/evidence/g0-android-ios-check.log` |
| macOS/Xcode/iOS signing | unverified / not applicable — container นี้เป็น Linux x86_64 ไม่ใช่ macOS; Apple toolchain ต้องใช้ hardware ของ Apple เท่านั้น | `docs/evidence/g0-android-ios-check.log` |
| เครื่องทดสอบจริง (phone/tablet, Android/iOS) | unverified — ไม่มีเครื่องจริงหรือ emulator ต่ออยู่กับ container นี้ | `docs/evidence/g0-android-ios-check.log` |

ผลคือ: ทุกอย่างเกี่ยวกับ Unity compile/build/device ใน session นี้เป็น
`not_run` ไม่ใช่ `failed` (เพราะไม่มีเครื่องมือให้รันตั้งแต่แรก) ดู
ADR-0002

## .NET (การทดลองแบบมีขอบเขต, ไม่ผูกกับ baseline หลัก)

| รายการ | สถานะ | evidence |
|---|---|---|
| `dotnet` ก่อนการทดลอง | missing | `docs/evidence/g0-dotnet-check.log` |
| `mono` | missing | `docs/evidence/g0-mono-check.log` |
| การทดลองติดตั้ง `dotnet-sdk-8.0` ผ่าน `apt-get` (Ubuntu archive, **ไม่ใช่** `dotnetcli.azureedge.net` ที่ถูกบล็อก) | **available หลังทดลอง** — ติดตั้งสำเร็จจาก `archive.ubuntu.com`/`security.ubuntu.com`, `dotnet --version` = `8.0.131` | `docs/evidence/g0-dotnet-experiment.log` |

**หมายเหตุสำคัญ**: การติดตั้งนี้เป็น container-local เท่านั้น ทำเพื่อให้
wave 3 (pure C# simulation core) มี `dotnet test` ใช้ทดสอบได้ใน
สภาพแวดล้อมพัฒนานี้ **ไม่ใช่การยืนยันว่าเครื่องของเจ้าของโครงการมี
.NET SDK ติดตั้งอยู่** ต้องตรวจซ้ำบนเครื่องจริงที่จะ build ก่อนใช้งานจริง
ระหว่างการทดลองพบว่า `ppa.launchpadcontent.net` (สอง PPA ที่ตั้งไว้ล่วงหน้า
ใน image: deadsnakes, ondrej/php) ถูกบล็อกเช่นกัน แต่ไม่กระทบ เพราะ
`dotnet-sdk-8.0` มีอยู่ใน main Ubuntu archive (`noble-updates`) โดยตรง
ไม่ต้องพึ่ง PPA

## Osmium/PyPI naming — สิ่งที่ค้นพบระหว่างตรวจ (ไม่ใช่ deviation แต่เป็น evidence)

PyPI package ชื่อ `pyosmium` (ที่แผนเดิมอ้างถึง) **404** ในปัจจุบัน โปรเจกต์
เปลี่ยนชื่อ distribution บน PyPI เป็น `osmium` แล้ว (import name เดิมคือ
`import osmium` อยู่แล้วไม่เปลี่ยน) แก้ไข `pyproject.toml` ให้ depend on
`osmium>=4.0,<5` และยืนยันว่า import ได้จริงบน Python 3.11.15 รายละเอียด
เต็มใน `docs/evidence/g0-pip-resolve-pyosmium.log`

## สรุป

ไม่มีการเดาว่าเครื่องมีโปรแกรมหรือสิทธิ์พร้อม ทุกแถว "available" มี
คำสั่งจริงที่รันสำเร็จรองรับ ทุกแถว "blocked" อ้างอิง proxy denial ที่
ยืนยันแล้วครั้งเดียว ไม่ retry ซ้ำ ทุกแถว "missing"/"unverified" คือผล
ตรวจจริงว่าไม่มี ไม่ใช่การคาดเดา
