using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Gloomveil.MapGeneration
{
    [Serializable]
    public readonly struct MinimapPoint2 : IEquatable<MinimapPoint2>
    {
        public int X2 { get; }
        public int Z2 { get; }

        public MinimapPoint2(int x2, int z2)
        {
            X2 = x2;
            Z2 = z2;
        }

        public bool Equals(MinimapPoint2 other) => X2 == other.X2 && Z2 == other.Z2;
        public override bool Equals(object obj) => obj is MinimapPoint2 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X2, Z2);
    }

    [Serializable]
    public readonly struct MinimapWallSegment
    {
        public GridEdgeKey Edge { get; }
        public MinimapPoint2 Start { get; }
        public MinimapPoint2 End { get; }
        public int Floor => Edge.Floor;

        public MinimapWallSegment(GridEdgeKey edge)
        {
            Edge = edge;
            Start = new MinimapPoint2(2 * edge.X, 2 * edge.Z);
            End = edge.Axis == GridEdgeAxis.AlongX
                ? new MinimapPoint2(2 * (edge.X + 1), 2 * edge.Z)
                : new MinimapPoint2(2 * edge.X, 2 * (edge.Z + 1));
        }
    }

    public enum MinimapStairDirection : byte
    {
        Up = 0,
        Down = 1
    }

    [Serializable]
    public readonly struct MinimapStairMarker
    {
        public int Floor { get; }
        public MinimapPoint2 Position { get; }
        public MinimapStairDirection Direction { get; }

        public MinimapStairMarker(
            int floor,
            MinimapPoint2 position,
            MinimapStairDirection direction)
        {
            Floor = floor;
            Position = position;
            Direction = direction;
        }
    }

    public sealed class MinimapTopology
    {
        public int WidthUnits { get; }
        public int DepthUnits { get; }
        public int FloorCount { get; }
        public IReadOnlyList<MinimapWallSegment> Walls { get; }
        public IReadOnlyList<MinimapStairMarker> Stairs { get; }

        public MinimapTopology(
            int widthUnits,
            int depthUnits,
            int floorCount,
            IEnumerable<MinimapWallSegment> walls,
            IEnumerable<MinimapStairMarker> stairs)
        {
            if (widthUnits < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(widthUnits));
            }

            if (depthUnits < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(depthUnits));
            }

            if (floorCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(floorCount));
            }

            WidthUnits = widthUnits;
            DepthUnits = depthUnits;
            FloorCount = floorCount;
            Walls = ReadOnly(walls, nameof(walls));
            Stairs = ReadOnly(stairs, nameof(stairs));
        }

        private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> source, string argumentName) =>
            new ReadOnlyCollection<T>(
                new List<T>(source ?? throw new ArgumentNullException(argumentName)));
    }
}
