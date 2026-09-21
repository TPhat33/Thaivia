"""Parse raw OSM entities (nodes/ways/relations) with references intact.

This module builds an in-memory `OsmDataset` directly from OSM nodes,
ways and relations via pyosmium -- it does NOT flatten anything into
display GeoJSON first. Downstream feature extraction and graph building
both read node ids, way node-reference lists, and relation member lists
straight from this dataset, so "two things share a node" is answerable
exactly (not inferred from coordinates).

IMPORTANT: osmium's `complete_ways` option (or simply feeding a bounded
extract) does NOT imply "complete relations". A relation can reference a
member way/node that fell outside the extract (a dangling reference).
This module records every way node-ref that cannot be resolved and lets
callers (features.py, graph.py) decide how to report it; it never
silently drops a relation just because one member is missing -- callers
assemble what they can and flag the relation as incomplete.
"""

from __future__ import annotations

from dataclasses import dataclass, field

import osmium

# osmium relation member `.type` is one of these single-character codes,
# not the words "node"/"way"/"relation".
MEMBER_TYPE_NODE = "n"
MEMBER_TYPE_WAY = "w"
MEMBER_TYPE_RELATION = "r"


@dataclass(frozen=True)
class OsmNode:
    id: int
    lat: float
    lon: float
    tags: dict[str, str] = field(default_factory=dict)


@dataclass(frozen=True)
class OsmWay:
    id: int
    node_refs: list[int]
    tags: dict[str, str] = field(default_factory=dict)


@dataclass(frozen=True)
class RelationMember:
    type: str  # "n" | "w" | "r"
    ref: int
    role: str


@dataclass(frozen=True)
class OsmRelation:
    id: int
    members: list[RelationMember]
    tags: dict[str, str] = field(default_factory=dict)


@dataclass
class OsmDataset:
    nodes: dict[int, OsmNode] = field(default_factory=dict)
    ways: dict[int, OsmWay] = field(default_factory=dict)
    relations: dict[int, OsmRelation] = field(default_factory=dict)

    def way_coords_and_missing(self, way_id: int) -> tuple[list[tuple[float, float]], list[int]]:
        """Returns ([(lon, lat), ...] for resolvable node refs, [missing node ids])."""
        way = self.ways.get(way_id)
        if way is None:
            return [], []
        coords: list[tuple[float, float]] = []
        missing: list[int] = []
        for nid in way.node_refs:
            node = self.nodes.get(nid)
            if node is None:
                missing.append(nid)
                continue
            coords.append((node.lon, node.lat))
        return coords, missing


class _CollectingHandler(osmium.SimpleHandler):
    def __init__(self, dataset: OsmDataset) -> None:
        super().__init__()
        self.dataset = dataset

    def node(self, n) -> None:  # noqa: ANN001 - osmium C++ binding type
        if not n.location.valid():
            # A node with a tag but no resolvable coordinate (rare, but
            # possible in a hand-edited/degenerate file): record it with
            # NaN-free zeros is wrong; skip and let downstream ways that
            # reference it report a dangling reference instead of a fake
            # (0, 0) location.
            return
        self.dataset.nodes[n.id] = OsmNode(id=n.id, lat=n.location.lat, lon=n.location.lon, tags=dict(n.tags))

    def way(self, w) -> None:  # noqa: ANN001
        self.dataset.ways[w.id] = OsmWay(id=w.id, node_refs=[nd.ref for nd in w.nodes], tags=dict(w.tags))

    def relation(self, r) -> None:  # noqa: ANN001
        members = [RelationMember(type=m.type, ref=m.ref, role=m.role) for m in r.members]
        self.dataset.relations[r.id] = OsmRelation(id=r.id, members=members, tags=dict(r.tags))


def parse_osm_file(path: str) -> OsmDataset:
    """Parses a `.osm.xml` or `.osm.pbf` file into an `OsmDataset`.

    Requires the standard OSM entity ordering (nodes before the ways that
    reference them, ways before the relations that reference them), which
    every valid `.osm.pbf`/`.osm.xml` file -- including a Geofabrik/
    Overpass extract -- satisfies. This is a hard requirement of file
    order, not of any osmium `locations=True` index, so it also works for
    small hand-authored synthetic fixtures without extra flags.
    """
    dataset = OsmDataset()
    handler = _CollectingHandler(dataset)
    handler.apply_file(str(path))
    return dataset
