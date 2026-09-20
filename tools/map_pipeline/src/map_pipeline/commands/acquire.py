"""`thaivia acquire` — download/lock a local OSM source snapshot.

G0 status: not implemented. All confirmed OSM source endpoints
(Geofabrik, Overpass) are blocked by the current organization egress
policy (see docs/environment.md and ADR-0003). This subcommand will not
fabricate a snapshot or silently substitute synthetic data as if it were
a real acquisition.
"""

from __future__ import annotations

import argparse

from map_pipeline.commands.not_implemented import not_implemented


def add_subparser(subparsers: argparse._SubParsersAction) -> None:
    p = subparsers.add_parser("acquire", help="Acquire and lock a local OSM source snapshot")
    p.add_argument(
        "--config",
        default="configs/pilot-area.json",
        help="Path to pilot-area config (default: configs/pilot-area.json)",
    )
    p.add_argument(
        "--force",
        action="store_true",
        help="(reserved for G1) re-download even if a cached snapshot exists",
    )
    p.set_defaults(func=run)


def run(_args) -> int:
    return not_implemented(
        "acquire",
        "G1",
        "all configured OSM source endpoints are blocked by egress policy; "
        "see docs/decisions/0003-osm-source-acquisition-blocked.md for the human unblock step",
    )
