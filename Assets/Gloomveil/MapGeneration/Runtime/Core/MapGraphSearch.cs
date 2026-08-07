using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Gloomveil.MapGeneration
{
    public static class MapGraphSearch
    {
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
    }
}
