using System;
using System.Collections;
using System.Linq;
using Game.Core;
using Game.Gameplay.Objectives;
using Game.Gameplay.World;
using Gloomveil.MapGeneration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Gameplay.Tests
{
    public sealed class MapFuseGateTests
    {
        [UnityTest]
        public IEnumerator OnMapReady_SelectsClosedWallFarthestFromOpenPortal()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.AddClosedSocket("N_00", new Vector3(0f, 0f, 4f), Vector3.forward);
                fixture.AddClosedSocket("S_00", new Vector3(0f, 0f, -4f), Vector3.back);
                fixture.AddPortal("N_00", new Vector3(0f, 0f, 3.5f));

                fixture.Publish();

                Transform spawned = fixture.Gate.SpawnedFuseBoxes.Single().transform;
                Assert.That(spawned.position.z, Is.LessThan(0f));
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator OnMapReady_EqualScoresChooseOrdinalSocketName()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.AddClosedSocket("W_00", new Vector3(-4f, 0f, 0f), Vector3.left);
                fixture.AddClosedSocket("E_00", new Vector3(4f, 0f, 0f), Vector3.right);

                fixture.Publish();

                Assert.That(
                    fixture.Gate.SpawnedFuseBoxes.Single().transform.position.x,
                    Is.GreaterThan(0f));
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator OnMapReady_NoClosedWallWarnsAndFallsBackToFuseAnchor()
        {
            Fixture fixture = CreateFixture();

            try
            {
                LogAssert.Expect(
                    LogType.Warning,
                    "MapFuseGate could not resolve a closed wall for anchor 'Anchor_Fuse_00' " +
                    "in module 'FuseRoom'; using the anchor fallback.");

                fixture.Publish();

                Assert.That(
                    Vector3.Distance(
                        fixture.Gate.SpawnedFuseBoxes.Single().transform.position,
                        fixture.Anchor.position),
                    Is.LessThan(0.01f));
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator OnMapReady_ComposesRotationOffsetAfterFacingIntoRoom()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.AddClosedSocket("N_00", new Vector3(0f, 0f, 4f), Vector3.forward);
                fixture.SetMountSettings(0f, 0f, Vector3.zero, new Vector3(0f, 90f, 0f));

                fixture.Publish();

                Transform spawned = fixture.Gate.SpawnedFuseBoxes.Single().transform;
                Quaternion expected = Quaternion.LookRotation(Vector3.back, Vector3.up) *
                                      Quaternion.Euler(0f, 90f, 0f);
                Assert.That(Quaternion.Angle(spawned.rotation, expected), Is.LessThan(0.01f));
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator OnMapReady_AppliesOffsetInChosenWallFrame()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.AddClosedSocket("N_00", new Vector3(0f, 0f, 4f), Vector3.forward);
                fixture.SetMountSettings(0f, 0f, new Vector3(1f, 2f, 3f), Vector3.zero);

                fixture.Publish();

                Vector3 expected = new Vector3(-1f, 2f, 1f);
                Assert.That(
                    Vector3.Distance(
                        fixture.Gate.SpawnedFuseBoxes.Single().transform.position,
                        expected),
                    Is.LessThan(0.01f));
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator OnMapReady_RenderedVisualSitsAtHeightAndFlushWithWall()
        {
            Fixture fixture = CreateFixture();

            try
            {
                Renderer wall = fixture.AddClosedSocket(
                    "N_00",
                    new Vector3(0f, 0f, 4f),
                    Vector3.forward,
                    addRenderer: true);
                fixture.UseRenderedVisual(new Vector3(1f, 0.5f, 0.2f));
                fixture.SetMountSettings(1.25f, 0.02f, Vector3.zero, Vector3.zero);

                fixture.Publish();

                Renderer visual = fixture.Gate.SpawnedFuseBoxes.Single()
                    .GetComponentInChildren<Renderer>();
                Assert.That(Mathf.Abs(visual.bounds.min.y - 1.25f), Is.LessThan(0.01f));
                Assert.That(
                    Mathf.Abs(visual.bounds.max.z - (wall.bounds.min.z - 0.02f)),
                    Is.LessThan(0.01f));
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        private static Fixture CreateFixture()
        {
            var mapRoot = new GameObject("MapFuseGateFixture_Map");
            GeneratedMapRegistry registry = mapRoot.AddComponent<GeneratedMapRegistry>();

            var moduleObject = new GameObject("FuseRoom");
            moduleObject.transform.SetParent(mapRoot.transform, false);
            GeneratedMapModule module = moduleObject.AddComponent<GeneratedMapModule>();

            var anchorObject = new GameObject("Anchor_Fuse_00");
            anchorObject.transform.SetParent(moduleObject.transform, false);

            var gameFlowObject = new GameObject("MapFuseGateFixture_GameFlow");
            GameFlowController gameFlow = gameFlowObject.AddComponent<GameFlowController>();

            var objectivesObject = new GameObject("MapFuseGateFixture_Objectives");
            ObjectiveManager objectives = objectivesObject.AddComponent<ObjectiveManager>();

            var miniGameObject = new GameObject("MapFuseGateFixture_MiniGame");
            FuseMiniGameController miniGame = miniGameObject.AddComponent<FuseMiniGameController>();

            var visualPrefab = new GameObject("MapFuseGateFixture_Visual");
            visualPrefab.AddComponent<BoxCollider>();

            var gateObject = new GameObject("MapFuseGateFixture_Gate");
            MapFuseGate gate = gateObject.AddComponent<MapFuseGate>();
            var serializedGate = new SerializedObject(gate);
            serializedGate.FindProperty("fuseBoxPrefab").objectReferenceValue = visualPrefab;
            serializedGate.ApplyModifiedPropertiesWithoutUndo();
            gate.Bind(registry, gameFlow, objectives, miniGame, null);

            return new Fixture(
                mapRoot,
                module,
                anchorObject.transform,
                gameFlowObject,
                objectivesObject,
                miniGameObject,
                visualPrefab,
                gateObject,
                registry,
                gate);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject mapRoot;
            private readonly GeneratedMapModule module;
            private readonly GameObject gameFlowObject;
            private readonly GameObject objectivesObject;
            private readonly GameObject miniGameObject;
            private readonly GameObject visualPrefab;
            private readonly GameObject gateObject;
            private readonly GeneratedMapRegistry registry;

            public Transform Anchor { get; }
            public MapFuseGate Gate { get; }

            public Fixture(
                GameObject mapRoot,
                GeneratedMapModule module,
                Transform anchor,
                GameObject gameFlowObject,
                GameObject objectivesObject,
                GameObject miniGameObject,
                GameObject visualPrefab,
                GameObject gateObject,
                GeneratedMapRegistry registry,
                MapFuseGate gate)
            {
                this.mapRoot = mapRoot;
                this.module = module;
                Anchor = anchor;
                this.gameFlowObject = gameFlowObject;
                this.objectivesObject = objectivesObject;
                this.miniGameObject = miniGameObject;
                this.visualPrefab = visualPrefab;
                this.gateObject = gateObject;
                this.registry = registry;
                Gate = gate;
            }

            public Renderer AddClosedSocket(
                string suffix,
                Vector3 position,
                Vector3 outward,
                bool addRenderer = false)
            {
                Transform socket = NewModuleChild("Socket_" + suffix, position);
                socket.rotation = Quaternion.LookRotation(outward, Vector3.up);

                if (!addRenderer)
                {
                    NewModuleChild("WallCap_" + suffix, position);
                    return null;
                }

                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "WallCap_" + suffix;
                wall.transform.SetParent(module.transform, false);
                wall.transform.position = position;
                wall.transform.rotation = socket.rotation;
                wall.transform.localScale = new Vector3(4f, 3f, 0.2f);
                return wall.GetComponent<Renderer>();
            }

            public void AddPortal(string suffix, Vector3 position) =>
                NewModuleChild("Portal_" + suffix, position);

            public void UseRenderedVisual(Vector3 size)
            {
                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "Visual";
                visual.transform.SetParent(visualPrefab.transform, false);
                visual.transform.localScale = size;
            }

            public void SetMountSettings(
                float height,
                float clearance,
                Vector3 offset,
                Vector3 euler)
            {
                var serializedGate = new SerializedObject(Gate);
                serializedGate.FindProperty("mountHeight").floatValue = height;
                serializedGate.FindProperty("wallClearance").floatValue = clearance;
                serializedGate.FindProperty("wallOffset").vector3Value = offset;
                serializedGate.FindProperty("rotationOffsetEuler").vector3Value = euler;
                serializedGate.ApplyModifiedPropertiesWithoutUndo();
            }

            public void Publish()
            {
                var data = new GeneratedMapRuntimeData(
                    mapRoot,
                    new[] { module },
                    Array.Empty<Transform>(),
                    new[] { Anchor },
                    Array.Empty<Transform>(),
                    Array.Empty<Transform>(),
                    Array.Empty<Transform>(),
                    null,
                    null,
                    null,
                    null,
                    2024,
                    1,
                    1,
                    1,
                    "fuse-wall-test");
                registry.Publish(data);
            }

            public void Dispose()
            {
                registry.Clear();
                UnityEngine.Object.DestroyImmediate(gateObject);
                UnityEngine.Object.DestroyImmediate(visualPrefab);
                UnityEngine.Object.DestroyImmediate(miniGameObject);
                UnityEngine.Object.DestroyImmediate(objectivesObject);
                UnityEngine.Object.DestroyImmediate(gameFlowObject);
                UnityEngine.Object.DestroyImmediate(mapRoot);
            }

            private Transform NewModuleChild(string name, Vector3 position)
            {
                var child = new GameObject(name);
                child.transform.SetParent(module.transform, false);
                child.transform.position = position;
                return child.transform;
            }
        }
    }
}
