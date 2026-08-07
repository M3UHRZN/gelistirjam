using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Gloomveil.MapGeneration
{
    public sealed class OrthogonalCorridorRouter
    {
        private const int FreshStepCost = 10;
        private const int ReusedStepCost = 8;
        private const int BendCost = 2;
        private const byte NoDirection = 4;

        private static readonly int[] DeltaX = { 0, 1, 0, -1 };
        private static readonly int[] DeltaZ = { 1, 0, -1, 0 };

        public bool TryRoute(
            int widthUnits,
            int depthUnits,
            int floor,
            IReadOnlyCollection<GridRect> blockedRects,
            IReadOnlyCollection<GridCoord3> existingCorridorCells,
            GridCoord3 start,
            GridCoord3 goal,
            out IReadOnlyList<GridCoord3> path)
        {
            if (blockedRects == null)
            {
                throw new ArgumentNullException(nameof(blockedRects));
            }

            if (existingCorridorCells == null)
            {
                throw new ArgumentNullException(nameof(existingCorridorCells));
            }

            if (start.Floor != floor || goal.Floor != floor)
            {
                throw new ArgumentException("Corridor endpoints must be on the routed floor.");
            }

            var blocked = ExpandBlockedCells(blockedRects, floor);
            var existing = new HashSet<GridCoord3>(existingCorridorCells);
            if (!IsInside(start, widthUnits, depthUnits) ||
                !IsInside(goal, widthUnits, depthUnits) ||
                blocked.Contains(start) ||
                blocked.Contains(goal))
            {
                path = Array.Empty<GridCoord3>();
                return false;
            }

            var startKey = new RouteStateKey(start.X, start.Z, NoDirection);
            var open = new List<OpenNode>
            {
                new OpenNode(startKey, 0, Heuristic(start.X, start.Z, goal))
            };
            var bestCost = new Dictionary<RouteStateKey, int> { [startKey] = 0 };
            var parent = new Dictionary<RouteStateKey, RouteStateKey>();

            while (open.Count > 0)
            {
                int bestIndex = FindBestOpenIndex(open);
                OpenNode current = open[bestIndex];
                open.RemoveAt(bestIndex);
                if (!bestCost.TryGetValue(current.Key, out int recordedCost) ||
                    recordedCost != current.GScore)
                {
                    continue;
                }

                if (current.Key.X == goal.X && current.Key.Z == goal.Z)
                {
                    path = ReconstructPath(parent, current.Key, floor);
                    return true;
                }

                for (byte direction = 0; direction < 4; direction++)
                {
                    int nextX = current.Key.X + DeltaX[direction];
                    int nextZ = current.Key.Z + DeltaZ[direction];
                    var nextCoord = new GridCoord3(nextX, nextZ, floor);
                    if (!IsInside(nextCoord, widthUnits, depthUnits) || blocked.Contains(nextCoord))
                    {
                        continue;
                    }

                    int stepCost = existing.Contains(nextCoord) ? ReusedStepCost : FreshStepCost;
                    if (current.Key.Direction != NoDirection && current.Key.Direction != direction)
                    {
                        stepCost += BendCost;
                    }

                    int nextCost = current.GScore + stepCost;
                    var nextKey = new RouteStateKey(nextX, nextZ, direction);
                    if (bestCost.TryGetValue(nextKey, out int previousCost) &&
                        previousCost <= nextCost)
                    {
                        continue;
                    }

                    bestCost[nextKey] = nextCost;
                    parent[nextKey] = current.Key;
                    open.Add(new OpenNode(
                        nextKey,
                        nextCost,
                        Heuristic(nextX, nextZ, goal)));
                }
            }

            path = Array.Empty<GridCoord3>();
            return false;
        }

        private static HashSet<GridCoord3> ExpandBlockedCells(
            IEnumerable<GridRect> rectangles,
            int floor)
        {
            var result = new HashSet<GridCoord3>();
            foreach (GridRect rectangle in rectangles)
            {
                for (int z = rectangle.Z; z < rectangle.MaxZ; z++)
                {
                    for (int x = rectangle.X; x < rectangle.MaxX; x++)
                    {
                        result.Add(new GridCoord3(x, z, floor));
                    }
                }
            }

            return result;
        }

        private static int FindBestOpenIndex(IReadOnlyList<OpenNode> open)
        {
            int best = 0;
            for (int index = 1; index < open.Count; index++)
            {
                if (Compare(open[index], open[best]) < 0)
                {
                    best = index;
                }
            }

            return best;
        }

        private static int Compare(OpenNode left, OpenNode right)
        {
            int comparison = left.FScore.CompareTo(right.FScore);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.HScore.CompareTo(right.HScore);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.Key.Z.CompareTo(right.Key.Z);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.Key.X.CompareTo(right.Key.X);
            return comparison != 0
                ? comparison
                : left.Key.Direction.CompareTo(right.Key.Direction);
        }

        private static IReadOnlyList<GridCoord3> ReconstructPath(
            IReadOnlyDictionary<RouteStateKey, RouteStateKey> parent,
            RouteStateKey goal,
            int floor)
        {
            var reversed = new List<GridCoord3>();
            RouteStateKey current = goal;
            reversed.Add(new GridCoord3(current.X, current.Z, floor));
            while (parent.TryGetValue(current, out RouteStateKey previous))
            {
                current = previous;
                reversed.Add(new GridCoord3(current.X, current.Z, floor));
            }

            reversed.Reverse();
            return new ReadOnlyCollection<GridCoord3>(reversed);
        }

        private static int Heuristic(int x, int z, GridCoord3 goal) =>
            (Math.Abs(goal.X - x) + Math.Abs(goal.Z - z)) * ReusedStepCost;

        private static bool IsInside(GridCoord3 coord, int widthUnits, int depthUnits) =>
            coord.X >= 0 && coord.Z >= 0 &&
            coord.X < widthUnits && coord.Z < depthUnits;

        private readonly struct RouteStateKey : IEquatable<RouteStateKey>
        {
            public int X { get; }
            public int Z { get; }
            public byte Direction { get; }

            public RouteStateKey(int x, int z, byte direction)
            {
                X = x;
                Z = z;
                Direction = direction;
            }

            public bool Equals(RouteStateKey other) =>
                X == other.X && Z == other.Z && Direction == other.Direction;

            public override bool Equals(object obj) => obj is RouteStateKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(X, Z, Direction);
        }

        private readonly struct OpenNode
        {
            public RouteStateKey Key { get; }
            public int GScore { get; }
            public int HScore { get; }
            public int FScore => GScore + HScore;

            public OpenNode(RouteStateKey key, int gScore, int hScore)
            {
                Key = key;
                GScore = gScore;
                HScore = hScore;
            }
        }
    }
}
