using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Gloomveil.MapGeneration
{
    public sealed class MapModuleSelection
    {
        public int PlacementId { get; }
        public string CatalogEntryId { get; }
        public string FamilyId { get; }
        public MapModuleCategory Category { get; }
        public RoomRole RoomRole { get; }
        public GameObject Prefab { get; }
        public GridCoord3 Origin { get; }
        public int MinFloor { get; }
        public int MaxFloor { get; }
        public GridSize2 CanonicalSize { get; }
        public GridSize2 RotatedSize { get; }
        public int QuarterTurns { get; }
        public bool UsesDynamicSockets { get; }
        public IReadOnlyList<SocketKey> OpenLocalSockets { get; }
        public IReadOnlyList<MapPort> Ports { get; }

        public MapModuleSelection(
            int placementId,
            string catalogEntryId,
            string familyId,
            MapModuleCategory category,
            RoomRole roomRole,
            GameObject prefab,
            GridCoord3 origin,
            int minFloor,
            int maxFloor,
            GridSize2 canonicalSize,
            int quarterTurns,
            bool usesDynamicSockets,
            IEnumerable<SocketKey> openLocalSockets,
            IEnumerable<MapPort> ports)
        {
            PlacementId = placementId;
            CatalogEntryId = catalogEntryId ?? throw new ArgumentNullException(nameof(catalogEntryId));
            FamilyId = familyId ?? throw new ArgumentNullException(nameof(familyId));
            Category = category;
            RoomRole = roomRole;
            Prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            Origin = origin;
            MinFloor = minFloor;
            MaxFloor = maxFloor;
            CanonicalSize = canonicalSize;
            QuarterTurns = ((quarterTurns % 4) + 4) % 4;
            RotatedSize = canonicalSize.Rotate(QuarterTurns);
            UsesDynamicSockets = usesDynamicSockets;
            OpenLocalSockets = new ReadOnlyCollection<SocketKey>(
                new List<SocketKey>(openLocalSockets ??
                                    throw new ArgumentNullException(nameof(openLocalSockets))));
            Ports = new ReadOnlyCollection<MapPort>(
                new List<MapPort>(ports ?? throw new ArgumentNullException(nameof(ports))));
        }
    }
}
