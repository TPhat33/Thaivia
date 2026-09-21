using System.IO;

namespace Thaivia.Core.Tests;

internal static class TestFixtures
{
    public static string RoadGraphLayersPath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "road_graph_layers.synthetic.mappack.json");

    public static string MultipartBuildingPath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "multipart_building.synthetic.mappack.json");

    public static string ReadRoadGraphLayersJson() => File.ReadAllText(RoadGraphLayersPath);

    public static string ReadMultipartBuildingJson() => File.ReadAllText(MultipartBuildingPath);
}
