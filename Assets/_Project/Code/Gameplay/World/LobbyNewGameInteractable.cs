using Game.Gameplay.Interaction;
using Gloomveil.MapGeneration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Gameplay.World
{
    /// <summary>
    /// Lives on the exit door prop in the Lobby scene. Always interactable — unlike
    /// MapExitController's in-run exit, there's no fuse gate here. Picks a fresh random seed so
    /// each run gets a different generated map (TGD's escape loop, TGD section 4/37) and loads the
    /// main game scene.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class LobbyNewGameInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string mainGameSceneName = "MainGame";
        [SerializeField] private string interactionPrompt;

        public bool CanInteract => true;
        public string InteractionPrompt => interactionPrompt;

        public void BeginInteract()
        {
            MapGenerationBootstrap.NextRunSeed = Random.Range(int.MinValue, int.MaxValue);
            SceneManager.LoadScene(mainGameSceneName);
        }

        public void EndInteract() { }
    }
}
