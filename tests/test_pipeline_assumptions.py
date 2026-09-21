"""Spec test #8: assumption isolation -- no visual_assumption or
simulation_assumption value can ever appear in the source-tag namespace.
Also spec test #5: a source gap does not become buildable empty land,
and the `unknown` marker survives all the way to the baked MapPack.
"""

from __future__ import annotations

from map_pipeline.pipeline.assumptions import building_assumptions, road_assumptions
from map_pipeline.pipeline.mappack import run_pipeline
from map_pipeline.pipeline.osm_parse import parse_osm_file
from map_pipeline.pipeline.sourcelock import build_source_lock, sha256_file
from map_pipeline.pipeline.tags import SIMULATION_ASSUMPTION, VISUAL_ASSUMPTION, SourceTags

BASE_CFG = {
    "map_id": "th-test-assumptions-001",
    "editable_bbox_wgs84_lonlat": [100.00, 13.00, 100.05, 13.05],
    "context_buffer_m": 300,
    "candidate_projected_crs": "EPSG:32647",
    "driving_side": "left",
}


def test_source_tags_is_a_structurally_distinct_type_from_assumption():
    tags = SourceTags({"building": "yes"})
    assumptions = building_assumptions(tags)
    assert assumptions  # height is missing -> an assumption was produced
    # SourceTags has no method that accepts an Assumption; the only way
    # to build one is via its own constructor from a plain source dict.
    assert not hasattr(tags, "add_assumption")
    assert not hasattr(tags, "merge")
    # The assumption's own dict form uses different keys than a tag dict.
    assumption_json = assumptions[0].to_json()
    assert set(assumption_json.keys()) == {"field", "value", "rule"}
    assert "building" not in assumption_json  # never merged with source tags


def test_height_missing_produces_visual_assumption_not_a_source_fact():
    tags = SourceTags({"building": "yes"})
    assumptions = building_assumptions(tags)
    assert len(assumptions) == 1
    assert assumptions[0].namespace == VISUAL_ASSUMPTION
    assert assumptions[0].field == "height_m"
    # The source tag dict itself is untouched: no "height" key appeared.
    assert not tags.has("height")
    assert tags.tags == {"building": "yes"}


def test_height_present_produces_no_assumption_at_all():
    tags = SourceTags({"building": "yes", "height": "12"})
    assert building_assumptions(tags) == []


def test_road_missing_maxspeed_produces_simulation_assumption():
    tags = SourceTags({"highway": "residential"})
    assumptions = road_assumptions(tags)
    speed_assumptions = [a for a in assumptions if a.field == "maxspeed_kph"]
    assert len(speed_assumptions) == 1
    assert speed_assumptions[0].namespace == SIMULATION_ASSUMPTION
    assert not tags.has("maxspeed")


def test_no_assumption_field_name_ever_appears_as_a_source_tags_key_in_a_baked_pack(synthetic_fixtures_dir):
    """Fuzz-style structural check over a real baked payload: walk every
    dict anywhere in the payload; wherever a "source_tags" dict appears,
    none of its keys may be a known assumption field name (height_m,
    width_m, maxspeed_kph) -- those must only ever appear inside
    "visual_assumptions"/"simulation_assumptions" entries."""
    path = synthetic_fixtures_dir / "road_graph_layers.synthetic.osm.xml"
    ds = parse_osm_file(str(path))
    sha, size = sha256_file(path)
    lock = build_source_lock(
        map_id=BASE_CFG["map_id"],
        source_kind="local_file",
        source_location=str(path),
        sha256=sha,
        size_bytes=size,
        pilot_area_config=BASE_CFG,
    )
    pack = run_pipeline(ds, BASE_CFG, lock, synthetic=True)

    assumption_field_names = {"height_m", "width_m", "maxspeed_kph"}
    violations = []

    def walk(node):
        if isinstance(node, dict):
            if "source_tags" in node and isinstance(node["source_tags"], dict):
                bad = assumption_field_names & set(node["source_tags"].keys())
                if bad:
                    violations.append((node.get("way_id") or node.get("source_id"), bad))
            for v in node.values():
                walk(v)
        elif isinstance(node, list):
            for v in node:
                walk(v)

    walk(pack["payload"])
    assert violations == [], f"assumption field(s) leaked into source_tags: {violations}"


def test_unknown_building_height_survives_as_unknown_to_the_baked_pack(synthetic_fixtures_dir):
    """The tiny_multipolygon fixture's building has no height/levels tag.
    Assert: (a) source_tags carries no height key (unknown, not a fact),
    (b) a visual_assumption fills the render need with a named rule, and
    (c) the quality report's unknown_counts reflects it -- unknown is
    tracked, not silently converted into a fact nor a blank/empty lot."""
    path = synthetic_fixtures_dir / "tiny_multipolygon.synthetic.osm.xml"
    ds = parse_osm_file(str(path))
    sha, size = sha256_file(path)
    lock = build_source_lock(
        map_id=BASE_CFG["map_id"],
        source_kind="local_file",
        source_location=str(path),
        sha256=sha,
        size_bytes=size,
        pilot_area_config=BASE_CFG,
    )
    pack = run_pipeline(ds, BASE_CFG, lock, synthetic=True)
    buildings = pack["payload"]["geography_base"]["buildings"]
    assert len(buildings) == 1
    building = buildings[0]
    assert "height" not in building["source_tags"]
    assert "building:levels" not in building["source_tags"]
    heights = [a for a in building["visual_assumptions"] if a["field"] == "height_m"]
    assert len(heights) == 1
    assert heights[0]["rule"] == "default_building_height_no_levels_or_height_tag_v1"
    assert pack["payload"]["quality_report"]["unknown_counts"]["building_height"] == 1


def test_no_vacant_land_feature_type_exists_anywhere_in_the_schema():
    """A source gap (no building present) must never be filled in with a
    synthesized 'buildable'/'vacant land' feature -- there simply is no
    such feature class anywhere in this pipeline's vocabulary."""
    import json
    from pathlib import Path

    schema_path = (
        Path(__file__).resolve().parent.parent / "tools" / "map_pipeline" / "schemas" / "mappack.schema.json"
    )
    schema_text = schema_path.read_text(encoding="utf-8").lower()
    for forbidden in ("vacant", "buildable", "empty_lot", "empty_land"):
        assert forbidden not in schema_text
