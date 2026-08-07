using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Gloomveil.MapGeneration.Tests
{
    public sealed class MapModuleResolverSmokeTests
    {
        private readonly List<GameObject> createdPrefabs = new List<GameObject>();
        private MapModuleCatalog catalog;
        private MapModuleResolver resolver;

        [SetUp]
        public void SetUp()
        {
            catalog = ScriptableObject.CreateInstance<MapModuleCatalog>();
            catalog.SetEntries(new[]
            {
                Entry(
                    "ROOM_2x3_01",
                    "ROOM_2x3",
                    MapModuleCategory.Room,
                    new GridSize2(2, 3),
                    CorridorShape.DeadEnd,
                    "PF_ROOM_Generic_2x3_01",
                    usesDynamicSockets: true),
                Entry(
                    "COR_STRAIGHT_1x1_01",
                    "COR_STRAIGHT_1x1",
                    MapModuleCategory.Corridor,
                    new GridSize2(1, 1),
                    CorridorShape.Straight,
                    "PF_COR_Straight_1x1_01"),
                Entry(
                    "COR_STRAIGHT_1x2_01",
                    "COR_STRAIGHT_1x2",
                    MapModuleCategory.Corridor,
                    new GridSize2(1, 2),
                    CorridorShape.Straight,
                    "PF_COR_Straight_1x2_01"),
                Entry(
                    "COR_STRAIGHT_1x3_01",
                    "COR_STRAIGHT_1x3",
                    MapModuleCategory.Corridor,
                    new GridSize2(1, 3),
                    CorridorShape.Straight,
                    "PF_COR_Straight_1x3_01")
            });
            resolver = new MapModuleResolver(catalog);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject prefab in createdPrefabs)
            {
                Object.DestroyImmediate(prefab);
            }

            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void ResolveRoom_TwoByThreeAtQuarterTurn_UsesThreeByTwoPlacementAndRotatedSockets()
        {
            PlacedRoom room = TestRoom(
                footprintId: "2x3",
                canonicalSize: new GridSize2(2, 3),
                southwestOrigin: new GridCoord3(10, 20, 0),
                quarterTurns: 1,
                openLocalSocket: new SocketKey(CardinalDirection.North, 0, 0));

            MapModuleSelection result = resolver.ResolveRoom(
                room,
                new SeededRandom(7, "modules"));

            Assert.That(result.RotatedSize, Is.EqualTo(new GridSize2(3, 2)));
            Assert.That(result.QuarterTurns, Is.EqualTo(1));
            Assert.That(result.OpenLocalSockets, Is.EqualTo(room.OpenLocalSockets));

            MapPort port = result.Ports.Single();
            Assert.That(
                port.RotatedSocket,
                Is.EqualTo(new SocketKey(CardinalDirection.East, 1, 0)));
            Assert.That(port.WorldFacing, Is.EqualTo(CardinalDirection.East));
            Assert.That(
                port.WorldEdge,
                Is.EqualTo(new GridEdgeKey(0, GridEdgeAxis.AlongZ, 13, 21)));
        }

        [TestCase(1, "PF_COR_Straight_1x1_01")]
        [TestCase(2, "PF_COR_Straight_1x2_01")]
        [TestCase(3, "PF_COR_Straight_1x3_01")]
        public void ResolveCorridor_StraightLength_UsesMatchingPrefab(
            int length,
            string expected)
        {
            MapModuleSelection result = resolver.ResolveCorridor(
                Straight(length),
                new SeededRandom(7, "modules"));

            Assert.That(result.Prefab.name, Is.EqualTo(expected));
            MapPort north = result.Ports.Single(port =>
                port.LocalSocket.Side == CardinalDirection.North);
            MapPort south = result.Ports.Single(port =>
                port.LocalSocket.Side == CardinalDirection.South);
            Assert.That(north.WorldFacing, Is.EqualTo(CardinalDirection.North));
            Assert.That(
                north.WorldEdge,
                Is.EqualTo(new GridEdgeKey(0, GridEdgeAxis.AlongX, 0, length)));
            Assert.That(south.WorldFacing, Is.EqualTo(CardinalDirection.South));
            Assert.That(
                south.WorldEdge,
                Is.EqualTo(new GridEdgeKey(0, GridEdgeAxis.AlongX, 0, 0)));
        }

        private MapModuleEntry Entry(
            string catalogEntryId,
            string familyId,
            MapModuleCategory category,
            GridSize2 size,
            CorridorShape shape,
            string prefabName,
            bool usesDynamicSockets = false)
        {
            var prefab = new GameObject(prefabName);
            createdPrefabs.Add(prefab);
            return new MapModuleEntry(
                catalogEntryId,
                familyId,
                category,
                size,
                shape,
                RoomRoleMask.All,
                prefab,
                weight: 1,
                allowQuarterTurns: true,
                usesDynamicSockets: usesDynamicSockets);
        }

        private static PlacedRoom TestRoom(
            string footprintId,
            GridSize2 canonicalSize,
            GridCoord3 southwestOrigin,
            int quarterTurns,
            SocketKey openLocalSocket)
        {
            var room = new PlacedRoom(
                placementId: 1,
                footprintId,
                southwestOrigin.Floor,
                southwestOrigin.X,
                southwestOrigin.Z,
                canonicalSize,
                quarterTurns);
            SocketKey rotated = openLocalSocket.Rotate(canonicalSize, quarterTurns);
            var port = new MapPort(
                room.PlacementId,
                openLocalSocket,
                rotated,
                new GridEdgeKey(0, GridEdgeAxis.AlongZ, 13, 21),
                new GridCoord3(13, 21, 0),
                CardinalDirection.East);
            room.SetPorts(new[] { port });
            return room;
        }

        private static CorridorModulePlacement Straight(int length) =>
            new CorridorModulePlacement(
                placementId: 100 + length,
                southwestOrigin: new GridCoord3(0, 0, 0),
                canonicalSize: new GridSize2(1, length),
                quarterTurns: 0,
                shape: CorridorShape.Straight,
                lengthUnits: length,
                openLocalSockets: new[]
                {
                    new SocketKey(CardinalDirection.North, 0, 0),
                    new SocketKey(CardinalDirection.South, 0, 0)
                });
    }
}
