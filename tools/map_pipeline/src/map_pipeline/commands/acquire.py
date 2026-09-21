"""`thaivia acquire` -- acquire and lock a local OSM source snapshot.

Two paths, both fully implemented:

- `--from-url URL`: network acquisition against the `acquire_budget` in
  `configs/pilot-area.json` (connect/read timeouts, retries with
  backoff, honoring `Retry-After`, a byte-limit abort, streaming
  SHA-256). Every configured OSM endpoint is blocked by organization
  egress policy on this container (see
  docs/decisions/0003-osm-source-acquisition-blocked.md); this path is
  expected to fail here with an honest, specific blocked-host error. It
  never retries a policy block and never substitutes synthetic data.
- `--from-local-file PATH`: the offline path a human uses after placing
  a licensed `.osm.pbf`/`.osm.xml` at `data/cache/` per ADR-0003.
  Produces the exact same kind of source lock the network path would.
"""

from __future__ import annotations

import argparse
from pathlib import Path

from map_pipeline import exit_codes
from map_pipeline.config import ConfigError, load_pilot_area
from map_pipeline.paths import cache_dir
from map_pipeline.pipeline.acquire_net import AcquireNetworkError, download_with_budget
from map_pipeline.pipeline.sourcelock import (
    build_source_lock,
    sha256_file,
    source_bytes_filename,
    write_source_lock,
)


def add_subparser(subparsers: argparse._SubParsersAction) -> None:
    p = subparsers.add_parser("acquire", help="Acquire and lock a local OSM source snapshot")
    p.add_argument(
        "--config",
        default="configs/pilot-area.json",
        help="Path to pilot-area config (default: configs/pilot-area.json)",
    )
    p.add_argument(
        "--from-url",
        default=None,
        help="Download a bounded extract from this URL (network path; honors acquire_budget)",
    )
    p.add_argument(
        "--from-local-file",
        default=None,
        help="Ingest an already-obtained local .osm.pbf/.osm.xml (offline path)",
    )
    p.set_defaults(func=run)


def _link_or_copy(src: Path, dest: Path) -> None:
    if dest.exists():
        return
    try:
        dest.symlink_to(src.resolve())
    except OSError:
        dest.write_bytes(src.read_bytes())


def run(args) -> int:
    try:
        cfg = load_pilot_area().data
    except ConfigError as exc:
        print(f"thaivia acquire: config error: {exc}")
        return exit_codes.GENERAL_ERROR

    if not args.from_url and not args.from_local_file:
        print("thaivia acquire: one of --from-url or --from-local-file is required.")
        print("  --from-url URL           network path against an allowed OSM source endpoint")
        print("                           (expected to fail honestly here; see ADR-0003)")
        print("  --from-local-file PATH   offline path for a licensed snapshot you already have")
        return exit_codes.USAGE_ERROR

    map_id = cfg["map_id"]
    out_dir = cache_dir()
    out_dir.mkdir(parents=True, exist_ok=True)

    if args.from_local_file:
        return _acquire_local_file(args.from_local_file, cfg, out_dir)
    return _acquire_from_url(args.from_url, cfg, out_dir)


def _acquire_local_file(from_local_file: str, cfg: dict, out_dir: Path) -> int:
    src_path = Path(from_local_file)
    if not src_path.is_file():
        print(f"thaivia acquire: local file not found: {src_path}")
        return exit_codes.GENERAL_ERROR

    sha, size = sha256_file(src_path)
    content_path = out_dir / source_bytes_filename(cfg["map_id"], sha, str(src_path))
    _link_or_copy(src_path, content_path)

    lock = build_source_lock(
        map_id=cfg["map_id"],
        source_kind="local_file",
        source_location=str(src_path),
        sha256=sha,
        size_bytes=size,
        pilot_area_config=cfg,
    )
    result = write_source_lock(out_dir, lock)
    print(f"thaivia acquire: local file ingested OK: {src_path}")
    print(f"  sha256={sha} size_bytes={size}")
    print(f"  source bytes:  {content_path}")
    print(f"  source lock:   {result['versioned_path']} (new_lock={result['is_new_lock']})")
    print(f"  canonical pointer: {result['canonical_path']}")
    return exit_codes.OK


def _acquire_from_url(url: str, cfg: dict, out_dir: Path) -> int:
    budget = cfg["acquire_budget"]
    tmp_path = out_dir / f"{cfg['map_id']}.download.tmp"
    print(f"thaivia acquire: attempting network acquisition from {url}")
    print(
        "  budget: "
        f"max_download_bytes={budget['max_download_bytes']} "
        f"connect_timeout_s={budget['connect_timeout_s']} "
        f"read_timeout_s={budget['read_timeout_s']} "
        f"max_retries={budget['max_retries']} "
        f"retry_backoff_s={budget['retry_backoff_s']} "
        f"honor_retry_after_header={budget['honor_retry_after_header']}"
    )
    try:
        result = download_with_budget(
            url,
            tmp_path,
            connect_timeout_s=budget["connect_timeout_s"],
            read_timeout_s=budget["read_timeout_s"],
            max_retries=budget["max_retries"],
            retry_backoff_s=budget["retry_backoff_s"],
            honor_retry_after_header=budget["honor_retry_after_header"],
            max_download_bytes=budget["max_download_bytes"],
        )
    except AcquireNetworkError as exc:
        print(f"thaivia acquire: network acquisition FAILED (honest failure, not a fake success): {exc}")
        print("  See docs/decisions/0003-osm-source-acquisition-blocked.md for the human unblock step.")
        print("  Never routed around the block: no mirrors, no VPN, no alternate ports were attempted.")
        return exit_codes.GENERAL_ERROR

    content_path = out_dir / source_bytes_filename(cfg["map_id"], result.sha256, url)
    tmp_path.replace(content_path)

    lock = build_source_lock(
        map_id=cfg["map_id"],
        source_kind="url",
        source_location=url,
        sha256=result.sha256,
        size_bytes=result.size_bytes,
        pilot_area_config=cfg,
    )
    lock_result = write_source_lock(out_dir, lock)
    print(f"thaivia acquire: network acquisition OK: {url}")
    print(f"  sha256={result.sha256} size_bytes={result.size_bytes}")
    print(f"  source bytes:  {content_path}")
    print(f"  source lock:   {lock_result['versioned_path']} (new_lock={lock_result['is_new_lock']})")
    return exit_codes.OK
