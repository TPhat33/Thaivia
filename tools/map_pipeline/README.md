# map_pipeline

Python tooling for the Thailand Urban Repair real-OSM map pipeline
(`acquire -> source lock/hash -> buffered extract -> entity parse ->
project -> normalize -> graph -> audit -> MapPack -> Unity load`).

Status (G0): only the `doctor` subcommand is fully functional. `acquire`,
`build`, `audit` and `verify` exist as real CLI subcommands that parse
arguments and exit with an explicit "not implemented yet (G1)" status.
They do not fabricate success.

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
```
