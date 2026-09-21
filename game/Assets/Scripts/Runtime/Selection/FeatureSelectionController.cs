// UNCOMPILED: never built by a Unity Editor. See
// game/Assets/Scripts/Runtime/README.md.
using System;
using Thaivia.Core.MapPack;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Thaivia.Runtime.SelectionHandling
{
    /// <summary>Tag component placed on a spawned road/building/water mesh
    /// GameObject so a raycast pick can recover which MapPack feature it
    /// came from. Carries a reference only -- never a copy -- of the
    /// underlying feature, and this controller never writes back into
    /// it: selection can highlight/read a feature, never mutate its
    /// geometry or tags (GeographyBase has no mutating members to call
    /// even if this tried to).</summary>
    public sealed class MapFeatureRef : MonoBehaviour
    {
        public PolygonFeature? Polygon;
        public LineFeature? Line;
        public RoadEdge? Road;
    }

    public sealed class FeatureSelectionController : MonoBehaviour
    {
        [SerializeField] private Camera pickCamera = null!;
        [SerializeField] private LayerMask pickableLayers = ~0;

        public event Action<MapFeatureRef>? FeatureSelected;
        public event Action? SelectionCleared;

        private void Update()
        {
            var pointer = Pointer.current;
            if (pointer is null || !WasTappedThisFrame(pointer))
            {
                return;
            }

            var screenPos = pointer.position.ReadValue();
            var ray = pickCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out var hit, maxDistance: 100000f, layerMask: pickableLayers))
            {
                var featureRef = hit.collider.GetComponentInParent<MapFeatureRef>();
                if (featureRef != null)
                {
                    FeatureSelected?.Invoke(featureRef);
                    return;
                }
            }

            SelectionCleared?.Invoke();
        }

        private static bool WasTappedThisFrame(Pointer pointer) =>
            pointer is Mouse mouse
                ? mouse.leftButton.wasPressedThisFrame
                : pointer.press.wasPressedThisFrame;
    }
}
