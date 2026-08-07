using System;

namespace Gloomveil.MapGeneration
{
    [Serializable]
    public readonly struct MapPort : IEquatable<MapPort>
    {
        public int OwnerPlacementId { get; }
        public SocketKey LocalSocket { get; }
        public SocketKey RotatedSocket { get; }
        public GridEdgeKey WorldEdge { get; }
        public GridCoord3 WorldOutsideCell { get; }
        public CardinalDirection WorldFacing { get; }

        public MapPort(
            int ownerPlacementId,
            SocketKey localSocket,
            SocketKey rotatedSocket,
            GridEdgeKey worldEdge,
            GridCoord3 worldOutsideCell,
            CardinalDirection worldFacing)
        {
            OwnerPlacementId = ownerPlacementId;
            LocalSocket = localSocket;
            RotatedSocket = rotatedSocket;
            WorldEdge = worldEdge;
            WorldOutsideCell = worldOutsideCell;
            WorldFacing = worldFacing;
        }

        public bool Equals(MapPort other) =>
            OwnerPlacementId == other.OwnerPlacementId &&
            LocalSocket == other.LocalSocket &&
            RotatedSocket == other.RotatedSocket &&
            WorldEdge == other.WorldEdge &&
            WorldOutsideCell == other.WorldOutsideCell &&
            WorldFacing == other.WorldFacing;

        public override bool Equals(object obj) => obj is MapPort other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(
            OwnerPlacementId,
            LocalSocket,
            RotatedSocket,
            WorldEdge,
            WorldOutsideCell,
            (int)WorldFacing);
    }
}
