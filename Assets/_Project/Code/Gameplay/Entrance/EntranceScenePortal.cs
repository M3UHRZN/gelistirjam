using System;
using Game.Gameplay.Player;
using Gloomveil.MapGeneration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Gameplay.Entrance
{
    public interface IEntranceSceneLoader
    {
        void Load(string sceneName);
    }

    internal sealed class UnityEntranceSceneLoader : IEntranceSceneLoader
    {
        public void Load(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    public sealed class EntranceScenePortal : MonoBehaviour
    {
        [SerializeField] private string destinationSceneName = "MainGame";

        private IEntranceSceneLoader sceneLoader;
        private Func<int> seedSource;
        private bool hasLoaded;

        private void Awake()
        {
            EnsureDependencies();
        }

        public void Bind(string destinationScene, IEntranceSceneLoader loader, Func<int> nextSeed)
        {
            destinationSceneName = destinationScene;
            sceneLoader = loader;
            seedSource = nextSeed;
        }

        public bool TryLoad()
        {
            if (hasLoaded) return false;

            EnsureDependencies();
            hasLoaded = true;
            MapGenerationBootstrap.NextRunSeed = seedSource();
            sceneLoader.Load(destinationSceneName);
            return true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerController>() == null) return;
            TryLoad();
        }

        private void EnsureDependencies()
        {
            sceneLoader ??= new UnityEntranceSceneLoader();
            seedSource ??= DefaultSeed;
        }

        private static int DefaultSeed() => UnityEngine.Random.Range(int.MinValue, int.MaxValue);
    }
}
