using UnityEngine;

namespace TutorialSystem
{
    /// <summary>
    /// The piece that makes the tutorial "camera-agnostic": it converts ANY target — UI or world,
    /// in any render mode — into a position (and bounding box) in <b>screen pixels</b>.
    ///
    /// Once everything is reduced to screen space, the arrow and the spotlight (which live on a
    /// Screen Space Overlay canvas) can position themselves uniformly via
    /// <see cref="RectTransformUtility.ScreenPointToLocalPointInRectangle"/> with a null camera.
    ///
    /// Supported target kinds:
    ///   • UI in Screen Space Overlay   → camera = null
    ///   • UI in Screen Space Camera    → camera = canvas.worldCamera
    ///   • UI in World Space            → camera = canvas.worldCamera
    ///   • World object, Orthographic   → camera = world camera
    ///   • World object, Perspective    → camera = world camera
    /// </summary>
    public static class TutorialScreenPositioner
    {
        // Reusable buffer for RectTransform.GetWorldCorners to avoid per-call allocation.
        private static readonly Vector3[] s_Corners = new Vector3[4];

        /// <summary>
        /// The camera to pass to <c>WorldToScreenPoint</c> for a UI canvas.
        /// Returns null for Screen Space Overlay (which is what the UI APIs expect).
        /// </summary>
        public static Camera CanvasCamera(Canvas canvas)
        {
            if (canvas == null) return null;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        /// <summary>
        /// Screen-pixel position of the target's center. <paramref name="worldCamera"/> is used for
        /// non-UI targets (falls back to <see cref="Camera.main"/> when null).
        /// </summary>
        public static Vector2 GetScreenPoint(TutorialTarget target, Camera worldCamera)
        {
            if (target == null) return Vector2.zero;

            if (target.IsUI)
            {
                RectTransform rt = target.RectTransform;
                Camera cam = CanvasCamera(target.Canvas);
                Vector3 worldCenter = rt.TransformPoint(rt.rect.center);
                return RectTransformUtility.WorldToScreenPoint(cam, worldCenter);
            }

            Camera wcam = worldCamera != null ? worldCamera : Camera.main;
            if (wcam == null) return Vector2.zero;
            Vector3 sp = wcam.WorldToScreenPoint(target.Transform.position);
            return new Vector2(sp.x, sp.y);
        }

        /// <summary>
        /// True if the target is currently in front of the camera (world targets only). UI targets
        /// always return true. Used to hide the arrow/highlight when a world target is behind us.
        /// </summary>
        public static bool IsInFront(TutorialTarget target, Camera worldCamera)
        {
            if (target == null) return false;
            if (target.IsUI) return true;
            Camera wcam = worldCamera != null ? worldCamera : Camera.main;
            if (wcam == null) return false;
            return wcam.WorldToScreenPoint(target.Transform.position).z > 0f;
        }

        /// <summary>
        /// Screen-pixel axis-aligned bounding box of the target. Used to size the spotlight hole and
        /// the highlight ring. Falls back to a small box around the center when no bounds exist.
        /// </summary>
        public static Rect GetScreenRect(TutorialTarget target, Camera worldCamera)
        {
            if (target == null) return Rect.zero;

            if (target.IsUI)
            {
                RectTransform rt = target.RectTransform;
                Camera cam = CanvasCamera(target.Canvas);
                rt.GetWorldCorners(s_Corners);
                return CornersToScreenRect(s_Corners, 4, cam);
            }

            Camera wcam = worldCamera != null ? worldCamera : Camera.main;
            if (wcam == null) return Rect.zero;

            // Prefer a renderer's bounds, then a 3D collider, then a 2D collider.
            Bounds bounds;
            if (TryGetWorldBounds(target.Transform, out bounds))
            {
                return BoundsToScreenRect(bounds, wcam);
            }

            // No bounds — make a default-sized box around the projected point.
            Vector2 center = GetScreenPoint(target, wcam);
            const float fallback = 96f;
            return new Rect(center.x - fallback * 0.5f, center.y - fallback * 0.5f, fallback, fallback);
        }

        private static bool TryGetWorldBounds(Transform t, out Bounds bounds)
        {
            Renderer r = t.GetComponentInChildren<Renderer>();
            if (r != null) { bounds = r.bounds; return true; }

            Collider c = t.GetComponentInChildren<Collider>();
            if (c != null) { bounds = c.bounds; return true; }

            Collider2D c2 = t.GetComponentInChildren<Collider2D>();
            if (c2 != null) { bounds = c2.bounds; return true; }

            bounds = default;
            return false;
        }

        private static Rect BoundsToScreenRect(Bounds b, Camera cam)
        {
            // Project all 8 corners so the box is correct under perspective foreshortening.
            Vector3 c = b.center, e = b.extents;
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3(
                    (i & 1) == 0 ? -e.x : e.x,
                    (i & 2) == 0 ? -e.y : e.y,
                    (i & 4) == 0 ? -e.z : e.z);
                Vector3 sp = cam.WorldToScreenPoint(corner);
                if (sp.x < minX) minX = sp.x;
                if (sp.y < minY) minY = sp.y;
                if (sp.x > maxX) maxX = sp.x;
                if (sp.y > maxY) maxY = sp.y;
            }
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        private static Rect CornersToScreenRect(Vector3[] worldCorners, int count, Camera cam)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, worldCorners[i]);
                if (sp.x < minX) minX = sp.x;
                if (sp.y < minY) minY = sp.y;
                if (sp.x > maxX) maxX = sp.x;
                if (sp.y > maxY) maxY = sp.y;
            }
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }
}
