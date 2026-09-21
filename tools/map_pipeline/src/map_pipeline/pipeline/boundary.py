"""Editable interior + read-only context buffer + external gateways.

Cropping a real road network to a bbox must never silently turn a
through-road into a dead-end soi. Any road edge that has a node outside
the buffered AOI (i.e. the source way continues past the crop) gets a
gateway at its outermost such node, carrying a bounded, explicitly
`simulation_assumption`-namespaced inbound/outbound demand and external
capacity -- never an infinite sink/source.
"""

from __future__ import annotations

from dataclasses import dataclass, field

from map_pipeline.pipeline.graph import RoadGraph
from map_pipeline.pipeline.osm_parse import OsmDataset
from map_pipeline.pipeline.projection import Projector

BBox = tuple[float, float, float, float]

INTERIOR = "interior"
BUFFER = "buffer"
OUTSIDE = "outside"

DEFAULT_GATEWAY_INBOUND_VEH_PER_HOUR = 150
DEFAULT_GATEWAY_OUTBOUND_VEH_PER_HOUR = 150
DEFAULT_GATEWAY_CAPACITY_VEH_PER_HOUR = 400


@dataclass(frozen=True)
class AoiExtent:
    interior_bbox_lonlat: BBox
    buffer_m: float
    projector: Projector


def classify_point(extent: AoiExtent, lon: float, lat: float) -> str:
    min_lon, min_lat, max_lon, max_lat = extent.interior_bbox_lonlat
    if min_lon <= lon <= max_lon and min_lat <= lat <= max_lat:
        return INTERIOR
    lx, lz = extent.projector.lonlat_to_local(lon, lat)
    ilx0, ilz0 = extent.projector.lonlat_to_local(min_lon, min_lat)
    ilx1, ilz1 = extent.projector.lonlat_to_local(max_lon, max_lat)
    bx0, bx1 = sorted((ilx0, ilx1))
    bz0, bz1 = sorted((ilz0, ilz1))
    bx0 -= extent.buffer_m
    bz0 -= extent.buffer_m
    bx1 += extent.buffer_m
    bz1 += extent.buffer_m
    if bx0 <= lx <= bx1 and bz0 <= lz <= bz1:
        return BUFFER
    return OUTSIDE


@dataclass(frozen=True)
class Gateway:
    node_id: int
    way_id: int
    lon: float
    lat: float
    inbound_demand_veh_per_hour: int
    outbound_demand_veh_per_hour: int
    external_capacity_veh_per_hour: int
    rule: str
    namespace: str = "simulation_assumption"

    def to_json(self) -> dict:
        return {
            "node_id": self.node_id,
            "way_id": self.way_id,
            "lon": self.lon,
            "lat": self.lat,
            "inbound_demand_veh_per_hour": self.inbound_demand_veh_per_hour,
            "outbound_demand_veh_per_hour": self.outbound_demand_veh_per_hour,
            "external_capacity_veh_per_hour": self.external_capacity_veh_per_hour,
            "rule": self.rule,
            "namespace": self.namespace,
        }


@dataclass
class BoundaryResult:
    node_classification: dict[int, str] = field(default_factory=dict)
    gateways: list[Gateway] = field(default_factory=list)


def classify_and_gate(extent: AoiExtent, dataset: OsmDataset, road_graph: RoadGraph) -> BoundaryResult:
    classification: dict[int, str] = {}
    gateways: list[Gateway] = []
    seen_gateway_nodes: set[int] = set()

    for edge in road_graph.edges:
        for nid in edge.node_refs:
            if nid in classification:
                continue
            node = dataset.nodes.get(nid)
            if node is None:
                continue
            classification[nid] = classify_point(extent, node.lon, node.lat)

        endpoint_ids = (edge.node_refs[0], edge.node_refs[-1])
        for nid in endpoint_ids:
            if classification.get(nid) != OUTSIDE:
                continue
            if nid in seen_gateway_nodes:
                continue
            seen_gateway_nodes.add(nid)
            node = dataset.nodes[nid]
            gateways.append(
                Gateway(
                    node_id=nid,
                    way_id=edge.way_id,
                    lon=node.lon,
                    lat=node.lat,
                    inbound_demand_veh_per_hour=DEFAULT_GATEWAY_INBOUND_VEH_PER_HOUR,
                    outbound_demand_veh_per_hour=DEFAULT_GATEWAY_OUTBOUND_VEH_PER_HOUR,
                    external_capacity_veh_per_hour=DEFAULT_GATEWAY_CAPACITY_VEH_PER_HOUR,
                    rule="gateway_default_bounded_demand_v1",
                )
            )
    return BoundaryResult(node_classification=classification, gateways=gateways)
