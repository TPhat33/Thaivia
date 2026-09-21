"""Spec test #4: multipolygon holes / multi-part / building:part do not
become duplicated buildings, and holes are preserved. Also covers
duplicate POI<->building association detection and incomplete-relation
reporting (complete_ways != complete relations).
"""

from __future__ import annotations

from map_pipeline.pipeline.features import (
    detect_duplicate_poi_building_associations,
    extract_building_features,
)
from map_pipeline.pipeline.osm_parse import OsmNode, parse_osm_file


def test_hole_is_preserved_not_dropped(synthetic_fixtures_dir):
    ds = parse_osm_file(str(synthetic_fixtures_dir / "tiny_multipolygon.synthetic.osm.xml"))
    feats, issues = extract_building_features(ds)
    assert len(feats) == 1
    feat = feats[0]
    assert feat.feature_class == "building"
    assert feat.source_kind == "relation"
    assert len(feat.ring_groups) == 1
    assert len(feat.ring_groups[0]["holes"]) == 1
    assert issues == []


def test_multipart_multipolygon_is_one_feature_with_two_ring_groups_not_two_buildings(synthetic_fixtures_dir):
    ds = parse_osm_file(str(synthetic_fixtures_dir / "multipart_building.synthetic.osm.xml"))
    feats, issues = extract_building_features(ds)
    buildings = [f for f in feats if f.feature_class == "building"]
    parts = [f for f in feats if f.feature_class == "building_part"]

    # Exactly ONE building feature (the relation), never duplicated into
    # two just because it has two disjoint outer rings.
    assert len(buildings) == 1
    relation_feat = buildings[0]
    assert relation_feat.source_kind == "relation"
    assert len(relation_feat.ring_groups) == 2
    for group in relation_feat.ring_groups:
        assert len(group["holes"]) == 1  # each part keeps its own hole

    # The standalone building:part way is its own feature, not merged
    # into the relation and not double-counted as a second "building".
    assert len(parts) == 1
    assert parts[0].source_kind == "way"
    assert parts[0].source_id == -1105


def test_way_consumed_by_a_relation_is_never_also_imported_standalone(synthetic_fixtures_dir):
    ds = parse_osm_file(str(synthetic_fixtures_dir / "tiny_multipolygon.synthetic.osm.xml"))
    feats, _issues = extract_building_features(ds)
    # Ways -101 (outer) and -102 (inner) must not appear as their own
    # standalone building features in addition to the relation feature.
    standalone_ids = {f.source_id for f in feats if f.source_kind == "way"}
    assert -101 not in standalone_ids
    assert -102 not in standalone_ids


def test_incomplete_relation_is_reported_not_silently_dropped():
    """complete_ways does NOT imply complete relations: a relation whose
    member way fell outside the extract must still be assembled from
    what IS present, and flagged as incomplete."""
    from map_pipeline.pipeline.osm_parse import OsmDataset, OsmRelation, OsmWay, RelationMember

    ds = OsmDataset()
    # Outer ring way IS present...
    ds.nodes = {
        1: OsmNode(1, 13.000, 100.000, {}),
        2: OsmNode(2, 13.000, 100.001, {}),
        3: OsmNode(3, 13.001, 100.001, {}),
        4: OsmNode(4, 13.001, 100.000, {}),
    }
    ds.ways = {
        101: OsmWay(101, [1, 2, 3, 4, 1], {}),
    }
    # ...but the relation also references an inner ring way (202) that
    # does NOT exist in this dataset (dangling / fell outside a bounded
    # extract).
    ds.relations = {
        901: OsmRelation(
            901,
            [
                RelationMember("w", 101, "outer"),
                RelationMember("w", 202, "inner"),
            ],
            {"type": "multipolygon", "building": "yes"},
        )
    }
    feats, issues = extract_building_features(ds)
    assert len(feats) == 1  # assembled from what IS present
    assert feats[0].ring_groups[0]["outer"]
    incomplete = [i for i in issues if i.kind == "incomplete_relation"]
    assert incomplete, "a relation with a missing member must be reported as incomplete, not silently dropped"
    assert "relation/901" in incomplete[0].feature_id


def test_duplicate_poi_building_association_is_reported_both_retained():
    from map_pipeline.pipeline.osm_parse import OsmDataset, OsmWay
    from map_pipeline.pipeline.tags import SourceTags
    from map_pipeline.pipeline.features import PolygonFeature

    ds = OsmDataset()
    # A POI node sitting inside a building polygon, sharing the same
    # shop=convenience tag as the building itself.
    ds.nodes = {
        1: OsmNode(1, 13.0005, 100.0005, {"shop": "convenience"}),
    }
    building = PolygonFeature(
        feature_class="building",
        source_kind="way",
        source_id=555,
        source_tags=SourceTags({"building": "yes", "shop": "convenience"}),
        ring_groups=[
            {
                "outer": [(100.000, 13.000), (100.001, 13.000), (100.001, 13.001), (100.000, 13.001), (100.000, 13.000)],
                "holes": [],
            }
        ],
    )
    issues = detect_duplicate_poi_building_associations(ds, [building])
    assert len(issues) == 1
    assert issues[0].kind == "duplicate_poi_building_association"
    assert "node/1" in issues[0].feature_id
