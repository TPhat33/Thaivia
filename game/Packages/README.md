# Packages/

Placeholder. In a real Unity project this directory holds `manifest.json`
and (once resolved by the Editor) `packages-lock.json`. Neither file is
committed here: both are Editor-generated, and AGENTS.md rule 5 forbids
fabricating them to look like an Editor has opened this project when
none has (see `docs/decisions/0002-unity-unverifiable-in-this-environment.md`).

## What a human needs to do here

1. Open `game/` as a Unity project in a licensed Editor (Unity 6.3 LTS —
   confirm the exact current patch on unity.com/releases at the time,
   per `TASKS.json` task `G2-01`).
2. Let the Editor generate `manifest.json`/`packages-lock.json` itself
   from the packages the project actually needs. At minimum, based on
   what `game/Assets/Scripts/Runtime` already assumes:
   - `com.unity.inputsystem` (pan/pinch — see
     `Assets/Scripts/Runtime/Input/PanPinchController.cs`)
   - `com.unity.ugui` (the inspector/attribution/disclaimer UI under
     `Assets/Scripts/Runtime/UI`)
3. Commit the real, Editor-generated `manifest.json`/`packages-lock.json`
   at that point — not before.
