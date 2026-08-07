using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Gloomveil.MapGeneration
{
    public enum MapTraversalNodeKind : byte
    {
        Room = 0,
        Corridor = 1,
        StairLanding = 2
    }

    public readonly struct MapTraversalNode
    {
        public int Id { get; }
        public MapTraversalNodeKind Kind { get; }
        public GridCoord3 Coord { get; }
        public int OwnerPlacementId { get; }

        public MapTraversalNode(
            int id,
            MapTraversalNodeKind kind,
            GridCoord3 coord,
            int ownerPlacementId)
        {
            Id = id;
            Kind = kind;
            Coord = coord;
            OwnerPlacementId = ownerPlacementId;
        }
    }

    public readonly struct MapTraversalEdge
    {
        public int FromNodeId { get; }
        public int ToNodeId { get; }
        public int Cost { get; }

        public MapTraversalEdge(int fromNodeId, int toNodeId, int cost)
        {
            if (cost < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cost), cost, "Edge cost cannot be negative.");
            }

            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            Cost = cost;
        }

        public int Other(int nodeId)
        {
            if (nodeId == FromNodeId)
            {
                return ToNodeId;
            }

            if (nodeId == ToNodeId)
            {
                return FromNodeId;
            }

            throw new ArgumentException($"Node {nodeId} is not part of this edge.", nameof(nodeId));
        }
    }

    public sealed class MapTraversalGraph
    {
        private readonly Dictionary<int, IReadOnlyList<MapTraversalEdge>> adjacency;

        public IReadOnlyList<MapTraversalNode> Nodes { get; }
        public IReadOnlyList<MapTraversalEdge> Edges { get; }
        public bool IsFullyConnected { get; }

        public MapTraversalGraph(
            IEnumerable<MapTraversalNode> nodes,
            IEnumerable<MapTraversalEdge> edges)
        {
            var nodeList = new List<MapTraversalNode>(
                nodes ?? throw new ArgumentNullException(nameof(nodes)));
            var edgeList = new List<MapTraversalEdge>(
                edges ?? throw new ArgumentNullException(nameof(edges)));

            if (nodeList.GroupBy(node => node.Id).Any(group => group.Count() > 1))
            {
                throw new ArgumentException("Traversal node IDs must be unique.", nameof(nodes));
            }

            var knownNodeIds = new HashSet<int>(nodeList.Select(node => node.Id));
            if (edgeList.Any(edge =>
                    !knownNodeIds.Contains(edge.FromNodeId) ||
                    !knownNodeIds.Contains(edge.ToNodeId)))
            {
                throw new ArgumentException("Every traversal edge must reference known nodes.", nameof(edges));
            }

            nodeList.Sort((left, right) => left.Id.CompareTo(right.Id));
            edgeList.Sort(CompareEdges);
            Nodes = new ReadOnlyCollection<MapTraversalNode>(nodeList);
            Edges = new ReadOnlyCollection<MapTraversalEdge>(edgeList);

            var mutableAdjacency = nodeList.ToDictionary(
                node => node.Id,
                _ => new List<MapTraversalEdge>());
            foreach (MapTraversalEdge edge in edgeList)
            {
                mutableAdjacency[edge.FromNodeId].Add(edge);
                mutableAdjacency[edge.ToNodeId].Add(edge);
            }

            adjacency = mutableAdjacency.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<MapTraversalEdge>)new ReadOnlyCollection<MapTraversalEdge>(pair.Value));
            IsFullyConnected = ComputeFullyConnected(nodeList, mutableAdjacency);
        }

        public IReadOnlyList<MapTraversalEdge> EdgesFrom(int nodeId)
        {
            if (!adjacency.TryGetValue(nodeId, out IReadOnlyList<MapTraversalEdge> result))
            {
                throw new ArgumentOutOfRangeException(nameof(nodeId), nodeId, "Unknown traversal node.");
            }

            return result;
        }

        private static int CompareEdges(MapTraversalEdge left, MapTraversalEdge right)
        {
            int leftMin = Math.Min(left.FromNodeId, left.ToNodeId);
            int rightMin = Math.Min(right.FromNodeId, right.ToNodeId);
            int comparison = leftMin.CompareTo(rightMin);
            if (comparison != 0)
            {
                return comparison;
            }

            int leftMax = Math.Max(left.FromNodeId, left.ToNodeId);
            int rightMax = Math.Max(right.FromNodeId, right.ToNodeId);
            comparison = leftMax.CompareTo(rightMax);
            return comparison != 0 ? comparison : left.Cost.CompareTo(right.Cost);
        }

        private static bool ComputeFullyConnected(
            IReadOnlyList<MapTraversalNode> nodes,
            IReadOnlyDictionary<int, List<MapTraversalEdge>> adjacency)
        {
            if (nodes.Count == 0)
            {
                return true;
            }

            var visited = new HashSet<int>();
            var pending = new Queue<int>();
            pending.Enqueue(nodes[0].Id);
            visited.Add(nodes[0].Id);
            while (pending.Count > 0)
            {
                int current = pending.Dequeue();
                foreach (MapTraversalEdge edge in adjacency[current])
                {
                    int other = edge.Other(current);
                    if (visited.Add(other))
                    {
                        pending.Enqueue(other);
                    }
                }
            }

            return visited.Count == nodes.Count;
        }
    }
}
