using System;
using System.Collections.Generic;
using System.Linq;

namespace Gloomveil.MapGeneration
{
    public sealed class MapModuleResolver
    {
        private readonly MapModuleCatalog catalog;

        public MapModuleResolver(MapModuleCatalog catalog)
        {
            this.catalog = catalog != null
                ? catalog
                : throw new ArgumentNullException(nameof(catalog));
        }

        public MapModuleSelection ResolveRoom(PlacedRoom room, SeededRandom random)
        {
            if (room == null)
            {
                throw new ArgumentNullException(nameof(room));
            }

            MapModuleEntry entry = SelectEntry(
                candidate =>
                    candidate.Category == MapModuleCategory.Room &&
                    candidate.CanonicalFootprintUnits == room.CanonicalSize &&
                    candidate.AllowedRoomRoles.Allows(room.Role) &&
                    SupportsTurns(candidate, room.QuarterTurns),
                random,
                $"room {room.PlacementId} ({room.FootprintId}, role {room.Role})");
            return Selection(
                room.PlacementId,
                entry,
                room.Role,
                room.SouthwestOrigin,
                room.Floor,
                room.Floor,
                room.CanonicalSize,
                room.QuarterTurns,
                room.OpenLocalSockets,
                room.Ports);
        }

        public MapModuleSelection ResolveCorridor(
            CorridorModulePlacement corridor,
            SeededRandom random)
        {
            if (corridor == null)
            {
                throw new ArgumentNullException(nameof(corridor));
            }

            MapModuleEntry entry = SelectEntry(
                candidate =>
                    candidate.Category == MapModuleCategory.Corridor &&
                    candidate.CanonicalFootprintUnits == corridor.CanonicalSize &&
                    candidate.CorridorShape == corridor.Shape &&
                    SupportsTurns(candidate, corridor.QuarterTurns),
                random,
                $"corridor {corridor.PlacementId} ({corridor.Shape}, {corridor.CanonicalSize})");
            IReadOnlyList<MapPort> ports = BuildPorts(
                corridor.PlacementId,
                corridor.SouthwestOrigin,
                corridor.CanonicalSize,
                corridor.QuarterTurns,
                corridor.OpenLocalSockets);
            return Selection(
                corridor.PlacementId,
                entry,
                RoomRole.None,
                corridor.SouthwestOrigin,
                corridor.SouthwestOrigin.Floor,
                corridor.SouthwestOrigin.Floor,
                corridor.CanonicalSize,
                corridor.QuarterTurns,
                corridor.OpenLocalSockets,
                ports);
        }

        public MapModuleSelection ResolveStair(PlacedStair stair, SeededRandom random)
        {
            if (stair == null)
            {
                throw new ArgumentNullException(nameof(stair));
            }

            MapModuleEntry entry = SelectEntry(
                candidate =>
                    candidate.Category == MapModuleCategory.Stair &&
                    candidate.CanonicalFootprintUnits == stair.CanonicalSize &&
                    SupportsTurns(candidate, stair.QuarterTurns),
                random,
                $"stair {stair.PlacementId} ({stair.FootprintId})");
            var sockets = new List<SocketKey>();
            var ports = new List<MapPort>();
            if (stair.BottomPort.HasValue)
            {
                sockets.Add(stair.BottomPort.Value.LocalSocket);
                ports.Add(stair.BottomPort.Value);
            }

            if (stair.TopPort.HasValue)
            {
                sockets.Add(stair.TopPort.Value.LocalSocket);
                ports.Add(stair.TopPort.Value);
            }

            return Selection(
                stair.PlacementId,
                entry,
                RoomRole.None,
                stair.SouthwestOrigin,
                stair.LowerFloor,
                stair.UpperFloor,
                stair.CanonicalSize,
                stair.QuarterTurns,
                sockets,
                ports);
        }

        private MapModuleEntry SelectEntry(
            Func<MapModuleEntry, bool> predicate,
            SeededRandom random,
            string description)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            MapModuleEntry[] candidates = catalog.Entries
                .Where(entry => entry != null && predicate(entry))
                .OrderBy(entry => entry.CatalogEntryId, StringComparer.Ordinal)
                .ToArray();
            if (candidates.Length == 0)
            {
                throw new MapGenerationException(
                    $"Module catalog '{catalog.name}' has no compatible entry for {description}.");
            }

            int totalWeight = candidates.Sum(entry => Math.Max(0, entry.Weight));
            if (totalWeight < 1)
            {
                throw new MapGenerationException(
                    $"Compatible entries for {description} have no positive weight.");
            }

            int roll = random.Next(totalWeight);
            foreach (MapModuleEntry candidate in candidates)
            {
                roll -= Math.Max(0, candidate.Weight);
                if (roll < 0)
                {
                    return candidate;
                }
            }

            return candidates[candidates.Length - 1];
        }

        private static MapModuleSelection Selection(
            int placementId,
            MapModuleEntry entry,
            RoomRole role,
            GridCoord3 origin,
            int minFloor,
            int maxFloor,
            GridSize2 canonicalSize,
            int quarterTurns,
            IEnumerable<SocketKey> openLocalSockets,
            IEnumerable<MapPort> ports) =>
            new MapModuleSelection(
                placementId,
                entry.CatalogEntryId,
                entry.FamilyId,
                entry.Category,
                role,
                entry.Prefab,
                origin,
                minFloor,
                maxFloor,
                canonicalSize,
                quarterTurns,
                entry.UsesDynamicSockets,
                openLocalSockets,
                ports);

        private static bool SupportsTurns(MapModuleEntry entry, int quarterTurns) =>
            (((quarterTurns % 4) + 4) % 4) == 0 || entry.AllowQuarterTurns;

        private static IReadOnlyList<MapPort> BuildPorts(
            int placementId,
            GridCoord3 origin,
            GridSize2 canonicalSize,
            int quarterTurns,
            IEnumerable<SocketKey> localSockets)
        {
            GridSize2 rotatedSize = canonicalSize.Rotate(quarterTurns);
            var rect = new GridRect(origin.X, origin.Z, rotatedSize.Width, rotatedSize.Depth);
            return localSockets
                .Select(local => BuildPort(placementId, origin.Floor, rect, canonicalSize, quarterTurns, local))
                .ToArray();
        }

        private static MapPort BuildPort(
            int placementId,
            int floor,
            GridRect rect,
            GridSize2 canonicalSize,
            int quarterTurns,
            SocketKey local)
        {
            SocketKey rotated = local.Rotate(canonicalSize, quarterTurns);
            GridCoord3 outside;
            GridEdgeKey edge;
            switch (rotated.Side)
            {
                case CardinalDirection.North:
                    outside = new GridCoord3(rect.X + rotated.Index, rect.MaxZ, floor);
                    edge = new GridEdgeKey(floor, GridEdgeAxis.AlongX, outside.X, outside.Z);
                    break;
                case CardinalDirection.East:
                    outside = new GridCoord3(rect.MaxX, rect.Z + rotated.Index, floor);
                    edge = new GridEdgeKey(floor, GridEdgeAxis.AlongZ, outside.X, outside.Z);
                    break;
                case CardinalDirection.South:
                    outside = new GridCoord3(rect.X + rotated.Index, rect.Z - 1, floor);
                    edge = new GridEdgeKey(floor, GridEdgeAxis.AlongX, outside.X, outside.Z + 1);
                    break;
                case CardinalDirection.West:
                    outside = new GridCoord3(rect.X - 1, rect.Z + rotated.Index, floor);
                    edge = new GridEdgeKey(floor, GridEdgeAxis.AlongZ, outside.X + 1, outside.Z);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return new MapPort(
                placementId,
                local,
                rotated,
                edge,
                outside,
                rotated.Side);
        }
    }
}
