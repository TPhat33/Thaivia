"""`thaivia verify` — verify a MapPack against its manifest hash/schema and
(optionally) confirm it loads in the Unity viewer contract.

G0 status: not implemented. There is no MapPack yet (see `thaivia
build`) and no Unity project in this repo yet (see docs/decisions/0002).
"""

from __future__ import annotations

import argparse

from map_pipeline.commands.not_implemented import not_implemented


def add_subparser(subparsers: argparse._SubParsersAction) -> None:
    p = subparsers.add_parser("verify", help="Verify a MapPack's integrity and load contract")
    p.add_argument(
        "--pack",
        default=None,
        help="Path to a MapPack directory (reserved for G1/G2)",
    )
    p.set_defaults(func=run)


def run(_args) -> int:
    return not_implemented(
        "verify",
        "G1/G2",
        "no MapPack exists yet, and no Unity project exists yet (see docs/decisions/0002)",
    )
