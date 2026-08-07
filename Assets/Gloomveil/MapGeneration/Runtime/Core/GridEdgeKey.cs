using System;

namespace Gloomveil.MapGeneration
{
    public enum GridEdgeAxis : byte
    {
        AlongX = 0,
        AlongZ = 1
    }

    [Serializable]
    public readonly struct GridEdgeKey : IEquatable<GridEdgeKey>, IComparable<GridEdgeKey>
    {
        public int Floor { get; }
        public GridEdgeAxis Axis { get; }
        public int X { get; }
        public int Z { get; }

        public GridEdgeKey(int floor, GridEdgeAxis axis, int x, int z)
        {
            Floor = floor;
            Axis = axis;
            X = x;
            Z = z;
        }

        public bool Equals(GridEdgeKey other) =>
            Floor == other.Floor && Axis == other.Axis && X == other.X && Z == other.Z;

        public override bool Equals(object obj) => obj is GridEdgeKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Floor, (int)Axis, X, Z);

        public int CompareTo(GridEdgeKey other)
        {
            int floorComparison = Floor.CompareTo(other.Floor);
            if (floorComparison != 0)
            {
                return floorComparison;
            }

            int axisComparison = Axis.CompareTo(other.Axis);
            if (axisComparison != 0)
            {
                return axisComparison;
            }

            int zComparison = Z.CompareTo(other.Z);
            return zComparison != 0 ? zComparison : X.CompareTo(other.X);
        }

        public override string ToString() => $"F{Floor}:{Axis}@({X},{Z})";

        public static bool operator ==(GridEdgeKey left, GridEdgeKey right) => left.Equals(right);

        public static bool operator !=(GridEdgeKey left, GridEdgeKey right) => !left.Equals(right);
    }
}
