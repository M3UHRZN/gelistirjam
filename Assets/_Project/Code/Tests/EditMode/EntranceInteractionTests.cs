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

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(portalObject);
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

        private sealed class RecordingSceneLoader : IEntranceSceneLoader
        {
            public List<string> LoadedScenes { get; } = new List<string>();

            public void Load(string sceneName)
            {
                LoadedScenes.Add(sceneName);
            }
        }
    }
}
