"""Spec test #9: degenerate/hostile input -- dangling references,
self-intersecting ring, zero-length way, duplicate node ids -- must be
handled with a quality-report entry, never a crash, anywhere in the
pipeline including a full bake.
"""

from __future__ import annotations

from map_pipeline.pipeline.mappack import run_pipeline
from map_pipeline.pipeline.osm_parse import parse_osm_file
from map_pipeline.pipeline.sourcelock import build_source_lock, sha256_file

BASE_CFG = {
    "map_id": "th-test-hostile-001",
    "editable_bbox_wgs84_lonlat": [100.00, 13.00, 100.03, 13.03],
    "context_buffer_m": 50,
    "candidate_projected_crs": "EPSG:32647",
    "driving_side": "left",
}


def _bake(synthetic_fixtures_dir):
    path = synthetic_fixtures_dir / "degenerate_hostile.synthetic.osm.xml"
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
    # Must not raise.
    return run_pipeline(ds, BASE_CFG, lock, synthetic=True)


def test_hostile_fixture_bakes_without_crashing(synthetic_fixtures_dir):
    pack = _bake(synthetic_fixtures_dir)
    assert pack["content_hash"].startswith("sha256:")


def test_dangling_reference_is_reported(synthetic_fixtures_dir):
    pack = _bake(synthetic_fixtures_dir)
    qr = pack["payload"]["quality_report"]
    dangling = [i for i in qr["dropped"] if i["kind"] == "dangling_reference"]
    assert any("way/-902" in i["feature_id"] for i in dangling)


def test_self_intersecting_ring_is_reported_as_topology_conflict(synthetic_fixtures_dir):
    pack = _bake(synthetic_fixtures_dir)
    qr = pack["payload"]["quality_report"]
    conflicts = qr["topology_conflicts"]
    assert any(
        "way/-901" in i["feature_id"] and "self-intersecting" in i["detail"] for i in conflicts
    )


def test_zero_length_way_is_dropped_and_reported(synthetic_fixtures_dir):
    pack = _bake(synthetic_fixtures_dir)
    qr = pack["payload"]["quality_report"]
    dropped = qr["dropped"]
    assert any("way/-903" in i["feature_id"] and "zero-length" in i["detail"] for i in dropped)
    # And it must not appear as a real building feature.
    building_ids = {b["source_id"] for b in pack["payload"]["geography_base"]["buildings"]}
    assert -903 not in building_ids


def test_duplicate_consecutive_node_ids_are_reported(synthetic_fixtures_dir):
    pack = _bake(synthetic_fixtures_dir)
    qr = pack["payload"]["quality_report"]
    conflicts = qr["topology_conflicts"]
    assert any(
        "way/-904" in i["feature_id"] and "duplicate consecutive" in i["detail"] for i in conflicts
    )


def test_quality_report_counts_are_internally_consistent(synthetic_fixtures_dir):
    pack = _bake(synthetic_fixtures_dir)
    qr = pack["payload"]["quality_report"]
    assert qr["rejected_counts"]["topology_conflicts"] == len(qr["topology_conflicts"])
    assert qr["rejected_counts"]["dropped_features"] == len(qr["dropped"])
