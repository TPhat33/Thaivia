// UNCOMPILED: never built by a Unity Editor. See
// game/Assets/Scripts/Runtime/README.md.
using System.Collections.Generic;
using Thaivia.Core.Coordinates;
using Thaivia.Core.MapPack;
using UnityEngine;

namespace Thaivia.Runtime.Rendering
{
    /// <summary>Flat water polygon mesh at a fixed small negative Y offset
    /// (below the default road/building ground plane) so it never
    /// Z-fights with roads that happen to touch the shoreline. Like
    /// BuildingMeshBuilder, this does not attempt roof-style hole capping
    /// for multi-ring water bodies -- a human should swap in a real
    /// polygon triangulator before this ships.</summary>
    public static class WaterMeshBuilder
    {
        private const float WaterYOffsetMeters = -0.05f;

        public static Mesh BuildMesh(PolygonFeature feature)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            foreach (var ring in feature.RingGroupsRender1Cm)
            {
                if (ring.Outer.Count < 3)
                {
                    continue;
                }

                var baseIndex = vertices.Count;
                foreach (var p in ring.Outer)
                {
                    var ground = CoordinateNarrowing.ToUnityGroundPlane(p, WaterYOffsetMeters);
                    vertices.Add(new Vector3(ground.X, ground.Y, ground.Z));
                }

                // Simple fan triangulation from vertex 0 -- correct only
                // for convex outer rings with no holes. Real OSM water
                // polygons are frequently concave/multi-hole; see
                // BuildingMeshBuilder's note on bringing a real
                // triangulator.
                for (var i = 1; i < ring.Outer.Count - 1; i++)
                {
                    triangles.Add(baseIndex);
                    triangles.Add(baseIndex + i);
                    triangles.Add(baseIndex + i + 1);
                }
            }

            var mesh = new Mesh { name = $"water-{feature.SourceId}" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
