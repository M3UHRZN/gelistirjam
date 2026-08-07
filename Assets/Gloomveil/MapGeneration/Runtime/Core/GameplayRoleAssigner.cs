using System;
using System.Collections.Generic;
using System.Linq;

namespace Gloomveil.MapGeneration
{
    public sealed class GameplayRoleAssigner
    {
        public void Assign(
            MapLayout layout,
            MapGenerationParameters parameters,
            SeededRandom random)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            parameters.Validate();
            if (!layout.TraversalGraph.IsFullyConnected)
            {
                throw new MapGenerationException("Room roles require a fully connected traversal graph.");
            }

            foreach (PlacedRoom room in layout.Rooms)
            {
                room.SetRole(RoomRole.None);
            }

            PlacedRoom start = SelectStart(layout.Rooms, random);
            start.SetRole(RoomRole.Start);
            var distanceCache = new Dictionary<int, IReadOnlyDictionary<int, int>>();
            IReadOnlyDictionary<int, int> startDistances = DistancesFrom(
                layout,
                start,
                distanceCache);

            PlacedRoom exit = SelectExit(layout.Rooms, start, startDistances, random);
            exit.SetRole(RoomRole.Exit);

            var unavailable = new HashSet<int> { start.PlacementId, exit.PlacementId };
            List<PlacedRoom> fuses = SelectFuses(
                layout,
                parameters.FuseCount,
                start,
                unavailable,
                random,
                distanceCache);
            foreach (PlacedRoom fuse in fuses)
            {
                fuse.SetRole(RoomRole.Fuse);
                unavailable.Add(fuse.PlacementId);
            }

            List<PlacedRoom> safeRooms = SelectSafeRooms(
                layout,
                parameters.SafeZoneCount,
                start,
                exit,
                fuses,
                unavailable,
                random,
                distanceCache);
            foreach (PlacedRoom safeRoom in safeRooms)
            {
                safeRoom.SetRole(RoomRole.SafeZone);
            }
        }

        private static PlacedRoom SelectStart(
            IReadOnlyList<PlacedRoom> rooms,
            SeededRandom random)
        {
            var floorZero = rooms.Where(room => room.Floor == 0).ToList();
            if (floorZero.Count == 0)
            {
                throw new MapGenerationException("Cannot assign Start: floor 0 has no rooms.");
            }

            List<PlacedRoom> singleDoor = floorZero
                .Where(room => room.Ports.Count == 1)
                .ToList();
            List<PlacedRoom> candidates = singleDoor.Count > 0 ? singleDoor : floorZero;
            random.Shuffle(candidates);
            return candidates[0];
        }

        private static PlacedRoom SelectExit(
            IReadOnlyList<PlacedRoom> rooms,
            PlacedRoom start,
            IReadOnlyDictionary<int, int> startDistances,
            SeededRandom random)
        {
            List<PlacedRoom> candidates = rooms
                .Where(room => room.PlacementId != start.PlacementId)
                .ToList();
            if (candidates.Count == 0)
            {
                throw new MapGenerationException("Cannot assign Exit: no room remains after Start.");
            }

            random.Shuffle(candidates);
            return candidates
                .OrderByDescending(room => startDistances[room.GraphNodeId])
                .ThenByDescending(room => room.Floor)
                .First();
        }

        private static List<PlacedRoom> SelectFuses(
            MapLayout layout,
            int requestedCount,
            PlacedRoom start,
            ISet<int> unavailable,
            SeededRandom random,
            IDictionary<int, IReadOnlyDictionary<int, int>> distanceCache)
        {
            var selected = new List<PlacedRoom>();
            var coveredFloors = new HashSet<int>();
            while (selected.Count < requestedCount)
            {
                List<PlacedRoom> candidates = layout.Rooms
                    .Where(room => !unavailable.Contains(room.PlacementId) &&
                                   selected.All(fuse => fuse.PlacementId != room.PlacementId))
                    .ToList();
                if (candidates.Count == 0)
                {
                    throw new MapGenerationException(
                        $"Cannot assign {requestedCount} Fuse rooms; only {selected.Count} fit.");
                }

                List<PlacedRoom> uncoveredCandidates = candidates
                    .Where(room => !coveredFloors.Contains(room.Floor))
                    .ToList();
                if (uncoveredCandidates.Count > 0 && coveredFloors.Count < layout.FloorCount)
                {
                    candidates = uncoveredCandidates;
                }

                random.Shuffle(candidates);
                PlacedRoom chosen = candidates
                    .OrderByDescending(room => MinimumDistance(
                        layout,
                        room,
                        selected.Prepend(start),
                        distanceCache))
                    .First();
                selected.Add(chosen);
                coveredFloors.Add(chosen.Floor);
            }

            return selected;
        }

        private static List<PlacedRoom> SelectSafeRooms(
            MapLayout layout,
            int requestedCount,
            PlacedRoom start,
            PlacedRoom exit,
            IReadOnlyCollection<PlacedRoom> fuses,
            ISet<int> unavailable,
            SeededRandom random,
            IDictionary<int, IReadOnlyDictionary<int, int>> distanceCache)
        {
            var selected = new List<PlacedRoom>();
            var fixedAnchors = new List<PlacedRoom> { start, exit };
            fixedAnchors.AddRange(fuses);

            while (selected.Count < requestedCount)
            {
                var anchors = new List<PlacedRoom>(fixedAnchors);
                anchors.AddRange(selected);
                List<PlacedRoom> candidates = layout.Rooms
                    .Where(room =>
                        !unavailable.Contains(room.PlacementId) &&
                        selected.All(safe => safe.PlacementId != room.PlacementId) &&
                        room.Ports.Count >= 2 &&
                        MinimumDistance(layout, room, anchors, distanceCache) >= 3)
                    .ToList();
                if (candidates.Count == 0)
                {
                    throw new MapGenerationException(
                        $"Cannot assign {requestedCount} SafeZone rooms; only {selected.Count} fit " +
                        "the two-door and distance constraints.");
                }

                random.Shuffle(candidates);
                PlacedRoom chosen = candidates
                    .OrderByDescending(room => MinimumDistance(
                        layout,
                        room,
                        anchors,
                        distanceCache))
                    .First();
                selected.Add(chosen);
            }

            return selected;
        }

        private static int MinimumDistance(
            MapLayout layout,
            PlacedRoom candidate,
            IEnumerable<PlacedRoom> anchors,
            IDictionary<int, IReadOnlyDictionary<int, int>> distanceCache)
        {
            int minimum = int.MaxValue;
            foreach (PlacedRoom anchor in anchors)
            {
                IReadOnlyDictionary<int, int> distances = DistancesFrom(
                    layout,
                    anchor,
                    distanceCache);
                minimum = Math.Min(minimum, distances[candidate.GraphNodeId]);
            }

            return minimum;
        }

        private static IReadOnlyDictionary<int, int> DistancesFrom(
            MapLayout layout,
            PlacedRoom room,
            IDictionary<int, IReadOnlyDictionary<int, int>> distanceCache)
        {
            if (!distanceCache.TryGetValue(
                    room.GraphNodeId,
                    out IReadOnlyDictionary<int, int> distances))
            {
                distances = MapGraphSearch.WeightedDistancesFrom(
                    layout.TraversalGraph,
                    room.GraphNodeId);
                distanceCache.Add(room.GraphNodeId, distances);
            }

            return distances;
        }
    }
}
