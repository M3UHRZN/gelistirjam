using System;

namespace Gloomveil.MapGeneration
{
    [Serializable]
    public readonly struct CorridorTile : IEquatable<CorridorTile>
    {
        public GridCoord3 Coord { get; }
        public CorridorConnectionMask ConnectionMask { get; }

        public CorridorTile(GridCoord3 coord, CorridorConnectionMask connectionMask)
        {
            Coord = coord;
            ConnectionMask = connectionMask;
        }

        public bool Equals(CorridorTile other) =>
            Coord == other.Coord && ConnectionMask == other.ConnectionMask;

        public override bool Equals(object obj) => obj is CorridorTile other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Coord, (int)ConnectionMask);
    }
}
