// UNCOMPILED: never built by a Unity Editor. See
// game/Assets/Scripts/Runtime/README.md.
using System.Collections.Generic;
using Thaivia.Core.Attributes;
using Thaivia.Core.Coordinates;
using Thaivia.Core.MapPack;
using UnityEngine;

namespace Thaivia.Runtime.Rendering
{
    /// <summary>
    /// Builds one flat ribbon mesh per RoadEdge from its render_1cm
    /// polyline. Width comes from RoadAttributes.GetWidthMeters (source,
    /// or a visual assumption -- never a bare literal), falling back to a
    /// documented minimum only when even the assumption is Unknown.
    /// Elevation is offset by `edge.Layer` * a fixed visual step so
    /// bridges/tunnels read as separated even though `Layer` itself stays
    /// a semantic integer, never metres, everywhere in Thaivia.Core.
    /// </summary>
    public static class RoadMeshBuilder
    {
        private const float FallbackWidthMeters = 3.0f;
        private const float LayerVisualStepMeters = 3.0f;

        public static Mesh BuildEdgeMesh(RoadEdge edge)
        {
            var widthSource = RoadAttributes.GetWidthMeters(edge);
            var width = widthSource.TryGetDisplayValue(out var w) ? (float)w : FallbackWidthMeters;
            var elevation = edge.Layer * LayerVisualStepMeters;

            var centerline = new List<Vec3F>(edge.CoordsRender1Cm.Count);
            foreach (var p in edge.CoordsRender1Cm)
            {
                centerline.Add(CoordinateNarrowing.ToUnityGroundPlane(p, elevation));
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var i = 0; i < centerline.Count; i++)
            {
                var dir = i < centerline.Count - 1
                    ? ToVector3(centerline[i + 1]) - ToVector3(centerline[i])
                    : ToVector3(centerline[i]) - ToVector3(centerline[i - 1]);
                dir.y = 0f;
                var normal = new Vector3(-dir.z, 0f, dir.x).normalized * (width * 0.5f);
                var c = ToVector3(centerline[i]);
                vertices.Add(c - normal);
                vertices.Add(c + normal);
            }

            for (var i = 0; i < centerline.Count - 1; i++)
            {
                var baseIdx = i * 2;
                triangles.Add(baseIdx);
                triangles.Add(baseIdx + 1);
                triangles.Add(baseIdx + 2);
                triangles.Add(baseIdx + 1);
                triangles.Add(baseIdx + 3);
                triangles.Add(baseIdx + 2);
            }

            var mesh = new Mesh { name = $"road-way-{edge.WayId}" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 ToVector3(Vec3F v) => new(v.X, v.Y, v.Z);
    }
}
