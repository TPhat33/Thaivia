"""No synthetic fixture may ever be labelled, or usable as, a real source.

Enforces the project rule (see AGENTS.md and IMPLEMENTATION_PLAN.th.md
section "การทดสอบและหลักฐาน"): synthetic fixtures are fine for unit
tests, but must carry an explicit synthetic marker and must never claim
real OSM provenance.
"""

from __future__ import annotations

import json
from pathlib import Path

FORBIDDEN_REAL_SOURCE_MARKERS = (
    "openstreetmap.org",
    "geofabrik.de",
    "overpass-api.de",
)


def _iter_fixture_files(synthetic_fixtures_dir: Path):
    for path in sorted(synthetic_fixtures_dir.rglob("*")):
        if path.is_file() and path.name != ".gitkeep":
            yield path


def test_synthetic_fixtures_directory_is_not_empty(synthetic_fixtures_dir):
    files = list(_iter_fixture_files(synthetic_fixtures_dir))
    assert files, "expected at least one fixture under tests/fixtures/synthetic/"


def test_every_json_fixture_declares_synthetic_true(synthetic_fixtures_dir):
    json_files = [p for p in _iter_fixture_files(synthetic_fixtures_dir) if p.suffix in (".json", ".geojson")]
    assert json_files, "expected at least one JSON/GeoJSON synthetic fixture"
    for path in json_files:
        with path.open("r", encoding="utf-8") as fh:
            data = json.load(fh)
        assert data.get("synthetic") is True, f"{path} must set top-level \"synthetic\": true"
        notice = data.get("synthetic_notice", "")
        assert notice, f"{path} must carry a non-empty synthetic_notice"


def test_every_xml_fixture_has_synthetic_header_comment(synthetic_fixtures_dir):
    xml_files = [p for p in _iter_fixture_files(synthetic_fixtures_dir) if p.suffix == ".xml"]
    for path in xml_files:
        head = path.read_text(encoding="utf-8")[:2000]
        assert "SYNTHETIC FIXTURE" in head or "synthetic=" in head, (
            f"{path} must carry a leading SYNTHETIC FIXTURE marker comment"
        )
        assert ".synthetic." in path.name or "synthetic" in path.name, (
            f"{path} filename should self-identify as synthetic"
        )


def test_no_fixture_claims_real_osm_provenance(synthetic_fixtures_dir):
    for path in _iter_fixture_files(synthetic_fixtures_dir):
        text = path.read_text(encoding="utf-8")
        for marker in FORBIDDEN_REAL_SOURCE_MARKERS:
            assert marker not in text, (
                f"{path} must not reference a real source domain ({marker}); "
                "synthetic fixtures must never be dressed up as real data"
            )
