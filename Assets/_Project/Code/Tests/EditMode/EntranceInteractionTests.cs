using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Gameplay.Entrance;
using Gloomveil.MapGeneration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Gameplay.Tests
{
    public sealed class EntranceInteractionTests
    {
        private GameObject portalObject;
        private GameObject trialButtonObject;
        private GameObject fallbackTrialControllerObject;
        private GameObject injectedTrialControllerObject;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(portalObject);
            Object.DestroyImmediate(trialButtonObject);
            Object.DestroyImmediate(fallbackTrialControllerObject);
            Object.DestroyImmediate(injectedTrialControllerObject);
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
    }
}
