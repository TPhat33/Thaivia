"""MapPack bake: assembles GeographyBase, RoadGraph, provenance, quality
report, and a separate SimulationInitialization section into one
immutable, versioned, schema-validated, byte-deterministic payload.

Determinism strategy (see docs/decisions for the ADR): the pack is split
into `{"volatile": {...}, "content_hash": "sha256:...", "payload": {...}}`.
`content_hash` is the SHA-256 of the canonical (sorted-key, fixed-
separator) JSON serialization of `payload` ONLY. Anything that varies
run-to-run for identical source bytes + settings (bake timestamp, source
retrieval timestamp) lives in `volatile`, outside the hashed region, so
two bakes of the same source+settings produce the same `content_hash`
even though `volatile.baked_at` differs.

This module performs no filesystem/network I/O; `run_pipeline` takes an
already-parsed `OsmDataset` and returns a plain dict. CLI commands own
reading/writing files.
"""

from __future__ import annotations

import hashlib
import json
from dataclasses import asdict
from datetime import datetime, timezone
from typing import Any

from map_pipeline.pipeline import features as features_mod
from map_pipeline.pipeline.assumptions import building_assumptions, road_assumptions
from map_pipeline.pipeline.boundary import AoiExtent, classify_and_gate
from map_pipeline.pipeline.graph import RoadGraph, build_road_graph
from map_pipeline.pipeline.normalize import canonical_round_coords, quantize_coords_1cm
from map_pipeline.pipeline.osm_parse import OsmDataset
from map_pipeline.pipeline.projection import (
    Projector,
    compute_local_origin,
    measure_roundtrip_error_m,
    validate_crs_for_aoi,
)
from map_pipeline.pipeline.quality import QualityIssue, QualityReport
from map_pipeline.pipeline.settings import IMPORTER_VERSION, SCHEMA_VERSION, effective_settings, settings_hash
from map_pipeline.pipeline.sourcelock import SourceLock
from map_pipeline.pipeline.tags import Assumption

ATTRIBUTION_NOTICE = "© OpenStreetMap contributors"
ODBL_LICENSE_URL = "https://opendatacommons.org/licenses/odbl/"
OSM_COPYRIGHT_URL = "https://www.openstreetmap.org/copyright"


def _quantize_ring_groups(ring_groups: list[dict], projector: Projector) -> tuple[list[dict], list[dict]]:
    canonical_groups = []
    render_groups = []
    for group in ring_groups:
        outer_local = [projector.lonlat_to_local(lon, lat) for lon, lat in group["outer"]]
        holes_local = [[projector.lonlat_to_local(lon, lat) for lon, lat in hole] for hole in group["holes"]]
        canonical_groups.append(
            {
                "outer": [list(c) for c in canonical_round_coords(outer_local)],
                "holes": [[list(c) for c in canonical_round_coords(h)] for h in holes_local],
            }
        )
        render_groups.append(
            {
                "outer": [list(c) for c in quantize_coords_1cm(outer_local)],
                "holes": [[list(c) for c in quantize_coords_1cm(h)] for h in holes_local],
            }
        )
    return canonical_groups, render_groups


def _serialize_polygon_feature(feat, projector: Projector, assumption_fn) -> dict[str, Any]:
    canonical_groups, render_groups = _quantize_ring_groups(feat.ring_groups, projector)
    assumptions: list[Assumption] = assumption_fn(feat.source_tags) if assumption_fn else []
    visual = [a.to_json() for a in assumptions if a.namespace == "visual_assumption"]
    simulation = [a.to_json() for a in assumptions if a.namespace == "simulation_assumption"]
    return {
        "feature_class": feat.feature_class,
        "source_kind": feat.source_kind,
        "source_id": feat.source_id,
        "source_tags": feat.source_tags.to_json(),
        "visual_assumptions": visual,
        "simulation_assumptions": simulation,
        "ring_groups_canonical": canonical_groups,
        "ring_groups_render_1cm": render_groups,
    }


def _serialize_line_feature(feat, projector: Projector) -> dict[str, Any]:
    local = [projector.lonlat_to_local(lon, lat) for lon, lat in feat.coords_lonlat]
    return {
        "feature_class": feat.feature_class,
        "source_kind": feat.source_kind,
        "source_id": feat.source_id,
        "source_tags": feat.source_tags.to_json(),
        "coords_canonical": [list(c) for c in canonical_round_coords(local)],
        "coords_render_1cm": [list(c) for c in quantize_coords_1cm(local)],
    }


def _serialize_road_edge(edge, projector: Projector) -> dict[str, Any]:
    assumptions = road_assumptions(edge.source_tags)
    visual = [a.to_json() for a in assumptions if a.namespace == "visual_assumption"]
    simulation = [a.to_json() for a in assumptions if a.namespace == "simulation_assumption"]
    return {
        "way_id": edge.way_id,
        "source_tags": edge.source_tags.to_json(),
        "visual_assumptions": visual,
        "simulation_assumptions": simulation,
        "node_refs": list(edge.node_refs),
        "coords_canonical": [list(c) for c in canonical_round_coords(edge.coords_local)],
        "coords_render_1cm": [list(c) for c in quantize_coords_1cm(edge.coords_local)],
        "oneway": edge.oneway,
        "layer": edge.layer,
        "bridge": edge.bridge,
        "tunnel": edge.tunnel,
        "grade_separated": edge.grade_separated,
        "access_modes": dict(sorted(edge.access_modes.items())),
        "from_node": edge.from_node,
        "to_node": edge.to_node,
    }


def _count_unknown_fields(polygon_features, edges) -> dict[str, int]:
    counts = {
        "building_height": 0,
        "building_use": 0,
        "road_width_or_lanes": 0,
        "road_maxspeed": 0,
    }
    for feat in polygon_features:
        if feat.feature_class in ("building", "building_part"):
            if not feat.source_tags.has("height") and not feat.source_tags.has("building:levels"):
                counts["building_height"] += 1
            if feat.source_tags.get("building") in (None, "yes"):
                counts["building_use"] += 1
    for edge in edges:
        if not edge.source_tags.has("width") and not edge.source_tags.has("lanes"):
            counts["road_width_or_lanes"] += 1
        if not edge.source_tags.has("maxspeed"):
            counts["road_maxspeed"] += 1
    return counts


def run_pipeline(
    dataset: OsmDataset,
    pilot_area_config: dict[str, Any],
    source_lock: SourceLock,
    *,
    synthetic: bool = False,
    now: datetime | None = None,
) -> dict[str, Any]:
    """Pure function: raw OSM entities + config + a source lock in ->
    a full MapPack dict out. No filesystem/network I/O."""
    map_id = pilot_area_config["map_id"]
    bbox = tuple(pilot_area_config["editable_bbox_wgs84_lonlat"])
    buffer_m = pilot_area_config["context_buffer_m"]
    candidate_crs = pilot_area_config["candidate_projected_crs"]

    crs_validation = validate_crs_for_aoi(candidate_crs, bbox)
    origin = compute_local_origin(candidate_crs, bbox)
    projector = Projector(candidate_crs, origin)

    quality = QualityReport()

    building_features, building_issues = features_mod.extract_building_features(dataset)
    water_polys, water_lines, water_issues = features_mod.extract_water_features(dataset)
    barrier_lines, barrier_issues = features_mod.extract_barrier_features(dataset)
    dup_issues = features_mod.detect_duplicate_poi_building_associations(dataset, building_features)
    road_graph, graph_issues = build_road_graph(dataset, projector)

    extent = AoiExtent(interior_bbox_lonlat=bbox, buffer_m=buffer_m, projector=projector)
    boundary_result = classify_and_gate(extent, dataset, road_graph)

    for issue in (*building_issues, *water_issues, *barrier_issues, *dup_issues, *graph_issues):
        quality.add(issue)

    # Round-trip error is measured over every distinct node coordinate the
    # pipeline actually touched (roads + polygon feature rings), not
    # asserted -- see projection.measure_roundtrip_error_m.
    from map_pipeline.pipeline.normalize import quantize_1cm

    sample_points: set[tuple[float, float]] = set()
    for node in dataset.nodes.values():
        sample_points.add((node.lon, node.lat))
    errors = [
        measure_roundtrip_error_m(projector, lon, lat, quantize_fn=quantize_1cm) for lon, lat in sample_points
    ]
    if errors:
        quality.roundtrip_error_m = {
            "max": max(errors),
            "mean": sum(errors) / len(errors),
            "sample_count": len(errors),
        }

    quality.unknown_counts = _count_unknown_fields(building_features + water_polys, road_graph.edges)
    quality.retained_counts = {
        "buildings": sum(1 for f in building_features if f.feature_class == "building"),
        "building_parts": sum(1 for f in building_features if f.feature_class == "building_part"),
        "water_polygons": len(water_polys),
        "waterways": len(water_lines),
        "barriers": len(barrier_lines),
        "road_edges": len(road_graph.edges),
        "road_nodes": len(road_graph.nodes),
        "turn_restrictions_supported": sum(1 for t in road_graph.turn_restrictions if t.supported),
        "gateways": len(boundary_result.gateways),
    }
    quality.rejected_counts = {
        "dropped_features": len(quality.dropped),
        "topology_conflicts": len(quality.topology_conflicts),
        "incomplete_relations": len(quality.incomplete_relations),
        "unsupported_restrictions": len(quality.unsupported_restrictions),
    }

    geography_base = {
        "buildings": [
            _serialize_polygon_feature(f, projector, building_assumptions)
            for f in building_features
            if f.feature_class == "building"
        ],
        "building_parts": [
            _serialize_polygon_feature(f, projector, building_assumptions)
            for f in building_features
            if f.feature_class == "building_part"
        ],
        "water_polygons": [_serialize_polygon_feature(f, projector, None) for f in water_polys],
        "waterways": [_serialize_line_feature(f, projector) for f in water_lines],
        "barriers": [_serialize_line_feature(f, projector) for f in barrier_lines],
    }

    road_graph_json = {
        "nodes": [
            {
                "node_id": n.node_id,
                "local_x": round(n.local_x, 9),
                "local_z": round(n.local_z, 9),
            }
            for n in sorted(road_graph.nodes.values(), key=lambda n: n.node_id)
        ],
        "edges": [_serialize_road_edge(e, projector) for e in sorted(road_graph.edges, key=lambda e: e.way_id)],
        "junction_node_ids": sorted(road_graph.junction_node_ids),
        "turn_restrictions": [
            {
                "relation_id": t.relation_id,
                "restriction_type": t.restriction_type,
                "from_way": t.from_way,
                "via": list(t.via),
                "via_kind": t.via_kind,
                "to_way": t.to_way,
                "supported": t.supported,
                "unsupported_reason": t.unsupported_reason,
            }
            for t in sorted(road_graph.turn_restrictions, key=lambda t: t.relation_id)
        ],
        "gateways": [g.to_json() for g in sorted(boundary_result.gateways, key=lambda g: g.node_id)],
        "boundary": {
            "editable_bbox_wgs84_lonlat": list(bbox),
            "context_buffer_m": buffer_m,
            "node_classification_counts": {
                cls: sum(1 for v in boundary_result.node_classification.values() if v == cls)
                for cls in ("interior", "buffer", "outside")
            },
        },
    }

    settings = effective_settings(pilot_area_config)
    payload = {
        "manifest": {
            "map_id": map_id,
            "schema_version": SCHEMA_VERSION,
            "importer_version": IMPORTER_VERSION,
            "settings_hash": settings_hash(pilot_area_config),
            "settings": settings,
            "driving_side": pilot_area_config["driving_side"],
            "crs_code": candidate_crs,
            "crs_validation_warnings": crs_validation.warnings,
            "local_origin": {
                "crs_code": origin.crs_code,
                "origin_easting_m": round(origin.origin_easting_m, 9),
                "origin_northing_m": round(origin.origin_northing_m, 9),
            },
            "unity_axis_convention": "X=east_offset_m, Z=north_offset_m, Y=up_reserved_no_elevation_source",
        },
        "provenance": {
            "source_kind": source_lock.source_kind,
            "source_location": source_lock.source_location,
            "sha256": source_lock.sha256,
            "size_bytes": source_lock.size_bytes,
            "source_snapshot_timestamp": source_lock.source_snapshot_timestamp,
            "source_snapshot_unknown_reason": source_lock.source_snapshot_unknown_reason,
            "settings_hash": source_lock.settings_hash,
            "synthetic": synthetic,
            "synthetic_notice": (
                "Derived from a tests/fixtures/synthetic/ fixture. NOT real OSM data and "
                "does not represent any real place in Thailand; never treat as a pilot pack."
                if synthetic
                else None
            ),
            "attribution": {
                "notice": ATTRIBUTION_NOTICE,
                "odbl_license_url": ODBL_LICENSE_URL,
                "copyright_url": OSM_COPYRIGHT_URL,
            },
        },
        "geography_base": geography_base,
        "road_graph": road_graph_json,
        "quality_report": quality.to_json(),
        "simulation_initialization": {
            "schema_version": "0.1.0",
            "note": (
                "G1 scope only: kept structurally separate from GeographyBase per the "
                "three-layer data separation rule. Population/jobs/noise/demand scenario "
                "seeding is G3 work; no simulation statistics are populated here yet."
            ),
            "seed": {},
        },
    }

    now = now or datetime.now(timezone.utc)
    content_hash = compute_content_hash(payload)
    return {
        "volatile": {
            "baked_at": now.isoformat(),
            "source_retrieved_at": source_lock.retrieved_at,
        },
        "content_hash": content_hash,
        "payload": payload,
    }


def canonical_json_bytes(payload: Any) -> bytes:
    return json.dumps(payload, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode("utf-8")


def compute_content_hash(payload: Any) -> str:
    return "sha256:" + hashlib.sha256(canonical_json_bytes(payload)).hexdigest()
