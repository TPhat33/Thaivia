# game/ — Unity viewer (wave 3 / G2)

**There is no Unity Editor, Unity Hub, or Unity license in this
environment, and `download.unity3d.com` is blocked.** Nothing under
`game/` has ever been opened, compiled, or run by a Unity Editor. See
`docs/decisions/0002-unity-unverifiable-in-this-environment.md`. This
file states plainly, and keeps stating on every wave that touches
`game/`, exactly which files here are real (compiled + tested with
`dotnet`) and which are written-but-unverified Unity code a human must
still build.

## The honest split

### Real: compiled and tested (`dotnet build` / `dotnet test`)

- `game/Thaivia.Core.csproj` + `game/Assets/Scripts/Core/**/*.cs` — a
  pure C# library with **zero** `UnityEngine` reference. It compiles
  standalone with `dotnet build` and is exercised by
  `game/Thaivia.Core.Tests` (`dotnet test`, 35 tests, all passing — see
  `docs/evidence/g2-dotnet-build.log` / `g2-dotnet-test.log`). This is
  the MapPack data contract, its strict loader/validator, the
  `SourceValue<T>` tri-state, coordinate narrowing, and road-graph
  traversal primitives — everything the spec's "pure simulation core"
  requirement calls for that does not itself need `UnityEngine`.
- `game/Assets/Scripts/Core/Thaivia.Core.asmdef` — a hand-written Unity
  assembly-definition file. It is plain JSON, not Editor-generated
  metadata, and it is the mechanism (`"noEngineReferences": true`) that
  will make the Unity compiler itself refuse a `UnityEngine` reference
  from this same source tree once a human opens the project — it does
  not, by itself, imply the Editor has ever run.
- `game/Thaivia.Core.Tests/Fixtures/*.synthetic.mappack.json` — real
  output of `thaivia build` against
  `tests/fixtures/synthetic/{road_graph_layers,multipart_building}.synthetic.osm.xml`
  (see `docs/evidence/g2-fixture-generation.log`). Both files carry
  `payload.provenance.synthetic: true` and a `synthetic_notice`; never
  mistake either for pilot data.

### Written, but **not compiled, not tested, not run** (`Assets/Scripts/Runtime/`)

Every file under `game/Assets/Scripts/Runtime/` references `UnityEngine`
(and, for input, `UnityEngine.InputSystem`) and starts with a
`// UNCOMPILED` comment for exactly that reason —
`Thaivia.Core.Tests.NoUnityEngineReferenceTests` checks that marker is
present on every file in that folder on every `dotnet test` run, so this
claim stays true as the code changes. These files were written by
reading `Thaivia.Core`'s real, compiled API and hand-tracing the calls a
MonoBehaviour would need; **no compiler has ever checked them.** Treat
every one as a first draft:

- `MapPackLoaderBehaviour.cs` — loads a MapPack via
  `Thaivia.Core.Serialization.MapPackLoader` and exposes the resulting
  `WorldState`.
- `Rendering/{Road,Building,Water}MeshBuilder.cs` — build Unity meshes
  from `GeographyBase`/`RoadGraph`. Roof/floor triangulation for
  buildings and water is a naive fan (documented in-file); a human
  should replace it with a real polygon triangulator before it is
  trusted on concave or multi-hole rings.
- `Camera/OrthoObliqueCameraRig.cs` — one orthographic camera, top-down
  and oblique pitch over the SAME scene geometry.
- `Input/PanPinchController.cs` — Input System pan/pinch (touch) with a
  mouse-drag/scroll fallback for Editor testing.
- `Selection/FeatureSelectionController.cs` — raycast feature pick;
  never mutates the picked feature (there is nothing to call that would
  let it — see `Thaivia.Core`'s immutability guarantees).
- `UI/InspectorPanelController.cs` — renders source / unknown / assumed
  as three visually distinct rows, driven entirely by
  `SourceValue<T>.Match(...)`.
- `UI/AttributionOverlay.cs`, `UI/SimulationDisclaimerLabel.cs` — the
  always-on OSM/ODbL notice and the "this is simulation" label.
- `Assets/Scripts/Runtime/Thaivia.Runtime.asmdef` — references
  `Thaivia.Core` and the Input System packages this code assumes exist;
  also never validated by an Editor.

### `game/Packages/`

Placeholder only — see `game/Packages/README.md` for the exact packages
this Runtime code assumes and the human step to generate the real
`manifest.json`/`packages-lock.json`.

## What a human with a licensed Unity install needs to do

1. Install Unity 6.3 LTS (confirm the current patch on unity.com/releases
   at install time) via Unity Hub.
2. Open `game/` as the project root. The Editor will generate
   `ProjectSettings/`, `Packages/manifest.json` /
   `packages-lock.json`, `.vsconfig`, and per-asset `.meta` files —
   **none of that is faked here**; it does not exist in this repo yet
   because no Editor has run.
3. Add the Input System package (`com.unity.inputsystem`) so
   `Assets/Scripts/Runtime/Input/PanPinchController.cs` resolves.
4. Fix whatever the Editor Console reports on first compile — these
   Runtime files have never been checked by a compiler.
5. Wire up scene objects (camera, canvas/UI, a MapPack file under
   `StreamingAssets/`) and confirm `G2-02`..`G2-07` in `TASKS.json`
   against a real MapPack.

## Android / iOS / phone / tablet

Not attempted in this wave, same as prior waves — see `TASKS.json`
(`G2-06`, `G2-07`) and `docs/progress.md` for the smallest human step
per platform (a licensed Unity install for the Editor itself; a macOS
machine + Xcode for iOS; Android SDK/adb + a device for Android). All
remain `not_run`.
