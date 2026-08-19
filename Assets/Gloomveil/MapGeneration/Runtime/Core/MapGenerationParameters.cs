using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Gloomveil.MapGeneration
{
    public sealed class MapGenerationParameters
    {
        public int WidthUnits { get; }
        public int DepthUnits { get; }
        public int FloorCount { get; }
        public int MinRoomsPerFloor { get; }
        public int MaxRoomsPerFloor { get; }
        public int RoomClearanceUnits { get; }
        public int ExtraRoomLinksPerFloor { get; }
        public int VerticalLinksPerFloorPair { get; }
        public double MinimumStairCenterSeparationUnits { get; }
        public int VerticalTraversalCostUnits { get; }
        public int FuseCount { get; }
        public int SafeZoneCount { get; }
        public int MaxLayoutAttempts { get; }
        public int MaxPlacementAttemptsPerArea { get; }
        public IReadOnlyList<FootprintSpec> RoomFootprints { get; }
        public IReadOnlyList<FootprintSpec> StairFootprints { get; }
        public IReadOnlyList<int> CorridorRunLengths { get; }

        public MapGenerationParameters(
            int widthUnits,
            int depthUnits,
            int floorCount,
            int minRoomsPerFloor,
            int maxRoomsPerFloor,
            int roomClearanceUnits,
            int extraRoomLinksPerFloor,
            int verticalLinksPerFloorPair,
            double minimumStairCenterSeparationUnits,
            int verticalTraversalCostUnits,
            int fuseCount,
            int safeZoneCount,
            int maxLayoutAttempts,
            int maxPlacementAttemptsPerArea,
            IReadOnlyList<FootprintSpec> roomFootprints,
            IReadOnlyList<FootprintSpec> stairFootprints,
            IReadOnlyList<int> corridorRunLengths)
        {
            WidthUnits = widthUnits;
            DepthUnits = depthUnits;
            FloorCount = floorCount;
            MinRoomsPerFloor = minRoomsPerFloor;
            MaxRoomsPerFloor = maxRoomsPerFloor;
            RoomClearanceUnits = roomClearanceUnits;
            ExtraRoomLinksPerFloor = extraRoomLinksPerFloor;
            VerticalLinksPerFloorPair = verticalLinksPerFloorPair;
            MinimumStairCenterSeparationUnits = minimumStairCenterSeparationUnits;
            VerticalTraversalCostUnits = verticalTraversalCostUnits;
            FuseCount = fuseCount;
            SafeZoneCount = safeZoneCount;
            MaxLayoutAttempts = maxLayoutAttempts;
            MaxPlacementAttemptsPerArea = maxPlacementAttemptsPerArea;
            RoomFootprints = new ReadOnlyCollection<FootprintSpec>(
                new List<FootprintSpec>(roomFootprints ?? throw new ArgumentNullException(nameof(roomFootprints))));
            StairFootprints = new ReadOnlyCollection<FootprintSpec>(
                new List<FootprintSpec>(stairFootprints ?? throw new ArgumentNullException(nameof(stairFootprints))));
            CorridorRunLengths = new ReadOnlyCollection<int>(
                new List<int>(corridorRunLengths ?? throw new ArgumentNullException(nameof(corridorRunLengths))));
        }

        public static MapGenerationParameters CreateProductionDefaults(int floors)
        {
            return new MapGenerationParameters(
                widthUnits: 16,
                depthUnits: 16,
                floorCount: floors,
                minRoomsPerFloor: 7,
                maxRoomsPerFloor: 9,
                roomClearanceUnits: 1,
                extraRoomLinksPerFloor: 2,
                verticalLinksPerFloorPair: 2,
                minimumStairCenterSeparationUnits: 5,
                verticalTraversalCostUnits: 4,
                fuseCount: 3,
                safeZoneCount: 2,
                maxLayoutAttempts: 12,
                maxPlacementAttemptsPerArea: 64,
                roomFootprints: new[]
                {
                    new FootprintSpec("1x2", new GridSize2(1, 2), 4, true),
                    new FootprintSpec("2x2", new GridSize2(2, 2), 3, true),
                    new FootprintSpec("2x3", new GridSize2(2, 3), 2, true)
                },
                stairFootprints: new[]
                {
                    new FootprintSpec("STA_1x2", new GridSize2(1, 2), 3, false),
                    new FootprintSpec("STA_2x2", new GridSize2(2, 2), 1, false)
                },
                corridorRunLengths: new[] { 1, 2, 3 });
        }

        public void Validate()
        {
            RequireAtLeast(nameof(WidthUnits), WidthUnits, 8);
            RequireAtLeast(nameof(DepthUnits), DepthUnits, 8);
            RequireAtLeast(nameof(FloorCount), FloorCount, 2);
            RequireAtLeast(nameof(MinRoomsPerFloor), MinRoomsPerFloor, 1);
            if (MaxRoomsPerFloor < MinRoomsPerFloor)
            {
                throw Invalid(nameof(MaxRoomsPerFloor), MaxRoomsPerFloor, "must be at least MinRoomsPerFloor");
            }

            if (RoomClearanceUnits < 0)
            {
                throw Invalid(nameof(RoomClearanceUnits), RoomClearanceUnits, "must not be negative");
            }

            if (ExtraRoomLinksPerFloor < 0)
            {
                throw Invalid(nameof(ExtraRoomLinksPerFloor), ExtraRoomLinksPerFloor, "must not be negative");
            }

            RequireAtLeast(nameof(VerticalLinksPerFloorPair), VerticalLinksPerFloorPair, 2);
            if (MinimumStairCenterSeparationUnits < 0)
            {
                throw Invalid(
                    nameof(MinimumStairCenterSeparationUnits),
                    MinimumStairCenterSeparationUnits,
                    "must not be negative");
            }

            if (double.IsNaN(MinimumStairCenterSeparationUnits) ||
                double.IsInfinity(MinimumStairCenterSeparationUnits))
            {
                throw Invalid(
                    nameof(MinimumStairCenterSeparationUnits),
                    MinimumStairCenterSeparationUnits,
                    "must be finite");
            }

            RequireAtLeast(nameof(VerticalTraversalCostUnits), VerticalTraversalCostUnits, 1);
            RequireAtLeast(nameof(FuseCount), FuseCount, 1);
            RequireAtLeast(nameof(SafeZoneCount), SafeZoneCount, 1);
            RequireAtLeast(nameof(MaxLayoutAttempts), MaxLayoutAttempts, 1);
            RequireAtLeast(nameof(MaxPlacementAttemptsPerArea), MaxPlacementAttemptsPerArea, 1);

            if (RoomFootprints.Count == 0 || StairFootprints.Count == 0)
            {
                throw new MapGenerationException("RoomFootprints and StairFootprints must not be empty.");
            }

            ValidateFootprintsFitBounds(nameof(RoomFootprints), RoomFootprints);
            ValidateFootprintsFitBounds(nameof(StairFootprints), StairFootprints);
            ValidateStairPairFeasibility();

            string[] requiredIds = { "1x2", "2x2", "2x3" };
            foreach (string requiredId in requiredIds)
            {
                if (!RoomFootprints.Any(spec => spec.Id == requiredId && spec.RequiredPerFloor))
                {
                    throw new MapGenerationException($"RoomFootprints is missing required {requiredId}.");
                }
            }

            int requiredRoomCount = RoomFootprints.Count(spec => spec.RequiredPerFloor);
            if (MinRoomsPerFloor < requiredRoomCount)
            {
                throw Invalid(
                    nameof(MinRoomsPerFloor),
                    MinRoomsPerFloor,
                    $"must fit {requiredRoomCount} required footprints");
            }

            if (RoomFootprints.GroupBy(spec => spec.Id).Any(group => group.Count() > 1) ||
                StairFootprints.GroupBy(spec => spec.Id).Any(group => group.Count() > 1))
            {
                throw new MapGenerationException("Footprint IDs must be unique within each list.");
            }

            if (CorridorRunLengths.Count == 0 || CorridorRunLengths.Any(length => length < 1))
            {
                throw new MapGenerationException("CorridorRunLengths must contain positive lengths.");
            }
        }

        private static void RequireAtLeast(string field, int value, int minimum)
        {
            if (value < minimum)
            {
                throw Invalid(field, value, $"must be at least {minimum}");
            }
        }

        private void ValidateFootprintsFitBounds(
            string collectionName,
            IEnumerable<FootprintSpec> footprints)
        {
            foreach (FootprintSpec footprint in footprints)
            {
                foreach (GridSize2 size in new[]
                         {
                             footprint.CanonicalSize,
                             footprint.CanonicalSize.Rotate(1)
                         }.Distinct())
                {
                    if (size.Width > WidthUnits || size.Depth > DepthUnits)
                    {
                        throw new MapGenerationException(
                            $"{collectionName} entry '{footprint.Id}' has orientation {size} that " +
                            $"does not fit inside {WidthUnits}x{DepthUnits}.");
                    }
                }
            }
        }

        private void ValidateStairPairFeasibility()
        {
            GridSize2[] possibleSizes = StairFootprints
                .SelectMany(spec => new[]
                {
                    spec.CanonicalSize,
                    spec.CanonicalSize.Rotate(1)
                })
                .Distinct()
                .ToArray();
            double maximumSeparation = double.NegativeInfinity;
            foreach (GridSize2 left in possibleSizes)
            {
                foreach (GridSize2 right in possibleSizes)
                {
                    bool canSeparateAlongX =
                        (long)left.Width + RoomClearanceUnits + right.Width <= WidthUnits;
                    bool canSeparateAlongZ =
                        (long)left.Depth + RoomClearanceUnits + right.Depth <= DepthUnits;
                    if (!canSeparateAlongX && !canSeparateAlongZ)
                    {
                        continue;
                    }

                    double separation =
                        WidthUnits - (((double)left.Width + right.Width) / 2.0) +
                        DepthUnits - (((double)left.Depth + right.Depth) / 2.0);
                    maximumSeparation = Math.Max(maximumSeparation, separation);
                }
            }

            if (double.IsNegativeInfinity(maximumSeparation))
            {
                throw new MapGenerationException(
                    $"Two distinct stairs cannot fit inside {WidthUnits}x{DepthUnits} with " +
                    $"RoomClearanceUnits={RoomClearanceUnits}.");
            }

            if (MinimumStairCenterSeparationUnits > maximumSeparation)
            {
                throw new MapGenerationException(
                    $"MinimumStairCenterSeparationUnits={MinimumStairCenterSeparationUnits} cannot " +
                    $"fit inside {WidthUnits}x{DepthUnits}; the maximum centre Manhattan separation " +
                    $"for two available stair footprints is {maximumSeparation}.");
            }

        }

        private static MapGenerationException Invalid(string field, object value, string rule) =>
            new MapGenerationException($"{field}={value} {rule}.");
    }
}
