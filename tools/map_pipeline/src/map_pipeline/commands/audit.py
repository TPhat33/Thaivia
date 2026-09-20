"""`thaivia audit` — produce docs/data/pilot-audit.md from a built MapPack.

G0 status: not implemented. A hand-written placeholder audit doc exists
at docs/data/pilot-audit.md, explicitly marked `status: blocked` with all
counts as `not_measured`; this subcommand will regenerate it for real
once `thaivia build` produces a MapPack.
"""

from __future__ import annotations

import argparse

from map_pipeline.commands.not_implemented import not_implemented


def add_subparser(subparsers: argparse._SubParsersAction) -> None:
    p = subparsers.add_parser("audit", help="Generate the pilot-area data audit report")
    p.add_argument(
        "--config",
        default="configs/pilot-area.json",
        help="Path to pilot-area config (default: configs/pilot-area.json)",
    )
    p.set_defaults(func=run)


def run(_args) -> int:
    return not_implemented(
        "audit",
        "G1",
        "depends on `thaivia build`, which has no MapPack to audit yet",
    )
