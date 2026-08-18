using System.Collections;
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
using UnityEngine.Events;
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

            EntranceResetStartResult result = resetter.TryBeginReset(player, null);

            Assert.That(result, Is.EqualTo(EntranceResetStartResult.Unavailable));
            Assert.That(player.enabled, Is.True);
            Assert.That(ReadIsResetting(resetter), Is.False);
        }

        [Test]
        public void SceneResetter_MissingBoundPlayer_DoesNotStartOrLatchReset()
        {
            trialSetupRoot = new GameObject("Missing Player Resetter Test Root");
            var resetter = trialSetupRoot.AddComponent<EntranceSceneResetter>();
            resetter.Bind(null, null, new RecordingSceneLoader());

            Assert.That(resetter.BeginReset(), Is.False);
            Assert.That(ReadIsResetting(resetter), Is.False);
        }

        [Test]
        public void SceneResetter_MissingBoundPlayer_AtomicStartReturnsMissingPlayer()
        {
            trialSetupRoot = new GameObject("Missing Bound Player Atomic Reset Root");
            PlayerController requiredPlayer = CreatePlayer(trialSetupRoot.transform);
            var runner = new RecordingRoutineRunner();
            EntranceSceneResetter resetter = CreateResetter(null, new RecordingSceneLoader(), runner);

            EntranceResetStartResult result = resetter.TryBeginReset(requiredPlayer, null);

            Assert.That(result, Is.EqualTo(EntranceResetStartResult.MissingPlayer));
            Assert.That(requiredPlayer.enabled, Is.True);
            Assert.That(resetter.IsResetting, Is.False);
            Assert.That(runner.StartCalls, Is.Zero);
        }

        [Test]
        public void SceneResetter_MismatchedPlayer_AtomicStartReturnsPlayerMismatch()
        {
            trialSetupRoot = new GameObject("Mismatched Player Atomic Reset Root");
            PlayerController boundPlayer = CreatePlayer(trialSetupRoot.transform);
            PlayerController requiredPlayer = CreatePlayer(trialSetupRoot.transform);
            var runner = new RecordingRoutineRunner();
            EntranceSceneResetter resetter = CreateResetter(boundPlayer, new RecordingSceneLoader(), runner);

            EntranceResetStartResult result = resetter.TryBeginReset(requiredPlayer, null);

            Assert.That(result, Is.EqualTo(EntranceResetStartResult.PlayerMismatch));
            Assert.That(boundPlayer.enabled, Is.True);
            Assert.That(requiredPlayer.enabled, Is.True);
            Assert.That(resetter.IsResetting, Is.False);
            Assert.That(runner.StartCalls, Is.Zero);
        }

        [Test]
        public void SceneResetter_LiveSamePlayerSecondRequest_JoinsExistingTransaction()
        {
            trialSetupRoot = new GameObject("Same Player Reset Join Root");
            PlayerController player = CreatePlayer(trialSetupRoot.transform);
            var runner = new RecordingRoutineRunner();
            EntranceSceneResetter resetter = CreateResetter(player, new RecordingSceneLoader(), runner);
            var firstInterruptions = new List<EntranceResetInterruptionReason>();
            var joinedInterruptions = new List<EntranceResetInterruptionReason>();

            EntranceResetStartResult first = resetter.TryBeginReset(player, firstInterruptions.Add);
            EntranceResetStartResult second = resetter.TryBeginReset(player, joinedInterruptions.Add);

            Assert.That(first, Is.EqualTo(EntranceResetStartResult.Started));
            Assert.That(second, Is.EqualTo(EntranceResetStartResult.AlreadyRunning));
            Assert.That(player.enabled, Is.False);
            Assert.That(resetter.IsResetting, Is.True);
            Assert.That(runner.StartCalls, Is.EqualTo(1));

            InvokePrivateMethod(resetter, "OnDisable");
            Assert.That(firstInterruptions, Is.EqualTo(new[] { EntranceResetInterruptionReason.Disabled }));
            Assert.That(joinedInterruptions, Is.EqualTo(new[] { EntranceResetInterruptionReason.Disabled }));
        }

        [Test]
        public void SceneResetter_LiveDifferentPlayerRequest_ReturnsPlayerMismatch()
        {
            trialSetupRoot = new GameObject("Live Different Player Reset Root");
            PlayerController owner = CreatePlayer(trialSetupRoot.transform);
            PlayerController differentPlayer = CreatePlayer(trialSetupRoot.transform);
            var runner = new RecordingRoutineRunner();
            EntranceSceneResetter resetter = CreateResetter(owner, new RecordingSceneLoader(), runner);
            Assert.That(resetter.TryBeginReset(owner, null), Is.EqualTo(EntranceResetStartResult.Started));

            EntranceResetStartResult result = resetter.TryBeginReset(differentPlayer, null);

            Assert.That(result, Is.EqualTo(EntranceResetStartResult.PlayerMismatch));
            Assert.That(owner.enabled, Is.False);
            Assert.That(differentPlayer.enabled, Is.True);
            Assert.That(resetter.IsResetting, Is.True);
            Assert.That(runner.StartCalls, Is.EqualTo(1));
        }

        [Test]
        public void SceneResetter_StartBoundary_SeesLatchedTransactionAndDisabledPlayer()
        {
            trialSetupRoot = new GameObject("Atomic Start Order Reset Root");
            PlayerController player = CreatePlayer(trialSetupRoot.transform);
            var runner = new RecordingRoutineRunner();
            EntranceSceneResetter resetter = CreateResetter(player, new RecordingSceneLoader(), runner);
            bool sawLatchedTransaction = false;
            bool sawDisabledPlayer = false;
            runner.OnTryStart = () =>
            {
                sawLatchedTransaction = resetter.IsResetting;
                sawDisabledPlayer = !player.enabled;
            };

            Assert.That(resetter.TryBeginReset(player, null), Is.EqualTo(EntranceResetStartResult.Started));

            Assert.That(sawLatchedTransaction, Is.True);
            Assert.That(sawDisabledPlayer, Is.True);
        }

        [Test]
        public void SceneResetter_RoutineStartFailure_RollsBackAndInvokesInterruptionOnce()
        {
            trialSetupRoot = new GameObject("Routine Start Failure Reset Root");
            PlayerController player = CreatePlayer(trialSetupRoot.transform);
            var runner = new RecordingRoutineRunner { ShouldStart = false };
            EntranceSceneResetter resetter = CreateResetter(player, new RecordingSceneLoader(), runner);
            var interruptions = new List<EntranceResetInterruptionReason>();

            EntranceResetStartResult result = resetter.TryBeginReset(player, interruptions.Add);

            Assert.That(result, Is.EqualTo(EntranceResetStartResult.Unavailable));
            Assert.That(player.enabled, Is.True);
            Assert.That(resetter.IsResetting, Is.False);
            Assert.That(interruptions, Is.EqualTo(new[] { EntranceResetInterruptionReason.RoutineStartFailed }));
            Assert.That(runner.StartCalls, Is.EqualTo(1));
        }

        [Test]
        public void SceneResetter_DisabledAfterStart_InterruptsOnceAndAllowsRestartAfterEnable()
        {
            trialSetupRoot = new GameObject("Disabled Live Reset Root");
            PlayerController player = CreatePlayer(trialSetupRoot.transform);
            var runner = new RecordingRoutineRunner();
            EntranceSceneResetter resetter = CreateResetter(player, new RecordingSceneLoader(), runner);
            var interruptions = new List<EntranceResetInterruptionReason>();
            Assert.That(
                resetter.TryBeginReset(player, interruptions.Add),
                Is.EqualTo(EntranceResetStartResult.Started));

            resetter.enabled = false;
            InvokePrivateMethod(resetter, "OnDisable");

            Assert.That(player.enabled, Is.True);
            Assert.That(resetter.IsResetting, Is.False);
            Assert.That(interruptions, Is.EqualTo(new[] { EntranceResetInterruptionReason.Disabled }));

            resetter.enabled = true;
            Assert.That(resetter.TryBeginReset(player, null), Is.EqualTo(EntranceResetStartResult.Started));
            Assert.That(player.enabled, Is.False);
            Assert.That(runner.StartCalls, Is.EqualTo(2));
        }

        [Test]
        public void SceneResetter_DestroyedAfterStart_InterruptionIsIdempotent()
        {
            trialSetupRoot = new GameObject("Destroyed Live Reset Root");
            PlayerController player = CreatePlayer(trialSetupRoot.transform);
            var runner = new RecordingRoutineRunner();
            EntranceSceneResetter resetter = CreateResetter(player, new RecordingSceneLoader(), runner);
            var interruptions = new List<EntranceResetInterruptionReason>();
            Assert.That(
                resetter.TryBeginReset(player, interruptions.Add),
                Is.EqualTo(EntranceResetStartResult.Started));

            InvokePrivateMethod(resetter, "OnDestroy");
            InvokePrivateMethod(resetter, "OnDestroy");

            Assert.That(player.enabled, Is.True);
            Assert.That(resetter.IsResetting, Is.False);
            Assert.That(interruptions, Is.EqualTo(new[] { EntranceResetInterruptionReason.Destroyed }));
        }

        [Test]
        public void SceneResetter_LoaderFailure_RollsBackAndInvokesInterruptionOnce()
        {
            trialSetupRoot = new GameObject("Loader Failure Reset Root");
            PlayerController player = CreatePlayer(trialSetupRoot.transform);
            var runner = new RecordingRoutineRunner();
            var loader = new ThrowingSceneLoader();
            EntranceSceneResetter resetter = CreateResetter(player, loader, runner);
            var interruptions = new List<EntranceResetInterruptionReason>();
            Assert.That(
                resetter.TryBeginReset(player, interruptions.Add),
                Is.EqualTo(EntranceResetStartResult.Started));

            Assert.DoesNotThrow(runner.RunToCompletion);

            Assert.That(loader.LoadCalls, Is.EqualTo(1));
            Assert.That(player.enabled, Is.True);
            Assert.That(resetter.IsResetting, Is.False);
            Assert.That(interruptions, Is.EqualTo(new[] { EntranceResetInterruptionReason.SceneLoadFailed }));
        }

        [Test]
        public void SceneResetter_SuccessfulLoad_IgnoresUnloadDisableAndClearsWithoutRollback()
        {
            trialSetupRoot = new GameObject("Successful Loader Reset Root");
            PlayerController player = CreatePlayer(trialSetupRoot.transform);
            var runner = new RecordingRoutineRunner();
            var loader = new CallbackSceneLoader();
            EntranceSceneResetter resetter = CreateResetter(player, loader, runner);
            var interruptions = new List<EntranceResetInterruptionReason>();
            loader.OnLoad = _ =>
            {
                resetter.enabled = false;
                InvokePrivateMethod(resetter, "OnDisable");
            };
            Assert.That(
                resetter.TryBeginReset(player, interruptions.Add),
                Is.EqualTo(EntranceResetStartResult.Started));

            runner.RunToCompletion();

            Assert.That(loader.LoadCalls, Is.EqualTo(1));
            Assert.That(player.enabled, Is.False);
            Assert.That(resetter.IsResetting, Is.False);
            Assert.That(interruptions, Is.Empty);
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
        public void TrialController_MismatchedSceneResetter_TryStartAbortsBeforeNavMeshSetup()
        {
            EntranceMonsterTrialController controller = CreateTrialControllerRig(
                out _,
                out GameObject monsterRoot,
                out EntranceDoorActuator corridorDoor,
                out EntranceSceneResetter resetter);
            PlayerController differentPlayer = CreatePlayer(trialSetupRoot.transform);
            resetter.Bind(differentPlayer, null, new RecordingSceneLoader());
            LogAssert.Expect(
                LogType.Error,
                new Regex("Entrance monster trial setup failed and returned to Ready.*same player"));

            Assert.That(controller.TryStart(), Is.False);
            Assert.That(controller.State, Is.EqualTo(EntranceMonsterTrialState.Ready));
            Assert.That(monsterRoot.activeSelf, Is.False);
            Assert.That(corridorDoor.IsOpen, Is.False);
        }

        [Test]
        public void TrialController_MissingBoundPlayer_TryStartAbortsBeforeNavMeshSetup()
        {
            EntranceMonsterTrialController controller = CreateTrialControllerRig(
                out _,
                out GameObject monsterRoot,
                out EntranceDoorActuator corridorDoor,
                out EntranceSceneResetter resetter);
            resetter.Bind(null, null, new RecordingSceneLoader());
            LogAssert.Expect(
                LogType.Error,
                new Regex("Entrance monster trial setup failed and returned to Ready.*bound player"));

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

        [Test]
        public void TrialController_DestroyedResetterBeforeCatch_FailsClosedWithoutDereference()
        {
            EntranceMonsterTrialController controller = CreateTrialControllerRig(
                out PlayerController player,
                out GameObject monsterRoot,
                out EntranceDoorActuator corridorDoor,
                out EntranceSceneResetter resetter);
            PrepareControllerForCatch(controller, monsterRoot, corridorDoor);
            Object.DestroyImmediate(resetter.gameObject);
            LogAssert.Expect(
                LogType.Error,
                new Regex("Entrance monster trial caught reset could not start.*player control remains enabled"));

            Assert.DoesNotThrow(() => InvokePrivateMethod(controller, "CatchPlayer"));

            Assert.That(controller.State, Is.EqualTo(EntranceMonsterTrialState.PlayerCaught));
            Assert.That(player.enabled, Is.True);
            Assert.That(monsterRoot.activeSelf, Is.False);
            Assert.That(corridorDoor.IsOpen, Is.False);
        }

        [Test]
        public void TrialController_OnCaughtDestroysLiveResetter_InterruptionFailsClosedOnce()
        {
            var runner = new RecordingRoutineRunner();
            EntranceMonsterTrialController controller = CreateTrialControllerRig(
                out PlayerController player,
                out GameObject monsterRoot,
                out EntranceDoorActuator corridorDoor,
                out EntranceSceneResetter resetter,
                runner);
            PrepareControllerForCatch(controller, monsterRoot, corridorDoor);
            var caughtEvent = new UnityEvent();
            caughtEvent.AddListener(() =>
            {
                InvokePrivateMethod(resetter, "OnDestroy");
                Object.DestroyImmediate(resetter.gameObject);
            });
            SetPrivateField(controller, "onCaught", caughtEvent);
            LogAssert.Expect(
                LogType.Error,
                new Regex("Entrance monster trial caught reset could not start.*player control remains enabled"));

            Assert.DoesNotThrow(() => InvokePrivateMethod(controller, "CatchPlayer"));

            Assert.That(runner.StartCalls, Is.EqualTo(1));
            Assert.That(player.enabled, Is.True);
            Assert.That(monsterRoot.activeSelf, Is.False);
            Assert.That(corridorDoor.IsOpen, Is.False);
        }

        [Test]
        public void TrialController_OnCaughtDisablesLiveResetter_InterruptionFailsClosedOnce()
        {
            var runner = new RecordingRoutineRunner();
            EntranceMonsterTrialController controller = CreateTrialControllerRig(
                out PlayerController player,
                out GameObject monsterRoot,
                out EntranceDoorActuator corridorDoor,
                out EntranceSceneResetter resetter,
                runner);
            PrepareControllerForCatch(controller, monsterRoot, corridorDoor);
            var caughtEvent = new UnityEvent();
            caughtEvent.AddListener(() =>
            {
                resetter.enabled = false;
                InvokePrivateMethod(resetter, "OnDisable");
            });
            SetPrivateField(controller, "onCaught", caughtEvent);
            LogAssert.Expect(
                LogType.Error,
                new Regex("Entrance monster trial caught reset could not start.*player control remains enabled"));

            InvokePrivateMethod(controller, "CatchPlayer");

            Assert.That(runner.StartCalls, Is.EqualTo(1));
            Assert.That(player.enabled, Is.True);
            Assert.That(monsterRoot.activeSelf, Is.False);
            Assert.That(corridorDoor.IsOpen, Is.False);
        }

        [Test]
        public void TrialController_SynchronousRoutineStartFailure_FailsClosedAndLogsOnce()
        {
            var runner = new RecordingRoutineRunner { ShouldStart = false };
            EntranceMonsterTrialController controller = CreateTrialControllerRig(
                out PlayerController player,
                out GameObject monsterRoot,
                out EntranceDoorActuator corridorDoor,
                out _,
                runner);
            PrepareControllerForCatch(controller, monsterRoot, corridorDoor);
            LogAssert.Expect(
                LogType.Error,
                new Regex("Entrance monster trial caught reset could not start.*player control remains enabled"));

            InvokePrivateMethod(controller, "CatchPlayer");

            Assert.That(runner.StartCalls, Is.EqualTo(1));
            Assert.That(player.enabled, Is.True);
            Assert.That(monsterRoot.activeSelf, Is.False);
            Assert.That(corridorDoor.IsOpen, Is.False);
        }

        private sealed class RecordingSceneLoader : IEntranceSceneLoader
        {
            public List<string> LoadedScenes { get; } = new List<string>();

            public void Load(string sceneName)
            {
                LoadedScenes.Add(sceneName);
            }
        }

        private sealed class ThrowingSceneLoader : IEntranceSceneLoader
        {
            public int LoadCalls { get; private set; }

            public void Load(string sceneName)
            {
                LoadCalls++;
                throw new System.InvalidOperationException("Synthetic scene-load failure.");
            }
        }

        private sealed class CallbackSceneLoader : IEntranceSceneLoader
        {
            public System.Action<string> OnLoad { get; set; }
            public int LoadCalls { get; private set; }

            public void Load(string sceneName)
            {
                LoadCalls++;
                OnLoad?.Invoke(sceneName);
            }
        }

        private sealed class RecordingRoutineRunner : IEntranceResetRoutineRunner
        {
            public bool ShouldStart { get; set; } = true;
            public System.Action OnTryStart { get; set; }
            public int StartCalls { get; private set; }
            public IEnumerator CapturedRoutine { get; private set; }

            public bool TryStart(IEnumerator routine)
            {
                StartCalls++;
                CapturedRoutine = routine;
                OnTryStart?.Invoke();
                return ShouldStart;
            }

            public void RunToCompletion()
            {
                Assert.That(CapturedRoutine, Is.Not.Null, "Expected a captured reset routine.");
                for (int step = 0; step < 16; step++)
                {
                    if (!CapturedRoutine.MoveNext()) return;
                }

                Assert.Fail("Reset routine did not complete within 16 synchronous steps.");
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
            out EntranceSceneResetter resetter,
            IEntranceResetRoutineRunner resetRunner = null)
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
            resetter.Bind(player, null, new RecordingSceneLoader(), resetRunner);

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

        private static void PrepareControllerForCatch(
            EntranceMonsterTrialController controller,
            GameObject monsterRoot,
            EntranceDoorActuator corridorDoor)
        {
            EntranceMonsterTrialStateMachine stateMachine = GetPrivateField<EntranceMonsterTrialStateMachine>(
                controller,
                "stateMachine");
            Assert.That(stateMachine.TryBegin(), Is.True);
            monsterRoot.SetActive(true);
            corridorDoor.SetOpen(true, true);
        }

        private EntranceSceneResetter CreateResetter(
            PlayerController player,
            IEntranceSceneLoader loader,
            IEntranceResetRoutineRunner runner)
        {
            trialSetupRoot ??= new GameObject("Entrance Reset Test Root");
            var resetterObject = new GameObject("Scene Resetter");
            resetterObject.transform.SetParent(trialSetupRoot.transform);
            var resetter = resetterObject.AddComponent<EntranceSceneResetter>();
            resetter.Bind(player, null, loader, runner);
            SetPrivateField(resetter, "fadeDuration", 0f);
            SetPrivateField(resetter, "resetDelay", 0f);
            return resetter;
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
