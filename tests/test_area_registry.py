"""G6-01: the map_pipeline CLI generalizes from one hardcoded pilot area
to N declared areas via `--area <id>` and `configs/areas/index.json`.

Everything here runs against synthetic fixtures inside a tmp repo root
(never presented as real coverage -- AGENTS.md rule 2). The pilot area's
own existing behavior (no `--area` flag at all) is proven unchanged by
`test_pipeline_end_to_end.py`; this file proves the *generalization*:
at least two independently registered areas, addressed by id, each
producing their own map_id-namespaced source lock and MapPack, without
interfering with each other or with the unchanged default path.
"""

from __future__ import annotations

import json
import shutil

import pytest

from map_pipeline import exit_codes
from map_pipeline.cli import main
from map_pipeline.config import ConfigError, load_area, registered_area_ids


SECOND_AREA_CONFIG = {
    "$comment": (
        "Test-only second registered area for G6-01 CLI-generalization coverage. "
        "Not a real candidate area (that is G6-02) and not real OSM data."
    ),
    "map_id": "th-test-area-alpha",
    "editable_bbox_wgs84_lonlat": [100.001, 13.001, 100.002, 13.002],
    "context_buffer_m": 300,
    "candidate_projected_crs": "EPSG:32647",
    "driving_side": "left",
    "coverage_status": "UNVERIFIED",
    "coverage_status_reason": "Test-only fixture area; never acquired from a real source.",
    "acquire_budget": {
        "max_download_bytes": 26214400,
        "connect_timeout_s": 10,
        "read_timeout_s": 60,
        "max_retries": 3,
        "retry_backoff_s": [2, 4, 8],
        "honor_retry_after_header": True,
        "max_disk_bytes_cache": 209715200,
        "require_human_approval_above_bytes": 104857600,
    },
}

THIRD_AREA_CONFIG = {
    **SECOND_AREA_CONFIG,
    "$comment": "Test-only third registered area for G6-01 CLI-generalization coverage.",
    "map_id": "th-test-area-beta",
    "editable_bbox_wgs84_lonlat": [100.003, 13.003, 100.004, 13.004],
}


@pytest.fixture
def fake_repo_root(tmp_path, repo_root):
    """Same shape as test_pipeline_end_to_end.py's fixture, plus a
    `configs/areas/index.json` registering two extra test-only areas
    alongside the real th-bkk-pilot-001 config."""
    root = tmp_path / "fakerepo"
    (root / "configs" / "areas").mkdir(parents=True)
    shutil.copy(repo_root / "configs" / "pilot-area.json", root / "configs" / "pilot-area.json")
    shutil.copy(repo_root / "configs" / "sources.json", root / "configs" / "sources.json")
    (root / "configs" / "areas" / "test-area-alpha.json").write_text(
        json.dumps(SECOND_AREA_CONFIG, indent=2), encoding="utf-8"
    )
    (root / "configs" / "areas" / "test-area-beta.json").write_text(
        json.dumps(THIRD_AREA_CONFIG, indent=2), encoding="utf-8"
    )
    (root / "configs" / "areas" / "index.json").write_text(
        json.dumps(
            {
                "areas": [
                    {"id": "th-bkk-pilot-001", "config_path": "configs/pilot-area.json"},
                    {"id": "test-area-alpha", "config_path": "configs/areas/test-area-alpha.json"},
                    {"id": "test-area-beta", "config_path": "configs/areas/test-area-beta.json"},
                ]
            },
            indent=2,
        ),
        encoding="utf-8",
    )
    (root / "tools" / "map_pipeline").mkdir(parents=True)
    return root


def test_registered_area_ids_lists_all_three(fake_repo_root, monkeypatch):
    monkeypatch.setenv("THAIVIA_REPO_ROOT", str(fake_repo_root))
    ids = registered_area_ids()
    assert ids == ["th-bkk-pilot-001", "test-area-alpha", "test-area-beta"]


def test_load_area_by_id_resolves_the_right_config_regardless_of_default_config_path(
    fake_repo_root, monkeypatch
):
    monkeypatch.setenv("THAIVIA_REPO_ROOT", str(fake_repo_root))
    cfg = load_area("test-area-alpha").data
    assert cfg["map_id"] == "th-test-area-alpha"

    cfg2 = load_area("test-area-beta").data
    assert cfg2["map_id"] == "th-test-area-beta"

    # Omitting area_id ignores the registry entirely and keeps loading
    # the default --config path -- the unchanged G1-G5 contract.
    default_cfg = load_area(None).data
    assert default_cfg["map_id"] == "th-bkk-pilot-001"


def test_unknown_area_id_is_a_clear_config_error_not_a_silent_fallback(fake_repo_root, monkeypatch):
    monkeypatch.setenv("THAIVIA_REPO_ROOT", str(fake_repo_root))
    with pytest.raises(ConfigError) as exc_info:
        load_area("no-such-area")
    message = str(exc_info.value)
    assert "no-such-area" in message
    assert "th-bkk-pilot-001" in message  # names what IS registered


@pytest.mark.parametrize(
    ("area_id", "expected_map_id"),
    [
        ("test-area-alpha", "th-test-area-alpha"),
        ("test-area-beta", "th-test-area-beta"),
    ],
)
def test_full_pipeline_via_cli_area_flag_for_two_distinct_registered_areas(
    fake_repo_root, synthetic_fixtures_dir, monkeypatch, area_id, expected_map_id
):
    """acquire -> build -> audit through `--area <id>` for two different
    registered (non-pilot) areas, each producing its own map_id-scoped
    source lock and MapPack -- proving the CLI is genuinely
    area-parameterized, not just accepting-and-ignoring the flag."""
    monkeypatch.setenv("THAIVIA_REPO_ROOT", str(fake_repo_root))
    fixture_path = synthetic_fixtures_dir / "tiny_multipolygon.synthetic.osm.xml"

    rc = main(["acquire", "--area", area_id, "--from-local-file", str(fixture_path)])
    assert rc == exit_codes.OK

    lock_path = fake_repo_root / "data" / "cache" / f"{expected_map_id}.sourcelock.json"
    assert lock_path.is_file(), f"acquire --area {area_id} must write a lock scoped to {expected_map_id}"

    rc = main(["build", "--area", area_id])
    assert rc == exit_codes.OK

    mappack_dir = fake_repo_root / "content" / "mappacks" / expected_map_id
    mappack_files = list(mappack_dir.rglob("*.mappack.json"))
    assert len(mappack_files) == 1, f"build --area {area_id} must bake exactly one MapPack under {mappack_dir}"

    pack = json.loads(mappack_files[0].read_text(encoding="utf-8"))
    assert pack["payload"]["manifest"]["map_id"] == expected_map_id
    assert pack["payload"]["provenance"]["synthetic"] is True

    audit_out = fake_repo_root / "docs" / "data" / f"{area_id}-audit.md"
    rc = main(["audit", "--area", area_id, "--pack", str(mappack_files[0]), "--out", str(audit_out)])
    assert rc == exit_codes.OK
    assert audit_out.is_file()
    assert expected_map_id in audit_out.read_text(encoding="utf-8")


def test_two_registered_areas_do_not_collide_when_acquired_in_the_same_cache_dir(
    fake_repo_root, synthetic_fixtures_dir, monkeypatch
):
    """Acquiring both non-pilot areas back to back must leave two
    independent, correctly-namespaced source locks -- the second
    acquire must not silently overwrite or get confused with the first."""
    monkeypatch.setenv("THAIVIA_REPO_ROOT", str(fake_repo_root))
    fixture_path = synthetic_fixtures_dir / "tiny_multipolygon.synthetic.osm.xml"

    assert main(["acquire", "--area", "test-area-alpha", "--from-local-file", str(fixture_path)]) == exit_codes.OK
    assert main(["acquire", "--area", "test-area-beta", "--from-local-file", str(fixture_path)]) == exit_codes.OK

    cache = fake_repo_root / "data" / "cache"
    assert (cache / "th-test-area-alpha.sourcelock.json").is_file()
    assert (cache / "th-test-area-beta.sourcelock.json").is_file()

    assert main(["build", "--area", "test-area-alpha"]) == exit_codes.OK
    assert main(["build", "--area", "test-area-beta"]) == exit_codes.OK

    alpha_packs = list((fake_repo_root / "content" / "mappacks" / "th-test-area-alpha").rglob("*.mappack.json"))
    beta_packs = list((fake_repo_root / "content" / "mappacks" / "th-test-area-beta").rglob("*.mappack.json"))
    assert len(alpha_packs) == 1
    assert len(beta_packs) == 1


def test_default_behavior_without_area_flag_never_touches_the_area_registry(tmp_path, repo_root, monkeypatch):
    """The back-compat contract: a repo root with NO configs/areas/ at
    all (exactly what test_pipeline_end_to_end.py's fake repo looks
    like) must keep working when --area is not passed."""
    root = tmp_path / "fakerepo_no_registry"
    (root / "configs").mkdir(parents=True)
    shutil.copy(repo_root / "configs" / "pilot-area.json", root / "configs" / "pilot-area.json")
    shutil.copy(repo_root / "configs" / "sources.json", root / "configs" / "sources.json")
    (root / "tools" / "map_pipeline").mkdir(parents=True)
    monkeypatch.setenv("THAIVIA_REPO_ROOT", str(root))

    assert not (root / "configs" / "areas").exists()
    cfg = load_area(None).data
    assert cfg["map_id"] == "th-bkk-pilot-001"
