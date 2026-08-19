using System;
using System.Reflection;
using Game.Gameplay.Navigation;
using Gloomveil.MapGeneration;
using NUnit.Framework;
using UnityEngine;

namespace Game.Gameplay.Tests
{
    public sealed class MinimapRuntimeModelTests
    {
        [Test]
        public void FindContainingNode_PrefersContainingRoomOverCloserCorridorAcrossWall()
        {
            MethodInfo method = typeof(MinimapRuntimeModel).GetMethod(
                "FindContainingNode",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Containing-node resolution has not been implemented yet.");

            var graph = new MapTraversalGraph(
                new[]
                {
                    new MapTraversalNode(1, MapTraversalNodeKind.Room, new GridCoord3(3, 3, 0), 10),
                    new MapTraversalNode(2, MapTraversalNodeKind.Corridor, new GridCoord3(8, 3, 0), 0)
                },
                Array.Empty<MapTraversalEdge>());
            var moduleObject = new GameObject("RoomModule");

            try
            {
                GeneratedMapModule module = CreateRoomModule(moduleObject);

                object result = method.Invoke(null, new object[]
                {
                    graph,
                    new[] { module },
                    new Vector2(7.9f, 3.5f),
                    0
                });

                Assert.That(result, Is.Not.Null);
                Assert.That(((MapTraversalNode)result).Id, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(moduleObject);
            }
        }

        [Test]
        public void ResolveRoomDoorwayWaypoint_TargetsDoorGapInsteadOfCorridorCentre()
        {
            MethodInfo method = typeof(MinimapRuntimeModel).GetMethod(
                "ResolveRoomDoorwayWaypoint",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Doorway waypoint resolution has not been implemented yet.");

            var moduleObject = new GameObject("RoomModule");
            try
            {
                GeneratedMapModule module = CreateRoomModule(moduleObject);
                var roomNode = new MapTraversalNode(
                    1,
                    MapTraversalNodeKind.Room,
                    new GridCoord3(3, 3, 0),
                    10);
                var corridorNode = new MapTraversalNode(
                    2,
                    MapTraversalNodeKind.Corridor,
                    new GridCoord3(8, 1, 0),
                    0);

                object result = method.Invoke(null, new object[]
                {
                    new[] { module },
                    roomNode,
                    corridorNode
                });

                Assert.That((Vector2)result, Is.EqualTo(new Vector2(8f, 1.5f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(moduleObject);
            }
        }

        private static GeneratedMapModule CreateRoomModule(GameObject moduleObject)
        {
            GeneratedMapModule module = moduleObject.AddComponent<GeneratedMapModule>();
            module.Initialize(new MapModuleSelection(
                10,
                "room",
                "room",
                MapModuleCategory.Room,
                RoomRole.None,
                moduleObject,
                new GridCoord3(0, 0, 0),
                0,
                0,
                new GridSize2(8, 8),
                0,
                false,
                Array.Empty<SocketKey>(),
                Array.Empty<MapPort>()), 1);
            return module;
        }
    }
}
