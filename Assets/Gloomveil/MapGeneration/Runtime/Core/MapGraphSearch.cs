using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Gloomveil.MapGeneration
{
    public static class MapGraphSearch
    {
        public static IReadOnlyList<MapTraversalNode> WeightedShortestPath(
            MapTraversalGraph graph,
            int startNodeId,
            int targetNodeId)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            var nodesById = graph.Nodes.ToDictionary(node => node.Id);
            if (!nodesById.ContainsKey(startNodeId))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startNodeId), startNodeId, "Unknown traversal node.");
            }

            if (!nodesById.ContainsKey(targetNodeId))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetNodeId), targetNodeId, "Unknown traversal node.");
            }

            var distances = nodesById.Keys.ToDictionary(nodeId => nodeId, _ => int.MaxValue);
            var previous = new Dictionary<int, int>();
            var unvisited = new HashSet<int>(nodesById.Keys);
            distances[startNodeId] = 0;

            while (unvisited.Count > 0)
            {
                int current = SelectNearest(unvisited, distances);
                if (current < 0 || distances[current] == int.MaxValue)
                {
                    break;
                }

                unvisited.Remove(current);
                if (current == targetNodeId)
                {
                    break;
                }

                foreach (MapTraversalEdge edge in graph.EdgesFrom(current))
                {
                    int other = edge.Other(current);
                    if (!unvisited.Contains(other))
                    {
                        continue;
                    }

                    int candidateDistance = checked(distances[current] + edge.Cost);
                    bool preferCandidate = candidateDistance < distances[other] ||
                        (candidateDistance == distances[other] &&
                         (!previous.TryGetValue(other, out int predecessor) || current < predecessor));
                    if (preferCandidate)
                    {
                        distances[other] = candidateDistance;
                        previous[other] = current;
                    }
                }
            }

            if (distances[targetNodeId] == int.MaxValue)
            {
                return Array.Empty<MapTraversalNode>();
            }

            var path = new List<MapTraversalNode>();
            int pathNodeId = targetNodeId;
            while (true)
            {
                path.Add(nodesById[pathNodeId]);
                if (pathNodeId == startNodeId)
                {
                    break;
                }

                pathNodeId = previous[pathNodeId];
            }

            path.Reverse();
            return new ReadOnlyCollection<MapTraversalNode>(path);
        }

        public static IReadOnlyDictionary<int, int> WeightedDistancesFrom(
            MapTraversalGraph graph,
            int startNodeId)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            if (!graph.Nodes.Any(node => node.Id == startNodeId))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startNodeId),
                    startNodeId,
                    "Unknown traversal node.");
            }

            var distances = graph.Nodes.ToDictionary(node => node.Id, _ => int.MaxValue);
            var unvisited = new HashSet<int>(distances.Keys);
            distances[startNodeId] = 0;

            while (unvisited.Count > 0)
            {
                int current = -1;
                int currentDistance = int.MaxValue;
                foreach (int candidate in unvisited)
                {
                    int distance = distances[candidate];
                    if (distance < currentDistance ||
                        (distance == currentDistance && (current < 0 || candidate < current)))
                    {
                        current = candidate;
                        currentDistance = distance;
                    }
                }

                if (current < 0 || currentDistance == int.MaxValue)
                {
                    break;
                }

                unvisited.Remove(current);
                foreach (MapTraversalEdge edge in graph.EdgesFrom(current))
                {
                    int other = edge.Other(current);
                    if (!unvisited.Contains(other))
                    {
                        continue;
                    }

                    int candidateDistance = checked(currentDistance + edge.Cost);
                    if (candidateDistance < distances[other])
                    {
                        distances[other] = candidateDistance;
                    }
                }
            }

            return new ReadOnlyDictionary<int, int>(distances);
        }

        private static int SelectNearest(
            IEnumerable<int> candidates,
            IReadOnlyDictionary<int, int> distances)
        {
            int nearest = -1;
            int nearestDistance = int.MaxValue;
            foreach (int candidate in candidates)
            {
                int distance = distances[candidate];
                if (distance < nearestDistance ||
                    (distance == nearestDistance && (nearest < 0 || candidate < nearest)))
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }
    }
}
