"""Spec test #7: deterministic bake -- the same source+settings produces
an identical payload hash; changed settings produce a different hash
(and, at the acquire layer, a new source lock rather than an overwrite).
"""

from __future__ import annotations

import time

from map_pipeline.pipeline.mappack import run_pipeline
from map_pipeline.pipeline.osm_parse import parse_osm_file
from map_pipeline.pipeline.sourcelock import build_source_lock, sha256_file

BASE_CFG = {
    "map_id": "th-test-determinism-001",
    "editable_bbox_wgs84_lonlat": [100.00, 13.00, 100.03, 13.03],
    "context_buffer_m": 300,
    "candidate_projected_crs": "EPSG:32647",
    "driving_side": "left",
}


def _lock(cfg, path):
    sha, size = sha256_file(path)
    return build_source_lock(
        map_id=cfg["map_id"],
        source_kind="local_file",
        source_location=str(path),
        sha256=sha,
        size_bytes=size,
        pilot_area_config=cfg,
    )


def test_same_source_and_settings_bake_to_identical_hash(synthetic_fixtures_dir):
    path = synthetic_fixtures_dir / "tiny_multipolygon.synthetic.osm.xml"
    ds = parse_osm_file(str(path))
    lock = _lock(BASE_CFG, path)

    pack1 = run_pipeline(ds, BASE_CFG, lock, synthetic=True)
    time.sleep(1.05)  # force a different wall-clock second
    pack2 = run_pipeline(ds, BASE_CFG, lock, synthetic=True)

    assert pack1["content_hash"] == pack2["content_hash"]
    # The volatile section is allowed -- expected -- to differ.
    assert pack1["volatile"]["baked_at"] != pack2["volatile"]["baked_at"]


def test_changed_settings_change_the_hash(synthetic_fixtures_dir):
    path = synthetic_fixtures_dir / "tiny_multipolygon.synthetic.osm.xml"
    ds = parse_osm_file(str(path))
    lock1 = _lock(BASE_CFG, path)
    pack1 = run_pipeline(ds, BASE_CFG, lock1, synthetic=True)

    changed_cfg = dict(BASE_CFG, context_buffer_m=999)
    lock2 = _lock(changed_cfg, path)
    pack2 = run_pipeline(ds, changed_cfg, lock2, synthetic=True)

    assert pack1["content_hash"] != pack2["content_hash"]
    assert lock1.settings_hash != lock2.settings_hash


def test_hash_covers_payload_only_not_volatile_fields(synthetic_fixtures_dir):
    from map_pipeline.pipeline.mappack import compute_content_hash

    path = synthetic_fixtures_dir / "tiny_multipolygon.synthetic.osm.xml"
    ds = parse_osm_file(str(path))
    lock = _lock(BASE_CFG, path)
    pack = run_pipeline(ds, BASE_CFG, lock, synthetic=True)
    assert compute_content_hash(pack["payload"]) == pack["content_hash"]


def test_different_source_bytes_change_the_hash(synthetic_fixtures_dir, tmp_path):
    path_a = synthetic_fixtures_dir / "tiny_multipolygon.synthetic.osm.xml"
    path_b = synthetic_fixtures_dir / "multipart_building.synthetic.osm.xml"
    ds_a = parse_osm_file(str(path_a))
    ds_b = parse_osm_file(str(path_b))
    lock_a = _lock(BASE_CFG, path_a)
    lock_b = _lock(BASE_CFG, path_b)
    pack_a = run_pipeline(ds_a, BASE_CFG, lock_a, synthetic=True)
    pack_b = run_pipeline(ds_b, BASE_CFG, lock_b, synthetic=True)
    assert pack_a["content_hash"] != pack_b["content_hash"]
