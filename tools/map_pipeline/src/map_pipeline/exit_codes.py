"""Shared process exit codes for the `thaivia` CLI.

Kept in one place so tests and docs can reference a stable contract
instead of magic numbers scattered across subcommands.
"""

OK = 0
GENERAL_ERROR = 1
USAGE_ERROR = 2
# A subcommand exists, parses its arguments correctly, but its real
# behavior has not been implemented yet. This is an honest status, never
# a fake success (exit 0) and never confused with a runtime failure
# (exit 1).
NOT_IMPLEMENTED = 3
