"""End-to-end pipeline test: `acquire --from-local-file` -> `build` ->
`audit` -> `verify`, genuinely executed through the real CLI entry point
against a synthetic `.osm.xml` fixture, entirely inside a tmp directory
so it never touches the real repo's data/cache/ or content/mappacks/.
The resulting pack is unmistakably labelled synthetic-derived so it can
never be confused with the real th-bkk-pilot-001 pilot pack.
"""

from __future__ import annotations

import json
import shutil

import pytest

from map_pipeline import exit_codes
from map_pipeline.cli import main


@pytest.fixture
def fake_repo_root(tmp_path, repo_root):
    """A minimal directory that `map_pipeline.paths.resolve_repo_root()`
    accepts as a repo root (it only requires `configs/` and
    `tools/map_pipeline/` to exist), carrying real copies of the shipped
    configs so `acquire`/`build`/`audit`/`verify` run against the exact
    same pilot-area.json this repo ships, entirely under tmp_path."""
    root = tmp_path / "fakerepo"
    (root / "configs").mkdir(parents=True)
    shutil.copy(repo_root / "configs" / "pilot-area.json", root / "configs" / "pilot-area.json")
    shutil.copy(repo_root / "configs" / "sources.json", root / "configs" / "sources.json")
    (root / "tools" / "map_pipeline").mkdir(parents=True)
    return root


def test_full_pipeline_end_to_end_via_cli_in_a_tmp_dir(fake_repo_root, synthetic_fixtures_dir, monkeypatch):
    monkeypatch.setenv("THAIVIA_REPO_ROOT", str(fake_repo_root))

    fixture_path = synthetic_fixtures_dir / "tiny_multipolygon.synthetic.osm.xml"

    rc = main(["acquire", "--from-local-file", str(fixture_path)])
    assert rc == exit_codes.OK

    lock_path = fake_repo_root / "data" / "cache" / "th-bkk-pilot-001.sourcelock.json"
    assert lock_path.is_file(), "acquire must write the canonical source lock"
    assert lock_path.is_relative_to(fake_repo_root), "must stay inside the tmp dir, never the real repo"

    rc = main(["build"])
    assert rc == exit_codes.OK

    mappack_files = list((fake_repo_root / "content" / "mappacks").rglob("*.mappack.json"))
    assert len(mappack_files) == 1, "build must produce exactly one MapPack file"
    pack_path = mappack_files[0]
    assert pack_path.is_relative_to(fake_repo_root)

    pack = json.loads(pack_path.read_text(encoding="utf-8"))
    # Unmistakably labelled synthetic-derived.
    assert pack["payload"]["provenance"]["synthetic"] is True
    assert pack["payload"]["provenance"]["synthetic_notice"]
    assert "NOT real OSM data" in pack["payload"]["provenance"]["synthetic_notice"]
    assert pack["payload"]["geography_base"]["buildings"], "the multipolygon building must have been baked in"

    audit_out = fake_repo_root / "docs" / "data" / "pilot-audit.md"
    rc = main(["audit", "--pack", str(pack_path), "--out", str(audit_out)])
    assert rc == exit_codes.OK
    assert audit_out.is_file()
    audit_text = audit_out.read_text(encoding="utf-8")
    assert "SYNTHETIC" in audit_text
    assert "NOT the real pilot area" in audit_text

    rc = main(["verify", "--pack", str(pack_path)])
    assert rc == exit_codes.OK


def test_end_to_end_acquire_from_url_fails_honestly_without_retry_around_the_block(
    fake_repo_root, monkeypatch
):
    """Sanity check that the network path is real (not a stub) and fails
    for the right, honest reason against a real policy-blocked host,
    without silently falling back to synthetic data."""
    monkeypatch.setenv("THAIVIA_REPO_ROOT", str(fake_repo_root))
    rc = main(["acquire", "--from-url", "https://download.geofabrik.de/asia/thailand-latest.osm.pbf"])
    assert rc == exit_codes.GENERAL_ERROR
    assert rc != exit_codes.OK
    # No source lock and no MapPack must have been produced from this
    # failed attempt.
    assert not (fake_repo_root / "data" / "cache").exists() or not list(
        (fake_repo_root / "data" / "cache").glob("*.sourcelock.json")
    )
