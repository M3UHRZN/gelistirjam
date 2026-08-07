using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Gloomveil.MapGeneration
{
    public sealed class CorridorModulePlanner
    {
        private const CorridorConnectionMask VerticalMask =
            CorridorConnectionMask.North | CorridorConnectionMask.South;
        private const CorridorConnectionMask HorizontalMask =
            CorridorConnectionMask.East | CorridorConnectionMask.West;

        public bool TryPlan(
            IReadOnlyList<CorridorTile> tiles,
            IReadOnlyCollection<GridCoord3> roomDoorCells,
            IReadOnlyCollection<GridCoord3> stairLandingCells,
            IReadOnlyList<int> allowedRunLengths,
            SeededRandom random,
            int firstPlacementId,
            out IReadOnlyList<CorridorModulePlacement> modules,
            out string failure)
        {
            if (tiles == null)
            {
                throw new ArgumentNullException(nameof(tiles));
            }

            if (roomDoorCells == null)
            {
                throw new ArgumentNullException(nameof(roomDoorCells));
            }

            if (stairLandingCells == null)
            {
                throw new ArgumentNullException(nameof(stairLandingCells));
            }

            if (allowedRunLengths == null)
            {
                throw new ArgumentNullException(nameof(allowedRunLengths));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            int[] allowed = allowedRunLengths
                .Distinct()
                .OrderByDescending(length => length)
                .ToArray();
            if (allowed.Length == 0 || allowed.Any(length => length < 1))
            {
                throw new ArgumentException("Corridor run lengths must be positive.", nameof(allowedRunLengths));
            }

            var byCoord = tiles.ToDictionary(tile => tile.Coord);
            var roomDoors = new HashSet<GridCoord3>(roomDoorCells);
            var stairLandings = new HashSet<GridCoord3>(stairLandingCells);
            var protectedTransitions = new HashSet<GridCoord3>(
                roomDoors.Where(coord =>
                    byCoord.TryGetValue(coord, out CorridorTile tile) &&
                    IsStraight(tile.ConnectionMask) &&
                    !stairLandings.Contains(coord)));
            var result = new List<CorridorModulePlacement>();
            int nextPlacementId = firstPlacementId;

            foreach (GridCoord3 coord in protectedTransitions.OrderBy(coord => coord))
            {
                CorridorTile tile = byCoord[coord];
                int turns = tile.ConnectionMask == VerticalMask ? 0 : 1;
                result.Add(new CorridorModulePlacement(
                    nextPlacementId++,
                    coord,
                    new GridSize2(1, 1),
                    turns,
                    CorridorShape.DoorTransition,
                    1,
                    StraightSockets()));
            }

            foreach (CorridorTile tile in tiles.OrderBy(tile => tile.Coord))
            {
                if (protectedTransitions.Contains(tile.Coord) || IsStraight(tile.ConnectionMask))
                {
                    continue;
                }

                CorridorShape shape = Classify(tile.ConnectionMask);
                int turns = QuarterTurnsFor(tile.ConnectionMask, shape);
                result.Add(new CorridorModulePlacement(
                    nextPlacementId++,
                    tile.Coord,
                    new GridSize2(1, 1),
                    turns,
                    shape,
                    1,
                    SocketsFor(shape)));
            }

            var processedStraight = new HashSet<GridCoord3>();
            int eligibleStraightCellCount = 0;
            foreach (CorridorTile tile in tiles.OrderBy(tile => tile.Coord))
            {
                if (!IsStraight(tile.ConnectionMask) ||
                    protectedTransitions.Contains(tile.Coord) ||
                    processedStraight.Contains(tile.Coord))
                {
                    continue;
                }

                bool vertical = tile.ConnectionMask == VerticalMask;
                List<GridCoord3> run = CollectRun(
                    tile.Coord,
                    vertical,
                    byCoord,
                    protectedTransitions,
                    processedStraight);
                eligibleStraightCellCount += run.Count;
                if (!TrySegment(run.Count, allowed, random, out IReadOnlyList<int> segmentLengths))
                {
                    modules = Array.Empty<CorridorModulePlacement>();
                    failure = $"Straight run of {run.Count} cells cannot use configured lengths.";
                    return false;
                }

                int cursor = 0;
                foreach (int length in segmentLengths)
                {
                    GridCoord3 origin = run[cursor];
                    result.Add(new CorridorModulePlacement(
                        nextPlacementId++,
                        origin,
                        new GridSize2(1, length),
                        vertical ? 0 : 1,
                        CorridorShape.Straight,
                        length,
                        StraightSockets()));
                    cursor += length;
                }
            }

            int distinctStraightLengths = result
                .Where(module => module.Shape == CorridorShape.Straight)
                .Select(module => module.LengthUnits)
                .Distinct()
                .Count();
            if (eligibleStraightCellCount >= 6 && distinctStraightLengths < 2)
            {
                modules = Array.Empty<CorridorModulePlacement>();
                failure = "Eligible straight corridors did not produce two visible lengths.";
                return false;
            }

            result.Sort((left, right) => left.PlacementId.CompareTo(right.PlacementId));
            modules = new ReadOnlyCollection<CorridorModulePlacement>(result);
            failure = string.Empty;
            return true;
        }

        private static List<GridCoord3> CollectRun(
            GridCoord3 start,
            bool vertical,
            IReadOnlyDictionary<GridCoord3, CorridorTile> byCoord,
            ISet<GridCoord3> protectedTransitions,
            ISet<GridCoord3> processed)
        {
            CorridorConnectionMask requiredMask = vertical ? VerticalMask : HorizontalMask;
            int deltaX = vertical ? 0 : 1;
            int deltaZ = vertical ? 1 : 0;
            GridCoord3 cursor = start;

            while (IsEligibleStraight(
                       new GridCoord3(cursor.X - deltaX, cursor.Z - deltaZ, cursor.Floor),
                       requiredMask,
                       byCoord,
                       protectedTransitions))
            {
                cursor = new GridCoord3(cursor.X - deltaX, cursor.Z - deltaZ, cursor.Floor);
            }

            var run = new List<GridCoord3>();
            while (IsEligibleStraight(cursor, requiredMask, byCoord, protectedTransitions))
            {
                run.Add(cursor);
                processed.Add(cursor);
                cursor = new GridCoord3(cursor.X + deltaX, cursor.Z + deltaZ, cursor.Floor);
            }

            return run;
        }

        private static bool IsEligibleStraight(
            GridCoord3 coord,
            CorridorConnectionMask requiredMask,
            IReadOnlyDictionary<GridCoord3, CorridorTile> byCoord,
            ISet<GridCoord3> protectedTransitions) =>
            !protectedTransitions.Contains(coord) &&
            byCoord.TryGetValue(coord, out CorridorTile tile) &&
            tile.ConnectionMask == requiredMask;

        private static bool TrySegment(
            int totalLength,
            IReadOnlyList<int> allowed,
            SeededRandom random,
            out IReadOnlyList<int> segments)
        {
            var result = new List<int>();
            int remaining = totalLength;
            int preferenceOffset = random.Next(allowed.Count);
            while (remaining > 0)
            {
                int selected = 0;
                for (int offset = 0; offset < allowed.Count; offset++)
                {
                    int candidate = allowed[(preferenceOffset + offset) % allowed.Count];
                    if (candidate <= remaining && CanCompose(remaining - candidate, allowed))
                    {
                        selected = candidate;
                        break;
                    }
                }

                if (selected == 0)
                {
                    segments = Array.Empty<int>();
                    return false;
                }

                result.Add(selected);
                remaining -= selected;
                preferenceOffset = (preferenceOffset + 1) % allowed.Count;
            }

            segments = new ReadOnlyCollection<int>(result);
            return true;
        }

        private static bool CanCompose(int length, IReadOnlyList<int> allowed)
        {
            if (length == 0)
            {
                return true;
            }

            var reachable = new bool[length + 1];
            reachable[0] = true;
            for (int current = 1; current <= length; current++)
            {
                for (int index = 0; index < allowed.Count; index++)
                {
                    int piece = allowed[index];
                    if (piece <= current && reachable[current - piece])
                    {
                        reachable[current] = true;
                        break;
                    }
                }
            }

            return reachable[length];
        }

        private static CorridorShape Classify(CorridorConnectionMask mask)
        {
            int count = CountConnections(mask);
            if (count <= 1)
            {
                return CorridorShape.DeadEnd;
            }

            if (mask == VerticalMask || mask == HorizontalMask)
            {
                return CorridorShape.Straight;
            }

            if (count == 2)
            {
                return CorridorShape.Corner;
            }

            return count == 3 ? CorridorShape.TJunction : CorridorShape.CrossJunction;
        }

        private static int QuarterTurnsFor(CorridorConnectionMask mask, CorridorShape shape)
        {
            CorridorConnectionMask canonical;
            switch (shape)
            {
                case CorridorShape.Straight:
                case CorridorShape.DoorTransition:
                    canonical = VerticalMask;
                    break;
                case CorridorShape.Corner:
                    canonical = CorridorConnectionMask.North | CorridorConnectionMask.East;
                    break;
                case CorridorShape.TJunction:
                    canonical = CorridorConnectionMask.North |
                                CorridorConnectionMask.East |
                                CorridorConnectionMask.West;
                    break;
                case CorridorShape.CrossJunction:
                    return 0;
                default:
                    canonical = CorridorConnectionMask.North;
                    break;
            }

            for (int turns = 0; turns < 4; turns++)
            {
                if (RotateMask(canonical, turns) == mask ||
                    (shape == CorridorShape.DeadEnd && mask == CorridorConnectionMask.None))
                {
                    return turns;
                }
            }

            return 0;
        }

        private static CorridorConnectionMask RotateMask(
            CorridorConnectionMask mask,
            int quarterTurns)
        {
            CorridorConnectionMask result = CorridorConnectionMask.None;
            for (int direction = 0; direction < 4; direction++)
            {
                CorridorConnectionMask bit = (CorridorConnectionMask)(1 << direction);
                if ((mask & bit) != 0)
                {
                    result |= (CorridorConnectionMask)(1 << ((direction + quarterTurns) % 4));
                }
            }

            return result;
        }

        private static int CountConnections(CorridorConnectionMask mask)
        {
            int value = (int)mask;
            int count = 0;
            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }

            return count;
        }

        private static bool IsStraight(CorridorConnectionMask mask) =>
            mask == VerticalMask || mask == HorizontalMask;

        private static IReadOnlyList<SocketKey> SocketsFor(CorridorShape shape)
        {
            switch (shape)
            {
                case CorridorShape.Straight:
                case CorridorShape.DoorTransition:
                    return StraightSockets();
                case CorridorShape.Corner:
                    return new[]
                    {
                        new SocketKey(CardinalDirection.North, 0, 0),
                        new SocketKey(CardinalDirection.East, 0, 0)
                    };
                case CorridorShape.TJunction:
                    return new[]
                    {
                        new SocketKey(CardinalDirection.North, 0, 0),
                        new SocketKey(CardinalDirection.East, 0, 0),
                        new SocketKey(CardinalDirection.West, 0, 0)
                    };
                case CorridorShape.CrossJunction:
                    return new[]
                    {
                        new SocketKey(CardinalDirection.North, 0, 0),
                        new SocketKey(CardinalDirection.East, 0, 0),
                        new SocketKey(CardinalDirection.South, 0, 0),
                        new SocketKey(CardinalDirection.West, 0, 0)
                    };
                default:
                    return new[] { new SocketKey(CardinalDirection.North, 0, 0) };
            }
        }

        private static IReadOnlyList<SocketKey> StraightSockets() => new[]
        {
            new SocketKey(CardinalDirection.North, 0, 0),
            new SocketKey(CardinalDirection.South, 0, 0)
        };
    }
}
