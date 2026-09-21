"""CLI smoke tests: every subcommand parses and exits with an honest code.

`doctor` must exit 0 with no hard problems in this environment. As of
G1, `acquire`/`build`/`audit`/`verify` are real subcommands wired to the
pipeline -- this file checks that calling them with no real input still
never fakes success (exit 0) when there is nothing to acquire/build/
audit/verify yet. End-to-end success paths are covered by
`test_pipeline_end_to_end.py`.
"""

from __future__ import annotations

import shutil

import pytest

from map_pipeline import exit_codes
from map_pipeline.cli import main
from map_pipeline.paths import cache_dir


@pytest.fixture(autouse=True)
def _clean_cache():
    """Hermetic: these tests assert behavior for the "nothing acquired
    yet" state, so data/cache/ (gitignored, not part of a checkout) must
    not already contain a lock from a previous local run."""
    d = cache_dir()
    shutil.rmtree(d, ignore_errors=True)
    yield
    shutil.rmtree(d, ignore_errors=True)


def test_doctor_exits_zero_when_environment_is_healthy():
    rc = main(["doctor"])
    assert rc == exit_codes.OK


def test_doctor_probe_network_flag_is_accepted():
    # Does not assert on network outcome (that is environment-dependent
    # and policy-blocked here); only that the opt-in flag is wired and
    # doctor still completes without crashing.
    rc = main(["doctor", "--probe-network"])
    assert rc == exit_codes.OK


def test_acquire_with_no_source_flag_is_usage_error_never_fake_success(capsys):
    rc = main(["acquire"])
    assert rc == exit_codes.USAGE_ERROR
    assert rc != exit_codes.OK
    out = capsys.readouterr().out
    assert "--from-url" in out
    assert "--from-local-file" in out


def test_acquire_from_local_file_missing_path_is_general_error(capsys):
    rc = main(["acquire", "--from-local-file", "/nonexistent/path/does-not-exist.osm.xml"])
    assert rc == exit_codes.GENERAL_ERROR
    assert rc != exit_codes.OK


def test_build_with_no_acquired_source_is_general_error_never_fake_success(capsys):
    rc = main(["build"])
    assert rc == exit_codes.GENERAL_ERROR
    assert rc != exit_codes.OK
    out = capsys.readouterr().out
    assert "no source lock" in out.lower()


def test_audit_with_no_pack_leaves_placeholder_and_is_honest(capsys):
    rc = main(["audit"])
    assert rc == exit_codes.NOT_IMPLEMENTED
    assert rc != exit_codes.OK
    out = capsys.readouterr().out
    assert "no --pack given" in out.lower()


def test_verify_requires_pack_argument():
    with pytest.raises(SystemExit) as exc_info:
        main(["verify"])
    assert exc_info.value.code == exit_codes.USAGE_ERROR


def test_verify_with_missing_pack_file_is_general_error():
    rc = main(["verify", "--pack", "/nonexistent/pack.mappack.json"])
    assert rc == exit_codes.GENERAL_ERROR
    assert rc != exit_codes.OK


def test_no_command_is_usage_error():
    with pytest.raises(SystemExit) as exc_info:
        main([])
    # argparse's `required=True` subparsers raise SystemExit(2) directly.
    assert exc_info.value.code == exit_codes.USAGE_ERROR


def test_unknown_command_is_usage_error():
    with pytest.raises(SystemExit) as exc_info:
        main(["not-a-real-command"])
    assert exc_info.value.code == exit_codes.USAGE_ERROR
