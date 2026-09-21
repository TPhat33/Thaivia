"""WGS84 lon/lat -> AOI-appropriate projected meters -> local origin ->
Unity ground-plane axes (X east, Z north, Y up/reserved for elevation
this pipeline does not have).

CRS is a per-AOI config value, never hardcoded for all of Thailand: this
module validates a *candidate* CRS against the AOI's own extent and
returns warnings (it does not silently "fix" the config; a human decision
governs the actual CRS used) rather than assuming EPSG:32647 is correct
everywhere.

Every coordinate transform in this module goes through pyproj with
`always_xy=True`, so axis order is always (lon, lat) in / (easting,
northing) out -- never silently swapped to (lat, lon).
"""

from __future__ import annotations

import math
from dataclasses import dataclass

from pyproj import CRS, Transformer

BBox = tuple[float, float, float, float]


def wgs84_utm_zone_for_lon(lon: float) -> int:
    return int(math.floor((lon + 180.0) / 6.0) + 1)


@dataclass(frozen=True)
class CrsValidation:
    crs_code: str
    expected_utm_zone: int | None
    configured_utm_zone: int | None
    warnings: list[str]

    @property
    def ok(self) -> bool:
        return not self.warnings


def validate_crs_for_aoi(candidate_crs: str, bbox_wgs84_lonlat: BBox) -> CrsValidation:
    """Checks whether `candidate_crs` (e.g. "EPSG:32647") is a sane
    projected CRS choice for the given WGS84 bbox. Only understands
    WGS84/UTM EPSG codes (326xx north, 327xx south) since that is the
    family the plan uses; any other CRS code is accepted with a warning
    that automatic validation was skipped."""
    min_lon, min_lat, max_lon, max_lat = bbox_wgs84_lonlat
    center_lon = (min_lon + max_lon) / 2.0
    center_lat = (min_lat + max_lat) / 2.0
    warnings: list[str] = []
    expected_zone = wgs84_utm_zone_for_lon(center_lon)
    configured_zone: int | None = None

    crs = CRS.from_user_input(candidate_crs)
    code = crs.to_epsg()
    if code is not None and 32601 <= code <= 32660:
        configured_zone = code - 32600
        if center_lat < 0:
            warnings.append(
                f"{candidate_crs} is a northern-hemisphere UTM CRS but the AOI center "
                f"latitude {center_lat:.4f} is south of the equator"
            )
    elif code is not None and 32701 <= code <= 32760:
        configured_zone = code - 32700
        if center_lat >= 0:
            warnings.append(
                f"{candidate_crs} is a southern-hemisphere UTM CRS but the AOI center "
                f"latitude {center_lat:.4f} is north of the equator"
            )
    else:
        warnings.append(
            f"{candidate_crs} is not a recognized WGS84/UTM EPSG code (326xx/327xx); "
            "automatic zone-vs-extent validation was skipped, verify manually"
        )

    if configured_zone is not None and configured_zone != expected_zone:
        warnings.append(
            f"AOI center longitude {center_lon:.4f} falls in UTM zone {expected_zone}, "
            f"but the configured CRS {candidate_crs} uses zone {configured_zone}. "
            "The pipeline does NOT auto-correct this -- config governs -- but this AOI "
            "likely needs a different EPSG code; do not reuse this CRS for a different AOI."
        )
    return CrsValidation(
        crs_code=candidate_crs,
        expected_utm_zone=expected_zone,
        configured_utm_zone=configured_zone,
        warnings=warnings,
    )


@dataclass(frozen=True)
class LocalOrigin:
    crs_code: str
    origin_easting_m: float
    origin_northing_m: float


def compute_local_origin(crs_code: str, bbox_wgs84_lonlat: BBox) -> LocalOrigin:
    """The local origin is the AOI bbox center, projected. Recorded in the
    MapPack so any consumer can go from local meters back to real-world
    projected/geographic coordinates."""
    min_lon, min_lat, max_lon, max_lat = bbox_wgs84_lonlat
    center_lon = (min_lon + max_lon) / 2.0
    center_lat = (min_lat + max_lat) / 2.0
    transformer = Transformer.from_crs("EPSG:4326", crs_code, always_xy=True)
    x, y = transformer.transform(center_lon, center_lat)
    return LocalOrigin(crs_code=crs_code, origin_easting_m=x, origin_northing_m=y)


class Projector:
    """Stateful lon/lat <-> local-meters projector for one AOI/CRS/origin.

    Pipeline math is float64 throughout this class (Python floats are
    IEEE-754 doubles); only a render/export layer outside this pipeline
    may narrow precision.
    """

    def __init__(self, crs_code: str, origin: LocalOrigin) -> None:
        self.crs_code = crs_code
        self.origin = origin
        self._fwd = Transformer.from_crs("EPSG:4326", crs_code, always_xy=True)
        self._inv = Transformer.from_crs(crs_code, "EPSG:4326", always_xy=True)

    def lonlat_to_projected(self, lon: float, lat: float) -> tuple[float, float]:
        return self._fwd.transform(lon, lat)

    def projected_to_lonlat(self, x: float, y: float) -> tuple[float, float]:
        return self._inv.transform(x, y)

    def lonlat_to_local(self, lon: float, lat: float) -> tuple[float, float]:
        x, y = self._fwd.transform(lon, lat)
        # Unity ground plane: local_x = east offset, local_z = north
        # offset. Y (up) is reserved for elevation this pipeline does not
        # derive from OSM; consumers must treat building height as a
        # visual_assumption, never as a projected Y.
        return x - self.origin.origin_easting_m, y - self.origin.origin_northing_m

    def local_to_lonlat(self, local_x: float, local_z: float) -> tuple[float, float]:
        x = local_x + self.origin.origin_easting_m
        y = local_z + self.origin.origin_northing_m
        return self._inv.transform(x, y)


def measure_roundtrip_error_m(
    projector: Projector, lon: float, lat: float, quantize_fn=None
) -> float:
    """lon/lat -> projected -> local -> (quantize) -> lon/lat -> projected
    again; returns the meter distance, in the projected CRS's own linear
    unit, between the original projected point and the round-tripped one.
    This is what the 1 cm quantization / <=0.10 m round-trip budget is
    measured against -- not a guess.
    """
    x0, y0 = projector.lonlat_to_projected(lon, lat)
    local_x, local_z = projector.lonlat_to_local(lon, lat)
    if quantize_fn is not None:
        local_x, local_z = quantize_fn(local_x), quantize_fn(local_z)
    lon2, lat2 = projector.local_to_lonlat(local_x, local_z)
    x1, y1 = projector.lonlat_to_projected(lon2, lat2)
    return math.hypot(x1 - x0, y1 - y0)
