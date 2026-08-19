using Game.UI.Minimap;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class MinimapViewportProjectionTests
    {
        [Test]
        public void ProjectMapPoint_KeepsPlayerAtCentreAndScalesWorldOffset()
        {
            Rect viewport = new Rect(-50f, -40f, 100f, 80f);

            Vector2 player = MinimapViewportProjection.ProjectMapPoint(
                new Vector2(12f, 7f),
                new Vector2(12f, 7f),
                viewport,
                10f);
            Vector2 east = MinimapViewportProjection.ProjectMapPoint(
                new Vector2(16f, 7f),
                new Vector2(12f, 7f),
                viewport,
                10f);

            Assert.That(player, Is.EqualTo(Vector2.zero));
            Assert.That(east, Is.EqualTo(new Vector2(32f, 0f)));
        }

        [Test]
        public void ArrowLocalPosition_NormalizesDirectionToFixedOrbitRadius()
        {
            Vector2 position = MinimapViewportProjection.ArrowLocalPosition(
                new Vector2(3f, 4f),
                12f);

            Assert.That(position.x, Is.EqualTo(7.2f).Within(0.0001f));
            Assert.That(position.y, Is.EqualTo(9.6f).Within(0.0001f));
        }

        [Test]
        public void EffectiveVisibleDiameter_DividesBaseUnitsByZoomLevel()
        {
            float visibleDiameter = MinimapViewportProjection.EffectiveVisibleDiameter(
                28f,
                2f);

            Assert.That(visibleDiameter, Is.EqualTo(14f));
        }
    }
}
