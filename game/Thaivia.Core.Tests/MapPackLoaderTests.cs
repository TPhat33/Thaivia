using System.Linq;
using System.Text;
using System.Text.Json;
using Thaivia.Core.MapPack;
using Thaivia.Core.Serialization;
using Xunit;

namespace Thaivia.Core.Tests;

public class MapPackLoaderTests
{
    [Fact]
    public void LoadsARealPipelinePack_AndSurfacesItAsSynthetic()
    {
        var doc = MapPackLoader.LoadText(TestFixtures.ReadRoadGraphLayersJson());

        Assert.True(doc.Payload.Provenance.Synthetic);
        Assert.NotNull(doc.Payload.Provenance.SyntheticNotice);
        Assert.Equal("th-bkk-pilot-001", doc.Payload.Manifest.MapId);
        Assert.Equal(MapPackVersions.SupportedSchemaVersion, doc.Payload.Manifest.SchemaVersion);
        Assert.StartsWith("sha256:", doc.ContentHash);

        // road_graph_layers fixture: 4 edges, 9 nodes, 7 gateways, one
        // supported + one unsupported turn restriction (see
        // tests/test_pipeline_graph.py on the Python side).
        Assert.Equal(4, doc.Payload.RoadGraph.Edges.Count);
        Assert.Equal(9, doc.Payload.RoadGraph.Nodes.Count);
        Assert.Equal(7, doc.Payload.RoadGraph.Gateways.Count);
        Assert.Contains(doc.Payload.RoadGraph.TurnRestrictions, t => t.Supported);
        Assert.Contains(doc.Payload.RoadGraph.TurnRestrictions, t => !t.Supported);
    }

    [Fact]
    public void LoadsBuildingsWithHolesAndParts_FromTheMultipartFixture()
    {
        var doc = MapPackLoader.LoadText(TestFixtures.ReadMultipartBuildingJson());

        Assert.Single(doc.Payload.GeographyBase.Buildings);
        Assert.Single(doc.Payload.GeographyBase.BuildingParts);
        var building = doc.Payload.GeographyBase.Buildings[0];
        Assert.NotEmpty(building.RingGroupsCanonical);
        Assert.NotEmpty(building.SourceTags.Tags);
    }

    [Fact]
    public void TamperedPayloadByte_IsRejectedOnContentHashMismatch()
    {
        var original = TestFixtures.ReadRoadGraphLayersJson();

        // Flip a single digit inside a coordinate deep in payload,
        // leaving content_hash (declared at the top level) untouched --
        // exactly the "mutate one byte of payload" case the wave asked
        // for.
        var tampered = FlipOneDigitInsidePayload(original);
        Assert.NotEqual(original, tampered);

        var ex = Assert.Throws<MapPackTamperedException>(() => MapPackLoader.LoadText(tampered));
        Assert.NotEqual(ex.DeclaredHash, ex.RecomputedHash);
    }

    [Fact]
    public void UntamperedFixture_LoadsWithoutThrowing_ProvingTheTamperTestIsReal()
    {
        // Companion to the tamper test above: if the hash check were a
        // no-op, both this test and the tamper test would "pass" for the
        // wrong reason. This asserts the untampered file is accepted.
        var doc = MapPackLoader.LoadText(TestFixtures.ReadRoadGraphLayersJson());
        Assert.NotNull(doc);
    }

    [Fact]
    public void SchemaVersionMismatch_IsRejectedBeforeTrustingAnythingElse()
    {
        var mutated = ReplaceJsonString(
            TestFixtures.ReadRoadGraphLayersJson(), "schema_version", "0.1.0", "9.9.9", expectedCount: 3);

        var ex = Assert.Throws<MapPackVersionMismatchException>(() => MapPackLoader.LoadText(mutated));
        Assert.Equal("schema_version", ex.Field);
        Assert.Equal("9.9.9", ex.Found);
        Assert.Equal(MapPackVersions.SupportedSchemaVersion, ex.Expected);
    }

    [Fact]
    public void ImporterVersionMismatch_IsRejected()
    {
        var mutated = ReplaceJsonString(
            TestFixtures.ReadRoadGraphLayersJson(), "importer_version", "0.1.0", "0.2.0", expectedCount: 2);

        var ex = Assert.Throws<MapPackVersionMismatchException>(() => MapPackLoader.LoadText(mutated));
        Assert.Equal("importer_version", ex.Field);
    }

    [Fact]
    public void MissingRequiredField_IsAHardErrorNamingThePath()
    {
        // This must exercise field validation in isolation from hash
        // validation: removing a field also changes payload's bytes, so
        // the top-level content_hash is recomputed here to match the
        // mutated payload (via the SAME CanonicalJson the loader itself
        // uses) -- otherwise this would just re-test the tamper case
        // above under a different name.
        using var doc = JsonDocument.Parse(TestFixtures.ReadRoadGraphLayersJson());
        var root = doc.RootElement;

        var mutatedPayloadJson = RemoveProperty(root.GetProperty("payload"), "map_id");
        using var mutatedPayloadDoc = JsonDocument.Parse(mutatedPayloadJson);
        var newHash = CanonicalJson.ComputeContentHash(mutatedPayloadDoc.RootElement);

        var volatileRaw = root.GetProperty("volatile").GetRawText();
        var finalJson = "{\"volatile\":" + volatileRaw
            + ",\"content_hash\":\"" + newHash + "\""
            + ",\"payload\":" + mutatedPayloadJson + "}";

        var ex = Assert.Throws<MapPackFieldException>(() => MapPackLoader.LoadText(finalJson));
        Assert.Contains("map_id", ex.Path);
    }

    [Fact]
    public void LoadingTheSamePackTwice_YieldsStructurallyEqualGraphs()
    {
        var json = TestFixtures.ReadRoadGraphLayersJson();
        var docA = MapPackLoader.LoadText(json);
        var docB = MapPackLoader.LoadText(json);

        Assert.Equal(docA.ContentHash, docB.ContentHash);
        Assert.Equal(docA.Payload.RoadGraph.Edges.Count, docB.Payload.RoadGraph.Edges.Count);
        Assert.Equal(
            docA.Payload.RoadGraph.Nodes.Select(n => (n.NodeId, n.LocalX, n.LocalZ)).OrderBy(t => t.NodeId).ToList(),
            docB.Payload.RoadGraph.Nodes.Select(n => (n.NodeId, n.LocalX, n.LocalZ)).OrderBy(t => t.NodeId).ToList());
        for (var i = 0; i < docA.Payload.RoadGraph.Edges.Count; i++)
        {
            Assert.Equal(docA.Payload.RoadGraph.Edges[i].WayId, docB.Payload.RoadGraph.Edges[i].WayId);
            Assert.Equal(docA.Payload.RoadGraph.Edges[i].Oneway, docB.Payload.RoadGraph.Edges[i].Oneway);
            Assert.Equal(docA.Payload.RoadGraph.Edges[i].NodeRefs, docB.Payload.RoadGraph.Edges[i].NodeRefs);
        }
    }

    private static string FlipOneDigitInsidePayload(string json)
    {
        // "local_x": <number> appears repeatedly in road_graph.nodes;
        // nudge the first one found strictly inside the payload region by
        // appending a digit, which changes payload bytes without
        // corrupting JSON syntax.
        var marker = "\"local_x\": ";
        var payloadStart = json.IndexOf("\"payload\"", StringComparison.Ordinal);
        var idx = json.IndexOf(marker, payloadStart, StringComparison.Ordinal);
        if (idx < 0)
        {
            throw new InvalidOperationException("fixture did not contain the expected 'local_x' marker");
        }

        var numberStart = idx + marker.Length;
        return json.Insert(numberStart + 1, "9");
    }

    private static string ReplaceJsonString(string json, string key, string from, string to, int expectedCount)
    {
        var pattern = $"\"{key}\": \"{from}\"";
        var replacement = $"\"{key}\": \"{to}\"";
        var count = CountOccurrences(json, pattern);
        Assert.Equal(expectedCount, count);
        return json.Replace(pattern, replacement);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }

        return count;
    }

    private static string RemoveProperty(JsonElement root, string propertyToRemove)
    {
        var sb = new StringBuilder();
        WriteWithoutProperty(root, propertyToRemove, sb);
        return sb.ToString();
    }

    private static void WriteWithoutProperty(JsonElement element, string propertyToRemove, StringBuilder sb)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                sb.Append('{');
                var first = true;
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Name == propertyToRemove)
                    {
                        continue;
                    }

                    if (!first)
                    {
                        sb.Append(',');
                    }

                    first = false;
                    sb.Append(JsonSerializer.Serialize(prop.Name));
                    sb.Append(':');
                    WriteWithoutProperty(prop.Value, propertyToRemove, sb);
                }

                sb.Append('}');
                break;

            case JsonValueKind.Array:
                sb.Append('[');
                var firstItem = true;
                foreach (var item in element.EnumerateArray())
                {
                    if (!firstItem)
                    {
                        sb.Append(',');
                    }

                    firstItem = false;
                    WriteWithoutProperty(item, propertyToRemove, sb);
                }

                sb.Append(']');
                break;

            default:
                sb.Append(element.GetRawText());
                break;
        }
    }
}
