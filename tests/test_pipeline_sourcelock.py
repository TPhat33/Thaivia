"""Source lock immutability: a settings change produces a NEW lock file,
never a silent overwrite of an existing one's content.
"""

from __future__ import annotations

import json

from map_pipeline.pipeline.settings import settings_hash
from map_pipeline.pipeline.sourcelock import (
    build_source_lock,
    canonical_lock_filename,
    read_source_lock,
    source_bytes_filename,
    versioned_lock_filename,
    write_source_lock,
)

BASE_CFG = {
    "map_id": "th-test-lock-001",
    "editable_bbox_wgs84_lonlat": [100.00, 13.00, 100.01, 13.01],
    "context_buffer_m": 300,
    "candidate_projected_crs": "EPSG:32647",
    "driving_side": "left",
}


def test_versioned_lock_filename_differs_when_settings_hash_differs():
    cfg_a = dict(BASE_CFG)
    cfg_b = dict(BASE_CFG, context_buffer_m=999)
    assert settings_hash(cfg_a) != settings_hash(cfg_b)
    name_a = versioned_lock_filename("m", "deadbeef" * 8, settings_hash(cfg_a))
    name_b = versioned_lock_filename("m", "deadbeef" * 8, settings_hash(cfg_b))
    assert name_a != name_b


def test_write_source_lock_never_overwrites_an_existing_versioned_lock(tmp_path):
    lock_a = build_source_lock(
        map_id="th-test-lock-001",
        source_kind="local_file",
        source_location="a.osm.xml",
        sha256="a" * 64,
        size_bytes=100,
        pilot_area_config=BASE_CFG,
    )
    result_a = write_source_lock(tmp_path, lock_a)
    original_bytes = result_a["versioned_path"].read_bytes()

    # Same map, DIFFERENT settings -> must be a different filename.
    cfg_changed = dict(BASE_CFG, context_buffer_m=999)
    lock_b = build_source_lock(
        map_id="th-test-lock-001",
        source_kind="local_file",
        source_location="a.osm.xml",
        sha256="a" * 64,
        size_bytes=100,
        pilot_area_config=cfg_changed,
    )
    result_b = write_source_lock(tmp_path, lock_b)

    assert result_a["versioned_path"] != result_b["versioned_path"]
    # The FIRST lock's file content must be byte-identical to what was
    # written originally -- never mutated by the second write.
    assert result_a["versioned_path"].read_bytes() == original_bytes
    # Both versioned files still exist on disk; nothing was deleted.
    assert result_a["versioned_path"].is_file()
    assert result_b["versioned_path"].is_file()

    # The canonical convenience pointer now reflects the newest lock...
    canonical = tmp_path / canonical_lock_filename("th-test-lock-001")
    canonical_data = json.loads(canonical.read_text(encoding="utf-8"))
    assert canonical_data["settings_hash"] == lock_b.settings_hash
    # ...but re-reading the OLD versioned lock file directly still gives
    # back the OLD settings hash: it was never touched.
    reread_a = read_source_lock(result_a["versioned_path"])
    assert reread_a.settings_hash == lock_a.settings_hash


def test_write_source_lock_is_idempotent_for_identical_content(tmp_path):
    lock = build_source_lock(
        map_id="th-test-lock-001",
        source_kind="local_file",
        source_location="a.osm.xml",
        sha256="b" * 64,
        size_bytes=42,
        pilot_area_config=BASE_CFG,
        now=__import__("datetime").datetime(2026, 1, 1, tzinfo=__import__("datetime").timezone.utc),
    )
    result1 = write_source_lock(tmp_path, lock)
    assert result1["is_new_lock"] is True
    result2 = write_source_lock(tmp_path, lock)
    assert result2["is_new_lock"] is False
    assert result1["versioned_path"] == result2["versioned_path"]


def test_source_bytes_filename_keeps_a_real_extension_for_osmium():
    name = source_bytes_filename("m", "a" * 64, "https://example.test/thailand-latest.osm.pbf")
    assert name.endswith(".osm.pbf")
    name2 = source_bytes_filename("m", "a" * 64, "/local/path/fixture.synthetic.osm.xml")
    assert name2.endswith(".osm.xml")
