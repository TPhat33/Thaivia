# Pilot area data audit -- th-bkk-pilot-001 (SYNTHETIC -- NOT the real pilot area)

**status: synthetic-derived -- NOT real OSM data, does not represent any real place in Thailand, must never be mistaken for the real pilot.**

> Derived from a tests/fixtures/synthetic/ fixture. NOT real OSM data and does not represent any real place in Thailand; never treat as a pilot pack.

MapPack `content_hash`: `sha256:6e52b443adcaa1ce25a0030d5d31299d5e23ed7763c95fe02d458978fdeff433`

Baked at: `2026-09-21T03:03:22.980689+00:00`

## Source snapshot

- Source kind: `local_file`
- Source location: `tests/fixtures/synthetic/tiny_multipolygon.synthetic.osm.xml`
- Retrieval time: `2026-09-21T03:03:22.394770+00:00`
- Source snapshot time: `None`
- Source snapshot unknown reason: `source snapshot time not derivable from this file/transfer; OSM PBF/XML extracts do not always carry a reliable snapshot timestamp header`
- SHA-256: `82e2c6c012483cde3d170fb112d6c050abd666c6ee03f5e4d9b463d4ad309106`
- Size: 1407 bytes
- Importer version: `0.1.0`
- Schema version: `0.1.0`
- Settings hash: `c9af71bb5a7d2579734e3e249345b90017a657b76baf5bf30deeb01c409156e4`
- CRS: `EPSG:32647`

## Feature inventory (not a "coverage %" -- there is no ground truth)

| Type | Count |
|---|---|
| barriers | 0 |
| building_parts | 0 |
| buildings | 1 |
| gateways | 0 |
| road_edges | 0 |
| road_nodes | 0 |
| turn_restrictions_supported | 0 |
| water_polygons | 0 |
| waterways | 0 |

## Known/unknown attribute counts

| Attribute | Unknown count |
|---|---|
| building_height | 1 |
| building_use | 1 |
| road_maxspeed | 0 |
| road_width_or_lanes | 0 |

## Round-trip geometry error (measured, not asserted)

max=0.005922 m, mean=0.004848 m, sample_count=8 (budget: <= 0.10 m)

## Topology conflicts / dropped / quarantined / incomplete relations / unsupported restrictions

### Topology conflicts (0)

(none)

### Dropped features (0)

(none)

### Quarantined features (0)

(none)

### Incomplete relations (0)

(none)

### Unsupported turn restrictions (0)

(none)

### Duplicate POI/building associations (0)

(none)

## Retained vs rejected

- Retained: `{'barriers': 0, 'building_parts': 0, 'buildings': 1, 'gateways': 0, 'road_edges': 0, 'road_nodes': 0, 'turn_restrictions_supported': 0, 'water_polygons': 0, 'waterways': 0}`
- Rejected: `{'dropped_features': 0, 'incomplete_relations': 0, 'topology_conflicts': 0, 'unsupported_restrictions': 0}`

## Attribution

© OpenStreetMap contributors -- [ODbL](https://opendatacommons.org/licenses/odbl/) -- [copyright/attribution](https://www.openstreetmap.org/copyright)

## Verdict

This audit was generated from a synthetic fixture for pipeline testing. It does NOT establish that the real th-bkk-pilot-001 crop is usable; that still requires a real acquired source (see docs/decisions/0003).
