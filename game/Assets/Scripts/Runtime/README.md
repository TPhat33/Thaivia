# Runtime (Unity-dependent, UNCOMPILED)

Every `.cs` file in this folder (and its subfolders) references
`UnityEngine` and/or `UnityEngine.InputSystem`. **None of it has ever
been compiled**, because this environment has no Unity Editor/Hub/
license and `download.unity3d.com` is blocked (see
`docs/decisions/0002-unity-unverifiable-in-this-environment.md`). The
first line of every file here says `// UNCOMPILED` for the same reason,
and `Thaivia.Core.Tests.NoUnityEngineReferenceTests` checks that marker
is present on every file under this folder on every `dotnet test` run.

These files were written by reading the MapPack contract in
`Thaivia.Core` and by hand-tracing the calls that would be needed; they
have NOT been validated by a compiler, an Editor Console, or a play-mode
run. Treat every one of them as a first draft a human must open in Unity
and actually build before trusting it. See `game/README.md` for the
full honesty statement and the exact human steps needed to get a real
build.

Layout:

- `MapPackLoaderBehaviour.cs` — loads a MapPack file via
  `Thaivia.Core.Serialization.MapPackLoader` and exposes the resulting
  `WorldState` to the rest of the scene.
- `Rendering/` — builds Unity meshes from `GeographyBase`/`RoadGraph`
  (roads as extruded polylines, buildings as extruded polygons, water as
  flat polygons), all through `Thaivia.Core.Coordinates.CoordinateNarrowing`
  for the float64->float32 step.
- `Camera/OrthoObliqueCameraRig.cs` — top-down and oblique views of the
  same scene geometry.
- `Input/PanPinchController.cs` — Input System pan/pinch handling.
- `Selection/FeatureSelectionController.cs` — raycast-based feature pick
  that never mutates geometry.
- `UI/` — the inspector panel (source vs. unknown vs. assumption, three
  distinct visual treatments), the OSM/ODbL attribution overlay, and the
  "this is simulation, not real socio-economic data" label.
