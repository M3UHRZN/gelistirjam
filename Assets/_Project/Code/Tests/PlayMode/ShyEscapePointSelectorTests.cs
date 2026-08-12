using System;
using Game.Gameplay.Monster;
using Game.Gameplay.Player;
using Game.Gameplay.World;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Gameplay.Tests
{
    public sealed class ShyEscapePointSelectorTests
    {
        [Test]
        public void PickBest_RejectsVisibleAndExcludedCorners()
        {
            using var fixture = new SelectorFixture();
            StalkPoint visible = fixture.CreatePoint("VisibleCorner", new Vector3(0f, 0f, 3f));
            StalkPoint exposed = fixture.CreatePoint("ExposedCorner", new Vector3(-3f, 0f, 8f));
            StalkPoint hidden = fixture.CreatePoint("HiddenCorner", new Vector3(0f, 0f, 10f));
            fixture.CreateWall(new Vector3(0f, 1.5f, 6f), new Vector3(5f, 3f, 0.25f));

            StalkPoint selected = ShyEscapePointSelector.PickBest(
                fixture.Points,
                fixture.PlayerPosition,
                fixture.MonsterPosition,
                fixture.Vision,
                fixture.Navigator,
                exposed);

            Assert.That(selected, Is.SameAs(hidden));
            Assert.That(selected, Is.Not.SameAs(visible));
            Assert.That(selected, Is.Not.SameAs(exposed));
        }

        [Test]
        public void PickBest_RejectsNonCornerSafeZoneAndUnreachablePoints()
        {
            using var fixture = new SelectorFixture(lookAway: true);
            StalkPoint valid = fixture.CreatePoint("ValidCorner", new Vector3(0f, 0f, 5f));
            fixture.CreatePoint("NotACorner", new Vector3(0f, 0f, 9f), nearCorner: false);
            StalkPoint safe = fixture.CreatePoint("SafeCorner", new Vector3(-3f, 0f, 7f));
            fixture.CreateSafeZone(safe.Position);
            fixture.CreatePoint("UnreachableCorner", new Vector3(50f, 0f, 0f));

            StalkPoint selected = ShyEscapePointSelector.PickBest(
                fixture.Points,
                fixture.PlayerPosition,
                fixture.MonsterPosition,
                fixture.Vision,
                fixture.Navigator,
                excluded: null);

            Assert.That(selected, Is.SameAs(valid));
        }

        [Test]
        public void PickBest_RanksDistanceThenDarknessThenRegistryOrder()
        {
            using var fixture = new SelectorFixture(lookAway: true);
            StalkPoint near = fixture.CreatePoint("Near", new Vector3(0f, 0f, 2f), darkness: 1f);
            StalkPoint farFirst = fixture.CreatePoint("FarFirst", new Vector3(-4f, 0f, 4f), darkness: 0.2f);
            StalkPoint farDarker = fixture.CreatePoint("FarDarker", new Vector3(4f, 0f, 4f), darkness: 0.8f);

            StalkPoint selected = ShyEscapePointSelector.PickBest(
                fixture.Points,
                fixture.PlayerPosition,
                fixture.MonsterPosition,
                fixture.Vision,
                fixture.Navigator,
                excluded: null);

            Assert.That(selected, Is.SameAs(farDarker),
                "Equal player distance must prefer darkness after distance has beaten the near point.");

            farFirst.ConfigureStaticDarkness(0.8f);
            selected = ShyEscapePointSelector.PickBest(
                fixture.Points,
                fixture.PlayerPosition,
                fixture.MonsterPosition,
                fixture.Vision,
                fixture.Navigator,
                excluded: null);

            Assert.That(selected, Is.SameAs(farFirst),
                "An exact ranking tie must retain stable registry order.");
            Assert.That(selected, Is.Not.SameAs(near));
        }

        [Test]
        public void PickBest_RejectsCornerWhoseResolvedNavMeshPositionIsVisible()
        {
            using var fixture = new SelectorFixture(floorWidth: 20f);
            StalkPoint offMeshCorner = fixture.CreatePoint(
                "OffMeshHiddenAnchor",
                new Vector3(11f, 0f, 6f));

            Assert.That(fixture.Vision.IsMonsterBodyVisibleAt(
                offMeshCorner.Position,
                fixture.Navigator.AgentHeight,
                fixture.Navigator.AgentRadius), Is.False,
                "The authored anchor must begin outside the camera viewport for this regression setup.");
            Assert.That(fixture.Navigator.TryResolveReachableDestination(
                offMeshCorner.Position,
                out Vector3 resolved), Is.True);
            Assert.That(fixture.Vision.IsMonsterBodyVisibleAt(
                resolved,
                fixture.Navigator.AgentHeight,
                fixture.Navigator.AgentRadius), Is.True,
                "The NavMesh snap must move the body into view for this regression setup.");

            StalkPoint selected = ShyEscapePointSelector.PickBest(
                fixture.Points,
                fixture.PlayerPosition,
                fixture.MonsterPosition,
                fixture.Vision,
                fixture.Navigator,
                excluded: null);

            Assert.That(selected, Is.Null,
                "Shy must evaluate the exact resolved destination, not the hidden authored anchor.");
        }

        private sealed class SelectorFixture : IDisposable
        {
            private readonly GameObject root;
            private readonly NavMeshSurface surface;
            private readonly SafeZoneRuntimeSet safeZones;

            public SelectorFixture(bool lookAway = false, float floorWidth = 30f)
            {
                root = new GameObject("ShySelectorFixture");

                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Floor";
                floor.transform.SetParent(root.transform, false);
                floor.transform.position = new Vector3(0f, -0.1f, 4f);
                floor.transform.localScale = new Vector3(floorWidth, 0.2f, 30f);

                surface = root.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.BuildNavMesh();

                var monster = new GameObject("Monster");
                monster.transform.SetParent(root.transform, false);
                monster.transform.position = Vector3.zero;
                NavMeshAgent agent = monster.AddComponent<NavMeshAgent>();
                agent.height = 2.2f;
                agent.radius = 0.35f;
                Navigator = monster.AddComponent<MonsterNavigator>();

                safeZones = ScriptableObject.CreateInstance<SafeZoneRuntimeSet>();
                Navigator.Bind(safeZones);

                var player = new GameObject("Player");
                player.transform.SetParent(root.transform, false);
                player.transform.position = new Vector3(0f, 0f, -4f);
                Vision = player.AddComponent<PlayerVisionModel>();

                var cameraObject = new GameObject("PlayerCamera");
                cameraObject.transform.SetParent(player.transform, false);
                cameraObject.transform.localPosition = Vector3.up * 1.1f;
                cameraObject.transform.localRotation = lookAway
                    ? Quaternion.Euler(0f, 180f, 0f)
                    : Quaternion.identity;
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.fieldOfView = 60f;
                camera.aspect = 16f / 9f;

                MonsterPerception perception = monster.AddComponent<MonsterPerception>();
                perception.Bind(player.transform);
                Vision.Bind(player.transform, camera, perception);

                Points = ScriptableObject.CreateInstance<StalkPointRuntimeSet>();
                Physics.SyncTransforms();
                Assert.That(agent.isOnNavMesh, Is.True, "Selector fixture monster must start on its runtime NavMesh.");
            }

            public StalkPointRuntimeSet Points { get; }
            public PlayerVisionModel Vision { get; }
            public MonsterNavigator Navigator { get; }
            public Vector3 PlayerPosition => new Vector3(0f, 0f, -4f);
            public Vector3 MonsterPosition => Vector3.zero;

            public StalkPoint CreatePoint(
                string name,
                Vector3 position,
                bool nearCorner = true,
                float darkness = 0.5f)
            {
                var pointObject = new GameObject(name);
                pointObject.transform.SetParent(root.transform, false);
                pointObject.transform.position = position;
                StalkPoint point = pointObject.AddComponent<StalkPoint>();
                point.ConfigureTopology(nearCorner, junction: false, deadEnd: false);
                point.ConfigureStaticDarkness(darkness);
                Points.Register(point);
                return point;
            }

            public void CreateWall(Vector3 position, Vector3 scale)
            {
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "OccludingWall";
                wall.transform.SetParent(root.transform, false);
                wall.transform.position = position;
                wall.transform.localScale = scale;
                Physics.SyncTransforms();
            }

            public void CreateSafeZone(Vector3 position)
            {
                var zoneObject = new GameObject("SafeZone");
                zoneObject.transform.SetParent(root.transform, false);
                zoneObject.transform.position = position + Vector3.up;
                BoxCollider collider = zoneObject.AddComponent<BoxCollider>();
                collider.size = new Vector3(2f, 2f, 2f);
                SafeZone zone = zoneObject.AddComponent<SafeZone>();
                zone.Bind(safeZones);
                Physics.SyncTransforms();
            }

            public void Dispose()
            {
                surface.RemoveData();
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(Points);
                UnityEngine.Object.DestroyImmediate(safeZones);
            }
        }
    }
}
