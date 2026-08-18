using System.Collections.Generic;
using Game.Gameplay.Entrance;
using Gloomveil.MapGeneration;
using NUnit.Framework;
using UnityEngine;

namespace Game.Gameplay.Tests
{
    public sealed class EntranceInteractionTests
    {
        private GameObject portalObject;
        private GameObject trialButtonObject;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(portalObject);
            Object.DestroyImmediate(trialButtonObject);
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
    }
}
