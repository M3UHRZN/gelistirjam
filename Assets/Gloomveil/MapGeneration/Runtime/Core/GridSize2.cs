using System;

namespace Gloomveil.MapGeneration
{
    [Serializable]
    public readonly struct GridSize2 : IEquatable<GridSize2>
    {
        public int Width { get; }
        public int Depth { get; }

        public GridSize2(int width, int depth)
        {
            if (width < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be at least 1.");
            }

            if (depth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(depth), depth, "Depth must be at least 1.");
            }

            Width = width;
            Depth = depth;
        }

        public GridSize2 Rotate(int quarterTurns)
        {
            int normalizedTurns = ((quarterTurns % 4) + 4) % 4;
            return (normalizedTurns & 1) == 0 ? this : new GridSize2(Depth, Width);
        }

        public bool Equals(GridSize2 other) => Width == other.Width && Depth == other.Depth;

        public override bool Equals(object obj) => obj is GridSize2 other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Width, Depth);

        public override string ToString() => $"{Width}x{Depth}";

        public static bool operator ==(GridSize2 left, GridSize2 right) => left.Equals(right);

        public static bool operator !=(GridSize2 left, GridSize2 right) => !left.Equals(right);
    }
}
