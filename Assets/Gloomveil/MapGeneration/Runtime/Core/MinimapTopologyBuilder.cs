using System;
using System.Collections.Generic;
using System.Linq;

namespace Gloomveil.MapGeneration
{
    public sealed class MinimapTopologyBuilder
    {
        public MinimapTopology Build(MapLayout layout)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            var walls = new HashSet<GridEdgeKey>();
            foreach (PlacedRoom room in layout.Rooms)
            {
                AddPerimeter(walls, room.RotatedRect, room.Floor);
            }

            foreach (CorridorTile tile in layout.CorridorTiles)
            {
                AddCellPerimeter(walls, tile.Coord);
            }

            foreach (PlacedStair stair in layout.Stairs)
            {
                AddPerimeter(walls, stair.RotatedRect, stair.LowerFloor);
                AddPerimeter(walls, stair.RotatedRect, stair.UpperFloor);
            }

            foreach (PlacedRoom room in layout.Rooms)
            {
                foreach (MapPort port in room.Ports)
                {
                    walls.Remove(port.WorldEdge);
                }
            }

            foreach (PlacedStair stair in layout.Stairs)
            {
                RemovePort(walls, stair.BottomPort);
                RemovePort(walls, stair.TopPort);
            }

            foreach (CorridorTile tile in layout.CorridorTiles)
            {
                RemoveCorridorOpenings(walls, tile);
            }

            MinimapWallSegment[] segments = walls
                .OrderBy(edge => edge)
                .Select(edge => new MinimapWallSegment(edge))
                .ToArray();

            MinimapStairMarker[] stairMarkers = layout.Stairs
                .OrderBy(stair => stair.PlacementId)
                .SelectMany(CreateStairMarkers)
                .ToArray();

            return new MinimapTopology(
                layout.WidthUnits,
                layout.DepthUnits,
                layout.FloorCount,
                segments,
                stairMarkers);
        }

        private static void AddPerimeter(HashSet<GridEdgeKey> walls, GridRect rect, int floor)
        {
            for (int x = rect.X; x < rect.MaxX; x++)
            {
                walls.Add(new GridEdgeKey(floor, GridEdgeAxis.AlongX, x, rect.Z));
                walls.Add(new GridEdgeKey(floor, GridEdgeAxis.AlongX, x, rect.MaxZ));
            }

            for (int z = rect.Z; z < rect.MaxZ; z++)
            {
                walls.Add(new GridEdgeKey(floor, GridEdgeAxis.AlongZ, rect.X, z));
                walls.Add(new GridEdgeKey(floor, GridEdgeAxis.AlongZ, rect.MaxX, z));
            }
        }

        private static void AddCellPerimeter(HashSet<GridEdgeKey> walls, GridCoord3 cell)
        {
            walls.Add(new GridEdgeKey(cell.Floor, GridEdgeAxis.AlongX, cell.X, cell.Z));
            walls.Add(new GridEdgeKey(cell.Floor, GridEdgeAxis.AlongX, cell.X, cell.Z + 1));
            walls.Add(new GridEdgeKey(cell.Floor, GridEdgeAxis.AlongZ, cell.X, cell.Z));
            walls.Add(new GridEdgeKey(cell.Floor, GridEdgeAxis.AlongZ, cell.X + 1, cell.Z));
        }

        private static void RemoveCorridorOpenings(
            HashSet<GridEdgeKey> walls,
            CorridorTile tile)
        {
            GridCoord3 cell = tile.Coord;
            if ((tile.ConnectionMask & CorridorConnectionMask.North) != 0)
            {
                walls.Remove(new GridEdgeKey(cell.Floor, GridEdgeAxis.AlongX, cell.X, cell.Z + 1));
            }

            if ((tile.ConnectionMask & CorridorConnectionMask.East) != 0)
            {
                walls.Remove(new GridEdgeKey(cell.Floor, GridEdgeAxis.AlongZ, cell.X + 1, cell.Z));
            }

            if ((tile.ConnectionMask & CorridorConnectionMask.South) != 0)
            {
                walls.Remove(new GridEdgeKey(cell.Floor, GridEdgeAxis.AlongX, cell.X, cell.Z));
            }

            if ((tile.ConnectionMask & CorridorConnectionMask.West) != 0)
            {
                walls.Remove(new GridEdgeKey(cell.Floor, GridEdgeAxis.AlongZ, cell.X, cell.Z));
            }
        }

        private static void RemovePort(HashSet<GridEdgeKey> walls, MapPort? port)
        {
            if (port.HasValue)
            {
                walls.Remove(port.Value.WorldEdge);
            }
        }

        private static IEnumerable<MinimapStairMarker> CreateStairMarkers(PlacedStair stair)
        {
            var center = new MinimapPoint2(
                stair.RotatedRect.CenterX2,
                stair.RotatedRect.CenterZ2);
            yield return new MinimapStairMarker(
                stair.LowerFloor,
                center,
                MinimapStairDirection.Up);
            yield return new MinimapStairMarker(
                stair.UpperFloor,
                center,
                MinimapStairDirection.Down);
        }
    }
}
