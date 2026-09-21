"""`thaivia verify` -- re-derive a MapPack from the same source+settings
and assert a byte-identical payload hash; validate against the schema;
check the source lock still matches. Exits non-zero on any mismatch.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from map_pipeline import exit_codes
from map_pipeline.config import ConfigError, load_pilot_area, validate_against_schema
from map_pipeline.paths import cache_dir, schemas_dir
from map_pipeline.pipeline.mappack import run_pipeline
from map_pipeline.pipeline.osm_parse import parse_osm_file
from map_pipeline.pipeline.sourcelock import (
    canonical_lock_filename,
    read_source_lock,
    sha256_file,
    source_bytes_filename,
)


def add_subparser(subparsers: argparse._SubParsersAction) -> None:
    p = subparsers.add_parser("verify", help="Verify a MapPack's integrity and re-derivation determinism")
    p.add_argument("--pack", required=True, help="Path to a baked MapPack JSON file")
    p.add_argument(
        "--config",
        default="configs/pilot-area.json",
        help="Path to pilot-area config (default: configs/pilot-area.json)",
    )
    p.set_defaults(func=run)


def run(args) -> int:
    pack_path = Path(args.pack)
    if not pack_path.is_file():
        print(f"thaivia verify: MapPack not found: {pack_path}")
        return exit_codes.GENERAL_ERROR
    pack = json.loads(pack_path.read_text(encoding="utf-8"))

    schema_path = schemas_dir() / "mappack.schema.json"
    try:
        validate_against_schema(pack, schema_path)
    except ConfigError as exc:
        print(f"thaivia verify: schema validation FAILED: {exc}")
        return exit_codes.GENERAL_ERROR
    print("thaivia verify: schema validation OK")

    try:
        cfg = load_pilot_area().data
    except ConfigError as exc:
        print(f"thaivia verify: config error: {exc}")
        return exit_codes.GENERAL_ERROR

    map_id = pack["payload"]["manifest"]["map_id"]
    cdir = cache_dir()
    lock_path = cdir / canonical_lock_filename(map_id)
    if not lock_path.is_file():
        print(f"thaivia verify: no source lock at {lock_path}; cannot re-derive to check determinism.")
        return exit_codes.GENERAL_ERROR
    lock = read_source_lock(lock_path)

    prov = pack["payload"]["provenance"]
    if lock.sha256 != prov["sha256"]:
        print(
            f"thaivia verify: MISMATCH -- source lock sha256 ({lock.sha256}) does not match the "
            f"pack's provenance.sha256 ({prov['sha256']}); the pack was not built from the current lock."
        )
        return exit_codes.GENERAL_ERROR
    print("thaivia verify: source lock sha256 matches pack provenance")

    bytes_path = cdir / source_bytes_filename(map_id, lock.sha256, lock.source_location)
    if not bytes_path.exists():
        print(f"thaivia verify: source bytes not found at {bytes_path}; cannot re-derive.")
        return exit_codes.GENERAL_ERROR
    actual_sha, _ = sha256_file(bytes_path)
    if actual_sha != lock.sha256:
        print(f"thaivia verify: source bytes at {bytes_path} have drifted from the lock's sha256.")
        return exit_codes.GENERAL_ERROR

    dataset = parse_osm_file(str(bytes_path))
    synthetic = bool(prov.get("synthetic"))
    rederived = run_pipeline(dataset, cfg, lock, synthetic=synthetic)

    if rederived["content_hash"] != pack["content_hash"]:
        print("thaivia verify: MISMATCH -- re-derived content_hash differs from the pack on disk.")
        print(f"  on-disk:    {pack['content_hash']}")
        print(f"  re-derived: {rederived['content_hash']}")
        return exit_codes.GENERAL_ERROR

    print(f"thaivia verify: OK -- re-derivation is byte-identical (content_hash={pack['content_hash']})")
    return exit_codes.OK
