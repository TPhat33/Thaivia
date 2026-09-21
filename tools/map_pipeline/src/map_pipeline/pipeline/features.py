"""Extract buildings, water, and barrier features from the raw OSM
dataset -- including multipolygon assembly (holes, multi-part outer
rings), `building:part`, and duplicate POI<->building association
detection.

Feature classes retained per spec: roads/paths are handled in
`graph.py`; this module covers water (areas + waterways/canals),
buildings (+ building:part), and barriers.
"""

from __future__ import annotations

from dataclasses import dataclass, field

from shapely.geometry import Point, Polygon

from map_pipeline.pipeline.geometry_utils import (
    group_multipolygon_rings,
    has_duplicate_consecutive_nodes,
    is_zero_length_way,
    ring_is_closed,
    ring_self_intersects,
    safe_polygon,
)
from map_pipeline.pipeline.osm_parse import MEMBER_TYPE_WAY, OsmDataset
from map_pipeline.pipeline.quality import QualityIssue
from map_pipeline.pipeline.tags import SourceTags

Coord = tuple[float, float]

POI_TAG_KEYS = ("shop", "amenity", "office", "tourism", "leisure")


@dataclass
class PolygonFeature:
    feature_class: str  # "building" | "building_part" | "water" | "barrier_area"
    source_kind: str  # "way" | "relation"
    source_id: int
    source_tags: SourceTags
    # ring_groups supports multi-part multipolygons: several disjoint
    # outer rings, each with its own holes.
    ring_groups: list[dict] = field(default_factory=list)  # [{"outer": [...], "holes": [[...], ...]}]


@dataclass
class LineFeature:
    feature_class: str  # "waterway" | "barrier_line"
    source_kind: str
    source_id: int
    source_tags: SourceTags
    coords_lonlat: list[Coord]


def _way_ring(dataset: OsmDataset, way_id: int, issues: list[QualityIssue]) -> list[Coord] | None:
    coords, missing = dataset.way_coords_and_missing(way_id)
    if missing:
        issues.append(
            QualityIssue("dangling_reference", f"way/{way_id}", f"missing node ref(s): {missing}")
        )
    way = dataset.ways.get(way_id)
    if way and has_duplicate_consecutive_nodes(way.node_refs):
        issues.append(
            QualityIssue("topology_conflict", f"way/{way_id}", "duplicate consecutive node id(s) in way")
        )
    if len(coords) < 3:
        return None
    return coords


def _extract_multipolygon_relation(
    dataset: OsmDataset,
    rel,
    feature_class: str,
    issues: list[QualityIssue],
    consumed_way_ids: set[int],
) -> PolygonFeature | None:
    outer_refs = [m.ref for m in rel.members if m.type == MEMBER_TYPE_WAY and m.role == "outer"]
    inner_refs = [m.ref for m in rel.members if m.type == MEMBER_TYPE_WAY and m.role == "inner"]
    missing_members = [ref for ref in outer_refs + inner_refs if ref not in dataset.ways]
    if missing_members:
        issues.append(
            QualityIssue(
                "incomplete_relation",
                f"relation/{rel.id}",
                f"missing member way(s) {missing_members}; complete_ways does not imply "
                "complete relations -- assembled from the members that ARE present",
            )
        )
    outer_rings: list[Coord] = []
    for ref in outer_refs:
        consumed_way_ids.add(ref)
        ring = _way_ring(dataset, ref, issues)
        if ring is not None:
            outer_rings.append(ring)
    inner_rings: list[Coord] = []
    for ref in inner_refs:
        consumed_way_ids.add(ref)
        ring = _way_ring(dataset, ref, issues)
        if ring is not None:
            inner_rings.append(ring)

    if not outer_rings:
        issues.append(
            QualityIssue("dropped_feature", f"relation/{rel.id}", "no usable outer ring could be assembled")
        )
        return None

    ring_groups, warnings = group_multipolygon_rings(outer_rings, inner_rings)
    for w in warnings:
        issues.append(QualityIssue("topology_conflict", f"relation/{rel.id}", w))
    if not ring_groups:
        issues.append(
            QualityIssue("dropped_feature", f"relation/{rel.id}", "outer ring(s) failed polygon assembly")
        )
        return None
    return PolygonFeature(
        feature_class=feature_class,
        source_kind="relation",
        source_id=rel.id,
        source_tags=SourceTags(dict(rel.tags)),
        ring_groups=ring_groups,
    )


def extract_building_features(dataset: OsmDataset) -> tuple[list[PolygonFeature], list[QualityIssue]]:
    """Buildings, including multipolygon relations (holes + multi-part)
    and `building:part` ways. A way consumed by a multipolygon relation
    is never also imported standalone (no duplicate buildings)."""
    issues: list[QualityIssue] = []
    features: list[PolygonFeature] = []
    consumed_way_ids: set[int] = set()

    for rel in sorted(dataset.relations.values(), key=lambda r: r.id):
        if rel.tags.get("type") != "multipolygon":
            continue
        if "building" not in rel.tags and "building:part" not in rel.tags:
            continue
        feature_class = "building_part" if "building:part" in rel.tags and "building" not in rel.tags else "building"
        feat = _extract_multipolygon_relation(dataset, rel, feature_class, issues, consumed_way_ids)
        if feat is not None:
            features.append(feat)

    for way in sorted(dataset.ways.values(), key=lambda w: w.id):
        if way.id in consumed_way_ids:
            continue
        is_building = "building" in way.tags
        is_part = "building:part" in way.tags
        if not (is_building or is_part):
            continue
        coords, missing = dataset.way_coords_and_missing(way.id)
        if missing:
            issues.append(
                QualityIssue("dangling_reference", f"way/{way.id}", f"missing node ref(s): {missing}")
            )
        if has_duplicate_consecutive_nodes(way.node_refs):
            issues.append(
                QualityIssue("topology_conflict", f"way/{way.id}", "duplicate consecutive node id(s) in way")
            )
        if is_zero_length_way(coords):
            issues.append(
                QualityIssue("dropped_feature", f"way/{way.id}", "zero-length/degenerate way (<2 distinct points)")
            )
            continue
        if not ring_is_closed(coords):
            issues.append(
                QualityIssue(
                    "topology_conflict",
                    f"way/{way.id}",
                    "building way is not a closed ring; kept as an open polyline, flagged",
                )
            )
        elif ring_self_intersects(coords):
            issues.append(
                QualityIssue("topology_conflict", f"way/{way.id}", "self-intersecting ring")
            )
        feature_class = "building_part" if (is_part and not is_building) else "building"
        features.append(
            PolygonFeature(
                feature_class=feature_class,
                source_kind="way",
                source_id=way.id,
                source_tags=SourceTags(dict(way.tags)),
                ring_groups=[{"outer": coords, "holes": []}],
            )
        )
    return features, issues


WATER_AREA_NATURAL = {"water"}
WATER_AREA_WATERWAY = {"riverbank"}
WATERWAY_LINE_TAGS = {"river", "stream", "canal", "drain", "ditch"}


def extract_water_features(
    dataset: OsmDataset,
) -> tuple[list[PolygonFeature], list[LineFeature], list[QualityIssue]]:
    issues: list[QualityIssue] = []
    polygons: list[PolygonFeature] = []
    lines: list[LineFeature] = []
    consumed_way_ids: set[int] = set()

    def _is_water_area(tags: dict[str, str]) -> bool:
        return tags.get("natural") in WATER_AREA_NATURAL or tags.get("waterway") in WATER_AREA_WATERWAY or tags.get("landuse") == "basin"

    for rel in sorted(dataset.relations.values(), key=lambda r: r.id):
        if rel.tags.get("type") != "multipolygon" or not _is_water_area(rel.tags):
            continue
        feat = _extract_multipolygon_relation(dataset, rel, "water", issues, consumed_way_ids)
        if feat is not None:
            polygons.append(feat)

    for way in sorted(dataset.ways.values(), key=lambda w: w.id):
        if way.id in consumed_way_ids:
            continue
        if _is_water_area(way.tags):
            coords, missing = dataset.way_coords_and_missing(way.id)
            if missing:
                issues.append(
                    QualityIssue("dangling_reference", f"way/{way.id}", f"missing node ref(s): {missing}")
                )
            if is_zero_length_way(coords):
                issues.append(QualityIssue("dropped_feature", f"way/{way.id}", "zero-length water way"))
                continue
            if ring_is_closed(coords) and ring_self_intersects(coords):
                issues.append(QualityIssue("topology_conflict", f"way/{way.id}", "self-intersecting water ring"))
            polygons.append(
                PolygonFeature(
                    feature_class="water",
                    source_kind="way",
                    source_id=way.id,
                    source_tags=SourceTags(dict(way.tags)),
                    ring_groups=[{"outer": coords, "holes": []}],
                )
            )
        elif way.tags.get("waterway") in WATERWAY_LINE_TAGS:
            coords, missing = dataset.way_coords_and_missing(way.id)
            if missing:
                issues.append(
                    QualityIssue("dangling_reference", f"way/{way.id}", f"missing node ref(s): {missing}")
                )
            if is_zero_length_way(coords):
                issues.append(QualityIssue("dropped_feature", f"way/{way.id}", "zero-length waterway"))
                continue
            lines.append(
                LineFeature(
                    feature_class="waterway",
                    source_kind="way",
                    source_id=way.id,
                    source_tags=SourceTags(dict(way.tags)),
                    coords_lonlat=coords,
                )
            )
    return polygons, lines, issues


def extract_barrier_features(dataset: OsmDataset) -> tuple[list[LineFeature], list[QualityIssue]]:
    issues: list[QualityIssue] = []
    lines: list[LineFeature] = []
    for way in sorted(dataset.ways.values(), key=lambda w: w.id):
        if "barrier" not in way.tags:
            continue
        coords, missing = dataset.way_coords_and_missing(way.id)
        if missing:
            issues.append(
                QualityIssue("dangling_reference", f"way/{way.id}", f"missing node ref(s): {missing}")
            )
        if is_zero_length_way(coords):
            issues.append(QualityIssue("dropped_feature", f"way/{way.id}", "zero-length barrier way"))
            continue
        lines.append(
            LineFeature(
                feature_class="barrier_line",
                source_kind="way",
                source_id=way.id,
                source_tags=SourceTags(dict(way.tags)),
                coords_lonlat=coords,
            )
        )
    return lines, issues


def detect_duplicate_poi_building_associations(
    dataset: OsmDataset, building_features: list[PolygonFeature]
) -> list[QualityIssue]:
    """Flags (never silently drops or double-counts) a POI node that
    shares a primary tag with the building polygon that contains it --
    e.g. a `shop=convenience` node sitting inside a way also tagged
    `shop=convenience`. Both features are retained; this is a report
    entry, not a merge."""
    issues: list[QualityIssue] = []
    polys: list[tuple[PolygonFeature, Polygon]] = []
    for feat in building_features:
        for group in feat.ring_groups:
            poly = safe_polygon(group["outer"], group["holes"])
            if poly is not None:
                polys.append((feat, poly))

    for node in sorted(dataset.nodes.values(), key=lambda n: n.id):
        poi_tags = {k: v for k, v in node.tags.items() if k in POI_TAG_KEYS}
        if not poi_tags:
            continue
        pt = Point(node.lon, node.lat)
        for feat, poly in polys:
            try:
                inside = poly.contains(pt)
            except Exception:
                inside = False
            if not inside:
                continue
            shared = {k: v for k, v in poi_tags.items() if feat.source_tags.get(k) == v}
            if shared:
                issues.append(
                    QualityIssue(
                        "duplicate_poi_building_association",
                        f"node/{node.id}",
                        f"POI node shares tag(s) {shared} with containing "
                        f"{feat.source_kind}/{feat.source_id}; both retained as separate features, not merged",
                    )
                )
    return issues
