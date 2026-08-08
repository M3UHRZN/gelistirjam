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
    }
}
