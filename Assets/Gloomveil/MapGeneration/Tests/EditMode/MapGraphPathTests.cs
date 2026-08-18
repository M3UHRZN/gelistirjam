using System.Linq;
using NUnit.Framework;

namespace Gloomveil.MapGeneration.Tests
{
    public sealed class MapGraphPathTests
    {
        [Test]
        public void WeightedShortestPath_PrefersCheaperMultiEdgeRouteOverDirectEdge()
        {
            var graph = new MapTraversalGraph(
                new[]
                {
                    Node(1, 0),
                    Node(2, 1),
                    Node(3, 2),
                    Node(4, 3)
                },
                new[]
                {
                    new MapTraversalEdge(1, 4, 10),
                    new MapTraversalEdge(1, 2, 2),
                    new MapTraversalEdge(2, 3, 2),
                    new MapTraversalEdge(3, 4, 2)
                });

            int[] path = MapGraphSearch
                .WeightedShortestPath(graph, 1, 4)
                .Select(node => node.Id)
                .ToArray();

            Assert.That(path, Is.EqualTo(new[] { 1, 2, 3, 4 }));
        }

        private static MapTraversalNode Node(int id, int x) =>
            new MapTraversalNode(
                id,
                MapTraversalNodeKind.Corridor,
                new GridCoord3(x, 0, 0),
                0);
    }
}

