"""`thaivia build` -- parse/project/normalize/graph/bake a MapPack from
an acquired OSM source (see `thaivia acquire`).

Re-verifies the pinned SHA-256 against the bytes currently on disk
before trusting them (a source lock's whole point is to catch drift),
runs the full pure-logic pipeline in `map_pipeline.pipeline.mappack`,
validates the result against the committed MapPack JSON Schema, and
writes it to `content/mappacks/<map_id>/<content_hash>.mappack.json`
(gitignored -- generated output, never committed).
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from map_pipeline import exit_codes
from map_pipeline.config import ConfigError, load_pilot_area, validate_against_schema
from map_pipeline.paths import cache_dir, repo_root, schemas_dir
from map_pipeline.pipeline.mappack import run_pipeline
from map_pipeline.pipeline.osm_parse import parse_osm_file
from map_pipeline.pipeline.sourcelock import (
    canonical_lock_filename,
    read_source_lock,
    sha256_file,
    source_bytes_filename,
)


def add_subparser(subparsers: argparse._SubParsersAction) -> None:
    p = subparsers.add_parser("build", help="Build a MapPack from an acquired OSM source")
    p.add_argument(
        "--config",
        default="configs/pilot-area.json",
        help="Path to pilot-area config (default: configs/pilot-area.json)",
    )
    p.add_argument(
        "--source-lock",
        default=None,
        help="Explicit source lock file (default: data/cache/<map_id>.sourcelock.json)",
    )
    p.add_argument(
        "--out",
        default=None,
        help="Output MapPack path (default: content/mappacks/<map_id>/<content_hash>.mappack.json)",
    )
    p.set_defaults(func=run)


def run(args) -> int:
    try:
        cfg = load_pilot_area().data
    except ConfigError as exc:
        print(f"thaivia build: config error: {exc}")
        return exit_codes.GENERAL_ERROR

    map_id = cfg["map_id"]
    cdir = cache_dir()
    lock_path = Path(args.source_lock) if args.source_lock else cdir / canonical_lock_filename(map_id)
    if not lock_path.is_file():
        print(f"thaivia build: no source lock at {lock_path}.")
        print("  Run `thaivia acquire --from-local-file PATH` (or --from-url) first.")
        return exit_codes.GENERAL_ERROR
    lock = read_source_lock(lock_path)

    bytes_path = cdir / source_bytes_filename(map_id, lock.sha256, lock.source_location)
    if not bytes_path.exists():
        print(f"thaivia build: source bytes not found at {bytes_path}.")
        print(f"  The lock at {lock_path} does not have matching bytes under data/cache/; re-run `thaivia acquire`.")
        return exit_codes.GENERAL_ERROR

    actual_sha, _actual_size = sha256_file(bytes_path)
    if actual_sha != lock.sha256:
        print(
            f"thaivia build: source bytes at {bytes_path} no longer match the lock's sha256 "
            f"(lock={lock.sha256}, actual={actual_sha}). Refusing to build from drifted input."
        )
        return exit_codes.GENERAL_ERROR

    dataset = parse_osm_file(str(bytes_path))
    synthetic = "synthetic" in lock.source_location.replace("\\", "/").lower() or "tests/fixtures/synthetic" in str(
        bytes_path
    ).replace("\\", "/")
    pack = run_pipeline(dataset, cfg, lock, synthetic=synthetic)

    schema_path = schemas_dir() / "mappack.schema.json"
    try:
        validate_against_schema(pack, schema_path)
    except ConfigError as exc:
        print(f"thaivia build: baked MapPack FAILED schema validation: {exc}")
        return exit_codes.GENERAL_ERROR

    if args.out:
        out_path = Path(args.out)
    else:
        hash_short = pack["content_hash"].split(":", 1)[1][:16]
        out_path = repo_root() / "content" / "mappacks" / map_id / f"{hash_short}.mappack.json"
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(pack, indent=2, sort_keys=True) + "\n", encoding="utf-8")

    qr = pack["payload"]["quality_report"]
    print(f"thaivia build: MapPack baked OK -> {out_path}")
    print(f"  content_hash={pack['content_hash']}")
    print(f"  synthetic={synthetic}")
    print(f"  retained_counts={qr['retained_counts']}")
    print(f"  rejected_counts={qr['rejected_counts']}")
    print(f"  roundtrip_error_m={qr['roundtrip_error_m']}")
    print(f"  crs_validation_warnings={pack['payload']['manifest']['crs_validation_warnings']}")
    return exit_codes.OK
