using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Gloomveil.MapGeneration
{
    public sealed class MapLayout
    {
        public int WidthUnits { get; }
        public int DepthUnits { get; }
        public int FloorCount { get; }
        public IReadOnlyList<PlacedRoom> Rooms { get; }
        public IReadOnlyList<PlacedStair> Stairs { get; }
        public IReadOnlyList<CorridorTile> CorridorTiles { get; }
        public IReadOnlyList<CorridorModulePlacement> CorridorModules { get; }
        public MapTraversalGraph TraversalGraph { get; }

        public MapLayout(
            int widthUnits,
            int depthUnits,
            int floorCount,
            IEnumerable<PlacedRoom> rooms,
            IEnumerable<PlacedStair> stairs,
            IEnumerable<CorridorTile> corridorTiles,
            IEnumerable<CorridorModulePlacement> corridorModules,
            MapTraversalGraph traversalGraph)
        {
            WidthUnits = widthUnits;
            DepthUnits = depthUnits;
            FloorCount = floorCount;
            Rooms = ReadOnly(rooms, nameof(rooms));
            Stairs = ReadOnly(stairs, nameof(stairs));
            CorridorTiles = ReadOnly(corridorTiles, nameof(corridorTiles));
            CorridorModules = ReadOnly(corridorModules, nameof(corridorModules));
            TraversalGraph = traversalGraph ?? throw new ArgumentNullException(nameof(traversalGraph));
        }

        public string CreateSignature()
        {
            var builder = new StringBuilder(4096);
            foreach (PlacedRoom room in Rooms.OrderBy(room => room.PlacementId))
            {
                builder.Append("R|");
                Append(builder, room.PlacementId, room.Floor, room.SouthwestOrigin.X, room.SouthwestOrigin.Z);
                builder.Append(room.FootprintId).Append('|');
                Append(builder, room.CanonicalSize.Width, room.CanonicalSize.Depth, room.QuarterTurns);
                foreach (MapPort port in room.Ports
                             .OrderBy(port => port.LocalSocket.ToString(), StringComparer.Ordinal))
                {
                    builder.Append(port.LocalSocket).Append('>')
                        .Append(port.WorldEdge).Append(',');
                }

                builder.AppendLine();
            }

            foreach (PlacedStair stair in Stairs.OrderBy(stair => stair.PlacementId))
            {
                builder.Append("S|");
                Append(
                    builder,
                    stair.PlacementId,
                    stair.LowerFloor,
                    stair.UpperFloor,
                    stair.RotatedRect.X,
                    stair.RotatedRect.Z,
                    stair.RotatedRect.Width,
                    stair.RotatedRect.Depth,
                    stair.QuarterTurns);
                builder.Append(stair.FootprintId).Append('|');
                AppendPort(builder, stair.BottomPort);
                AppendPort(builder, stair.TopPort);
                builder.AppendLine();
            }

            foreach (CorridorTile tile in CorridorTiles.OrderBy(tile => tile.Coord))
            {
                builder.Append("T|");
                Append(
                    builder,
                    tile.Coord.Floor,
                    tile.Coord.X,
                    tile.Coord.Z,
                    (int)tile.ConnectionMask);
                builder.AppendLine();
            }

            foreach (CorridorModulePlacement module in CorridorModules
                         .OrderBy(module => module.PlacementId))
            {
                builder.Append("M|");
                Append(
                    builder,
                    module.PlacementId,
                    module.SouthwestOrigin.Floor,
                    module.SouthwestOrigin.X,
                    module.SouthwestOrigin.Z,
                    module.CanonicalSize.Width,
                    module.CanonicalSize.Depth,
                    module.QuarterTurns,
                    (int)module.Shape,
                    module.LengthUnits);
                builder.AppendLine();
            }

            foreach (PlacedRoom room in Rooms.OrderBy(room => room.PlacementId))
            {
                builder.Append("ROLE|");
                Append(builder, room.PlacementId, (int)room.Role);
                builder.AppendLine();
            }

            return builder.ToString();
        }

        private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> source, string argumentName) =>
            new ReadOnlyCollection<T>(
                new List<T>(source ?? throw new ArgumentNullException(argumentName)));

        private static void Append(StringBuilder builder, params int[] values)
        {
            for (int index = 0; index < values.Length; index++)
            {
                builder.Append(values[index].ToString(CultureInfo.InvariantCulture)).Append('|');
            }
        }

        private static void AppendPort(StringBuilder builder, MapPort? port)
        {
            if (!port.HasValue)
            {
                builder.Append("none|");
                return;
            }

            builder.Append(port.Value.LocalSocket).Append('>')
                .Append(port.Value.WorldEdge).Append('|');
        }
    }
}
