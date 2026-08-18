using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Gloomveil.MapGeneration.Tests
{
    public sealed class MinimapTopologyTests
    {
        [Test]
        public void Build_OpenRoomPortIsDoorGapWhileClosedPerimeterRemainsWall()
        {
            MapGenerationParameters parameters =
                MapGenerationParameters.CreateProductionDefaults(2);
            MapLayout layout = new VariableFootprintLayoutGenerator().Generate(parameters, 1042);
            PlacedRoom room = layout.Rooms.First(candidate => candidate.Ports.Count > 0);

            MinimapTopology topology = new MinimapTopologyBuilder().Build(layout);
            var wallEdges = new HashSet<GridEdgeKey>(topology.Walls.Select(wall => wall.Edge));
            GridEdgeKey openDoor = room.Ports[0].WorldEdge;
            GridEdgeKey closedWall = PerimeterEdges(room.RotatedRect, room.Floor)
                .First(edge => room.Ports.All(port => port.WorldEdge != edge));

            Assert.That(wallEdges.Contains(openDoor), Is.False,
                $"Open port {openDoor} must be a visible gap on the minimap.");
            Assert.That(wallEdges.Contains(closedWall), Is.True,
                $"Closed perimeter {closedWall} must remain visible on the minimap.");
        }

        private static IEnumerable<GridEdgeKey> PerimeterEdges(GridRect rect, int floor)
        {
            for (int x = rect.X; x < rect.MaxX; x++)
            {
                yield return new GridEdgeKey(floor, GridEdgeAxis.AlongX, x, rect.Z);
                yield return new GridEdgeKey(floor, GridEdgeAxis.AlongX, x, rect.MaxZ);
            }

            for (int z = rect.Z; z < rect.MaxZ; z++)
            {
                yield return new GridEdgeKey(floor, GridEdgeAxis.AlongZ, rect.X, z);
                yield return new GridEdgeKey(floor, GridEdgeAxis.AlongZ, rect.MaxX, z);
            }
        }
    }
}

