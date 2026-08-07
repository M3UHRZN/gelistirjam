using System;

namespace Gloomveil.MapGeneration
{
    [Serializable]
    public readonly struct GridRect : IEquatable<GridRect>
    {
        public int X { get; }
        public int Z { get; }
        public int Width { get; }
        public int Depth { get; }
        public int MaxX => X + Width;
        public int MaxZ => Z + Depth;
        public int CenterX2 => (2 * X) + Width;
        public int CenterZ2 => (2 * Z) + Depth;

        public GridRect(int x, int z, int width, int depth)
        {
            if (width < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be at least 1.");
            }

            if (depth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(depth), depth, "Depth must be at least 1.");
            }

            X = x;
            Z = z;
            Width = width;
            Depth = depth;
        }

        public bool Overlaps(GridRect other) =>
            X < other.MaxX && MaxX > other.X &&
            Z < other.MaxZ && MaxZ > other.Z;

        public bool Equals(GridRect other) =>
            X == other.X && Z == other.Z && Width == other.Width && Depth == other.Depth;

        public override bool Equals(object obj) => obj is GridRect other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Z, Width, Depth);

        public override string ToString() => $"({X},{Z}) {Width}x{Depth}";

        public static bool operator ==(GridRect left, GridRect right) => left.Equals(right);

        public static bool operator !=(GridRect left, GridRect right) => !left.Equals(right);
    }
}
