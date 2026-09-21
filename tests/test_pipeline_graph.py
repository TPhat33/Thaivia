"""Spec test #2 and #3: a bridge crossing a road at a different `layer`
does NOT become a junction (only a genuine shared node does); oneway=-1
survives as a reversed edge; turn restrictions survive, and unsupported
ones land in the report rather than being dropped.

Uses tests/fixtures/synthetic/road_graph_layers.synthetic.osm.xml.
"""

from __future__ import annotations

from map_pipeline.pipeline.graph import build_road_graph
from map_pipeline.pipeline.osm_parse import parse_osm_file
from map_pipeline.pipeline.projection import Projector, compute_local_origin

BBOX = (100.00, 13.00, 100.03, 13.03)


def _graph(synthetic_fixtures_dir):
    ds = parse_osm_file(str(synthetic_fixtures_dir / "road_graph_layers.synthetic.osm.xml"))
    origin = compute_local_origin("EPSG:32647", BBOX)
    proj = Projector("EPSG:32647", origin)
    return build_road_graph(ds, proj)


def test_only_the_genuinely_shared_node_is_a_junction(synthetic_fixtures_dir):
    graph, _issues = _graph(synthetic_fixtures_dir)
    # -302 is shared by Road A (-401) and Road B (-402): a real junction.
    assert -302 in graph.junction_node_ids
    # The bridge (-403) crosses Road A in plan view at the exact location
    # of node -302 but uses its own distinct node ids (-305/-306); it
    # must NOT be treated as sharing that junction.
    assert -305 not in graph.junction_node_ids
    assert -306 not in graph.junction_node_ids


def test_bridge_edge_carries_semantic_layer_not_a_shared_node(synthetic_fixtures_dir):
    graph, _issues = _graph(synthetic_fixtures_dir)
    bridge = next(e for e in graph.edges if e.way_id == -403)
    road_a = next(e for e in graph.edges if e.way_id == -401)
    assert bridge.layer == 1
    assert bridge.bridge is True
    assert bridge.grade_separated is True
    assert road_a.layer == 0
    # No node id in common between the two edges.
    assert set(bridge.node_refs).isdisjoint(set(road_a.node_refs))


def test_shared_node_ways_do_connect(synthetic_fixtures_dir):
    graph, _issues = _graph(synthetic_fixtures_dir)
    road_a = next(e for e in graph.edges if e.way_id == -401)
    road_b = next(e for e in graph.edges if e.way_id == -402)
    assert -302 in road_a.node_refs
    assert -302 in road_b.node_refs


def test_oneway_minus_one_survives_as_reversed(synthetic_fixtures_dir):
    graph, _issues = _graph(synthetic_fixtures_dir)
    road_a = next(e for e in graph.edges if e.way_id == -401)
    assert road_a.oneway == "reversed"
    assert road_a.source_tags.get("oneway") == "-1"  # source fact preserved verbatim too


def test_roundabout_implies_oneway_forward(synthetic_fixtures_dir):
    graph, _issues = _graph(synthetic_fixtures_dir)
    roundabout = next(e for e in graph.edges if e.way_id == -404)
    assert roundabout.oneway == "forward"


def test_supported_turn_restriction_survives_with_semantics(synthetic_fixtures_dir):
    graph, _issues = _graph(synthetic_fixtures_dir)
    supported = next(t for t in graph.turn_restrictions if t.relation_id == -501)
    assert supported.supported is True
    assert supported.restriction_type == "no_left_turn"
    assert supported.from_way == -401
    assert supported.to_way == -402
    assert supported.via == [-302]
    assert supported.via_kind == "n"


def test_unsupported_via_way_restriction_lands_in_report_not_dropped(synthetic_fixtures_dir):
    graph, issues = _graph(synthetic_fixtures_dir)
    unsupported = next(t for t in graph.turn_restrictions if t.relation_id == -502)
    assert unsupported.supported is False
    assert unsupported.unsupported_reason is not None
    assert "via-way" in unsupported.unsupported_reason
    # And it must show up in the quality report issues, not vanish.
    matches = [i for i in issues if i.kind == "unsupported_restriction" and i.feature_id == "relation/-502"]
    assert matches, "unsupported restriction must be reported, never silently dropped"


def test_crossing_in_plan_view_is_never_decided_by_shapely():
    # Structural guarantee: graph.py must not IMPORT shapely at all --
    # junction detection is purely node-reference-based. This protects
    # against a future edit accidentally reintroducing a planar-crossing
    # check. (The module's docstring is allowed to mention "shapely" in
    # prose explaining why; only real import statements are checked.)
    import ast
    import inspect

    from map_pipeline.pipeline import graph as graph_module

    tree = ast.parse(inspect.getsource(graph_module))
    imported_names = set()
    for node in ast.walk(tree):
        if isinstance(node, ast.Import):
            imported_names.update(alias.name.split(".")[0] for alias in node.names)
        elif isinstance(node, ast.ImportFrom) and node.module:
            imported_names.add(node.module.split(".")[0])
    assert "shapely" not in imported_names
