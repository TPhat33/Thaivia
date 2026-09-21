"""Small geometry helpers used by feature extraction and normalization.

Kept deliberately dumb and dependency-light: Shapely is used only where
it is safe to (planar validity/containment checks on a single ring or
polygon), never to decide vertical stacking or graph connectivity -- see
`pipeline/graph.py` for why plan-view crossings are not junctions.
"""

from __future__ import annotations

from shapely.geometry import LinearRing, Polygon
from shapely.geometry.base import BaseGeometry

Coord = tuple[float, float]


def ring_is_closed(coords: list[Coord]) -> bool:
    return len(coords) >= 4 and coords[0] == coords[-1]


def is_zero_length_way(coords: list[Coord]) -> bool:
    if len(coords) < 2:
        return True
    return all(c == coords[0] for c in coords)


def has_duplicate_consecutive_nodes(node_refs: list[int]) -> bool:
    return any(a == b for a, b in zip(node_refs, node_refs[1:]))


def ring_self_intersects(coords: list[Coord]) -> bool:
    if len(coords) < 4:
        return False
    try:
        ring = LinearRing(coords)
    except Exception:
        return True
    return not ring.is_simple


def safe_polygon(outer: list[Coord], holes: list[list[Coord]]) -> BaseGeometry | None:
    """Builds a Shapely polygon defensively; returns None instead of
    raising on degenerate/hostile input (dangling refs already filtered
    upstream should make this rare, but self-intersecting rings and
    duplicate points must never crash the pipeline)."""
    try:
        poly = Polygon(outer, holes)
    except Exception:
        return None
    if not poly.is_valid:
        try:
            poly = poly.buffer(0)
        except Exception:
            return None
    if poly.is_empty:
        return None
    return poly


def group_multipolygon_rings(
    outer_rings: list[list[Coord]], inner_rings: list[list[Coord]]
) -> tuple[list[dict], list[str]]:
    """Matches inner (hole) rings to the outer ring that contains them.

    Supports "multi-part" multipolygons (several outer rings, e.g. a
    building relation with two disjoint footprints) by containment
    matching rather than assuming a single outer ring. Returns
    (ring_groups, warnings) where each ring_group is
    {"outer": [...], "holes": [[...], ...]}; unmatched holes are reported
    as warnings, never silently dropped from the report (though the
    unmatched ring itself is not rendered as a hole).
    """
    warnings: list[str] = []
    outer_polys: list[tuple[list[Coord], BaseGeometry]] = []
    for ring in outer_rings:
        poly = safe_polygon(ring, [])
        if poly is None:
            warnings.append(f"outer ring with {len(ring)} points could not be assembled into a polygon")
            continue
        outer_polys.append((ring, poly))

    ring_groups: list[dict] = [{"outer": ring, "holes": []} for ring, _poly in outer_polys]
    used_inner: set[int] = set()
    for i, hole_ring in enumerate(inner_rings):
        hole_poly = safe_polygon(hole_ring, [])
        if hole_poly is None:
            warnings.append(f"inner ring with {len(hole_ring)} points could not be assembled into a polygon")
            continue
        matched = False
        for group_idx, (_outer_ring, outer_poly) in enumerate(outer_polys):
            try:
                contains = outer_poly.contains(hole_poly.representative_point())
            except Exception:
                contains = False
            if contains:
                ring_groups[group_idx]["holes"].append(hole_ring)
                used_inner.add(i)
                matched = True
                break
        if not matched:
            warnings.append(f"inner ring #{i} could not be matched to any outer ring (kept unmatched, not rendered as a hole)")
    return ring_groups, warnings
