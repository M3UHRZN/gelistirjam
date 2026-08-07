using System;

namespace Gloomveil.MapGeneration
{
    [Flags]
    public enum RoomRoleMask : byte
    {
        None = 0,
        Unassigned = 1 << 0,
        Start = 1 << 1,
        Exit = 1 << 2,
        Fuse = 1 << 3,
        SafeZone = 1 << 4,
        All = Unassigned | Start | Exit | Fuse | SafeZone
    }

    public static class RoomRoleMaskExtensions
    {
        public static bool Allows(this RoomRoleMask mask, RoomRole role)
        {
            RoomRoleMask requested;
            switch (role)
            {
                case RoomRole.Start:
                    requested = RoomRoleMask.Start;
                    break;
                case RoomRole.Exit:
                    requested = RoomRoleMask.Exit;
                    break;
                case RoomRole.Fuse:
                    requested = RoomRoleMask.Fuse;
                    break;
                case RoomRole.SafeZone:
                    requested = RoomRoleMask.SafeZone;
                    break;
                default:
                    requested = RoomRoleMask.Unassigned;
                    break;
            }

            return (mask & requested) != 0;
        }
    }
}
