// UNCOMPILED: never built by a Unity Editor. See
// game/Assets/Scripts/Runtime/README.md.
using Thaivia.Runtime.CameraRig;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Thaivia.Runtime.InputHandling
{
    /// <summary>
    /// One-finger drag pans; two-finger pinch zooms. Uses the Unity Input
    /// System's EnhancedTouch API (must be enabled once with
    /// `EnhancedTouchSupport.Enable()` -- done in OnEnable here) so the
    /// same code path works for touch (phone/tablet) and, for desktop
    /// testing in the Editor, a mouse-drag fallback via `Mouse.current`.
    /// </summary>
    public sealed class PanPinchController : MonoBehaviour
    {
        [SerializeField] private OrthoObliqueCameraRig cameraRig = null!;
        [SerializeField] private float mousePanSpeed = 0.02f;
        [SerializeField] private float pinchZoomSpeed = 0.01f;

        private Vector2 _lastMousePosition;
        private bool _mouseDragging;
        private float _lastPinchDistance;
        private bool _pinching;

        private void OnEnable() => EnhancedTouchSupport.Enable();

        private void OnDisable() => EnhancedTouchSupport.Disable();

        private void Update()
        {
            HandleTouch();
            HandleMouseFallback();
        }

        private void HandleTouch()
        {
            var touches = Touch.activeTouches;
            if (touches.Count == 1)
            {
                _pinching = false;
                var t = touches[0];
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Moved)
                {
                    cameraRig.Pan(-t.delta * mousePanSpeed);
                }
            }
            else if (touches.Count >= 2)
            {
                var a = touches[0].screenPosition;
                var b = touches[1].screenPosition;
                var distance = Vector2.Distance(a, b);

                if (!_pinching)
                {
                    _pinching = true;
                    _lastPinchDistance = distance;
                    return;
                }

                var delta = distance - _lastPinchDistance;
                cameraRig.Zoom(-delta * pinchZoomSpeed);
                _lastPinchDistance = distance;
            }
            else
            {
                _pinching = false;
            }
        }

        private void HandleMouseFallback()
        {
            var mouse = Mouse.current;
            if (mouse is null)
            {
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                _mouseDragging = true;
                _lastMousePosition = mouse.position.ReadValue();
            }
            else if (mouse.leftButton.wasReleasedThisFrame)
            {
                _mouseDragging = false;
            }
            else if (_mouseDragging)
            {
                var current = mouse.position.ReadValue();
                var delta = current - _lastMousePosition;
                cameraRig.Pan(-delta * mousePanSpeed);
                _lastMousePosition = current;
            }

            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                cameraRig.Zoom(-scroll * pinchZoomSpeed);
            }
        }
    }
}
