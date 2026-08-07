using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Gloomveil.MapGeneration
{
    public sealed class PlacedRoom
    {
        private readonly List<SocketKey> openLocalSockets = new List<SocketKey>();
        private readonly List<MapPort> ports = new List<MapPort>();

        public int PlacementId { get; }
        public string FootprintId { get; }
        public int Floor { get; }
        public GridCoord3 SouthwestOrigin { get; }
        public GridSize2 CanonicalSize { get; }
        public int QuarterTurns { get; }
        public GridRect RotatedRect { get; }
        public RoomRole Role { get; private set; }
        public int GraphNodeId { get; private set; } = -1;
        public IReadOnlyList<SocketKey> OpenLocalSockets =>
            new ReadOnlyCollection<SocketKey>(openLocalSockets);
        public IReadOnlyList<MapPort> Ports => new ReadOnlyCollection<MapPort>(ports);

        public PlacedRoom(
            int placementId,
            string footprintId,
            int floor,
            int x,
            int z,
            GridSize2 canonicalSize,
            int quarterTurns)
        {
            if (placementId < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(placementId),
                    placementId,
                    "Placement ID must be positive.");
            }

            PlacementId = placementId;
            FootprintId = footprintId ?? throw new ArgumentNullException(nameof(footprintId));
            Floor = floor;
            SouthwestOrigin = new GridCoord3(x, z, floor);
            CanonicalSize = canonicalSize;
            QuarterTurns = ((quarterTurns % 4) + 4) % 4;
            GridSize2 rotatedSize = canonicalSize.Rotate(QuarterTurns);
            RotatedRect = new GridRect(x, z, rotatedSize.Width, rotatedSize.Depth);
            Role = RoomRole.None;
        }

        internal void SetRole(RoomRole role) => Role = role;

        internal void SetGraphNodeId(int graphNodeId) => GraphNodeId = graphNodeId;

        internal void SetPorts(IEnumerable<MapPort> selectedPorts)
        {
            openLocalSockets.Clear();
            ports.Clear();
            foreach (MapPort port in selectedPorts)
            {
                openLocalSockets.Add(port.LocalSocket);
                ports.Add(port);
            }
        }
    }
}
