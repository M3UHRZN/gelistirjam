using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Gloomveil.MapGeneration
{
    public sealed class PackedAreaLayout
    {
        private readonly IReadOnlyList<GridRect>[] occupiedByFloor;

        public int FloorCount { get; }
        public IReadOnlyList<PlacedRoom> Rooms { get; }
        public IReadOnlyList<PlacedStair> Stairs { get; }

        public PackedAreaLayout(
            int floorCount,
            IEnumerable<PlacedRoom> rooms,
            IEnumerable<PlacedStair> stairs)
        {
            if (floorCount < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(floorCount),
                    floorCount,
                    "Floor count must be at least 2.");
            }

            FloorCount = floorCount;
            var roomList = new List<PlacedRoom>(rooms ?? throw new ArgumentNullException(nameof(rooms)));
            var stairList = new List<PlacedStair>(stairs ?? throw new ArgumentNullException(nameof(stairs)));
            Rooms = new ReadOnlyCollection<PlacedRoom>(roomList);
            Stairs = new ReadOnlyCollection<PlacedStair>(stairList);

            var mutableOccupancy = new List<GridRect>[floorCount];
            occupiedByFloor = new IReadOnlyList<GridRect>[floorCount];
            for (int floor = 0; floor < floorCount; floor++)
            {
                mutableOccupancy[floor] = new List<GridRect>();
            }

            foreach (PlacedRoom room in roomList)
            {
                mutableOccupancy[room.Floor].Add(room.RotatedRect);
            }

            foreach (PlacedStair stair in stairList)
            {
                mutableOccupancy[stair.LowerFloor].Add(stair.RotatedRect);
                mutableOccupancy[stair.UpperFloor].Add(stair.RotatedRect);
            }

            for (int floor = 0; floor < floorCount; floor++)
            {
                occupiedByFloor[floor] = new ReadOnlyCollection<GridRect>(mutableOccupancy[floor]);
            }
        }

        public IReadOnlyList<GridRect> OccupiedRectsForFloor(int floor)
        {
            if (floor < 0 || floor >= FloorCount)
            {
                throw new ArgumentOutOfRangeException(nameof(floor), floor, "Floor is outside the layout.");
            }

            return occupiedByFloor[floor];
        }
    }
}
