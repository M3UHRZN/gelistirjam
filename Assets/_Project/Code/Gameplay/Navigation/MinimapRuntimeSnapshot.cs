using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Gloomveil.MapGeneration;
using UnityEngine;

namespace Game.Gameplay.Navigation
{
    public readonly struct MinimapRoutePoint
    {
        public Vector2 Position { get; }
        public int Floor { get; }

        public MinimapRoutePoint(Vector2 position, int floor)
        {
            Position = position;
            Floor = floor;
        }
    }

    public sealed class MinimapRuntimeSnapshot
    {
        public static MinimapRuntimeSnapshot Empty { get; } = new MinimapRuntimeSnapshot();

        public bool IsReady { get; }
        public MinimapTopology Topology { get; }
        public int CurrentFloor { get; }
        public Vector2 PlayerMapPosition { get; }
        public float PlayerYawDegrees { get; }
        public bool HasFuseDirection { get; }
        public Vector2 FuseDirection { get; }
        public IReadOnlyList<MinimapRoutePoint> ExitRoute { get; }

        private MinimapRuntimeSnapshot()
        {
            ExitRoute = Array.Empty<MinimapRoutePoint>();
        }

        public MinimapRuntimeSnapshot(
            MinimapTopology topology,
            int currentFloor,
            Vector2 playerMapPosition,
            float playerYawDegrees,
            bool hasFuseDirection,
            Vector2 fuseDirection,
            IEnumerable<MinimapRoutePoint> exitRoute)
        {
            Topology = topology ?? throw new ArgumentNullException(nameof(topology));
            IsReady = true;
            CurrentFloor = currentFloor;
            PlayerMapPosition = playerMapPosition;
            PlayerYawDegrees = playerYawDegrees;
            HasFuseDirection = hasFuseDirection;
            FuseDirection = hasFuseDirection ? fuseDirection.normalized : Vector2.zero;
            ExitRoute = new ReadOnlyCollection<MinimapRoutePoint>(
                new List<MinimapRoutePoint>(
                    exitRoute ?? throw new ArgumentNullException(nameof(exitRoute))));
        }
    }
}
