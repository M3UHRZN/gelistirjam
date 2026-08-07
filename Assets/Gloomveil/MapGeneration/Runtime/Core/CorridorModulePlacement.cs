using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Gloomveil.MapGeneration
{
    public sealed class CorridorModulePlacement
    {
        public int PlacementId { get; }
        public GridCoord3 SouthwestOrigin { get; }
        public GridSize2 CanonicalSize { get; }
        public GridSize2 RotatedSize => CanonicalSize.Rotate(QuarterTurns);
        public int QuarterTurns { get; }
        public CorridorShape Shape { get; }
        public int LengthUnits { get; }
        public IReadOnlyList<SocketKey> OpenLocalSockets { get; }

        public CorridorModulePlacement(
            int placementId,
            GridCoord3 southwestOrigin,
            GridSize2 canonicalSize,
            int quarterTurns,
            CorridorShape shape,
            int lengthUnits,
            IEnumerable<SocketKey> openLocalSockets)
        {
            if (placementId < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(placementId),
                    placementId,
                    "Placement ID must be positive.");
            }

            if (lengthUnits < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lengthUnits),
                    lengthUnits,
                    "Corridor length must be positive.");
            }

            PlacementId = placementId;
            SouthwestOrigin = southwestOrigin;
            CanonicalSize = canonicalSize;
            QuarterTurns = ((quarterTurns % 4) + 4) % 4;
            Shape = shape;
            LengthUnits = lengthUnits;
            OpenLocalSockets = new ReadOnlyCollection<SocketKey>(
                new List<SocketKey>(
                    openLocalSockets ?? throw new ArgumentNullException(nameof(openLocalSockets))));
        }
    }
}
