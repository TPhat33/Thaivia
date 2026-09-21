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
    /// Extrudes a PolygonFeature's outer ring(s) (holes are triangulated
    /// out, not filled) to BuildingAttributes.GetHeightMeters -- which may
    /// be Known, Assumed, or Unknown. This builder never treats an Unknown
    /// height as 0m: it uses a documented placeholder height AND expects
    /// the caller (the inspector/material layer) to mark the building as
    /// "height unknown" via a distinct material/outline, per the
    /// source/unknown/assumption visual contract this wave's inspector
    /// panel implements.
    /// </summary>
    public static class BuildingMeshBuilder
    {
        private const float UnknownHeightPlaceholderMeters = 6.0f; // ~2 storeys; visually flagged elsewhere.

        public static Mesh BuildMesh(PolygonFeature feature, out bool heightIsKnownOrAssumed)
        {
            var heightSource = BuildingAttributes.GetHeightMeters(feature);
            heightIsKnownOrAssumed = heightSource.TryGetDisplayValue(out var heightValue);
            var height = heightIsKnownOrAssumed ? (float)heightValue : UnknownHeightPlaceholderMeters;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            foreach (var ring in feature.RingGroupsRender1Cm)
            {
                if (ring.Outer.Count < 3)
                {
                    continue; // degenerate ring; skip rather than emit a broken mesh silently claiming success.
                }

                var baseIndex = vertices.Count;
                var floorPoints = new List<Vector3>(ring.Outer.Count);
                foreach (var p in ring.Outer)
                {
                    var ground = CoordinateNarrowing.ToUnityGroundPlane(p, 0f);
                    floorPoints.Add(new Vector3(ground.X, ground.Y, ground.Z));
                }

                // Floor + roof vertices.
                foreach (var fp in floorPoints)
                {
                    vertices.Add(fp);
                }

                foreach (var fp in floorPoints)
                {
                    vertices.Add(fp + Vector3.up * height);
                }

                var n = floorPoints.Count;
                for (var i = 0; i < n; i++)
                {
                    var next = (i + 1) % n;
                    var floorA = baseIndex + i;
                    var floorB = baseIndex + next;
                    var roofA = baseIndex + n + i;
                    var roofB = baseIndex + n + next;

                    triangles.Add(floorA);
                    triangles.Add(roofA);
                    triangles.Add(floorB);

                    triangles.Add(floorB);
                    triangles.Add(roofA);
                    triangles.Add(roofB);
                }

                // NOTE: no roof cap triangulation here -- holes make a
                // general roof fan/ear-clip nontrivial and this file has
                // never been exercised by a compiler, let alone a
                // triangulation edge case. A human implementing this for
                // real should bring a tested polygon triangulator (Unity's
                // own `UnityEngine.U2D` or a package) rather than trust an
                // untested hand-rolled ear-clip here.
            }

            var mesh = new Mesh { name = $"{feature.FeatureClass}-{feature.SourceId}" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
