using System;

namespace Gloomveil.MapGeneration
{
    [Serializable]
    public readonly struct SocketKey : IEquatable<SocketKey>
    {
        public CardinalDirection Side { get; }
        public int Index { get; }
        public int FloorOffset { get; }

        public SocketKey(CardinalDirection side, int index, int floorOffset)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Socket index must not be negative.");
            }

            if (floorOffset < 0 || floorOffset > 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(floorOffset),
                    floorOffset,
                    "Socket floor offset must be 0 or 1.");
            }

            Side = side;
            Index = index;
            FloorOffset = floorOffset;
        }

        public SocketKey Rotate(GridSize2 canonicalSize, int quarterTurns)
        {
            ValidateIndex(canonicalSize);
            int turns = ((quarterTurns % 4) + 4) % 4;
            int x2;
            int z2;
            ToDoubledBoundaryPosition(canonicalSize, out x2, out z2);

            for (int i = 0; i < turns; i++)
            {
                int previousX2 = x2;
                x2 = z2;
                z2 = -previousX2;
            }

            GridSize2 rotatedSize = canonicalSize.Rotate(turns);
            return FromDoubledBoundaryPosition(rotatedSize, x2, z2, FloorOffset);
        }

        private void ValidateIndex(GridSize2 size)
        {
            int count = Side == CardinalDirection.North || Side == CardinalDirection.South
                ? size.Width
                : size.Depth;
            if (Index >= count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(Index),
                    Index,
                    $"Socket {Side}_{Index:00} is outside footprint {size}.");
            }
        }

        private void ToDoubledBoundaryPosition(GridSize2 size, out int x2, out int z2)
        {
            switch (Side)
            {
                case CardinalDirection.North:
                    x2 = (2 * Index) + 1 - size.Width;
                    z2 = size.Depth;
                    return;
                case CardinalDirection.East:
                    x2 = size.Width;
                    z2 = (2 * Index) + 1 - size.Depth;
                    return;
                case CardinalDirection.South:
                    x2 = (2 * Index) + 1 - size.Width;
                    z2 = -size.Depth;
                    return;
                case CardinalDirection.West:
                    x2 = -size.Width;
                    z2 = (2 * Index) + 1 - size.Depth;
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Side), Side, "Unknown cardinal direction.");
            }
        }

        private static SocketKey FromDoubledBoundaryPosition(
            GridSize2 size,
            int x2,
            int z2,
            int floorOffset)
        {
            if (z2 == size.Depth)
            {
                return new SocketKey(CardinalDirection.North, (x2 + size.Width - 1) / 2, floorOffset);
            }

            if (x2 == size.Width)
            {
                return new SocketKey(CardinalDirection.East, (z2 + size.Depth - 1) / 2, floorOffset);
            }

            if (z2 == -size.Depth)
            {
                return new SocketKey(CardinalDirection.South, (x2 + size.Width - 1) / 2, floorOffset);
            }

            if (x2 == -size.Width)
            {
                return new SocketKey(CardinalDirection.West, (z2 + size.Depth - 1) / 2, floorOffset);
            }

            throw new InvalidOperationException(
                $"Rotated socket position ({x2},{z2}) is not on footprint {size} boundary.");
        }

        public bool Equals(SocketKey other) =>
            Side == other.Side && Index == other.Index && FloorOffset == other.FloorOffset;

        public override bool Equals(object obj) => obj is SocketKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine((int)Side, Index, FloorOffset);

        public override string ToString() => $"{Side}_{Index:00}@{FloorOffset}";

        public static bool operator ==(SocketKey left, SocketKey right) => left.Equals(right);

        public static bool operator !=(SocketKey left, SocketKey right) => !left.Equals(right);
    }
}
