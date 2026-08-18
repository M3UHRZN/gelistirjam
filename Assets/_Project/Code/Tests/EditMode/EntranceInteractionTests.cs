using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Gameplay.Entrance;
using Game.Gameplay.Monster;
using Game.Gameplay.Player;
using Gloomveil.MapGeneration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Game.Gameplay.Tests
{
    public sealed class EntranceInteractionTests
    {
        private GameObject portalObject;
        private GameObject trialButtonObject;
        private GameObject fallbackTrialControllerObject;
        private GameObject injectedTrialControllerObject;
        private GameObject trialSetupRoot;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(portalObject);
            Object.DestroyImmediate(trialButtonObject);
            Object.DestroyImmediate(fallbackTrialControllerObject);
            Object.DestroyImmediate(injectedTrialControllerObject);
            Object.DestroyImmediate(trialSetupRoot);
            MapGenerationBootstrap.NextRunSeed = null;
        }

        [Test]
        public void TryLoad_AssignsSeedAndLoadsDestinationOnlyOnce()
        {
            portalObject = new GameObject("Entrance Portal");
            var portal = portalObject.AddComponent<EntranceScenePortal>();
            var loader = new RecordingSceneLoader();
            portal.Bind("MainGame", loader, () => 24680);

            Assert.That(portal.TryLoad(), Is.True);
            Assert.That(MapGenerationBootstrap.NextRunSeed, Is.EqualTo(24680));
            Assert.That(loader.LoadedScenes, Is.EqualTo(new[] { "MainGame" }));

            Assert.That(portal.TryLoad(), Is.False);
            Assert.That(loader.LoadedScenes, Is.EqualTo(new[] { "MainGame" }));
        }

        [Test]
        public void TrialButton_DefaultPromptAndReadyState_ForwardStartOnce()
        {
            trialButtonObject = new GameObject("DON'T Trial Button");
            trialButtonObject.AddComponent<BoxCollider>();
            var button = trialButtonObject.AddComponent<EntranceMonsterTrialButton>();
            var starter = new RecordingTrialStarter();
            button.Bind(starter);

            Assert.That(button.InteractionPrompt, Is.EqualTo("DON'T"));
            Assert.That(button.CanInteract, Is.True);

            button.BeginInteract();

            Assert.That(starter.StartCalls, Is.EqualTo(1));
            Assert.That(starter.State, Is.EqualTo(EntranceMonsterTrialState.Approaching));
            Assert.That(button.CanInteract, Is.False);

            button.BeginInteract();
            Assert.That(starter.StartCalls, Is.EqualTo(1));
        }

        [Test]
        public void TrialButton_CompletedTrial_CannotInteractOrStart()
        {
            trialButtonObject = new GameObject("Completed Trial Button");
            trialButtonObject.AddComponent<BoxCollider>();
            var button = trialButtonObject.AddComponent<EntranceMonsterTrialButton>();
            var starter = new RecordingTrialStarter(EntranceMonsterTrialState.Completed);
            button.Bind(starter);

            Assert.That(button.CanInteract, Is.False);

            button.BeginInteract();
            Assert.That(starter.StartCalls, Is.Zero);
        }

        [Test]
        public void TrialButton_DestroyedInjectedUnityStarter_DoesNotFallBackToController()
        {
            trialButtonObject = new GameObject("Trial Button With Fallback");
            trialButtonObject.AddComponent<BoxCollider>();
            var button = trialButtonObject.AddComponent<EntranceMonsterTrialButton>();

            fallbackTrialControllerObject = new GameObject("Fallback Trial Controller");
            var fallback = fallbackTrialControllerObject.AddComponent<EntranceMonsterTrialController>();
            SetPrivateField(button, "controller", fallback);
            button.Bind(null);
            Assert.That(button.CanInteract, Is.True);

            injectedTrialControllerObject = new GameObject("Injected Trial Controller");
            var injected = injectedTrialControllerObject.AddComponent<EntranceMonsterTrialController>();
            button.Bind(injected);
            Object.DestroyImmediate(injectedTrialControllerObject);

            Assert.That(button.CanInteract, Is.False);
            button.BeginInteract();
            Assert.That(fallback.State, Is.EqualTo(EntranceMonsterTrialState.Ready));
        }

        [Test]
        public void TrialButton_DestroyedSerializedController_CannotInteract()
        {
            trialButtonObject = new GameObject("Trial Button With Destroyed Controller");
            trialButtonObject.AddComponent<BoxCollider>();
            var button = trialButtonObject.AddComponent<EntranceMonsterTrialButton>();

            fallbackTrialControllerObject = new GameObject("Serialized Trial Controller");
            var controller = fallbackTrialControllerObject.AddComponent<EntranceMonsterTrialController>();
            SetPrivateField(button, "controller", controller);
            Object.DestroyImmediate(fallbackTrialControllerObject);

            Assert.That(button.CanInteract, Is.False);
            button.BeginInteract();
        }

        [Test]
        public void TrialController_InactiveCoordinator_TryStartAbortsToReady()
        {
            fallbackTrialControllerObject = new GameObject("Inactive Trial Coordinator");
            var controller = fallbackTrialControllerObject.AddComponent<EntranceMonsterTrialController>();
            fallbackTrialControllerObject.SetActive(false);
            LogAssert.Expect(
                LogType.Error,
                new Regex("Entrance monster trial setup failed and returned to Ready.*coordinator must be active and enabled"));

            Assert.That(controller.TryStart(), Is.False);
            Assert.That(controller.State, Is.EqualTo(EntranceMonsterTrialState.Ready));
        }

        [Test]
        public void TrialController_SimultaneousGazeAndContact_ChoosesRetreat()
        {
            MethodInfo arbitrateApproach = typeof(EntranceMonsterTrialController).GetMethod(
                "ArbitrateApproach",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(arbitrateApproach, Is.Not.Null, "Expected a pure approach-arbitration seam.");
            Assert.That(
                arbitrateApproach.Invoke(null, new object[] { true, true }),
                Is.EqualTo(EntranceMonsterTrialState.Retreating));
        }

        [Test]
        public void TrialController_ProcessesTrialAfterComponentUpdates()
        {
            const BindingFlags declaredInstanceMethods =
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            Assert.That(
                typeof(EntranceMonsterTrialController).GetMethod("Update", declaredInstanceMethods),
                Is.Null,
                "Trial arbitration must not run in the unordered Update phase.");
            Assert.That(
                typeof(EntranceMonsterTrialController).GetMethod("LateUpdate", declaredInstanceMethods),
                Is.Not.Null,
                "LateUpdate must consume PlayerGazeDetector.CurrentTarget after its Update completes.");
        }

        [Test]
        public void SceneResetter_InactiveRunner_DoesNotDisablePlayerOrLatchReset()
        {
            trialSetupRoot = new GameObject("Inactive Resetter Test Root");
            PlayerController player = CreatePlayer(trialSetupRoot.transform);
            var resetterObject = new GameObject("Inactive Scene Resetter");
            resetterObject.transform.SetParent(trialSetupRoot.transform);
            var resetter = resetterObject.AddComponent<EntranceSceneResetter>();
            resetter.Bind(player, null, new RecordingSceneLoader());
            resetterObject.SetActive(false);

            bool beganReset = resetter.BeginReset();

            Assert.That(beganReset, Is.False);
            Assert.That(player.enabled, Is.True);
            Assert.That(ReadIsResetting(resetter), Is.False);
        }

        [Test]
        public void TrialController_InactiveSceneResetter_TryStartAbortsBeforeNavMeshSetup()
        {
            EntranceMonsterTrialController controller = CreateTrialControllerRig(
                out _,
                out GameObject monsterRoot,
                out EntranceDoorActuator corridorDoor,
                out EntranceSceneResetter resetter);
            resetter.gameObject.SetActive(false);
            LogAssert.Expect(
                LogType.Error,
                new Regex("Entrance monster trial setup failed and returned to Ready.*scene resetter must be active and enabled"));

            Assert.That(controller.TryStart(), Is.False);
            Assert.That(controller.State, Is.EqualTo(EntranceMonsterTrialState.Ready));
            Assert.That(monsterRoot.activeSelf, Is.False);
            Assert.That(corridorDoor.IsOpen, Is.False);
        }

        [Test]
        public void TrialController_ResetterBecomesInactiveOnCatch_FailsClosedAndRestoresPlayer()
        {
            EntranceMonsterTrialController controller = CreateTrialControllerRig(
                out PlayerController player,
                out GameObject monsterRoot,
                out EntranceDoorActuator corridorDoor,
                out EntranceSceneResetter resetter);
            EntranceMonsterTrialStateMachine stateMachine = GetPrivateField<EntranceMonsterTrialStateMachine>(
                controller,
                "stateMachine");
            Assert.That(stateMachine.TryBegin(), Is.True);
            monsterRoot.SetActive(true);
            corridorDoor.SetOpen(true, true);
            resetter.gameObject.SetActive(false);
            LogAssert.Expect(
                LogType.Error,
                new Regex("Entrance monster trial caught reset could not start.*player control remains enabled"));

            InvokePrivateMethod(controller, "CatchPlayer");

            Assert.That(controller.State, Is.EqualTo(EntranceMonsterTrialState.PlayerCaught));
            Assert.That(player.enabled, Is.True);
            Assert.That(monsterRoot.activeSelf, Is.False);
            Assert.That(corridorDoor.IsOpen, Is.False);
            Assert.That(ReadIsResetting(resetter), Is.False);
        }

        private sealed class RecordingSceneLoader : IEntranceSceneLoader
        {
            public List<string> LoadedScenes { get; } = new List<string>();

            public void Load(string sceneName)
            {
                LoadedScenes.Add(sceneName);
            }
        }

        private sealed class RecordingTrialStarter : IEntranceMonsterTrialStarter
        {
            public RecordingTrialStarter(EntranceMonsterTrialState state = EntranceMonsterTrialState.Ready)
            {
                State = state;
            }

            public EntranceMonsterTrialState State { get; private set; }
            public int StartCalls { get; private set; }

            public bool TryStart()
            {
                StartCalls++;
                if (State != EntranceMonsterTrialState.Ready) return false;

                State = EntranceMonsterTrialState.Approaching;
                return true;
            }
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private EntranceMonsterTrialController CreateTrialControllerRig(
            out PlayerController player,
            out GameObject monsterRoot,
            out EntranceDoorActuator corridorDoor,
            out EntranceSceneResetter resetter)
        {
            trialSetupRoot = new GameObject("Trial Controller Rig");

            var controllerObject = new GameObject("Trial Coordinator");
            controllerObject.transform.SetParent(trialSetupRoot.transform);
            var controller = controllerObject.AddComponent<EntranceMonsterTrialController>();

            player = CreatePlayer(trialSetupRoot.transform);

            var gazeObject = new GameObject("Player Gaze Camera");
            gazeObject.transform.SetParent(trialSetupRoot.transform);
            gazeObject.AddComponent<Camera>();
            var gazeDetector = gazeObject.AddComponent<PlayerGazeDetector>();

            monsterRoot = new GameObject("Lightweight Monster");
            monsterRoot.transform.SetParent(trialSetupRoot.transform);
            var agent = monsterRoot.AddComponent<NavMeshAgent>();
            var gazeReceiver = monsterRoot.AddComponent<MonsterGazeReceiver>();

            var startPoint = new GameObject("Start Point").transform;
            startPoint.SetParent(trialSetupRoot.transform);
            var retreatPoint = new GameObject("Retreat Point").transform;
            retreatPoint.SetParent(trialSetupRoot.transform);

            var doorObject = new GameObject("Corridor Door");
            doorObject.transform.SetParent(trialSetupRoot.transform);
            corridorDoor = doorObject.AddComponent<EntranceDoorActuator>();
            corridorDoor.Bind(doorObject.transform, Vector3.right, Vector3.zero, 0f, null);

            var resetterObject = new GameObject("Scene Resetter");
            resetterObject.transform.SetParent(trialSetupRoot.transform);
            resetter = resetterObject.AddComponent<EntranceSceneResetter>();
            resetter.Bind(player, null, new RecordingSceneLoader());

            SetPrivateField(controller, "player", player);
            SetPrivateField(controller, "gazeDetector", gazeDetector);
            SetPrivateField(controller, "gazeReceiver", gazeReceiver);
            SetPrivateField(controller, "agent", agent);
            SetPrivateField(controller, "monsterRoot", monsterRoot);
            SetPrivateField(controller, "startPoint", startPoint);
            SetPrivateField(controller, "retreatPoint", retreatPoint);
            SetPrivateField(controller, "corridorDoor", corridorDoor);
            SetPrivateField(controller, "sceneResetter", resetter);
            return controller;
        }

        private static PlayerController CreatePlayer(Transform parent)
        {
            var playerObject = new GameObject("Player");
            playerObject.transform.SetParent(parent);
            playerObject.SetActive(false);
            var player = playerObject.AddComponent<PlayerController>();
            SetPrivateField(player, "manageCursor", false);
            playerObject.SetActive(true);
            return player;
        }

        private static bool ReadIsResetting(EntranceSceneResetter resetter)
        {
            PropertyInfo property = typeof(EntranceSceneResetter).GetProperty("IsResetting");
            Assert.That(property, Is.Not.Null, "Expected resetter status to distinguish a running reset.");
            return (bool)property.GetValue(resetter);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}' on {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Expected private method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, null);
        }
    }
}
