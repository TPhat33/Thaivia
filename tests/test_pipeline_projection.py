"""Spec test #1: lon/lat <-> projected <-> local round-trip; no axis
swap, no mirroring, no forced snapping; measured (not asserted-away)
error bound <= 0.10 m after 1 cm quantization.
"""

from __future__ import annotations

import math

from map_pipeline.pipeline.normalize import quantize_1cm
from map_pipeline.pipeline.projection import (
    Projector,
    compute_local_origin,
    measure_roundtrip_error_m,
    validate_crs_for_aoi,
    wgs84_utm_zone_for_lon,
)

BKK_BBOX = (100.586, 13.710, 100.595, 13.719)


def test_utm_zone_for_bangkok_is_47():
    # Bangkok is squarely inside UTM zone 47N; this pins the zone formula.
    assert wgs84_utm_zone_for_lon(100.59) == 47


def test_pilot_crs_validates_cleanly_for_the_pilot_bbox():
    result = validate_crs_for_aoi("EPSG:32647", BKK_BBOX)
    assert result.warnings == []
    assert result.configured_utm_zone == 47
    assert result.expected_utm_zone == 47


def test_wrong_utm_zone_for_an_aoi_produces_a_warning_not_a_silent_fix():
    # A Chiang Mai-ish AOI (zone 47 too, actually) vs a deliberately wrong
    # southern-hemisphere CRS should be flagged, not silently "corrected".
    result = validate_crs_for_aoi("EPSG:32747", BKK_BBOX)  # zone 47 SOUTH
    assert result.warnings, "expected a hemisphere mismatch warning"
    assert any("hemisphere" in w for w in result.warnings)
    # The code must not auto-correct: it still reports the configured CRS
    # as given.
    assert result.crs_code == "EPSG:32747"


def test_wrong_zone_entirely_is_flagged():
    # EPSG:32648 is zone 48 (east of Thailand); Bangkok's bbox is zone 47.
    result = validate_crs_for_aoi("EPSG:32648", BKK_BBOX)
    assert any("zone" in w and "48" in w for w in result.warnings)


def test_roundtrip_lonlat_local_lonlat_is_within_micrometres_before_quantization():
    origin = compute_local_origin("EPSG:32647", BKK_BBOX)
    proj = Projector("EPSG:32647", origin)
    lon, lat = 100.5905, 13.7145
    local_x, local_z = proj.lonlat_to_local(lon, lat)
    lon2, lat2 = proj.local_to_lonlat(local_x, local_z)
    assert math.isclose(lon, lon2, abs_tol=1e-9)
    assert math.isclose(lat, lat2, abs_tol=1e-9)


def test_roundtrip_error_after_1cm_quantization_is_measured_and_within_budget():
    origin = compute_local_origin("EPSG:32647", BKK_BBOX)
    proj = Projector("EPSG:32647", origin)
    samples = [
        (100.5860, 13.7100),
        (100.5950, 13.7190),
        (100.5905, 13.7145),
        (100.5870, 13.7185),
    ]
    errors = [measure_roundtrip_error_m(proj, lon, lat, quantize_fn=quantize_1cm) for lon, lat in samples]
    for e in errors:
        assert e >= 0.0
        # Spec budget: <= 0.10 m. This is a MEASUREMENT, not an assertion
        # that quantization is exactly zero error.
        assert e <= 0.10, f"round-trip error {e} m exceeds the 0.10 m budget"
    # 1 cm quantization on a UTM projection should be in the millimeter
    # range, nowhere near the 0.10 m budget ceiling -- catches an axis
    # mix-up that would otherwise still slip under a loose bound.
    assert max(errors) < 0.02


def test_no_axis_swap_east_and_north_move_independently():
    origin = compute_local_origin("EPSG:32647", BKK_BBOX)
    proj = Projector("EPSG:32647", origin)
    base_x, base_z = proj.lonlat_to_local(100.590, 13.714)
    east_x, east_z = proj.lonlat_to_local(100.591, 13.714)  # +lon only
    north_x, north_z = proj.lonlat_to_local(100.590, 13.715)  # +lat only

    # Moving +longitude must move local_x (east) far more than local_z.
    assert abs(east_x - base_x) > 50.0
    assert abs(east_z - base_z) < 5.0
    # Moving +latitude must move local_z (north) far more than local_x,
    # and northward must be POSITIVE (no mirroring).
    assert abs(north_z - base_z) > 50.0
    assert north_z > base_z
    assert abs(north_x - base_x) < 5.0


def test_no_forced_snapping_arbitrary_coordinates_stay_arbitrary():
    origin = compute_local_origin("EPSG:32647", BKK_BBOX)
    proj = Projector("EPSG:32647", origin)
    lon, lat = 100.58731234, 13.71156789
    local_x, local_z = proj.lonlat_to_local(lon, lat)
    # Neither coordinate should land suspiciously on a round grid value
    # (which would indicate silent snapping instead of true projection).
    assert not math.isclose(local_x, round(local_x), abs_tol=1e-6)
    assert not math.isclose(local_z, round(local_z), abs_tol=1e-6)
