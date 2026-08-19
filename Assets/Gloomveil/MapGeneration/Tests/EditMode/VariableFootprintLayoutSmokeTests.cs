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

        [Test]
        public void Pack_FourVerticalLinks_CreatesFourSeparatedStairsForTheFloorPair()
        {
            MapGenerationParameters parameters = CreateParametersWithVerticalLinks(4);

            PackedAreaLayout packed = PackFirstValidAreas(parameters, 99999);
            PlacedStair[] stairs = packed.Stairs
                .Where(stair => stair.LowerFloor == 0)
                .ToArray();

            Assert.That(stairs, Has.Length.EqualTo(4));
            for (int left = 0; left < stairs.Length; left++)
            {
                for (int right = left + 1; right < stairs.Length; right++)
                {
                    Assert.That(
                        CentreManhattanUnits(stairs[left], stairs[right]),
                        Is.GreaterThanOrEqualTo(parameters.MinimumStairCenterSeparationUnits));
                }
            }
        }

        [Test]
        public void Generate_FourVerticalLinks_CreatesAConnectedLayoutWithFourStairs()
        {
            MapGenerationParameters parameters = CreateParametersWithVerticalLinks(4);

            MapLayout layout = new VariableFootprintLayoutGenerator()
                .Generate(parameters, 99999);

            Assert.That(layout.TraversalGraph.IsFullyConnected, Is.True);
            Assert.That(
                layout.Stairs.Count(stair => stair.LowerFloor == 0),
                Is.EqualTo(4));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void Generate_Defaults_AreDeterministicConnectedAndVisiblyVaried(int floors)
        {
            MapGenerationParameters parameters =
                MapGenerationParameters.CreateProductionDefaults(floors);
            var generator = new VariableFootprintLayoutGenerator();

            MapLayout first = generator.Generate(parameters, 1042);
            MapLayout second = generator.Generate(parameters, 1042);

            new GameplayRoleAssigner().Assign(
                first,
                parameters,
                new SeededRandom(1042, "roles"));
            new GameplayRoleAssigner().Assign(
                second,
                parameters,
                new SeededRandom(1042, "roles"));

            Assert.That(second.CreateSignature(), Is.EqualTo(first.CreateSignature()));
            Assert.That(first.TraversalGraph.IsFullyConnected, Is.True);
            Assert.That(
                first.CorridorModules
                    .Where(module => module.Shape == CorridorShape.Straight)
                    .Select(module => module.LengthUnits)
                    .Distinct()
                    .Count(),
                Is.GreaterThanOrEqualTo(2));
            Assert.That(
                first.Rooms.Select(room => room.FootprintId).Distinct(),
                Is.SupersetOf(new[] { "1x2", "2x2", "2x3" }));

            IReadOnlyDictionary<GridCoord3, CorridorConnectionMask> corridorMasks =
                first.CorridorTiles.ToDictionary(tile => tile.Coord, tile => tile.ConnectionMask);
            MapPort[] selectedAreaPorts = first.Rooms
                .SelectMany(room => room.Ports)
                .Concat(first.Stairs.SelectMany(StairPorts))
                .ToArray();
            foreach (MapPort port in selectedAreaPorts)
            {
                Assert.That(
                    corridorMasks.TryGetValue(port.WorldOutsideCell, out CorridorConnectionMask mask),
                    Is.True,
                    $"Port {port.WorldEdge} has no corridor tile at {port.WorldOutsideCell}.");
                Assert.That(
                    (mask & ConnectionTowardArea(port.WorldFacing)) != 0,
                    Is.True,
                    $"Corridor tile {port.WorldOutsideCell} is closed toward port {port.WorldEdge}.");
            }

            var stairLandingCells = new HashSet<GridCoord3>(
                first.Stairs.SelectMany(StairPorts).Select(port => port.WorldOutsideCell));
            foreach (CorridorModulePlacement module in first.CorridorModules
                         .Where(module => module.LengthUnits > 1))
            {
                GridSize2 rotated = module.RotatedSize;
                var rect = new GridRect(
                    module.SouthwestOrigin.X,
                    module.SouthwestOrigin.Z,
                    rotated.Width,
                    rotated.Depth);
                Assert.That(
                    stairLandingCells.Any(cell =>
                        cell.Floor == module.SouthwestOrigin.Floor &&
                        cell.X >= rect.X && cell.X < rect.MaxX &&
                        cell.Z >= rect.Z && cell.Z < rect.MaxZ),
                    Is.False,
                    $"Corridor module {module.PlacementId} compresses across a stair landing.");
            }

            MapValidationReport report = new MapLayoutValidator().Validate(first, parameters);
            Assert.That(
                report.IsValid,
                Is.True,
                string.Join(Environment.NewLine, report.Errors));

            PlacedRoom start = first.Rooms.Single(room => room.Role == RoomRole.Start);
            PlacedRoom exit = first.Rooms.Single(room => room.Role == RoomRole.Exit);
            IReadOnlyDictionary<int, int> distances =
                MapGraphSearch.WeightedDistancesFrom(
                    first.TraversalGraph,
                    start.GraphNodeId);
            int farthestEligible = first.Rooms
                .Where(room => room.Role != RoomRole.Start)
                .Max(room => distances[room.GraphNodeId]);
            Assert.That(distances[exit.GraphNodeId], Is.EqualTo(farthestEligible));
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

        private static MapGenerationParameters CreateParametersWithVerticalLinks(int verticalLinks)
        {
            MapGenerationParameters defaults =
                MapGenerationParameters.CreateProductionDefaults(2);
            return new MapGenerationParameters(
                widthUnits: 20,
                depthUnits: 20,
                floorCount: defaults.FloorCount,
                minRoomsPerFloor: defaults.MinRoomsPerFloor,
                maxRoomsPerFloor: defaults.MaxRoomsPerFloor,
                roomClearanceUnits: defaults.RoomClearanceUnits,
                extraRoomLinksPerFloor: defaults.ExtraRoomLinksPerFloor,
                verticalLinksPerFloorPair: verticalLinks,
                minimumStairCenterSeparationUnits: defaults.MinimumStairCenterSeparationUnits,
                verticalTraversalCostUnits: defaults.VerticalTraversalCostUnits,
                fuseCount: defaults.FuseCount,
                safeZoneCount: defaults.SafeZoneCount,
                maxLayoutAttempts: defaults.MaxLayoutAttempts,
                maxPlacementAttemptsPerArea: defaults.MaxPlacementAttemptsPerArea,
                roomFootprints: defaults.RoomFootprints,
                stairFootprints: defaults.StairFootprints,
                corridorRunLengths: defaults.CorridorRunLengths);
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

        private static IEnumerable<MapPort> StairPorts(PlacedStair stair)
        {
            if (stair.BottomPort.HasValue)
            {
                yield return stair.BottomPort.Value;
            }

            if (stair.TopPort.HasValue)
            {
                yield return stair.TopPort.Value;
            }
        }

        private static CorridorConnectionMask ConnectionTowardArea(
            CardinalDirection areaPortFacing)
        {
            switch (areaPortFacing)
            {
                case CardinalDirection.North:
                    return CorridorConnectionMask.South;
                case CardinalDirection.East:
                    return CorridorConnectionMask.West;
                case CardinalDirection.South:
                    return CorridorConnectionMask.North;
                case CardinalDirection.West:
                    return CorridorConnectionMask.East;
                default:
                    throw new ArgumentOutOfRangeException(nameof(areaPortFacing));
            }
        }

    }
}
