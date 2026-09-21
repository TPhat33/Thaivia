"""`thaivia` CLI entry point.

Subcommands: doctor, acquire, build, audit, verify.
Only `doctor` has real behavior in G0; the others parse arguments for
real and exit with an explicit not-implemented status (see
map_pipeline.commands.not_implemented).
"""

from __future__ import annotations

import argparse
from collections.abc import Sequence

from map_pipeline import doctor, exit_codes
from map_pipeline.commands import acquire, audit, build, verify


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="thaivia",
        description=(
            "Thailand Urban Repair map pipeline CLI. "
            "acquire -> build -> audit -> verify, plus `doctor` for environment health."
        ),
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    doctor_p = subparsers.add_parser("doctor", help="Check environment/toolchain health")
    doctor_p.add_argument(
        "--probe-network",
        action="store_true",
        help=(
            "Live-probe configured OSM source endpoints instead of reporting the cached "
            "status from configs/sources.json (re-attempts hosts already denied by policy)"
        ),
    )
    doctor_p.set_defaults(func=doctor.main)

    acquire.add_subparser(subparsers)
    build.add_subparser(subparsers)
    audit.add_subparser(subparsers)
    verify.add_subparser(subparsers)

    return parser


def main(argv: Sequence[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    func = getattr(args, "func", None)
    if func is None:  # pragma: no cover - argparse `required=True` prevents this
        parser.print_help()
        return exit_codes.USAGE_ERROR
    return func(args)


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(main())
