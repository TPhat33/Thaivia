"""Spec test #6: buffered crop -- interior/buffer/outside classification
is correct, and boundary gateways are created instead of silent dead
ends where a real way leaves the AOI.
"""

from __future__ import annotations

from map_pipeline.pipeline.boundary import BUFFER, INTERIOR, OUTSIDE, AoiExtent, classify_and_gate, classify_point
from map_pipeline.pipeline.graph import build_road_graph
from map_pipeline.pipeline.osm_parse import parse_osm_file
from map_pipeline.pipeline.projection import Projector, compute_local_origin

BBOX = (100.0000, 13.0000, 100.0020, 13.0020)
BUFFER_M = 50.0


def _extent():
    origin = compute_local_origin("EPSG:32647", BBOX)
    proj = Projector("EPSG:32647", origin)
    return AoiExtent(interior_bbox_lonlat=BBOX, buffer_m=BUFFER_M, projector=proj)


def test_classify_point_interior_buffer_outside():
    extent = _extent()
    assert classify_point(extent, 100.0010, 13.0010) == INTERIOR
    # ~11 m past the interior edge: inside the 50 m buffer.
    assert classify_point(extent, 100.0021, 13.0010) == BUFFER
    # ~100+ m past the interior edge: past the buffer.
    assert classify_point(extent, 100.0030, 13.0010) == OUTSIDE


def test_road_fully_inside_aoi_gets_no_gateway(synthetic_fixtures_dir):
    ds = parse_osm_file(str(synthetic_fixtures_dir / "boundary_gateway.synthetic.osm.xml"))
    proj = Projector("EPSG:32647", compute_local_origin("EPSG:32647", BBOX))
    graph, _issues = build_road_graph(ds, proj)
    extent = _extent()
    result = classify_and_gate(extent, ds, graph)
    interior_road_nodes = {-601, -602}
    for nid in interior_road_nodes:
        assert result.node_classification[nid] == INTERIOR
    gateway_node_ids = {g.node_id for g in result.gateways}
    assert interior_road_nodes.isdisjoint(gateway_node_ids)


def test_road_leaving_the_aoi_gets_a_bounded_gateway_not_a_dead_end(synthetic_fixtures_dir):
    ds = parse_osm_file(str(synthetic_fixtures_dir / "boundary_gateway.synthetic.osm.xml"))
    proj = Projector("EPSG:32647", compute_local_origin("EPSG:32647", BBOX))
    graph, _issues = build_road_graph(ds, proj)
    extent = _extent()
    result = classify_and_gate(extent, ds, graph)

    assert result.node_classification[-604] == OUTSIDE
    gateways = [g for g in result.gateways if g.node_id == -604]
    assert len(gateways) == 1
    gw = gateways[0]
    assert gw.way_id == -702
    # Bounded, never an infinite sink/source.
    assert gw.inbound_demand_veh_per_hour > 0
    assert gw.outbound_demand_veh_per_hour > 0
    assert gw.external_capacity_veh_per_hour > 0
    assert gw.inbound_demand_veh_per_hour < 1_000_000
    assert gw.external_capacity_veh_per_hour < 1_000_000
    # Explicitly namespaced as a simulation assumption, never a source fact.
    assert gw.namespace == "simulation_assumption"
    assert gw.rule
