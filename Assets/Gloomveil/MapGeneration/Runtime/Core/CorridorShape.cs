using System;

namespace Gloomveil.MapGeneration
{
    [Flags]
    public enum CorridorConnectionMask : byte
    {
        None = 0,
        North = 1 << 0,
        East = 1 << 1,
        South = 1 << 2,
        West = 1 << 3
    }

    public enum CorridorShape : byte
    {
        Straight = 0,
        Corner = 1,
        TJunction = 2,
        CrossJunction = 3,
        DeadEnd = 4,
        DoorTransition = 5
    }
}
