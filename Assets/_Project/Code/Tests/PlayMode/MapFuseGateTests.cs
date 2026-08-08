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
            gate.Bind(registry, gameFlow, objectives, miniGame);

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

            public void AddClosedSocket(string suffix, Vector3 position, Vector3 outward)
            {
                Transform socket = NewModuleChild("Socket_" + suffix, position);
                socket.rotation = Quaternion.LookRotation(outward, Vector3.up);
                NewModuleChild("WallCap_" + suffix, position);
            }

            public void AddPortal(string suffix, Vector3 position) =>
                NewModuleChild("Portal_" + suffix, position);

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
