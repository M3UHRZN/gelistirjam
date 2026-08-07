using NUnit.Framework;

namespace Gloomveil.MapGeneration.Tests
{
    public sealed class VariableFootprintLayoutSmokeTests
    {
        [TestCase(1, 2, 0, 1, 2)]
        [TestCase(1, 2, 1, 2, 1)]
        [TestCase(2, 3, 3, 3, 2)]
        public void Rotate_RectangularFootprint_SwapsOnlyOnOddQuarterTurns(
            int width,
            int depth,
            int turns,
            int expectedWidth,
            int expectedDepth)
        {
            GridSize2 rotated = new GridSize2(width, depth).Rotate(turns);

            Assert.That(rotated, Is.EqualTo(new GridSize2(expectedWidth, expectedDepth)));
        }

        [Test]
        public void Overlaps_TouchingHalfOpenBounds_DoesNotCountAsOverlap()
        {
            var left = new GridRect(0, 0, 2, 2);
            var touching = new GridRect(2, 0, 1, 2);
            var overlapping = new GridRect(1, 0, 2, 2);

            Assert.That(left.Overlaps(touching), Is.False);
            Assert.That(left.Overlaps(overlapping), Is.True);
        }
    }
}
