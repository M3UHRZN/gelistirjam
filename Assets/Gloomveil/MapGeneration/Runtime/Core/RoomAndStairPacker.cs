using System;
using System.Collections.Generic;
using System.Linq;

namespace Gloomveil.MapGeneration
{
    public sealed class RoomAndStairPacker
    {
        public bool TryPack(
            MapGenerationParameters parameters,
            SeededRandom attemptRandom,
            out PackedAreaLayout layout,
            out string failure)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (attemptRandom == null)
            {
                throw new ArgumentNullException(nameof(attemptRandom));
            }

            parameters.Validate();
            var rooms = new List<PlacedRoom>();
            var stairs = new List<PlacedStair>();
            var occupied = CreateFloorLists(parameters.FloorCount);
            int nextPlacementId = 1;

            for (int lowerFloor = 0; lowerFloor < parameters.FloorCount - 1; lowerFloor++)
            {
                for (int link = 0; link < parameters.VerticalLinksPerFloorPair; link++)
                {
                    if (!TryPlaceStair(
                            parameters,
                            attemptRandom,
                            lowerFloor,
                            nextPlacementId,
                            occupied,
                            stairs,
                            out PlacedStair stair))
                    {
                        layout = null;
                        failure = $"Could not place stair {link + 1} for F{lowerFloor}->F{lowerFloor + 1}.";
                        return false;
                    }

                    stairs.Add(stair);
                    occupied[stair.LowerFloor].Add(stair.RotatedRect);
                    occupied[stair.UpperFloor].Add(stair.RotatedRect);
                    nextPlacementId++;
                }
            }

            FootprintSpec[] required = parameters.RoomFootprints
                .Where(spec => spec.RequiredPerFloor)
                .OrderByDescending(spec => spec.CanonicalSize.Width * spec.CanonicalSize.Depth)
                .ThenBy(spec => spec.Id, StringComparer.Ordinal)
                .ToArray();

            for (int floor = 0; floor < parameters.FloorCount; floor++)
            {
                foreach (FootprintSpec spec in required)
                {
                    int? parity = RequiredRotationParity(spec, floor);
                    if (!TryPlaceRoom(
                            parameters,
                            attemptRandom,
                            spec,
                            floor,
                            parity,
                            nextPlacementId,
                            occupied,
                            out PlacedRoom room))
                    {
                        layout = null;
                        failure = $"Could not place required room {spec.Id} on floor {floor}.";
                        return false;
                    }

                    rooms.Add(room);
                    occupied[floor].Add(room.RotatedRect);
                    nextPlacementId++;
                }

                int targetRoomCount = attemptRandom.Next(
                    parameters.MinRoomsPerFloor,
                    parameters.MaxRoomsPerFloor + 1);
                int roomsOnFloor = required.Length;
                while (roomsOnFloor < targetRoomCount)
                {
                    FootprintSpec spec = SelectWeighted(parameters.RoomFootprints, attemptRandom);
                    if (!TryPlaceRoom(
                            parameters,
                            attemptRandom,
                            spec,
                            floor,
                            null,
                            nextPlacementId,
                            occupied,
                            out PlacedRoom room))
                    {
                        layout = null;
                        failure = $"Could not place filler room {roomsOnFloor + 1}/{targetRoomCount} on floor {floor}.";
                        return false;
                    }

                    rooms.Add(room);
                    occupied[floor].Add(room.RotatedRect);
                    nextPlacementId++;
                    roomsOnFloor++;
                }
            }

            if (!rooms.Any(room => room.FootprintId == "2x3" && (room.QuarterTurns & 1) == 0) ||
                !rooms.Any(room => room.FootprintId == "2x3" && (room.QuarterTurns & 1) == 1))
            {
                layout = null;
                failure = "The layout does not contain both 2x3 and rotated 3x2 room forms.";
                return false;
            }

            layout = new PackedAreaLayout(parameters.FloorCount, rooms, stairs);
            failure = string.Empty;
            return true;
        }

        private static List<GridRect>[] CreateFloorLists(int floorCount)
        {
            var lists = new List<GridRect>[floorCount];
            for (int floor = 0; floor < floorCount; floor++)
            {
                lists[floor] = new List<GridRect>();
            }

            return lists;
        }

        private static bool TryPlaceStair(
            MapGenerationParameters parameters,
            SeededRandom random,
            int lowerFloor,
            int placementId,
            IReadOnlyList<GridRect>[] occupied,
            IReadOnlyList<PlacedStair> placedStairs,
            out PlacedStair stair)
        {
            for (int attempt = 0; attempt < parameters.MaxPlacementAttemptsPerArea; attempt++)
            {
                FootprintSpec spec = SelectWeighted(parameters.StairFootprints, random);
                int turns = random.Next(4);
                GridSize2 size = spec.CanonicalSize.Rotate(turns);
                int x = random.Next(0, parameters.WidthUnits - size.Width + 1);
                int z = random.Next(0, parameters.DepthUnits - size.Depth + 1);
                var candidate = new GridRect(x, z, size.Width, size.Depth);

                if (!Fits(candidate, parameters, occupied[lowerFloor]) ||
                    !Fits(candidate, parameters, occupied[lowerFloor + 1]))
                {
                    continue;
                }

                bool separated = placedStairs
                    .Where(existing => existing.LowerFloor == lowerFloor)
                    .All(existing => CentreManhattanUnits(existing.RotatedRect, candidate) >=
                                     parameters.MinimumStairCenterSeparationUnits);
                if (!separated)
                {
                    continue;
                }

                stair = new PlacedStair(
                    placementId,
                    spec.Id,
                    lowerFloor,
                    x,
                    z,
                    spec.CanonicalSize,
                    turns);
                return true;
            }

            stair = null;
            return false;
        }

        private static bool TryPlaceRoom(
            MapGenerationParameters parameters,
            SeededRandom random,
            FootprintSpec spec,
            int floor,
            int? requiredParity,
            int placementId,
            IReadOnlyList<GridRect>[] occupied,
            out PlacedRoom room)
        {
            for (int attempt = 0; attempt < parameters.MaxPlacementAttemptsPerArea; attempt++)
            {
                int turns = requiredParity.HasValue
                    ? requiredParity.Value + (2 * random.Next(2))
                    : random.Next(4);
                GridSize2 size = spec.CanonicalSize.Rotate(turns);
                int x = random.Next(0, parameters.WidthUnits - size.Width + 1);
                int z = random.Next(0, parameters.DepthUnits - size.Depth + 1);
                var candidate = new GridRect(x, z, size.Width, size.Depth);
                if (!Fits(candidate, parameters, occupied[floor]))
                {
                    continue;
                }

                room = new PlacedRoom(
                    placementId,
                    spec.Id,
                    floor,
                    x,
                    z,
                    spec.CanonicalSize,
                    turns);
                return true;
            }

            room = null;
            return false;
        }

        private static bool Fits(
            GridRect candidate,
            MapGenerationParameters parameters,
            IReadOnlyList<GridRect> occupied)
        {
            if (candidate.X < 0 || candidate.Z < 0 ||
                candidate.MaxX > parameters.WidthUnits || candidate.MaxZ > parameters.DepthUnits)
            {
                return false;
            }

            int clearance = parameters.RoomClearanceUnits;
            var padded = new GridRect(
                candidate.X - clearance,
                candidate.Z - clearance,
                candidate.Width + (2 * clearance),
                candidate.Depth + (2 * clearance));
            for (int index = 0; index < occupied.Count; index++)
            {
                if (padded.Overlaps(occupied[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static int? RequiredRotationParity(FootprintSpec spec, int floor)
        {
            if (spec.Id == "2x3")
            {
                return floor & 1;
            }

            if (spec.CanonicalSize.Width != spec.CanonicalSize.Depth)
            {
                return 1;
            }

            return null;
        }

        private static FootprintSpec SelectWeighted(
            IReadOnlyList<FootprintSpec> specs,
            SeededRandom random)
        {
            int totalWeight = 0;
            for (int index = 0; index < specs.Count; index++)
            {
                totalWeight += specs[index].Weight;
            }

            int roll = random.Next(totalWeight);
            for (int index = 0; index < specs.Count; index++)
            {
                roll -= specs[index].Weight;
                if (roll < 0)
                {
                    return specs[index];
                }
            }

            return specs[specs.Count - 1];
        }

        private static double CentreManhattanUnits(GridRect left, GridRect right)
        {
            int doubledDistance =
                Math.Abs(left.CenterX2 - right.CenterX2) +
                Math.Abs(left.CenterZ2 - right.CenterZ2);
            return doubledDistance / 2.0;
        }
    }
}
