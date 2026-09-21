// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Thaivia.Core.MapPack;

namespace Thaivia.Core.Serialization;

/// <summary>
/// Loads and strictly validates a MapPack JSON file into a
/// <see cref="MapPackDocument"/>. Validation order (each step's failure
/// stops loading immediately, never falling through to a partial
/// result):
///   1. Parse as JSON.
///   2. Every field this loader's contract requires is present with the
///      right JSON kind (<see cref="JsonRequire"/>) -- missing/mistyped
///      required fields throw <see cref="MapPackFieldException"/>.
///   3. `manifest.schema_version`/`importer_version` match
///      <see cref="MapPackVersions"/> -- otherwise
///      <see cref="MapPackVersionMismatchException"/>.
///   4. `content_hash` matches a fresh SHA-256 of the canonical
///      serialization of `payload` (<see cref="CanonicalJson"/>) --
///      otherwise <see cref="MapPackTamperedException"/>.
///   5. Only then is the strongly-typed object graph built and returned.
/// </summary>
public static class MapPackLoader
{
    public static MapPackDocument LoadFile(string path)
    {
        var json = File.ReadAllText(path);
        return LoadText(json);
    }

    public static MapPackDocument LoadText(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return Load(doc.RootElement);
    }

    public static MapPackDocument Load(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new MapPackFieldException("$", $"expected a JSON object at the document root, found {root.ValueKind}.");
        }

        var volatileElement = JsonRequire.Object(root, "volatile", "$");
        var contentHash = JsonRequire.String(root, "content_hash", "$");
        var payloadElement = JsonRequire.Object(root, "payload", "$");

        var manifestElement = JsonRequire.Object(payloadElement, "manifest", "$.payload");
        var schemaVersion = JsonRequire.String(manifestElement, "schema_version", "$.payload.manifest");
        if (schemaVersion != MapPackVersions.SupportedSchemaVersion)
        {
            throw new MapPackVersionMismatchException(
                "schema_version", schemaVersion, MapPackVersions.SupportedSchemaVersion);
        }

        var importerVersion = JsonRequire.String(manifestElement, "importer_version", "$.payload.manifest");
        if (importerVersion != MapPackVersions.SupportedImporterVersion)
        {
            throw new MapPackVersionMismatchException(
                "importer_version", importerVersion, MapPackVersions.SupportedImporterVersion);
        }

        // Recompute content_hash from the payload bytes actually in the
        // file BEFORE trusting anything else in payload -- a tampered file
        // must never reach the point of being turned into typed objects.
        var recomputed = CanonicalJson.ComputeContentHash(payloadElement);
        if (recomputed != contentHash)
        {
            throw new MapPackTamperedException(contentHash, recomputed);
        }

        var volatileFields = new MapPackVolatile(
            JsonRequire.String(volatileElement, "baked_at", "$.volatile"),
            JsonRequire.String(volatileElement, "source_retrieved_at", "$.volatile"));

        var manifest = ReadManifest(manifestElement, schemaVersion, importerVersion);
        var provenance = ReadProvenance(JsonRequire.Object(payloadElement, "provenance", "$.payload"));
        var geographyBase = ReadGeographyBase(JsonRequire.Object(payloadElement, "geography_base", "$.payload"));
        var roadGraph = ReadRoadGraph(JsonRequire.Object(payloadElement, "road_graph", "$.payload"));
        var qualityReport = ReadQualityReport(JsonRequire.Object(payloadElement, "quality_report", "$.payload"));
        var simulationInit = ReadSimulationInitialization(
            JsonRequire.Object(payloadElement, "simulation_initialization", "$.payload"));

        var payload = new MapPackPayload(manifest, provenance, geographyBase, roadGraph, qualityReport, simulationInit);
        return new MapPackDocument(volatileFields, contentHash, payload);
    }

    private static Manifest ReadManifest(JsonElement m, string schemaVersion, string importerVersion)
    {
        const string path = "$.payload.manifest";
        var drivingSideRaw = JsonRequire.String(m, "driving_side", path);
        var drivingSide = drivingSideRaw switch
        {
            "left" => DrivingSide.Left,
            "right" => DrivingSide.Right,
            _ => throw new MapPackFieldException($"{path}.driving_side", $"expected 'left' or 'right', found '{drivingSideRaw}'."),
        };

        var localOriginElement = JsonRequire.Object(m, "local_origin", path);
        var localOrigin = new LocalOrigin(
            JsonRequire.String(localOriginElement, "crs_code", $"{path}.local_origin"),
            JsonRequire.Double(localOriginElement, "origin_easting_m", $"{path}.local_origin"),
            JsonRequire.Double(localOriginElement, "origin_northing_m", $"{path}.local_origin"));

        var warningsElement = JsonRequire.Array(m, "crs_validation_warnings", path);
        var warnings = new List<string>();
        foreach (var w in warningsElement.EnumerateArray())
        {
            warnings.Add(w.GetString() ?? string.Empty);
        }

        return new Manifest(
            JsonRequire.String(m, "map_id", path),
            schemaVersion,
            importerVersion,
            JsonRequire.String(m, "settings_hash", path),
            JsonRequire.Object(m, "settings", path),
            drivingSide,
            JsonRequire.String(m, "crs_code", path),
            warnings,
            localOrigin,
            JsonRequire.String(m, "unity_axis_convention", path));
    }

    private static Provenance ReadProvenance(JsonElement p)
    {
        const string path = "$.payload.provenance";
        var sourceKindRaw = JsonRequire.String(p, "source_kind", path);
        var sourceKind = sourceKindRaw switch
        {
            "url" => ProvenanceSourceKind.Url,
            "local_file" => ProvenanceSourceKind.LocalFile,
            _ => throw new MapPackFieldException($"{path}.source_kind", $"expected 'url' or 'local_file', found '{sourceKindRaw}'."),
        };

        var attributionElement = JsonRequire.Object(p, "attribution", path);
        var attribution = new Attribution(
            JsonRequire.String(attributionElement, "notice", $"{path}.attribution"),
            JsonRequire.String(attributionElement, "odbl_license_url", $"{path}.attribution"),
            JsonRequire.String(attributionElement, "copyright_url", $"{path}.attribution"));

        return new Provenance(
            sourceKind,
            JsonRequire.String(p, "source_location", path),
            JsonRequire.String(p, "sha256", path),
            JsonRequire.Int64(p, "size_bytes", path),
            JsonRequire.NullableString(p, "source_snapshot_timestamp", path),
            JsonRequire.NullableString(p, "source_snapshot_unknown_reason", path),
            JsonRequire.String(p, "settings_hash", path),
            JsonRequire.Bool(p, "synthetic", path),
            ReadOptionalNullableString(p, "synthetic_notice"),
            attribution);
    }

    private static string? ReadOptionalNullableString(JsonElement obj, string name)
    {
        var value = JsonRequire.OptionalProperty(obj, name);
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            _ => null,
        };
    }

    private static GeographyBase ReadGeographyBase(JsonElement g)
    {
        const string path = "$.payload.geography_base";
        return new GeographyBase(
            ReadPolygonFeatures(JsonRequire.Array(g, "buildings", path), $"{path}.buildings"),
            ReadPolygonFeatures(JsonRequire.Array(g, "building_parts", path), $"{path}.building_parts"),
            ReadPolygonFeatures(JsonRequire.Array(g, "water_polygons", path), $"{path}.water_polygons"),
            ReadLineFeatures(JsonRequire.Array(g, "waterways", path), $"{path}.waterways"),
            ReadLineFeatures(JsonRequire.Array(g, "barriers", path), $"{path}.barriers"));
    }

    private static List<PolygonFeature> ReadPolygonFeatures(JsonElement arr, string path) =>
        JsonRequire.MapArray(arr, path, (item, itemPath, _) =>
        {
            return new PolygonFeature(
                JsonRequire.String(item, "feature_class", itemPath),
                JsonRequire.String(item, "source_kind", itemPath),
                JsonRequire.Int64(item, "source_id", itemPath),
                ReadSourceTags(JsonRequire.Object(item, "source_tags", itemPath)),
                ReadAssumptions(JsonRequire.Array(item, "visual_assumptions", itemPath), $"{itemPath}.visual_assumptions"),
                ReadAssumptions(JsonRequire.Array(item, "simulation_assumptions", itemPath), $"{itemPath}.simulation_assumptions"),
                ReadRingGroups(JsonRequire.Array(item, "ring_groups_canonical", itemPath), $"{itemPath}.ring_groups_canonical"),
                ReadRingGroups(JsonRequire.Array(item, "ring_groups_render_1cm", itemPath), $"{itemPath}.ring_groups_render_1cm"));
        });

    private static List<LineFeature> ReadLineFeatures(JsonElement arr, string path) =>
        JsonRequire.MapArray(arr, path, (item, itemPath, _) =>
        {
            return new LineFeature(
                JsonRequire.String(item, "feature_class", itemPath),
                JsonRequire.String(item, "source_kind", itemPath),
                JsonRequire.Int64(item, "source_id", itemPath),
                ReadSourceTags(JsonRequire.Object(item, "source_tags", itemPath)),
                ReadCoords(JsonRequire.Array(item, "coords_canonical", itemPath)),
                ReadCoords(JsonRequire.Array(item, "coords_render_1cm", itemPath)));
        });

    private static SourceTags ReadSourceTags(JsonElement obj)
    {
        var tags = new Dictionary<string, string>();
        foreach (var prop in obj.EnumerateObject())
        {
            tags[prop.Name] = prop.Value.GetString() ?? string.Empty;
        }

        return new SourceTags(tags);
    }

    private static List<AssumptionRecord> ReadAssumptions(JsonElement arr, string path) =>
        JsonRequire.MapArray(arr, path, (item, itemPath, _) => new AssumptionRecord(
            JsonRequire.String(item, "field", itemPath),
            JsonRequire.Property(item, "value", itemPath),
            JsonRequire.String(item, "rule", itemPath)));

    private static List<RingGroup> ReadRingGroups(JsonElement arr, string path) =>
        JsonRequire.MapArray(arr, path, (item, itemPath, _) =>
        {
            var outer = ReadCoords(JsonRequire.Array(item, "outer", itemPath));
            var holesArray = JsonRequire.Array(item, "holes", itemPath);
            var holes = new List<IReadOnlyList<Vec2>>();
            foreach (var hole in holesArray.EnumerateArray())
            {
                holes.Add(ReadCoordsFromElement(hole, itemPath));
            }

            return new RingGroup(outer, holes);
        });

    private static List<Vec2> ReadCoords(JsonElement arr) => ReadCoordsFromElement(arr, "$");

    private static List<Vec2> ReadCoordsFromElement(JsonElement arr, string path)
    {
        var result = new List<Vec2>();
        var i = 0;
        foreach (var pair in arr.EnumerateArray())
        {
            if (pair.ValueKind != JsonValueKind.Array || pair.GetArrayLength() != 2)
            {
                throw new MapPackFieldException($"{path}[{i}]", "expected a 2-element [x, z] coordinate array.");
            }

            var x = pair[0].GetDouble();
            var z = pair[1].GetDouble();
            result.Add(new Vec2(x, z));
            i++;
        }

        return result;
    }

    private static RoadGraph ReadRoadGraph(JsonElement r)
    {
        const string path = "$.payload.road_graph";

        var nodes = JsonRequire.MapArray(JsonRequire.Array(r, "nodes", path), $"{path}.nodes", (item, itemPath, _) =>
            new RoadGraphNode(
                JsonRequire.Int64(item, "node_id", itemPath),
                JsonRequire.Double(item, "local_x", itemPath),
                JsonRequire.Double(item, "local_z", itemPath)));

        var edges = JsonRequire.MapArray(JsonRequire.Array(r, "edges", path), $"{path}.edges", (item, itemPath, _) =>
            ReadRoadEdge(item, itemPath));

        var junctionIds = new HashSet<long>();
        foreach (var idElement in JsonRequire.Array(r, "junction_node_ids", path).EnumerateArray())
        {
            junctionIds.Add(idElement.GetInt64());
        }

        var restrictions = JsonRequire.MapArray(
            JsonRequire.Array(r, "turn_restrictions", path), $"{path}.turn_restrictions", ReadTurnRestriction);

        var gateways = JsonRequire.MapArray(JsonRequire.Array(r, "gateways", path), $"{path}.gateways", (item, itemPath, _) =>
            new Gateway(
                JsonRequire.Int64(item, "node_id", itemPath),
                JsonRequire.Int64(item, "way_id", itemPath),
                JsonRequire.Double(item, "lon", itemPath),
                JsonRequire.Double(item, "lat", itemPath),
                JsonRequire.Int32(item, "inbound_demand_veh_per_hour", itemPath),
                JsonRequire.Int32(item, "outbound_demand_veh_per_hour", itemPath),
                JsonRequire.Int32(item, "external_capacity_veh_per_hour", itemPath),
                JsonRequire.String(item, "rule", itemPath),
                JsonRequire.String(item, "namespace", itemPath)));

        var boundaryElement = JsonRequire.Object(r, "boundary", path);
        var bboxElement = JsonRequire.Array(boundaryElement, "editable_bbox_wgs84_lonlat", $"{path}.boundary");
        var bbox = new List<double>();
        foreach (var v in bboxElement.EnumerateArray())
        {
            bbox.Add(v.GetDouble());
        }

        var countsElement = JsonRequire.Object(boundaryElement, "node_classification_counts", $"{path}.boundary");
        var counts = new Dictionary<string, int>();
        foreach (var prop in countsElement.EnumerateObject())
        {
            counts[prop.Name] = prop.Value.GetInt32();
        }

        var boundary = new RoadGraphBoundary(
            bbox,
            JsonRequire.Double(boundaryElement, "context_buffer_m", $"{path}.boundary"),
            counts);

        return new RoadGraph(nodes, edges, junctionIds, restrictions, gateways, boundary);
    }

    private static RoadEdge ReadRoadEdge(JsonElement item, string itemPath)
    {
        var onewayRaw = JsonRequire.String(item, "oneway", itemPath);
        var oneway = onewayRaw switch
        {
            "no" => OnewayDirection.No,
            "forward" => OnewayDirection.Forward,
            "reversed" => OnewayDirection.Reversed,
            _ => throw new MapPackFieldException($"{itemPath}.oneway", $"expected 'no'/'forward'/'reversed', found '{onewayRaw}'."),
        };

        var nodeRefsElement = JsonRequire.Array(item, "node_refs", itemPath);
        var nodeRefs = new List<long>();
        foreach (var n in nodeRefsElement.EnumerateArray())
        {
            nodeRefs.Add(n.GetInt64());
        }

        var accessModesElement = JsonRequire.Object(item, "access_modes", itemPath);
        var accessModes = new Dictionary<string, string>();
        foreach (var prop in accessModesElement.EnumerateObject())
        {
            accessModes[prop.Name] = prop.Value.GetString() ?? string.Empty;
        }

        return new RoadEdge(
            JsonRequire.Int64(item, "way_id", itemPath),
            ReadSourceTags(JsonRequire.Object(item, "source_tags", itemPath)),
            ReadAssumptions(JsonRequire.Array(item, "visual_assumptions", itemPath), $"{itemPath}.visual_assumptions"),
            ReadAssumptions(JsonRequire.Array(item, "simulation_assumptions", itemPath), $"{itemPath}.simulation_assumptions"),
            nodeRefs,
            ReadCoords(JsonRequire.Array(item, "coords_canonical", itemPath)),
            ReadCoords(JsonRequire.Array(item, "coords_render_1cm", itemPath)),
            oneway,
            JsonRequire.Int32(item, "layer", itemPath),
            JsonRequire.Bool(item, "bridge", itemPath),
            JsonRequire.Bool(item, "tunnel", itemPath),
            JsonRequire.Bool(item, "grade_separated", itemPath),
            accessModes,
            JsonRequire.Int64(item, "from_node", itemPath),
            JsonRequire.Int64(item, "to_node", itemPath));
    }

    private static TurnRestrictionRecord ReadTurnRestriction(JsonElement item, string itemPath, int _)
    {
        var viaElement = JsonRequire.Array(item, "via", itemPath);
        var via = new List<long>();
        foreach (var v in viaElement.EnumerateArray())
        {
            via.Add(v.GetInt64());
        }

        return new TurnRestrictionRecord(
            JsonRequire.Int64(item, "relation_id", itemPath),
            JsonRequire.String(item, "restriction_type", itemPath),
            ReadNullableInt64(item, "from_way"),
            via,
            JsonRequire.String(item, "via_kind", itemPath),
            ReadNullableInt64(item, "to_way"),
            JsonRequire.Bool(item, "supported", itemPath),
            ReadOptionalNullableString(item, "unsupported_reason"));
    }

    private static long? ReadNullableInt64(JsonElement obj, string name)
    {
        var value = JsonRequire.OptionalProperty(obj, name);
        return value.ValueKind == JsonValueKind.Number ? value.GetInt64() : null;
    }

    private static QualityReport ReadQualityReport(JsonElement q)
    {
        const string path = "$.payload.quality_report";
        return new QualityReport(
            ReadIntDict(JsonRequire.Object(q, "unknown_counts", path)),
            ReadIssues(JsonRequire.Array(q, "dropped", path), $"{path}.dropped"),
            ReadIssues(JsonRequire.Array(q, "quarantined", path), $"{path}.quarantined"),
            ReadIssues(JsonRequire.Array(q, "incomplete_relations", path), $"{path}.incomplete_relations"),
            ReadIssues(JsonRequire.Array(q, "unsupported_restrictions", path), $"{path}.unsupported_restrictions"),
            ReadIssues(JsonRequire.Array(q, "topology_conflicts", path), $"{path}.topology_conflicts"),
            ReadIssues(JsonRequire.Array(q, "duplicate_associations", path), $"{path}.duplicate_associations"),
            ReadIntDict(JsonRequire.Object(q, "retained_counts", path)),
            ReadIntDict(JsonRequire.Object(q, "rejected_counts", path)),
            ReadDoubleDict(JsonRequire.Object(q, "roundtrip_error_m", path)));
    }

    private static List<QualityIssueRecord> ReadIssues(JsonElement arr, string path) =>
        JsonRequire.MapArray(arr, path, (item, itemPath, _) => new QualityIssueRecord(
            JsonRequire.String(item, "kind", itemPath),
            JsonRequire.String(item, "feature_id", itemPath),
            JsonRequire.String(item, "detail", itemPath)));

    private static Dictionary<string, int> ReadIntDict(JsonElement obj)
    {
        var result = new Dictionary<string, int>();
        foreach (var prop in obj.EnumerateObject())
        {
            result[prop.Name] = prop.Value.GetInt32();
        }

        return result;
    }

    private static Dictionary<string, double> ReadDoubleDict(JsonElement obj)
    {
        var result = new Dictionary<string, double>();
        foreach (var prop in obj.EnumerateObject())
        {
            result[prop.Name] = prop.Value.GetDouble();
        }

        return result;
    }

    private static SimulationInitialization ReadSimulationInitialization(JsonElement s)
    {
        const string path = "$.payload.simulation_initialization";
        return new SimulationInitialization(
            JsonRequire.String(s, "schema_version", path),
            JsonRequire.String(s, "note", path),
            JsonRequire.Object(s, "seed", path));
    }
}
