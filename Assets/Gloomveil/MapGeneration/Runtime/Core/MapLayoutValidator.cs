using System;
using System.Collections.Generic;
using System.Linq;

namespace Gloomveil.MapGeneration
{
    public sealed class MapLayoutValidator
    {
        public MapValidationReport Validate(
            MapLayout layout,
            MapGenerationParameters parameters)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            var errors = new List<string>();
            ValidateDimensions(layout, parameters, errors);
            ValidateRoomVariety(layout, parameters, errors);
            ValidateAreaPlacements(layout, parameters, errors);
            ValidateCorridors(layout, parameters, errors);
            ValidatePorts(layout, errors);
            ValidateTraversal(layout, parameters, errors);
            ValidateRoles(layout, parameters, errors);
            ValidateStairs(layout, parameters, errors);
            ValidateVisibleLengths(layout, errors);
            ValidatePlacementIds(layout, errors);
            return new MapValidationReport(errors);
        }

        private static void ValidateDimensions(
            MapLayout layout,
            MapGenerationParameters parameters,
            ICollection<string> errors)
        {
            if (layout.FloorCount != parameters.FloorCount)
            {
                errors.Add(
                    $"FloorCount is {layout.FloorCount}; expected {parameters.FloorCount}.");
            }

            if (layout.WidthUnits != parameters.WidthUnits ||
                layout.DepthUnits != parameters.DepthUnits)
            {
                errors.Add(
                    $"Layout bounds are {layout.WidthUnits}x{layout.DepthUnits}; expected " +
                    $"{parameters.WidthUnits}x{parameters.DepthUnits}.");
            }
        }

        private static void ValidateRoomVariety(
            MapLayout layout,
            MapGenerationParameters parameters,
            ICollection<string> errors)
        {
            string[] requiredIds = parameters.RoomFootprints
                .Where(spec => spec.RequiredPerFloor)
                .Select(spec => spec.Id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            for (int floor = 0; floor < parameters.FloorCount; floor++)
            {
                PlacedRoom[] rooms = layout.Rooms.Where(room => room.Floor == floor).ToArray();
                foreach (string requiredId in requiredIds)
                {
                    if (!rooms.Any(room => room.FootprintId == requiredId))
                    {
                        errors.Add($"Floor {floor} is missing required room footprint {requiredId}.");
                    }
                }

                if (!rooms.Any(room =>
                        room.CanonicalSize.Width != room.CanonicalSize.Depth &&
                        (room.QuarterTurns & 1) == 1))
                {
                    errors.Add($"Floor {floor} has no odd-turn non-square room.");
                }
            }

            if (!layout.Rooms.Any(room =>
                    room.FootprintId == "2x3" && (room.QuarterTurns & 1) == 0))
            {
                errors.Add("Layout has no canonical-orientation 2x3 room.");
            }

            if (!layout.Rooms.Any(room =>
                    room.FootprintId == "2x3" && (room.QuarterTurns & 1) == 1))
            {
                errors.Add("Layout has no rotated 3x2 room.");
            }
        }

        private static void ValidateAreaPlacements(
            MapLayout layout,
            MapGenerationParameters parameters,
            ICollection<string> errors)
        {
            for (int floor = 0; floor < parameters.FloorCount; floor++)
            {
                var placements = new List<AreaPlacement>();
                placements.AddRange(layout.Rooms
                    .Where(room => room.Floor == floor)
                    .Select(room => new AreaPlacement($"room {room.PlacementId}", room.RotatedRect)));
                placements.AddRange(layout.Stairs
                    .Where(stair => stair.LowerFloor == floor || stair.UpperFloor == floor)
                    .Select(stair => new AreaPlacement($"stair {stair.PlacementId}", stair.RotatedRect)));

                foreach (AreaPlacement placement in placements)
                {
                    if (!InsideBounds(placement.Rect, parameters))
                    {
                        errors.Add($"Floor {floor} {placement.Name} lies outside map bounds.");
                    }
                }

                for (int left = 0; left < placements.Count; left++)
                {
                    for (int right = left + 1; right < placements.Count; right++)
                    {
                        if (placements[left].Rect.Overlaps(placements[right].Rect))
                        {
                            errors.Add(
                                $"Floor {floor} overlap: {placements[left].Name} and " +
                                $"{placements[right].Name}.");
                        }
                    }
                }
            }
        }

        private static void ValidateCorridors(
            MapLayout layout,
            MapGenerationParameters parameters,
            ICollection<string> errors)
        {
            var tileCoords = new HashSet<GridCoord3>(layout.CorridorTiles.Select(tile => tile.Coord));
            foreach (CorridorTile tile in layout.CorridorTiles)
            {
                if (!InsideBounds(tile.Coord, parameters))
                {
                    errors.Add($"Corridor tile {tile.Coord} lies outside map bounds.");
                }
                else if (AreaContains(layout, tile.Coord))
                {
                    errors.Add($"Corridor tile {tile.Coord} overlaps a room or stair interior.");
                }
            }

            foreach (CorridorModulePlacement module in layout.CorridorModules)
            {
                GridSize2 rotated = module.RotatedSize;
                var rect = new GridRect(
                    module.SouthwestOrigin.X,
                    module.SouthwestOrigin.Z,
                    rotated.Width,
                    rotated.Depth);
                if (module.SouthwestOrigin.Floor < 0 ||
                    module.SouthwestOrigin.Floor >= parameters.FloorCount ||
                    !InsideBounds(rect, parameters))
                {
                    errors.Add($"Corridor module {module.PlacementId} lies outside map bounds.");
                    continue;
                }

                for (int z = rect.Z; z < rect.MaxZ; z++)
                {
                    for (int x = rect.X; x < rect.MaxX; x++)
                    {
                        var coord = new GridCoord3(x, z, module.SouthwestOrigin.Floor);
                        if (AreaContains(layout, coord))
                        {
                            errors.Add(
                                $"Corridor module {module.PlacementId} overlaps an area at {coord}.");
                        }

                        if (!tileCoords.Contains(coord))
                        {
                            errors.Add(
                                $"Corridor module {module.PlacementId} covers non-corridor cell {coord}.");
                        }
                    }
                }
            }
        }

        private static void ValidatePorts(MapLayout layout, ICollection<string> errors)
        {
            var corridorCells = new HashSet<GridCoord3>(
                layout.CorridorTiles.Select(tile => tile.Coord));
            MapPort[] areaPorts = layout.Rooms
                .SelectMany(room => room.Ports)
                .Concat(layout.Stairs.SelectMany(StairPorts))
                .ToArray();
            foreach (MapPort port in areaPorts)
            {
                if (!corridorCells.Contains(port.WorldOutsideCell))
                {
                    errors.Add(
                        $"Placement {port.OwnerPlacementId} port {port.LocalSocket} has no corridor landing.");
                }

                GridEdgeKey expected = EdgeForOutsideCell(port.WorldOutsideCell, port.WorldFacing);
                if (port.WorldEdge != expected)
                {
                    errors.Add(
                        $"Placement {port.OwnerPlacementId} port {port.LocalSocket} has non-normalized edge " +
                        $"{port.WorldEdge}; expected {expected}.");
                }
            }

            MapPort[] corridorPorts = layout.CorridorModules
                .SelectMany(CorridorPorts)
                .ToArray();
            IGrouping<GridEdgeKey, MapPort>[] portGroups = areaPorts
                .Concat(corridorPorts)
                .GroupBy(port => port.WorldEdge)
                .ToArray();
            foreach (IGrouping<GridEdgeKey, MapPort> group in portGroups)
            {
                MapPort[] shared = group.ToArray();
                if (shared.Length != 2)
                {
                    errors.Add(
                        $"World edge {group.Key} has {shared.Length} open module ports; expected 2.");
                    continue;
                }

                if (!AreOpposite(shared[0].WorldFacing, shared[1].WorldFacing))
                {
                    errors.Add($"Ports sharing world edge {group.Key} do not face opposite directions.");
                }
            }

            HashSet<GridEdgeKey> expectedEdges = ExpectedOpenModuleEdges(layout, errors);
            var actualEdges = new HashSet<GridEdgeKey>(portGroups.Select(group => group.Key));
            foreach (GridEdgeKey missing in expectedEdges.Where(edge => !actualEdges.Contains(edge)))
            {
                errors.Add(
                    $"Corridor topology opens world edge {missing}, but no module port declares it.");
            }

            foreach (GridEdgeKey unexpected in actualEdges.Where(edge => !expectedEdges.Contains(edge)))
            {
                errors.Add(
                    $"Module ports open world edge {unexpected}, but corridor topology closes it.");
            }
        }

        private static IEnumerable<MapPort> CorridorPorts(CorridorModulePlacement module)
        {
            GridSize2 rotated = module.RotatedSize;
            var rect = new GridRect(
                module.SouthwestOrigin.X,
                module.SouthwestOrigin.Z,
                rotated.Width,
                rotated.Depth);
            foreach (SocketKey localSocket in module.OpenLocalSockets)
            {
                yield return MapPortGeometry.Create(
                    module.PlacementId,
                    localSocket,
                    module.CanonicalSize,
                    module.QuarterTurns,
                    rect,
                    module.SouthwestOrigin.Floor);
            }
        }

        private static HashSet<GridEdgeKey> ExpectedOpenModuleEdges(
            MapLayout layout,
            ICollection<string> errors)
        {
            var tilesByCoord = new Dictionary<GridCoord3, CorridorTile>();
            foreach (CorridorTile tile in layout.CorridorTiles)
            {
                if (tilesByCoord.ContainsKey(tile.Coord))
                {
                    errors.Add($"Corridor tile {tile.Coord} is duplicated.");
                    continue;
                }

                tilesByCoord.Add(tile.Coord, tile);
            }

            var moduleByCell = new Dictionary<GridCoord3, int>();
            foreach (CorridorModulePlacement module in layout.CorridorModules)
            {
                GridSize2 size = module.RotatedSize;
                for (int z = module.SouthwestOrigin.Z;
                     z < module.SouthwestOrigin.Z + size.Depth;
                     z++)
                {
                    for (int x = module.SouthwestOrigin.X;
                         x < module.SouthwestOrigin.X + size.Width;
                         x++)
                    {
                        var coord = new GridCoord3(x, z, module.SouthwestOrigin.Floor);
                        if (moduleByCell.TryGetValue(coord, out int owner))
                        {
                            errors.Add(
                                $"Corridor cell {coord} is covered by modules {owner} and " +
                                $"{module.PlacementId}.");
                            continue;
                        }

                        moduleByCell.Add(coord, module.PlacementId);
                    }
                }
            }

            foreach (CorridorTile tile in layout.CorridorTiles)
            {
                if (!moduleByCell.ContainsKey(tile.Coord))
                {
                    errors.Add($"Corridor tile {tile.Coord} is not covered by a module.");
                    continue;
                }

                ValidateInternalModuleEdge(
                    tile,
                    CardinalDirection.North,
                    tilesByCoord,
                    moduleByCell,
                    errors);
                ValidateInternalModuleEdge(
                    tile,
                    CardinalDirection.East,
                    tilesByCoord,
                    moduleByCell,
                    errors);
            }

            var expected = new HashSet<GridEdgeKey>();
            foreach (CorridorTile tile in layout.CorridorTiles)
            {
                RecordExpectedOpenEdge(
                    tile,
                    CardinalDirection.North,
                    tilesByCoord,
                    moduleByCell,
                    expected,
                    errors);
                RecordExpectedOpenEdge(
                    tile,
                    CardinalDirection.East,
                    tilesByCoord,
                    moduleByCell,
                    expected,
                    errors);
                RecordExpectedOpenEdge(
                    tile,
                    CardinalDirection.South,
                    tilesByCoord,
                    moduleByCell,
                    expected,
                    errors);
                RecordExpectedOpenEdge(
                    tile,
                    CardinalDirection.West,
                    tilesByCoord,
                    moduleByCell,
                    expected,
                    errors);
            }

            return expected;
        }

        private static void ValidateInternalModuleEdge(
            CorridorTile tile,
            CardinalDirection direction,
            IReadOnlyDictionary<GridCoord3, CorridorTile> tilesByCoord,
            IReadOnlyDictionary<GridCoord3, int> moduleByCell,
            ICollection<string> errors)
        {
            GridCoord3 neighborCoord = Neighbor(tile.Coord, direction);
            if (!tilesByCoord.TryGetValue(neighborCoord, out CorridorTile neighbor) ||
                !moduleByCell.TryGetValue(tile.Coord, out int owner) ||
                !moduleByCell.TryGetValue(neighborCoord, out int neighborOwner) ||
                owner != neighborOwner)
            {
                return;
            }

            bool forwardOpen =
                (tile.ConnectionMask & ConnectionFor(direction)) != 0;
            bool backwardOpen =
                (neighbor.ConnectionMask & ConnectionFor(Opposite(direction))) != 0;
            if (!forwardOpen || !backwardOpen)
            {
                errors.Add(
                    $"Corridor module {owner} has a closed internal edge between " +
                    $"{tile.Coord} and {neighborCoord}.");
            }
        }

        private static void RecordExpectedOpenEdge(
            CorridorTile tile,
            CardinalDirection direction,
            IReadOnlyDictionary<GridCoord3, CorridorTile> tilesByCoord,
            IReadOnlyDictionary<GridCoord3, int> moduleByCell,
            ISet<GridEdgeKey> expected,
            ICollection<string> errors)
        {
            CorridorConnectionMask connection = ConnectionFor(direction);
            if ((tile.ConnectionMask & connection) == 0)
            {
                return;
            }

            GridCoord3 neighborCoord = Neighbor(tile.Coord, direction);
            if (tilesByCoord.TryGetValue(neighborCoord, out CorridorTile neighbor))
            {
                CorridorConnectionMask opposite = ConnectionFor(Opposite(direction));
                if ((neighbor.ConnectionMask & opposite) == 0)
                {
                    errors.Add(
                        $"Corridor connection {tile.Coord} -> {neighborCoord} is not reciprocal.");
                }

                if (moduleByCell.TryGetValue(tile.Coord, out int owner) &&
                    moduleByCell.TryGetValue(neighborCoord, out int neighborOwner) &&
                    owner == neighborOwner)
                {
                    return;
                }
            }

            expected.Add(EdgeForCorridorCell(tile.Coord, direction));
        }

        private static CorridorConnectionMask ConnectionFor(CardinalDirection direction)
        {
            switch (direction)
            {
                case CardinalDirection.North:
                    return CorridorConnectionMask.North;
                case CardinalDirection.East:
                    return CorridorConnectionMask.East;
                case CardinalDirection.South:
                    return CorridorConnectionMask.South;
                case CardinalDirection.West:
                    return CorridorConnectionMask.West;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        private static CardinalDirection Opposite(CardinalDirection direction)
        {
            switch (direction)
            {
                case CardinalDirection.North:
                    return CardinalDirection.South;
                case CardinalDirection.East:
                    return CardinalDirection.West;
                case CardinalDirection.South:
                    return CardinalDirection.North;
                case CardinalDirection.West:
                    return CardinalDirection.East;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        private static GridCoord3 Neighbor(
            GridCoord3 coord,
            CardinalDirection direction)
        {
            switch (direction)
            {
                case CardinalDirection.North:
                    return new GridCoord3(coord.X, coord.Z + 1, coord.Floor);
                case CardinalDirection.East:
                    return new GridCoord3(coord.X + 1, coord.Z, coord.Floor);
                case CardinalDirection.South:
                    return new GridCoord3(coord.X, coord.Z - 1, coord.Floor);
                case CardinalDirection.West:
                    return new GridCoord3(coord.X - 1, coord.Z, coord.Floor);
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        private static GridEdgeKey EdgeForCorridorCell(
            GridCoord3 coord,
            CardinalDirection direction)
        {
            switch (direction)
            {
                case CardinalDirection.North:
                    return new GridEdgeKey(
                        coord.Floor,
                        GridEdgeAxis.AlongX,
                        coord.X,
                        coord.Z + 1);
                case CardinalDirection.East:
                    return new GridEdgeKey(
                        coord.Floor,
                        GridEdgeAxis.AlongZ,
                        coord.X + 1,
                        coord.Z);
                case CardinalDirection.South:
                    return new GridEdgeKey(
                        coord.Floor,
                        GridEdgeAxis.AlongX,
                        coord.X,
                        coord.Z);
                case CardinalDirection.West:
                    return new GridEdgeKey(
                        coord.Floor,
                        GridEdgeAxis.AlongZ,
                        coord.X,
                        coord.Z);
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        private static void ValidateTraversal(
            MapLayout layout,
            MapGenerationParameters parameters,
            ICollection<string> errors)
        {
            if (!layout.TraversalGraph.IsFullyConnected)
            {
                errors.Add("Weighted traversal graph is disconnected.");
            }

            for (int floor = 0; floor < parameters.FloorCount; floor++)
            {
                int cycleRank = FloorCycleRank(layout.TraversalGraph, floor);
                if (cycleRank < parameters.ExtraRoomLinksPerFloor)
                {
                    errors.Add(
                        $"Floor {floor} cycle rank is {cycleRank}; expected at least " +
                        $"{parameters.ExtraRoomLinksPerFloor}.");
                }
            }
        }

        private static void ValidateRoles(
            MapLayout layout,
            MapGenerationParameters parameters,
            ICollection<string> errors)
        {
            PlacedRoom[] starts = layout.Rooms.Where(room => room.Role == RoomRole.Start).ToArray();
            PlacedRoom[] exits = layout.Rooms.Where(room => room.Role == RoomRole.Exit).ToArray();
            PlacedRoom[] fuses = layout.Rooms.Where(room => room.Role == RoomRole.Fuse).ToArray();
            PlacedRoom[] safeRooms = layout.Rooms
                .Where(room => room.Role == RoomRole.SafeZone)
                .ToArray();

            RequireCount("Start", starts.Length, 1, errors);
            RequireCount("Exit", exits.Length, 1, errors);
            RequireCount("Fuse", fuses.Length, parameters.FuseCount, errors);
            RequireCount("SafeZone", safeRooms.Length, parameters.SafeZoneCount, errors);

            if (starts.Length == 1 && exits.Length == 1 && layout.TraversalGraph.IsFullyConnected)
            {
                IReadOnlyDictionary<int, int> distances =
                    MapGraphSearch.WeightedDistancesFrom(
                        layout.TraversalGraph,
                        starts[0].GraphNodeId);
                int farthest = layout.Rooms
                    .Where(room => room.PlacementId != starts[0].PlacementId)
                    .Max(room => distances[room.GraphNodeId]);
                if (distances[exits[0].GraphNodeId] != farthest)
                {
                    errors.Add("Exit is not the farthest room from Start by weighted distance.");
                }
            }

            int expectedFuseCoverage = Math.Min(parameters.FuseCount, parameters.FloorCount);
            int actualFuseCoverage = fuses.Select(room => room.Floor).Distinct().Count();
            if (actualFuseCoverage != expectedFuseCoverage)
            {
                errors.Add(
                    $"Fuse floor coverage is {actualFuseCoverage}; expected {expectedFuseCoverage}.");
            }
        }

        private static void ValidateStairs(
            MapLayout layout,
            MapGenerationParameters parameters,
            ICollection<string> errors)
        {
            var corridorCells = new HashSet<GridCoord3>(
                layout.CorridorTiles.Select(tile => tile.Coord));
            for (int lowerFloor = 0; lowerFloor < parameters.FloorCount - 1; lowerFloor++)
            {
                PlacedStair[] stairs = layout.Stairs
                    .Where(stair => stair.LowerFloor == lowerFloor)
                    .OrderBy(stair => stair.PlacementId)
                    .ToArray();
                if (stairs.Length != parameters.VerticalLinksPerFloorPair)
                {
                    errors.Add(
                        $"F{lowerFloor}->F{lowerFloor + 1} has {stairs.Length} stairs; expected " +
                        $"{parameters.VerticalLinksPerFloorPair}.");
                }

                for (int left = 0; left < stairs.Length; left++)
                {
                    for (int right = left + 1; right < stairs.Length; right++)
                    {
                        double distance = CentreManhattanUnits(
                            stairs[left].RotatedRect,
                            stairs[right].RotatedRect);
                        if (distance < parameters.MinimumStairCenterSeparationUnits)
                        {
                            errors.Add(
                                $"Stairs {stairs[left].PlacementId}/{stairs[right].PlacementId} on " +
                                $"F{lowerFloor} are only {distance} units apart.");
                        }
                    }
                }
            }

            foreach (PlacedStair stair in layout.Stairs)
            {
                if (!stair.BottomPort.HasValue || !stair.TopPort.HasValue)
                {
                    errors.Add($"Stair {stair.PlacementId} is missing a landing port.");
                    continue;
                }

                if (!corridorCells.Contains(stair.BottomPort.Value.WorldOutsideCell) ||
                    !corridorCells.Contains(stair.TopPort.Value.WorldOutsideCell))
                {
                    errors.Add($"Stair {stair.PlacementId} has an unconnected landing route.");
                }
            }
        }

        private static void ValidateVisibleLengths(MapLayout layout, ICollection<string> errors)
        {
            var protectedTransitionCells = new HashSet<GridCoord3>(
                layout.Rooms
                    .SelectMany(room => room.Ports)
                    .Concat(layout.Stairs.SelectMany(StairPorts))
                    .Select(port => port.WorldOutsideCell));
            int eligibleStraightCells = layout.CorridorTiles.Count(tile =>
                IsStraight(tile.ConnectionMask) && !protectedTransitionCells.Contains(tile.Coord));
            if (eligibleStraightCells >= 6)
            {
                int distinctLengths = layout.CorridorModules
                    .Where(module => module.Shape == CorridorShape.Straight)
                    .Select(module => module.LengthUnits)
                    .Distinct()
                    .Count();
                if (distinctLengths < 2)
                {
                    errors.Add("Eligible straight corridors expose fewer than two module lengths.");
                }
            }
        }

        private static void ValidatePlacementIds(MapLayout layout, ICollection<string> errors)
        {
            int[] ids = layout.Rooms.Select(room => room.PlacementId)
                .Concat(layout.Stairs.Select(stair => stair.PlacementId))
                .Concat(layout.CorridorModules.Select(module => module.PlacementId))
                .ToArray();
            int duplicate = ids
                .GroupBy(id => id)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .DefaultIfEmpty(-1)
                .First();
            if (duplicate >= 0)
            {
                errors.Add($"PlacementId {duplicate} is duplicated.");
            }
        }

        private static int FloorCycleRank(MapTraversalGraph graph, int floor)
        {
            int[] nodes = graph.Nodes
                .Where(node => node.Coord.Floor == floor)
                .Select(node => node.Id)
                .ToArray();
            var nodeSet = new HashSet<int>(nodes);
            MapTraversalEdge[] edges = graph.Edges
                .Where(edge =>
                    nodeSet.Contains(edge.FromNodeId) && nodeSet.Contains(edge.ToNodeId))
                .ToArray();
            int components = 0;
            var visited = new HashSet<int>();
            foreach (int node in nodes)
            {
                if (!visited.Add(node))
                {
                    continue;
                }

                components++;
                var pending = new Queue<int>();
                pending.Enqueue(node);
                while (pending.Count > 0)
                {
                    int current = pending.Dequeue();
                    foreach (MapTraversalEdge edge in edges.Where(edge =>
                                 edge.FromNodeId == current || edge.ToNodeId == current))
                    {
                        int other = edge.Other(current);
                        if (visited.Add(other))
                        {
                            pending.Enqueue(other);
                        }
                    }
                }
            }

            return edges.Length - nodes.Length + components;
        }

        private static bool AreaContains(MapLayout layout, GridCoord3 coord) =>
            layout.Rooms.Any(room => room.Floor == coord.Floor && Contains(room.RotatedRect, coord)) ||
            layout.Stairs.Any(stair =>
                (stair.LowerFloor == coord.Floor || stair.UpperFloor == coord.Floor) &&
                Contains(stair.RotatedRect, coord));

        private static bool Contains(GridRect rect, GridCoord3 coord) =>
            coord.X >= rect.X && coord.X < rect.MaxX &&
            coord.Z >= rect.Z && coord.Z < rect.MaxZ;

        private static bool InsideBounds(GridRect rect, MapGenerationParameters parameters) =>
            rect.X >= 0 && rect.Z >= 0 &&
            rect.MaxX <= parameters.WidthUnits && rect.MaxZ <= parameters.DepthUnits;

        private static bool InsideBounds(GridCoord3 coord, MapGenerationParameters parameters) =>
            coord.Floor >= 0 && coord.Floor < parameters.FloorCount &&
            coord.X >= 0 && coord.Z >= 0 &&
            coord.X < parameters.WidthUnits && coord.Z < parameters.DepthUnits;

        private static GridEdgeKey EdgeForOutsideCell(
            GridCoord3 outside,
            CardinalDirection facing)
        {
            switch (facing)
            {
                case CardinalDirection.North:
                    return new GridEdgeKey(
                        outside.Floor,
                        GridEdgeAxis.AlongX,
                        outside.X,
                        outside.Z);
                case CardinalDirection.East:
                    return new GridEdgeKey(
                        outside.Floor,
                        GridEdgeAxis.AlongZ,
                        outside.X,
                        outside.Z);
                case CardinalDirection.South:
                    return new GridEdgeKey(
                        outside.Floor,
                        GridEdgeAxis.AlongX,
                        outside.X,
                        outside.Z + 1);
                case CardinalDirection.West:
                    return new GridEdgeKey(
                        outside.Floor,
                        GridEdgeAxis.AlongZ,
                        outside.X + 1,
                        outside.Z);
                default:
                    throw new ArgumentOutOfRangeException(nameof(facing), facing, null);
            }
        }

        private static IEnumerable<MapPort> StairPorts(PlacedStair stair)
        {
            if (stair.BottomPort.HasValue)
            {
                yield return stair.BottomPort.Value;
            }

            if (stair.TopPort.HasValue)
            {
                yield return stair.TopPort.Value;
            }
        }

        private static bool AreOpposite(CardinalDirection left, CardinalDirection right) =>
            ((int)left + 2) % 4 == (int)right;

        private static bool IsStraight(CorridorConnectionMask mask) =>
            mask == (CorridorConnectionMask.North | CorridorConnectionMask.South) ||
            mask == (CorridorConnectionMask.East | CorridorConnectionMask.West);

        private static double CentreManhattanUnits(GridRect left, GridRect right)
        {
            int doubledDistance =
                Math.Abs(left.CenterX2 - right.CenterX2) +
                Math.Abs(left.CenterZ2 - right.CenterZ2);
            return doubledDistance / 2.0;
        }

        private static void RequireCount(
            string role,
            int actual,
            int expected,
            ICollection<string> errors)
        {
            if (actual != expected)
            {
                errors.Add($"Role {role} count is {actual}; expected {expected}.");
            }
        }

        private readonly struct AreaPlacement
        {
            public string Name { get; }
            public GridRect Rect { get; }

            public AreaPlacement(string name, GridRect rect)
            {
                Name = name;
                Rect = rect;
            }
        }
    }
}
