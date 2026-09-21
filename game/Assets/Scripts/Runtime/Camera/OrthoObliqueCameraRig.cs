// UNCOMPILED: never built by a Unity Editor. See
// game/Assets/Scripts/Runtime/README.md.
using UnityEngine;

namespace Thaivia.Runtime.CameraRig
{
    public enum ViewMode
    {
        TopDown,
        Oblique,
    }

    /// <summary>
    /// One orthographic camera that switches between a pure top-down pitch
    /// (90 degrees) and a fixed oblique pitch over the SAME scene
    /// geometry -- there is no separate "oblique-only" mesh or scene; both
    /// modes read the meshes RoadMeshBuilder/BuildingMeshBuilder/
    /// WaterMeshBuilder already built, so grade separation (a bridge drawn
    /// above a tunnel) stays visually consistent between the two angles.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class OrthoObliqueCameraRig : MonoBehaviour
    {
        [SerializeField] private float obliquePitchDegrees = 45f;
        [SerializeField] private float transitionSeconds = 0.35f;

        private Camera _camera = null!;
        private ViewMode _mode = ViewMode.TopDown;
        private float _transitionT = 1f;
        private Quaternion _fromRotation;
        private Quaternion _toRotation;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        public void SetMode(ViewMode mode)
        {
            if (mode == _mode)
            {
                return;
            }

            _mode = mode;
            _fromRotation = transform.rotation;
            _toRotation = mode == ViewMode.TopDown
                ? Quaternion.Euler(90f, 0f, 0f)
                : Quaternion.Euler(90f - obliquePitchDegrees, 0f, 0f);
            _transitionT = 0f;
        }

        private void Update()
        {
            if (_transitionT >= 1f)
            {
                return;
            }

            _transitionT = Mathf.Min(1f, _transitionT + Time.deltaTime / Mathf.Max(0.0001f, transitionSeconds));
            transform.rotation = Quaternion.Slerp(_fromRotation, _toRotation, _transitionT);
        }

        public void Pan(Vector2 worldDeltaXZ)
        {
            transform.position += new Vector3(worldDeltaXZ.x, 0f, worldDeltaXZ.y);
        }

        public void Zoom(float orthographicSizeDelta, float minSize = 5f, float maxSize = 2000f)
        {
            _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize + orthographicSizeDelta, minSize, maxSize);
        }
    }
}
