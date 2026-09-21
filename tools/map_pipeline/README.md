# map_pipeline

Python tooling for the Thailand Urban Repair real-OSM map pipeline
(`acquire -> source lock/hash -> buffered extract -> entity parse ->
project -> normalize -> graph -> audit -> MapPack -> Unity load`).

Status (G1): every subcommand is fully implemented -- `doctor`,
`acquire` (`--from-url` network path, `--from-local-file` offline
path), `build`, `audit`, `verify`. The pipeline itself (parse/project/
normalize/graph/boundary/bake) is complete and unit-tested against
`tests/fixtures/synthetic/`. No real OSM source has been acquired yet
(every configured endpoint is policy-blocked; see ADR-0003), so no real
MapPack for `th-bkk-pilot-001` exists -- only synthetic-derived packs,
which are labelled unmistakably as such in `provenance.synthetic`.

See repository root `docs/environment.md`, `docs/decisions/`, and
`docs/progress.md` for the full G0 audit and rationale (in Thai, per
project documentation policy; code and CLI output stay in English).

## Install (editable, inside the repo venv)

```sh
python3 -m venv .venv
./.venv/bin/pip install -e tools/map_pipeline[dev]
```

## Run

```sh
./.venv/bin/thaivia doctor
./.venv/bin/python -m map_pipeline doctor

# Offline path (no network needed), once you have a licensed snapshot:
./.venv/bin/thaivia acquire --from-local-file /path/to/snapshot.osm.pbf
./.venv/bin/thaivia build
./.venv/bin/thaivia audit --pack content/mappacks/<map_id>/<hash>.mappack.json
./.venv/bin/thaivia verify --pack content/mappacks/<map_id>/<hash>.mappack.json
```
