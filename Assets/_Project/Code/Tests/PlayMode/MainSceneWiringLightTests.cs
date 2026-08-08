using System.Collections;
using Game.Core;
using Game.Gameplay.Objectives;
using Game.Gameplay.World;
using Gloomveil.MapGeneration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Gameplay.Tests
{
    public sealed class MainSceneWiringLightTests
    {
        [UnityTest]
        public IEnumerator FinalChaseState_ShutsDownEveryRegisteredLightZone()
        {
            var gameFlowObject = new GameObject("GameFlow");
            GameFlowController gameFlow = gameFlowObject.AddComponent<GameFlowController>();

            var objectiveManagerObject = new GameObject("Objectives");
            ObjectiveManager objectiveManager = objectiveManagerObject.AddComponent<ObjectiveManager>();

            var registryObject = new GameObject("Registry");
            GeneratedMapRegistry registry = registryObject.AddComponent<GeneratedMapRegistry>();
            var mapExitObject = new GameObject("MapExit");
            MapExitController mapExit = mapExitObject.AddComponent<MapExitController>();
            mapExit.Bind(registry, gameFlow);

            var fuseMiniGameObject = new GameObject("FuseMiniGame");
            FuseMiniGameController fuseMiniGame = fuseMiniGameObject.AddComponent<FuseMiniGameController>();

            var wiringObject = new GameObject("Wiring");
            MainSceneWiring wiring = wiringObject.AddComponent<MainSceneWiring>();
            wiring.Bind(gameFlow, objectiveManager, mapExit, fuseMiniGame);

            var zoneObject = new GameObject("Zone");
            LightZone zone = zoneObject.AddComponent<LightZone>();
            zone.SetActive(true);
            wiring.LightDirector.SetShutdownOrder(new[] { zone });

            try
            {
                gameFlow.ChangeState(gameFlow.FinalChaseState);

                Assert.That(zone.State, Is.EqualTo(LightZoneState.Disabled));
            }
            finally
            {
                Object.DestroyImmediate(zoneObject);
                Object.DestroyImmediate(wiringObject);
                Object.DestroyImmediate(fuseMiniGameObject);
                Object.DestroyImmediate(mapExitObject);
                Object.DestroyImmediate(registryObject);
                Object.DestroyImmediate(objectiveManagerObject);
                Object.DestroyImmediate(gameFlowObject);
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator Wire_AppliesConfiguredShutdownTargetPercentsToLightDirector()
        {
            var gameFlowObject = new GameObject("GameFlow");
            GameFlowController gameFlow = gameFlowObject.AddComponent<GameFlowController>();

            var objectiveManagerObject = new GameObject("Objectives");
            ObjectiveManager objectiveManager = objectiveManagerObject.AddComponent<ObjectiveManager>();

            var registryObject = new GameObject("Registry");
            GeneratedMapRegistry registry = registryObject.AddComponent<GeneratedMapRegistry>();
            var mapExitObject = new GameObject("MapExit");
            MapExitController mapExit = mapExitObject.AddComponent<MapExitController>();
            mapExit.Bind(registry, gameFlow);

            var fuseMiniGameObject = new GameObject("FuseMiniGame");
            FuseMiniGameController fuseMiniGame = fuseMiniGameObject.AddComponent<FuseMiniGameController>();

            var wiringObject = new GameObject("Wiring");
            MainSceneWiring wiring = wiringObject.AddComponent<MainSceneWiring>();

            // Deliberately different from LightDirector's own built-in default ({75f, 45f, 15f}) so
            // this test can only pass if Wire() actually pushed MainSceneWiring's configured curve
            // into the LightDirector it owns -- not merely that LightDirector fell back to its own
            // default (which would look identical if Wire() never called SetShutdownTargetPercents).
            var distinctCurve = new[] { 20f };
            System.Reflection.FieldInfo field = typeof(MainSceneWiring).GetField(
                "shutdownTargetPercents",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(wiring, distinctCurve);

            wiring.Bind(gameFlow, objectiveManager, mapExit, fuseMiniGame);

            var zoneObjects = new GameObject[10];
            var zones = new LightZone[10];
            for (int i = 0; i < zones.Length; i++)
            {
                zoneObjects[i] = new GameObject($"Zone_{i}");
                zones[i] = zoneObjects[i].AddComponent<LightZone>();
                zones[i].SetActive(true);
            }
            wiring.LightDirector.SetShutdownOrder(zones);

            try
            {
                // Drive LightDirector directly rather than through a real FuseBox/ObjectiveManager
                // completion flow (ObjectiveManager only exposes OnAnyFuseCompleted via a registered
                // FuseBox's own interaction flow — irrelevant to what this test checks). This test's
                // job is only to confirm Wire() applied MainSceneWiring's configured curve to the
                // LightDirector instance it owns, not to re-test fuse completion plumbing (already
                // covered by MapFuseGate/FuseBox's own tests).
                wiring.LightDirector.OnFuseCompleted(); // one fuse -> the distinct 20% curve, not the 75% default

                int activeCount = 0;
                foreach (LightZone zone in zones)
                {
                    if (zone.State == LightZoneState.Active) activeCount++;
                }

                Assert.That(activeCount, Is.EqualTo(2),
                    "configured 20% curve (not LightDirector's own 75% default) -> 2 of 10 active after Fuse 1");
            }
            finally
            {
                foreach (GameObject go in zoneObjects) Object.DestroyImmediate(go);
                Object.DestroyImmediate(wiringObject);
                Object.DestroyImmediate(fuseMiniGameObject);
                Object.DestroyImmediate(mapExitObject);
                Object.DestroyImmediate(registryObject);
                Object.DestroyImmediate(objectiveManagerObject);
                Object.DestroyImmediate(gameFlowObject);
            }

            yield break;
        }
    }
}
