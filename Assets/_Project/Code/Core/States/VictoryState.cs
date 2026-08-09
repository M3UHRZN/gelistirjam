using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>
    /// Terminal state: the player escaped (TGD section 4). Loads the Lobby scene, where the same
    /// exit-door prefab sits facing the player — interacting with it there starts a fresh run on a
    /// new random seed (see Gloomveil.MapGeneration.MapGenerationBootstrap.NextRunSeed).
    /// </summary>
    public class VictoryState : IGameState
    {
        private const string LobbySceneName = "Lobby";

        private readonly GameFlowController flow;

        public VictoryState(GameFlowController flow) => this.flow = flow;

        public void Enter()
        {
            flow.SetPlayerControlEnabled(false);
            SceneManager.LoadScene(LobbySceneName);
        }

        public void Tick(float deltaTime) { }

        public void Exit() { }
    }
}
