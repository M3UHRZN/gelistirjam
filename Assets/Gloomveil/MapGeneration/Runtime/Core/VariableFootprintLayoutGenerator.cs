using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Gloomveil.MapGeneration
{
    public sealed class VariableFootprintLayoutGenerator
    {
        private readonly RoomAndStairPacker packer = new RoomAndStairPacker();
        private readonly OrthogonalCorridorRouter router = new OrthogonalCorridorRouter();
        private readonly CorridorModulePlanner modulePlanner = new CorridorModulePlanner();
        private readonly MapTraversalGraphBuilder graphBuilder = new MapTraversalGraphBuilder();

        public MapLayout Generate(MapGenerationParameters parameters, int seed)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            parameters.Validate();
            string lastStage = "packing";
            string lastFailure = "No layout attempt ran.";

            for (int attempt = 0; attempt < parameters.MaxLayoutAttempts; attempt++)
            {
                if (!packer.TryPack(
                        parameters,
                        new SeededRandom(seed, $"pack-{attempt}"),
                        out PackedAreaLayout packed,
                        out lastFailure))
                {
                    lastStage = "packing";
                    continue;
                }

                lastStage = "routing";
                if (!TryRoutePackedAreas(
                        parameters,
                        packed,
                        new SeededRandom(seed, $"route-{attempt}"),
                        out HashSet<GridCoord3> corridorCells,
                        out lastFailure))
                {
                    continue;
                }

                IReadOnlyList<CorridorTile> corridorTiles = CreateCorridorTiles(corridorCells);
                int firstCorridorPlacementId = packed.Rooms
                    .Select(room => room.PlacementId)
                    .Concat(packed.Stairs.Select(stair => stair.PlacementId))
                    .DefaultIfEmpty(0)
                    .Max() + 1;
                var roomDoorCells = new HashSet<GridCoord3>(
                    packed.Rooms.SelectMany(room => room.Ports).Select(port => port.WorldOutsideCell));
                var stairLandingCells = new HashSet<GridCoord3>(
                    packed.Stairs.SelectMany(StairPorts).Select(port => port.WorldOutsideCell));

                lastStage = "segmentation";
                if (!modulePlanner.TryPlan(
                        corridorTiles,
                        roomDoorCells,
                        stairLandingCells,
                        parameters.CorridorRunLengths,
                        new SeededRandom(seed, $"segment-{attempt}"),
                        firstCorridorPlacementId,
                        out IReadOnlyList<CorridorModulePlacement> corridorModules,
                        out lastFailure))
                {
                    continue;
                }

                if (corridorModules
                        .Where(module => module.Shape == CorridorShape.Straight)
                        .Select(module => module.LengthUnits)
                        .Distinct()
                        .Count() < 2)
                {
                    lastFailure = "The accepted layout did not expose two straight corridor lengths.";
                    continue;
                }

                lastStage = "graph";
                MapTraversalGraph traversalGraph = graphBuilder.Build(
                    packed.Rooms,
                    packed.Stairs,
                    corridorTiles,
                    parameters.VerticalTraversalCostUnits);
                if (!traversalGraph.IsFullyConnected)
                {
                    lastFailure = "The weighted traversal graph is disconnected.";
                    continue;
                }

                return new MapLayout(
                    parameters.WidthUnits,
                    parameters.DepthUnits,
                    parameters.FloorCount,
                    packed.Rooms,
                    packed.Stairs,
                    corridorTiles,
                    corridorModules,
                    traversalGraph);
            }

            throw new MapGenerationException(
                $"Map generation failed for seed {seed} after {parameters.MaxLayoutAttempts} attempts. " +
                $"Stage={lastStage}. LastFailure={lastFailure}");
        }

        private bool TryRoutePackedAreas(
            MapGenerationParameters parameters,
            PackedAreaLayout packed,
            SeededRandom routeRandom,
            out HashSet<GridCoord3> corridorCells,
            out string failure)
        {
            corridorCells = new HashSet<GridCoord3>();
            var roomPorts = new Dictionary<int, List<MapPort>>();
            var stairBottomPorts = new Dictionary<int, MapPort>();
            var stairTopPorts = new Dictionary<int, MapPort>();

            for (int floor = 0; floor < parameters.FloorCount; floor++)
            {
                IReadOnlyList<GridRect> blocked = packed.OccupiedRectsForFloor(floor);
                List<ConnectionTarget> targets = BuildTargets(
                    parameters,
                    packed,
                    floor,
                    blocked);
                if (targets.Any(target => target.CandidatePorts.Count == 0))
                {
                    failure = $"Floor {floor} has a room or stair landing without an exterior port.";
                    return false;
                }

                List<ConnectionCandidate> candidates = BuildCandidates(targets);
                if (!TryBuildMinimumSpanningTree(
                        targets.Count,
                        candidates,
                        out List<ConnectionCandidate> treeEdges))
                {
                    failure = $"Floor {floor} connection targets cannot form a spanning tree.";
                    return false;
                }

                var selectedByTarget = targets.ToDictionary(
                    target => target.Id,
                    _ => new List<MapPort>());
                var acceptedConnections = new List<AcceptedConnection>();
                foreach (ConnectionCandidate treeEdge in treeEdges)
                {
                    if (!TryAcceptConnection(
                            parameters,
                            floor,
                            blocked,
                            targets,
                            treeEdge,
                            selectedByTarget,
                            corridorCells,
                            acceptedConnections,
                            null))
                    {
                        failure = $"Required floor {floor} tree edge " +
                                  $"{treeEdge.AIndex}->{treeEdge.BIndex} could not be routed.";
                        return false;
                    }
                }

                int cycleRank = ComputeCycleRank(floor, targets, corridorCells, acceptedConnections);
                var treeKeys = new HashSet<TargetPair>(
                    treeEdges.Select(edge => new TargetPair(edge.AIndex, edge.BIndex)));
                var nonTree = candidates
                    .Where(candidate => !treeKeys.Contains(
                        new TargetPair(candidate.AIndex, candidate.BIndex)))
                    .ToList();
                routeRandom.Shuffle(nonTree);

                int acceptedExtraLinks = 0;
                foreach (ConnectionCandidate candidate in nonTree)
                {
                    if (TryAcceptConnection(
                            parameters,
                            floor,
                            blocked,
                            targets,
                            candidate,
                            selectedByTarget,
                            corridorCells,
                            acceptedConnections,
                            cycleRank + 1))
                    {
                        cycleRank++;
                        acceptedExtraLinks++;
                        if (acceptedExtraLinks == parameters.ExtraRoomLinksPerFloor)
                        {
                            break;
                        }
                    }
                }

                if (acceptedExtraLinks != parameters.ExtraRoomLinksPerFloor)
                {
                    failure = $"Floor {floor} produced {acceptedExtraLinks}/" +
                              $"{parameters.ExtraRoomLinksPerFloor} independent extra links.";
                    return false;
                }

                RecordSelectedPorts(
                    acceptedConnections,
                    roomPorts,
                    stairBottomPorts,
                    stairTopPorts);
            }

            foreach (PlacedRoom room in packed.Rooms)
            {
                if (!roomPorts.TryGetValue(room.PlacementId, out List<MapPort> ports) ||
                    ports.Count == 0)
                {
                    failure = $"Room {room.PlacementId} was not connected.";
                    return false;
                }

                room.SetPorts(ports
                    .Distinct()
                    .OrderBy(port => port.WorldEdge)
                    .ThenBy(port => port.LocalSocket.ToString(), StringComparer.Ordinal));
            }

            foreach (PlacedStair stair in packed.Stairs)
            {
                if (!stairBottomPorts.TryGetValue(stair.PlacementId, out MapPort bottomPort) ||
                    !stairTopPorts.TryGetValue(stair.PlacementId, out MapPort topPort))
                {
                    failure = $"Stair {stair.PlacementId} is missing a landing connection.";
                    return false;
                }

                stair.SetPorts(bottomPort, topPort);
            }

            failure = string.Empty;
            return true;
        }

        private bool TryAcceptConnection(
            MapGenerationParameters parameters,
            int floor,
            IReadOnlyCollection<GridRect> blocked,
            IReadOnlyList<ConnectionTarget> targets,
            ConnectionCandidate candidate,
            IDictionary<int, List<MapPort>> selectedByTarget,
            HashSet<GridCoord3> corridorCells,
            List<AcceptedConnection> acceptedConnections,
            int? requiredCycleRank)
        {
            ConnectionTarget left = targets[candidate.AIndex];
            ConnectionTarget right = targets[candidate.BIndex];
            IReadOnlyList<PortPair> portPairs = BuildPortPairs(
                left,
                right,
                selectedByTarget[left.Id],
                selectedByTarget[right.Id]);

            foreach (PortPair pair in portPairs)
            {
                if (!router.TryRoute(
                        parameters.WidthUnits,
                        parameters.DepthUnits,
                        floor,
                        blocked,
                        corridorCells,
                        pair.Left.WorldOutsideCell,
                        pair.Right.WorldOutsideCell,
                        out IReadOnlyList<GridCoord3> path))
                {
                    continue;
                }

                var accepted = new AcceptedConnection(left, right, pair.Left, pair.Right);
                if (requiredCycleRank.HasValue)
                {
                    var trialCells = new HashSet<GridCoord3>(corridorCells);
                    trialCells.UnionWith(path);
                    var trialConnections = new List<AcceptedConnection>(acceptedConnections)
                    {
                        accepted
                    };
                    int trialRank = ComputeCycleRank(floor, targets, trialCells, trialConnections);
                    if (trialRank != requiredCycleRank.Value)
                    {
                        continue;
                    }
                }

                corridorCells.UnionWith(path);
                acceptedConnections.Add(accepted);
                AddSelectedPort(selectedByTarget[left.Id], pair.Left, left.IsStairLanding);
                AddSelectedPort(selectedByTarget[right.Id], pair.Right, right.IsStairLanding);
                return true;
            }

            return false;
        }

        private static void AddSelectedPort(List<MapPort> ports, MapPort port, bool singlePortOnly)
        {
            if (ports.Contains(port))
            {
                return;
            }

            if (!singlePortOnly || ports.Count == 0)
            {
                ports.Add(port);
            }
        }

        private static List<ConnectionTarget> BuildTargets(
            MapGenerationParameters parameters,
            PackedAreaLayout packed,
            int floor,
            IReadOnlyCollection<GridRect> blocked)
        {
            var targets = new List<ConnectionTarget>();
            foreach (PlacedRoom room in packed.Rooms
                         .Where(room => room.Floor == floor)
                         .OrderBy(room => room.PlacementId))
            {
                targets.Add(ConnectionTarget.ForRoom(
                    targets.Count,
                    room,
                    CreateCandidatePorts(
                        parameters,
                        room.PlacementId,
                        room.CanonicalSize,
                        room.QuarterTurns,
                        room.RotatedRect,
                        floor,
                        AllRoomLocalSockets(room.CanonicalSize),
                        blocked)));
            }

            foreach (PlacedStair stair in packed.Stairs
                         .Where(stair => stair.LowerFloor == floor || stair.UpperFloor == floor)
                         .OrderBy(stair => stair.PlacementId))
            {
                bool topLanding = stair.UpperFloor == floor;
                targets.Add(ConnectionTarget.ForStair(
                    targets.Count,
                    stair,
                    topLanding,
                    CreateCandidatePorts(
                        parameters,
                        stair.PlacementId,
                        stair.CanonicalSize,
                        stair.QuarterTurns,
                        stair.RotatedRect,
                        floor,
                        StairLocalSockets(stair, topLanding),
                        blocked)));
            }

            return targets;
        }

        private static IReadOnlyList<MapPort> CreateCandidatePorts(
            MapGenerationParameters parameters,
            int ownerPlacementId,
            GridSize2 canonicalSize,
            int quarterTurns,
            GridRect rotatedRect,
            int floor,
            IEnumerable<SocketKey> localSockets,
            IReadOnlyCollection<GridRect> blocked)
        {
            var result = new List<MapPort>();
            foreach (SocketKey localSocket in localSockets)
            {
                SocketKey rotatedSocket = localSocket.Rotate(canonicalSize, quarterTurns);
                MapPort port = CreateWorldPort(
                    ownerPlacementId,
                    localSocket,
                    rotatedSocket,
                    rotatedRect,
                    floor);
                GridCoord3 outside = port.WorldOutsideCell;
                if (outside.X < 0 || outside.Z < 0 ||
                    outside.X >= parameters.WidthUnits || outside.Z >= parameters.DepthUnits ||
                    blocked.Any(rect => Contains(rect, outside.X, outside.Z)))
                {
                    continue;
                }

                result.Add(port);
            }

            result.Sort(ComparePorts);
            return new ReadOnlyCollection<MapPort>(result);
        }

        private static MapPort CreateWorldPort(
            int ownerPlacementId,
            SocketKey localSocket,
            SocketKey rotatedSocket,
            GridRect rect,
            int floor)
        {
            GridCoord3 outside;
            GridEdgeKey edge;
            switch (rotatedSocket.Side)
            {
                case CardinalDirection.North:
                    outside = new GridCoord3(rect.X + rotatedSocket.Index, rect.MaxZ, floor);
                    edge = new GridEdgeKey(
                        floor,
                        GridEdgeAxis.AlongX,
                        rect.X + rotatedSocket.Index,
                        rect.MaxZ);
                    break;
                case CardinalDirection.East:
                    outside = new GridCoord3(rect.MaxX, rect.Z + rotatedSocket.Index, floor);
                    edge = new GridEdgeKey(
                        floor,
                        GridEdgeAxis.AlongZ,
                        rect.MaxX,
                        rect.Z + rotatedSocket.Index);
                    break;
                case CardinalDirection.South:
                    outside = new GridCoord3(rect.X + rotatedSocket.Index, rect.Z - 1, floor);
                    edge = new GridEdgeKey(
                        floor,
                        GridEdgeAxis.AlongX,
                        rect.X + rotatedSocket.Index,
                        rect.Z);
                    break;
                case CardinalDirection.West:
                    outside = new GridCoord3(rect.X - 1, rect.Z + rotatedSocket.Index, floor);
                    edge = new GridEdgeKey(
                        floor,
                        GridEdgeAxis.AlongZ,
                        rect.X,
                        rect.Z + rotatedSocket.Index);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return new MapPort(
                ownerPlacementId,
                localSocket,
                rotatedSocket,
                edge,
                outside,
                rotatedSocket.Side);
        }

        private static IEnumerable<SocketKey> AllRoomLocalSockets(GridSize2 size)
        {
            for (int index = 0; index < size.Width; index++)
            {
                yield return new SocketKey(CardinalDirection.North, index, 0);
                yield return new SocketKey(CardinalDirection.South, index, 0);
            }

            for (int index = 0; index < size.Depth; index++)
            {
                yield return new SocketKey(CardinalDirection.East, index, 0);
                yield return new SocketKey(CardinalDirection.West, index, 0);
            }
        }

        private static IEnumerable<SocketKey> StairLocalSockets(
            PlacedStair stair,
            bool topLanding)
        {
            int floorOffset = topLanding ? 1 : 0;
            if (stair.FootprintId == "STA_2x2")
            {
                yield return new SocketKey(
                    CardinalDirection.South,
                    topLanding ? 1 : 0,
                    floorOffset);
                yield break;
            }

            yield return new SocketKey(
                topLanding ? CardinalDirection.North : CardinalDirection.South,
                0,
                floorOffset);
        }

        private static List<ConnectionCandidate> BuildCandidates(
            IReadOnlyList<ConnectionTarget> targets)
        {
            var result = new List<ConnectionCandidate>();
            for (int left = 0; left < targets.Count; left++)
            {
                for (int right = left + 1; right < targets.Count; right++)
                {
                    int distance = int.MaxValue;
                    foreach (MapPort leftPort in targets[left].CandidatePorts)
                    {
                        foreach (MapPort rightPort in targets[right].CandidatePorts)
                        {
                            distance = Math.Min(
                                distance,
                                Manhattan(leftPort.WorldOutsideCell, rightPort.WorldOutsideCell));
                        }
                    }

                    if (distance != int.MaxValue)
                    {
                        result.Add(new ConnectionCandidate(left, right, distance));
                    }
                }
            }

            result.Sort(CompareCandidates);
            return result;
        }

        private static bool TryBuildMinimumSpanningTree(
            int targetCount,
            IReadOnlyList<ConnectionCandidate> candidates,
            out List<ConnectionCandidate> treeEdges)
        {
            treeEdges = new List<ConnectionCandidate>();
            var unionFind = new UnionFind(targetCount);
            foreach (ConnectionCandidate candidate in candidates)
            {
                if (unionFind.Union(candidate.AIndex, candidate.BIndex))
                {
                    treeEdges.Add(candidate);
                    if (treeEdges.Count == targetCount - 1)
                    {
                        return true;
                    }
                }
            }

            return targetCount <= 1;
        }

        private static IReadOnlyList<PortPair> BuildPortPairs(
            ConnectionTarget left,
            ConnectionTarget right,
            IReadOnlyCollection<MapPort> selectedLeft,
            IReadOnlyCollection<MapPort> selectedRight)
        {
            IEnumerable<MapPort> leftPorts = left.IsStairLanding && selectedLeft.Count > 0
                ? selectedLeft
                : left.CandidatePorts;
            IEnumerable<MapPort> rightPorts = right.IsStairLanding && selectedRight.Count > 0
                ? selectedRight
                : right.CandidatePorts;
            var pairs = new List<PortPair>();
            foreach (MapPort leftPort in leftPorts)
            {
                foreach (MapPort rightPort in rightPorts)
                {
                    int usedCount =
                        (selectedLeft.Contains(leftPort) ? 1 : 0) +
                        (selectedRight.Contains(rightPort) ? 1 : 0);
                    pairs.Add(new PortPair(
                        leftPort,
                        rightPort,
                        usedCount,
                        Manhattan(leftPort.WorldOutsideCell, rightPort.WorldOutsideCell)));
                }
            }

            pairs.Sort(ComparePortPairs);
            return pairs;
        }

        private static int ComputeCycleRank(
            int floor,
            IReadOnlyList<ConnectionTarget> targets,
            IReadOnlyCollection<GridCoord3> corridorCells,
            IReadOnlyList<AcceptedConnection> connections)
        {
            GridCoord3[] floorCells = corridorCells
                .Where(cell => cell.Floor == floor)
                .OrderBy(cell => cell)
                .ToArray();
            var cellNodeIds = new Dictionary<GridCoord3, int>();
            for (int index = 0; index < floorCells.Length; index++)
            {
                cellNodeIds.Add(floorCells[index], targets.Count + index);
            }

            int vertexCount = targets.Count + floorCells.Length;
            var unionFind = new UnionFind(vertexCount);
            var edges = new HashSet<TopologyEdge>();
            foreach (GridCoord3 cell in floorCells)
            {
                int from = cellNodeIds[cell];
                AddTopologyNeighbour(
                    floor,
                    cell,
                    1,
                    0,
                    from,
                    cellNodeIds,
                    edges,
                    unionFind);
                AddTopologyNeighbour(
                    floor,
                    cell,
                    0,
                    1,
                    from,
                    cellNodeIds,
                    edges,
                    unionFind);
            }

            foreach (AcceptedConnection connection in connections)
            {
                AddTargetTopologyEdge(
                    connection.Left.Id,
                    connection.LeftPort.WorldOutsideCell,
                    cellNodeIds,
                    edges,
                    unionFind);
                AddTargetTopologyEdge(
                    connection.Right.Id,
                    connection.RightPort.WorldOutsideCell,
                    cellNodeIds,
                    edges,
                    unionFind);
            }

            int components = 0;
            for (int vertex = 0; vertex < vertexCount; vertex++)
            {
                if (unionFind.Find(vertex) == vertex)
                {
                    components++;
                }
            }

            return edges.Count - vertexCount + components;
        }

        private static void AddTopologyNeighbour(
            int floor,
            GridCoord3 cell,
            int deltaX,
            int deltaZ,
            int from,
            IReadOnlyDictionary<GridCoord3, int> cellNodeIds,
            ISet<TopologyEdge> edges,
            UnionFind unionFind)
        {
            var neighbour = new GridCoord3(cell.X + deltaX, cell.Z + deltaZ, floor);
            if (cellNodeIds.TryGetValue(neighbour, out int to))
            {
                var edge = new TopologyEdge(from, to);
                if (edges.Add(edge))
                {
                    unionFind.Union(from, to);
                }
            }
        }

        private static void AddTargetTopologyEdge(
            int targetNodeId,
            GridCoord3 cell,
            IReadOnlyDictionary<GridCoord3, int> cellNodeIds,
            ISet<TopologyEdge> edges,
            UnionFind unionFind)
        {
            if (!cellNodeIds.TryGetValue(cell, out int cellNodeId))
            {
                return;
            }

            var edge = new TopologyEdge(targetNodeId, cellNodeId);
            if (edges.Add(edge))
            {
                unionFind.Union(targetNodeId, cellNodeId);
            }
        }

        private static void RecordSelectedPorts(
            IEnumerable<AcceptedConnection> connections,
            IDictionary<int, List<MapPort>> roomPorts,
            IDictionary<int, MapPort> stairBottomPorts,
            IDictionary<int, MapPort> stairTopPorts)
        {
            foreach (AcceptedConnection connection in connections)
            {
                RecordPort(
                    connection.Left,
                    connection.LeftPort,
                    roomPorts,
                    stairBottomPorts,
                    stairTopPorts);
                RecordPort(
                    connection.Right,
                    connection.RightPort,
                    roomPorts,
                    stairBottomPorts,
                    stairTopPorts);
            }
        }

        private static void RecordPort(
            ConnectionTarget target,
            MapPort port,
            IDictionary<int, List<MapPort>> roomPorts,
            IDictionary<int, MapPort> stairBottomPorts,
            IDictionary<int, MapPort> stairTopPorts)
        {
            if (target.Room != null)
            {
                if (!roomPorts.TryGetValue(target.Room.PlacementId, out List<MapPort> ports))
                {
                    ports = new List<MapPort>();
                    roomPorts.Add(target.Room.PlacementId, ports);
                }

                if (!ports.Contains(port))
                {
                    ports.Add(port);
                }

                return;
            }

            IDictionary<int, MapPort> destination = target.IsTopLanding
                ? stairTopPorts
                : stairBottomPorts;
            if (!destination.ContainsKey(target.Stair.PlacementId))
            {
                destination.Add(target.Stair.PlacementId, port);
            }
        }

        private static IReadOnlyList<CorridorTile> CreateCorridorTiles(
            IReadOnlyCollection<GridCoord3> corridorCells)
        {
            var cells = new HashSet<GridCoord3>(corridorCells);
            var result = new List<CorridorTile>();
            foreach (GridCoord3 cell in cells.OrderBy(cell => cell))
            {
                CorridorConnectionMask mask = CorridorConnectionMask.None;
                if (cells.Contains(new GridCoord3(cell.X, cell.Z + 1, cell.Floor)))
                {
                    mask |= CorridorConnectionMask.North;
                }

                if (cells.Contains(new GridCoord3(cell.X + 1, cell.Z, cell.Floor)))
                {
                    mask |= CorridorConnectionMask.East;
                }

                if (cells.Contains(new GridCoord3(cell.X, cell.Z - 1, cell.Floor)))
                {
                    mask |= CorridorConnectionMask.South;
                }

                if (cells.Contains(new GridCoord3(cell.X - 1, cell.Z, cell.Floor)))
                {
                    mask |= CorridorConnectionMask.West;
                }

                result.Add(new CorridorTile(cell, mask));
            }

            return new ReadOnlyCollection<CorridorTile>(result);
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

        private static bool Contains(GridRect rect, int x, int z) =>
            x >= rect.X && x < rect.MaxX && z >= rect.Z && z < rect.MaxZ;

        private static int Manhattan(GridCoord3 left, GridCoord3 right) =>
            Math.Abs(left.X - right.X) + Math.Abs(left.Z - right.Z);

        private static int ComparePorts(MapPort left, MapPort right)
        {
            int comparison = left.WorldOutsideCell.CompareTo(right.WorldOutsideCell);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.WorldFacing.CompareTo(right.WorldFacing);
            return comparison != 0
                ? comparison
                : string.CompareOrdinal(left.LocalSocket.ToString(), right.LocalSocket.ToString());
        }

        private static int CompareCandidates(ConnectionCandidate left, ConnectionCandidate right)
        {
            int comparison = left.EstimatedDistance.CompareTo(right.EstimatedDistance);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.AIndex.CompareTo(right.AIndex);
            return comparison != 0 ? comparison : left.BIndex.CompareTo(right.BIndex);
        }

        private static int ComparePortPairs(PortPair left, PortPair right)
        {
            int comparison = left.UsedPortCount.CompareTo(right.UsedPortCount);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.Distance.CompareTo(right.Distance);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = ComparePorts(left.Left, right.Left);
            return comparison != 0 ? comparison : ComparePorts(left.Right, right.Right);
        }

        private sealed class ConnectionTarget
        {
            public int Id { get; }
            public PlacedRoom Room { get; }
            public PlacedStair Stair { get; }
            public bool IsTopLanding { get; }
            public bool IsStairLanding => Stair != null;
            public IReadOnlyList<MapPort> CandidatePorts { get; }

            private ConnectionTarget(
                int id,
                PlacedRoom room,
                PlacedStair stair,
                bool isTopLanding,
                IReadOnlyList<MapPort> candidatePorts)
            {
                Id = id;
                Room = room;
                Stair = stair;
                IsTopLanding = isTopLanding;
                CandidatePorts = candidatePorts;
            }

            public static ConnectionTarget ForRoom(
                int id,
                PlacedRoom room,
                IReadOnlyList<MapPort> candidatePorts) =>
                new ConnectionTarget(id, room, null, false, candidatePorts);

            public static ConnectionTarget ForStair(
                int id,
                PlacedStair stair,
                bool isTopLanding,
                IReadOnlyList<MapPort> candidatePorts) =>
                new ConnectionTarget(id, null, stair, isTopLanding, candidatePorts);
        }

        private readonly struct ConnectionCandidate
        {
            public int AIndex { get; }
            public int BIndex { get; }
            public int EstimatedDistance { get; }

            public ConnectionCandidate(int aIndex, int bIndex, int estimatedDistance)
            {
                AIndex = aIndex;
                BIndex = bIndex;
                EstimatedDistance = estimatedDistance;
            }
        }

        private readonly struct PortPair
        {
            public MapPort Left { get; }
            public MapPort Right { get; }
            public int UsedPortCount { get; }
            public int Distance { get; }

            public PortPair(MapPort left, MapPort right, int usedPortCount, int distance)
            {
                Left = left;
                Right = right;
                UsedPortCount = usedPortCount;
                Distance = distance;
            }
        }

        private readonly struct AcceptedConnection
        {
            public ConnectionTarget Left { get; }
            public ConnectionTarget Right { get; }
            public MapPort LeftPort { get; }
            public MapPort RightPort { get; }

            public AcceptedConnection(
                ConnectionTarget left,
                ConnectionTarget right,
                MapPort leftPort,
                MapPort rightPort)
            {
                Left = left;
                Right = right;
                LeftPort = leftPort;
                RightPort = rightPort;
            }
        }

        private readonly struct TargetPair : IEquatable<TargetPair>
        {
            public int Low { get; }
            public int High { get; }

            public TargetPair(int left, int right)
            {
                Low = Math.Min(left, right);
                High = Math.Max(left, right);
            }

            public bool Equals(TargetPair other) => Low == other.Low && High == other.High;

            public override bool Equals(object obj) => obj is TargetPair other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Low, High);
        }

        private readonly struct TopologyEdge : IEquatable<TopologyEdge>
        {
            public int Low { get; }
            public int High { get; }

            public TopologyEdge(int left, int right)
            {
                Low = Math.Min(left, right);
                High = Math.Max(left, right);
            }

            public bool Equals(TopologyEdge other) => Low == other.Low && High == other.High;

            public override bool Equals(object obj) => obj is TopologyEdge other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Low, High);
        }

        private sealed class UnionFind
        {
            private readonly int[] parent;
            private readonly byte[] rank;

            public UnionFind(int count)
            {
                parent = new int[count];
                rank = new byte[count];
                for (int index = 0; index < count; index++)
                {
                    parent[index] = index;
                }
            }

            public int Find(int value)
            {
                int root = value;
                while (parent[root] != root)
                {
                    root = parent[root];
                }

                while (parent[value] != value)
                {
                    int next = parent[value];
                    parent[value] = root;
                    value = next;
                }

                return root;
            }

            public bool Union(int left, int right)
            {
                int leftRoot = Find(left);
                int rightRoot = Find(right);
                if (leftRoot == rightRoot)
                {
                    return false;
                }

                if (rank[leftRoot] < rank[rightRoot])
                {
                    parent[leftRoot] = rightRoot;
                }
                else if (rank[leftRoot] > rank[rightRoot])
                {
                    parent[rightRoot] = leftRoot;
                }
                else
                {
                    parent[rightRoot] = leftRoot;
                    rank[leftRoot]++;
                }

                return true;
            }
        }
    }
}
