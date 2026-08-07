using System;
using System.Collections.Generic;
using System.Linq;

namespace Gloomveil.MapGeneration
{
    public sealed class MapTraversalGraphBuilder
    {
        public MapTraversalGraph Build(
            IReadOnlyList<PlacedRoom> rooms,
            IReadOnlyList<PlacedStair> stairs,
            IReadOnlyList<CorridorTile> corridorTiles,
            int verticalTraversalCostUnits)
        {
            if (rooms == null)
            {
                throw new ArgumentNullException(nameof(rooms));
            }

            if (stairs == null)
            {
                throw new ArgumentNullException(nameof(stairs));
            }

            if (corridorTiles == null)
            {
                throw new ArgumentNullException(nameof(corridorTiles));
            }

            if (verticalTraversalCostUnits < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(verticalTraversalCostUnits));
            }

            var nodes = new List<MapTraversalNode>();
            var edges = new Dictionary<NodePair, int>();
            int nextNodeId = 1;

            foreach (PlacedRoom room in rooms.OrderBy(room => room.PlacementId))
            {
                int nodeId = nextNodeId++;
                room.SetGraphNodeId(nodeId);
                nodes.Add(new MapTraversalNode(
                    nodeId,
                    MapTraversalNodeKind.Room,
                    CentreCell(room.RotatedRect, room.Floor),
                    room.PlacementId));
            }

            foreach (PlacedStair stair in stairs.OrderBy(stair => stair.PlacementId))
            {
                int bottomId = nextNodeId++;
                int topId = nextNodeId++;
                stair.SetGraphNodeIds(bottomId, topId);
                nodes.Add(new MapTraversalNode(
                    bottomId,
                    MapTraversalNodeKind.StairLanding,
                    CentreCell(stair.RotatedRect, stair.LowerFloor),
                    stair.PlacementId));
                nodes.Add(new MapTraversalNode(
                    topId,
                    MapTraversalNodeKind.StairLanding,
                    CentreCell(stair.RotatedRect, stair.UpperFloor),
                    stair.PlacementId));
                AddEdge(edges, bottomId, topId, verticalTraversalCostUnits);
            }

            var corridorNodeIds = new Dictionary<GridCoord3, int>();
            foreach (CorridorTile tile in corridorTiles.OrderBy(tile => tile.Coord))
            {
                int nodeId = nextNodeId++;
                corridorNodeIds.Add(tile.Coord, nodeId);
                nodes.Add(new MapTraversalNode(
                    nodeId,
                    MapTraversalNodeKind.Corridor,
                    tile.Coord,
                    0));
            }

            foreach (CorridorTile tile in corridorTiles)
            {
                int from = corridorNodeIds[tile.Coord];
                AddCorridorNeighbour(
                    edges,
                    corridorNodeIds,
                    from,
                    tile.Coord,
                    CorridorConnectionMask.North,
                    0,
                    1);
                AddCorridorNeighbour(
                    edges,
                    corridorNodeIds,
                    from,
                    tile.Coord,
                    CorridorConnectionMask.East,
                    1,
                    0);
            }

            foreach (PlacedRoom room in rooms)
            {
                foreach (MapPort port in room.Ports)
                {
                    if (corridorNodeIds.TryGetValue(port.WorldOutsideCell, out int corridorNodeId))
                    {
                        AddEdge(
                            edges,
                            room.GraphNodeId,
                            corridorNodeId,
                            CentreToPortCost(room.RotatedRect, port.WorldOutsideCell));
                    }
                }
            }

            foreach (PlacedStair stair in stairs)
            {
                ConnectStairPort(
                    edges,
                    corridorNodeIds,
                    stair.BottomGraphNodeId,
                    stair.RotatedRect,
                    stair.BottomPort);
                ConnectStairPort(
                    edges,
                    corridorNodeIds,
                    stair.TopGraphNodeId,
                    stair.RotatedRect,
                    stair.TopPort);
            }

            var graphEdges = edges
                .Select(pair => new MapTraversalEdge(pair.Key.Low, pair.Key.High, pair.Value))
                .ToArray();
            return new MapTraversalGraph(nodes, graphEdges);
        }

        private static void ConnectStairPort(
            IDictionary<NodePair, int> edges,
            IReadOnlyDictionary<GridCoord3, int> corridorNodeIds,
            int stairNodeId,
            GridRect rect,
            MapPort? port)
        {
            if (port.HasValue &&
                corridorNodeIds.TryGetValue(port.Value.WorldOutsideCell, out int corridorNodeId))
            {
                AddEdge(edges, stairNodeId, corridorNodeId, CentreToPortCost(rect, port.Value.WorldOutsideCell));
            }
        }

        private static void AddCorridorNeighbour(
            IDictionary<NodePair, int> edges,
            IReadOnlyDictionary<GridCoord3, int> corridorNodeIds,
            int from,
            GridCoord3 coord,
            CorridorConnectionMask requiredMask,
            int deltaX,
            int deltaZ)
        {
            var neighbour = new GridCoord3(coord.X + deltaX, coord.Z + deltaZ, coord.Floor);
            if (corridorNodeIds.TryGetValue(neighbour, out int to))
            {
                AddEdge(edges, from, to, 1);
            }
        }

        private static int CentreToPortCost(GridRect rect, GridCoord3 outsideCell)
        {
            int cellCenterX2 = (2 * outsideCell.X) + 1;
            int cellCenterZ2 = (2 * outsideCell.Z) + 1;
            int doubledDistance =
                Math.Abs(rect.CenterX2 - cellCenterX2) +
                Math.Abs(rect.CenterZ2 - cellCenterZ2);
            return Math.Max(1, (doubledDistance + 1) / 2);
        }

        private static GridCoord3 CentreCell(GridRect rect, int floor) =>
            new GridCoord3(rect.X + ((rect.Width - 1) / 2), rect.Z + ((rect.Depth - 1) / 2), floor);

        private static void AddEdge(
            IDictionary<NodePair, int> edges,
            int left,
            int right,
            int cost)
        {
            var key = new NodePair(left, right);
            if (!edges.TryGetValue(key, out int existingCost) || cost < existingCost)
            {
                edges[key] = cost;
            }
        }

        private readonly struct NodePair : IEquatable<NodePair>
        {
            public int Low { get; }
            public int High { get; }

            public NodePair(int left, int right)
            {
                Low = Math.Min(left, right);
                High = Math.Max(left, right);
            }

            public bool Equals(NodePair other) => Low == other.Low && High == other.High;

            public override bool Equals(object obj) => obj is NodePair other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Low, High);
        }
    }
}
