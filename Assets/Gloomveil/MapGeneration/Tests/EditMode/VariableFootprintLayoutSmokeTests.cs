using System;
using System.Collections.Generic;
using System.Linq;
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

        [Test]
        public void Rotate_NorthSocketOnTwoByThreeClockwise_BecomesSecondEastSocket()
        {
            var socket = new SocketKey(CardinalDirection.North, 0, 0);

            SocketKey rotated = socket.Rotate(new GridSize2(2, 3), 1);

            Assert.That(
                rotated,
                Is.EqualTo(new SocketKey(CardinalDirection.East, 1, 0)));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void Pack_Defaults_CreateVariedNonOverlappingRoomsAndSeparatedStairs(int floors)
        {
            MapGenerationParameters parameters =
                MapGenerationParameters.CreateProductionDefaults(floors);
            PackedAreaLayout packed = PackFirstValidAreas(parameters, 1042);

            Assert.That(packed.FloorCount, Is.EqualTo(floors));
            for (int floor = 0; floor < floors; floor++)
            {
                PlacedRoom[] rooms = packed.Rooms.Where(room => room.Floor == floor).ToArray();
                string[] actualFootprints = rooms
                    .Select(room => room.FootprintId)
                    .Distinct()
                    .ToArray();

                CollectionAssert.IsSubsetOf(
                    new[] { "1x2", "2x2", "2x3" },
                    actualFootprints);
                Assert.That(
                    rooms.Any(room =>
                        room.CanonicalSize.Width != room.CanonicalSize.Depth &&
                        (room.QuarterTurns & 1) == 1),
                    Is.True,
                    $"Floor {floor} has no odd-quarter-turn non-square room.");
                AssertNoOverlaps(packed.OccupiedRectsForFloor(floor));
            }

            Assert.That(
                packed.Rooms.Any(room => room.FootprintId == "2x3" && (room.QuarterTurns & 1) == 0),
                Is.True);
            Assert.That(
                packed.Rooms.Any(room => room.FootprintId == "2x3" && (room.QuarterTurns & 1) == 1),
                Is.True);

            for (int floor = 0; floor < floors - 1; floor++)
            {
                PlacedStair[] stairs = packed.Stairs
                    .Where(stair => stair.LowerFloor == floor)
                    .ToArray();
                Assert.That(stairs, Has.Length.EqualTo(2));
                Assert.That(
                    CentreManhattanUnits(stairs[0], stairs[1]),
                    Is.GreaterThanOrEqualTo(parameters.MinimumStairCenterSeparationUnits));
            }
        }

        private static PackedAreaLayout PackFirstValidAreas(
            MapGenerationParameters parameters,
            int seed)
        {
            var packer = new RoomAndStairPacker();
            string lastFailure = "No packing attempt ran.";
            for (int attempt = 0; attempt < parameters.MaxLayoutAttempts; attempt++)
            {
                if (packer.TryPack(
                        parameters,
                        new SeededRandom(seed, $"areas-{attempt}"),
                        out PackedAreaLayout packed,
                        out string failure))
                {
                    return packed;
                }

                lastFailure = failure;
            }

            Assert.Fail($"Seed {seed} could not pack: {lastFailure}");
            return null;
        }

        private static void AssertNoOverlaps(IReadOnlyList<GridRect> rectangles)
        {
            for (int left = 0; left < rectangles.Count; left++)
            {
                for (int right = left + 1; right < rectangles.Count; right++)
                {
                    Assert.That(
                        rectangles[left].Overlaps(rectangles[right]),
                        Is.False,
                        $"Overlap: {rectangles[left]} and {rectangles[right]}");
                }
            }
        }

        private static double CentreManhattanUnits(PlacedStair left, PlacedStair right)
        {
            int doubledDistance =
                Math.Abs(left.RotatedRect.CenterX2 - right.RotatedRect.CenterX2) +
                Math.Abs(left.RotatedRect.CenterZ2 - right.RotatedRect.CenterZ2);
            return doubledDistance / 2.0;
        }

    }
}
