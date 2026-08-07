using System;

namespace Gloomveil.MapGeneration
{
    [Serializable]
    public readonly struct GridCoord3 : IEquatable<GridCoord3>, IComparable<GridCoord3>
    {
        public int X { get; }
        public int Z { get; }
        public int Floor { get; }

        public GridCoord3(int x, int z, int floor)
        {
            X = x;
            Z = z;
            Floor = floor;
        }

        public bool Equals(GridCoord3 other) => X == other.X && Z == other.Z && Floor == other.Floor;

        public override bool Equals(object obj) => obj is GridCoord3 other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Z, Floor);

        public int CompareTo(GridCoord3 other)
        {
            int floorComparison = Floor.CompareTo(other.Floor);
            if (floorComparison != 0)
            {
                return floorComparison;
            }

            int zComparison = Z.CompareTo(other.Z);
            return zComparison != 0 ? zComparison : X.CompareTo(other.X);
        }

        public override string ToString() => $"({X},{Z},F{Floor})";

        public static bool operator ==(GridCoord3 left, GridCoord3 right) => left.Equals(right);

        public static bool operator !=(GridCoord3 left, GridCoord3 right) => !left.Equals(right);
    }
}
