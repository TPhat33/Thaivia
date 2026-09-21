"""Semantic RoadGraph builder.

Semantic, not visual: a junction exists ONLY where two ways genuinely
share an OSM node reference. This module never asks Shapely (or anything
else) whether two polylines cross in plan view -- junctions are computed
purely from the `node_refs` lists osmium gave us, so a bridge crossing a
road at a different `layer` can never become an intersection just
because their drawn geometry happens to overlap on screen.

`layer` is an ordering, not a height in metres: it is carried through as
a plain semantic integer on each edge, never used as a Z coordinate or
fed to Shapely.
"""

from __future__ import annotations

from collections import Counter
from dataclasses import dataclass, field

from map_pipeline.pipeline.osm_parse import MEMBER_TYPE_NODE, MEMBER_TYPE_WAY, OsmDataset
from map_pipeline.pipeline.projection import Projector
from map_pipeline.pipeline.quality import QualityIssue
from map_pipeline.pipeline.tags import SourceTags

Coord = tuple[float, float]

ACCESS_TAG_KEYS = (
    "access",
    "vehicle",
    "motor_vehicle",
    "motorcar",
    "foot",
    "bicycle",
    "psv",
    "bus",
    "hgv",
)

RESTRICTION_TYPES = {
    "no_left_turn",
    "no_right_turn",
    "no_straight_on",
    "no_u_turn",
    "no_entry",
    "no_exit",
    "only_left_turn",
    "only_right_turn",
    "only_straight_on",
    "only_u_turn",
}


@dataclass(frozen=True)
class GraphNode:
    node_id: int
    lon: float
    lat: float
    local_x: float
    local_z: float


@dataclass(frozen=True)
class TurnRestriction:
    relation_id: int
    restriction_type: str
    from_way: int | None
    via: list[int]
    via_kind: str  # "n" | "w" | "unknown"
    to_way: int | None
    supported: bool
    unsupported_reason: str | None


@dataclass
class RoadEdge:
    way_id: int
    source_tags: SourceTags
    node_refs: list[int]  # ordered, resolvable-only OSM node ids
    coords_lonlat: list[Coord]
    coords_local: list[Coord]
    oneway: str  # "no" | "forward" | "reversed"
    layer: int  # semantic ordering, NOT metres
    bridge: bool
    tunnel: bool
    grade_separated: bool
    access_modes: dict[str, str]
    from_node: int
    to_node: int


@dataclass
class RoadGraph:
    nodes: dict[int, GraphNode] = field(default_factory=dict)
    edges: list[RoadEdge] = field(default_factory=list)
    junction_node_ids: set[int] = field(default_factory=set)
    turn_restrictions: list[TurnRestriction] = field(default_factory=list)


def _normalize_oneway(tags: dict[str, str]) -> str:
    raw = tags.get("oneway")
    if raw in ("yes", "true", "1"):
        return "forward"
    if raw == "-1":
        return "reversed"
    if raw in ("no", "false", "0"):
        return "no"
    if tags.get("junction") == "roundabout":
        # A roundabout is oneway unless explicitly overridden above.
        return "forward"
    return "no"


def _normalize_layer(tags: dict[str, str]) -> int:
    raw = tags.get("layer")
    if raw is None:
        return 0
    try:
        return int(raw)
    except ValueError:
        return 0


def _extract_access_modes(tags: dict[str, str]) -> dict[str, str]:
    return {k: tags[k] for k in ACCESS_TAG_KEYS if k in tags}


def build_road_graph(dataset: OsmDataset, projector: Projector) -> tuple[RoadGraph, list[QualityIssue]]:
    issues: list[QualityIssue] = []
    road_ways = {wid: w for wid, w in dataset.ways.items() if "highway" in w.tags}

    node_way_count: Counter[int] = Counter()
    for way in road_ways.values():
        for nid in set(way.node_refs):
            node_way_count[nid] += 1
    junction_ids = {nid for nid, count in node_way_count.items() if count >= 2}

    nodes: dict[int, GraphNode] = {}
    edges: list[RoadEdge] = []
    for wid in sorted(road_ways):
        way = road_ways[wid]
        resolvable_refs: list[int] = []
        coords_lonlat: list[Coord] = []
        coords_local: list[Coord] = []
        missing: list[int] = []
        for nid in way.node_refs:
            n = dataset.nodes.get(nid)
            if n is None:
                missing.append(nid)
                continue
            resolvable_refs.append(nid)
            coords_lonlat.append((n.lon, n.lat))
            lx, lz = projector.lonlat_to_local(n.lon, n.lat)
            coords_local.append((lx, lz))
            if nid not in nodes:
                nodes[nid] = GraphNode(nid, n.lon, n.lat, lx, lz)
        if missing:
            issues.append(
                QualityIssue("dangling_reference", f"way/{wid}", f"missing node ref(s): {missing}")
            )
        if len(coords_local) < 2:
            issues.append(
                QualityIssue(
                    "dropped_feature",
                    f"way/{wid}",
                    "fewer than 2 resolvable nodes; way dropped from the road graph",
                )
            )
            continue
        bridge = way.tags.get("bridge", "no") not in ("no", "")
        tunnel = way.tags.get("tunnel", "no") not in ("no", "")
        layer = _normalize_layer(way.tags)
        edges.append(
            RoadEdge(
                way_id=wid,
                source_tags=SourceTags(dict(way.tags)),
                node_refs=resolvable_refs,
                coords_lonlat=coords_lonlat,
                coords_local=coords_local,
                oneway=_normalize_oneway(way.tags),
                layer=layer,
                bridge=bridge,
                tunnel=tunnel,
                grade_separated=bridge or tunnel or layer != 0,
                access_modes=_extract_access_modes(way.tags),
                from_node=resolvable_refs[0],
                to_node=resolvable_refs[-1],
            )
        )

    turn_restrictions = _build_turn_restrictions(dataset, road_ways, issues)
    graph = RoadGraph(nodes=nodes, edges=edges, junction_node_ids=junction_ids, turn_restrictions=turn_restrictions)
    return graph, issues


def _build_turn_restrictions(
    dataset: OsmDataset, road_ways: dict[int, object], issues: list[QualityIssue]
) -> list[TurnRestriction]:
    restrictions: list[TurnRestriction] = []
    for rel in sorted(dataset.relations.values(), key=lambda r: r.id):
        if rel.tags.get("type") != "restriction":
            continue
        restriction_value = None
        for k, v in rel.tags.items():
            if k == "restriction" or k.startswith("restriction:"):
                restriction_value = v
                break

        from_way = None
        to_way = None
        via_refs: list[int] = []
        via_kind = "unknown"
        missing_members: list[str] = []
        for m in rel.members:
            if m.role == "from" and m.type == MEMBER_TYPE_WAY:
                from_way = m.ref
                if m.ref not in dataset.ways:
                    missing_members.append(f"way/{m.ref}")
            elif m.role == "to" and m.type == MEMBER_TYPE_WAY:
                to_way = m.ref
                if m.ref not in dataset.ways:
                    missing_members.append(f"way/{m.ref}")
            elif m.role == "via":
                via_refs.append(m.ref)
                via_kind = m.type
                if m.type == MEMBER_TYPE_NODE and m.ref not in dataset.nodes:
                    missing_members.append(f"node/{m.ref}")
                elif m.type == MEMBER_TYPE_WAY and m.ref not in dataset.ways:
                    missing_members.append(f"way/{m.ref}")

        supported = True
        reason = None
        if from_way is None or to_way is None:
            supported = False
            reason = "missing from/to way member"
        elif missing_members:
            supported = False
            reason = f"dangling member reference(s): {missing_members}"
        elif via_kind == MEMBER_TYPE_WAY:
            supported = False
            reason = "via-way (complex multi-way) restriction not modeled by this graph builder"
        elif restriction_value is None or restriction_value not in RESTRICTION_TYPES:
            supported = False
            reason = f"unrecognized/unsupported restriction type: {restriction_value!r}"

        tr = TurnRestriction(
            relation_id=rel.id,
            restriction_type=restriction_value or "unknown",
            from_way=from_way,
            via=via_refs,
            via_kind=via_kind,
            to_way=to_way,
            supported=supported,
            unsupported_reason=reason,
        )
        restrictions.append(tr)
        if not supported:
            issues.append(QualityIssue("unsupported_restriction", f"relation/{rel.id}", reason or "unsupported"))
    return restrictions
