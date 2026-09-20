"""CLI smoke tests: every subcommand parses and exits with an honest code.

`doctor` must exit 0 with no hard problems in this environment (deps
installed, configs valid). `acquire`/`build`/`audit`/`verify` must exit
with the shared NOT_IMPLEMENTED code, never 0 (never a fake success).
"""

from __future__ import annotations

import pytest

from map_pipeline import exit_codes
from map_pipeline.cli import main


def test_doctor_exits_zero_when_environment_is_healthy():
    rc = main(["doctor"])
    assert rc == exit_codes.OK


@pytest.mark.parametrize("command", ["acquire", "build", "audit", "verify"])
def test_stub_commands_exit_not_implemented_never_fake_success(command, capsys):
    rc = main([command])
    assert rc == exit_codes.NOT_IMPLEMENTED
    assert rc != exit_codes.OK
    out = capsys.readouterr().out
    assert "not implemented" in out.lower()


def test_no_command_is_usage_error():
    with pytest.raises(SystemExit) as exc_info:
        main([])
    # argparse's `required=True` subparsers raise SystemExit(2) directly.
    assert exc_info.value.code == exit_codes.USAGE_ERROR


def test_unknown_command_is_usage_error():
    with pytest.raises(SystemExit) as exc_info:
        main(["not-a-real-command"])
    assert exc_info.value.code == exit_codes.USAGE_ERROR
