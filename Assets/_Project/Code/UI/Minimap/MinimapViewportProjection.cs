using System;
using UnityEngine;

namespace Game.UI.Minimap
{
    public static class MinimapViewportProjection
    {
        public static float EffectiveVisibleDiameter(float visibleMapUnits, float zoomLevel)
        {
            if (visibleMapUnits <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(visibleMapUnits));
            }

            if (zoomLevel <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(zoomLevel));
            }

            return visibleMapUnits / zoomLevel;
        }

        public static Vector2 ProjectMapPoint(
            Vector2 mapPoint,
            Vector2 playerMapPosition,
            Rect viewport,
            float visibleDiameterUnits)
        {
            if (visibleDiameterUnits <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(visibleDiameterUnits));
            }

            float scale = Mathf.Min(viewport.width, viewport.height) / visibleDiameterUnits;
            return viewport.center + ((mapPoint - playerMapPosition) * scale);
        }

        public static Vector2 ArrowLocalPosition(Vector2 direction, float orbitRadius)
        {
            if (orbitRadius < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(orbitRadius));
            }

            return direction.sqrMagnitude > 0.0001f
                ? direction.normalized * orbitRadius
                : Vector2.zero;
        }
    }
}
