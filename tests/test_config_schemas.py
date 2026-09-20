"""The shipped configs must validate against their JSON Schemas."""

from __future__ import annotations

import json
from pathlib import Path

import jsonschema
import pytest


def _load(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as fh:
        return json.load(fh)


@pytest.mark.parametrize(
    ("config_name", "schema_name"),
    [
        ("pilot-area.json", "pilot-area.schema.json"),
        ("sources.json", "sources.schema.json"),
    ],
)
def test_config_validates_against_schema(configs_dir, schemas_dir, config_name, schema_name):
    config = _load(configs_dir / config_name)
    schema = _load(schemas_dir / schema_name)
    jsonschema.validate(instance=config, schema=schema)


def test_pilot_area_matches_spec_defaults(configs_dir):
    config = _load(configs_dir / "pilot-area.json")
    assert config["map_id"] == "th-bkk-pilot-001"
    assert config["editable_bbox_wgs84_lonlat"] == [100.586, 13.710, 100.595, 13.719]
    assert config["context_buffer_m"] == 300
    assert config["candidate_projected_crs"] == "EPSG:32647"
    assert config["driving_side"] == "left"
    assert config["coverage_status"] == "UNVERIFIED"


def test_pilot_area_bbox_is_lon_lat_order_not_swapped(configs_dir):
    # Thailand: longitude ~90-110, latitude ~5-21. Guards against a
    # lon/lat swap bug in the config itself.
    config = _load(configs_dir / "pilot-area.json")
    min_lon, min_lat, max_lon, max_lat = config["editable_bbox_wgs84_lonlat"]
    assert 90 < min_lon < 110
    assert 90 < max_lon < 110
    assert 5 < min_lat < 21
    assert 5 < max_lat < 21
    assert min_lon < max_lon
    assert min_lat < max_lat


def test_acquire_budget_requires_human_approval_below_country_scale(configs_dir):
    config = _load(configs_dir / "pilot-area.json")
    budget = config["acquire_budget"]
    # A country-sized Thailand .osm.pbf is on the order of hundreds of MB;
    # the automatic budget must sit far below that so a country download
    # can never happen without explicit human approval.
    assert budget["max_download_bytes"] < budget["require_human_approval_above_bytes"]
    assert budget["require_human_approval_above_bytes"] < 500 * 1024 * 1024
    assert budget["honor_retry_after_header"] is True


def test_sources_lists_no_prohibited_source_as_allowed(configs_dir):
    config = _load(configs_dir / "sources.json")
    allowed_names = {s["base_url"] for s in config["sources"]}
    for prohibited_substr in ("google.com/maps", "streetview"):
        assert not any(prohibited_substr in url for url in allowed_names)
