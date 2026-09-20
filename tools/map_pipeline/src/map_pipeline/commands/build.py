"""`thaivia build` — extract/parse/project/normalize/graph/bake a MapPack.

G0 status: not implemented. This depends on a real acquired OSM source
(see `thaivia acquire`), which is currently blocked.
"""

from __future__ import annotations

import argparse

from map_pipeline.commands.not_implemented import not_implemented


def add_subparser(subparsers: argparse._SubParsersAction) -> None:
    p = subparsers.add_parser("build", help="Build a MapPack from an acquired OSM source")
    p.add_argument(
        "--config",
        default="configs/pilot-area.json",
        help="Path to pilot-area config (default: configs/pilot-area.json)",
    )
    p.set_defaults(func=run)


def run(_args) -> int:
    return not_implemented(
        "build",
        "G1",
        "depends on `thaivia acquire`, which has no real source to build from yet",
    )
