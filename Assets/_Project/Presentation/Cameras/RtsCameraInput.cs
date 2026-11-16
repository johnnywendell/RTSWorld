using UnityEngine;
using UnityEngine.InputSystem;

namespace RTSWorld.Presentation.Cameras
{
    /// <summary>New Input System adapter. UI/player ownership can suppress either input channel.</summary>
    public sealed class RtsCameraInput : MonoBehaviour
    {
        [SerializeField] private bool inputEnabled = true;
        private bool wasDragging;
        public bool InputEnabled { get => inputEnabled; set { inputEnabled = value; if (!value) wasDragging = false; } }
        public bool KeyboardBlocked { get; set; }
        public bool PointerBlocked { get; set; }
        public Rect ExcludedGuiRect { get; set; }
        public static Vector2 EdgePan(Vector2 pointer, Vector2 viewport, float pixels)
        {
            if (viewport.x <= 0 || viewport.y <= 0 || pointer.x < 0 || pointer.y < 0 || pointer.x > viewport.x || pointer.y > viewport.y) return Vector2.zero;
            return Vector2.ClampMagnitude(new Vector2(pointer.x < pixels ? -1 : pointer.x > viewport.x - pixels ? 1 : 0,
                pointer.y < pixels ? -1 : pointer.y > viewport.y - pixels ? 1 : 0), 1);
        }
        public RtsCameraInputFrame Read(RtsCameraSettings settings, bool hasFocus, Vector2 viewport)
        {
            if (!inputEnabled || !hasFocus) { wasDragging = false; return default; }
            Vector2 pan = Vector2.zero, drag = Vector2.zero;
            float rotation = 0, zoom = 0; bool dragging = false;
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            if (!KeyboardBlocked && keyboard != null)
            {
                pan.x = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
                pan.y = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
                rotation = (keyboard.eKey.isPressed ? 1 : 0) - (keyboard.qKey.isPressed ? 1 : 0);
            }
            if (mouse != null)
            {
                var pointer = mouse.position.ReadValue();
                bool inside = pointer.x >= 0 && pointer.y >= 0 && pointer.x <= viewport.x && pointer.y <= viewport.y;
                bool allowed = inside && !PointerBlocked && !ExcludedGuiRect.Contains(new Vector2(pointer.x, viewport.y - pointer.y));
                if (allowed)
                {
                    dragging = mouse.middleButton.isPressed;
                    if (dragging && wasDragging) drag = mouse.delta.ReadValue();
                    if (!dragging && settings.EnableEdgePan) pan += EdgePan(pointer, viewport, settings.EdgePixels);
                    zoom = mouse.scroll.ReadValue().y / 120f;
                }
            }
            wasDragging = dragging;
            return new RtsCameraInputFrame(Vector2.ClampMagnitude(pan, 1), drag, zoom, rotation, dragging);
        }
        private void OnDisable() => wasDragging = false;
    }
}
