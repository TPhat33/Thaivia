"""Shared behavior for G0 stub subcommands (acquire, build, audit, verify).

Each of these subcommands is real: it is registered on the CLI, it
parses its own arguments, and it prints an honest status. None of them
fake a successful run. Real behavior lands in G1 (acquire/build/audit)
and G2 (verify, once a Unity MapPack loader exists).
"""

from __future__ import annotations

from map_pipeline import exit_codes


def not_implemented(command: str, gate: str, blocker: str) -> int:
    print(f"thaivia {command}: not implemented yet ({gate}).")
    print(f"Blocked on: {blocker}")
    print("See docs/decisions/ and docs/progress.md for current status.")
    return exit_codes.NOT_IMPLEMENTED
