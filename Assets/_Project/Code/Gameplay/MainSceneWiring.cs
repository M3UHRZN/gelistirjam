using System;
using Game.Core;
using Game.Gameplay.Objectives;
using Game.Gameplay.World;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Composition-root wiring for the real procedural main scene (see chat: Fuse Memory Minigame
    /// main-scene integration). Mirrors SceneWiring — the MonsterAI_TestScene's composition root —
    /// but adapted to what the main scene actually has: GameFlowController (not a second GameManager),
    /// MapExitController (not the hand-built-scene ExitController), and no Monster or LightZone
    /// spawner yet. Anger/fuse-awareness wiring for MonsterBrain and the LightZone shutdown order are
    /// deliberately left for whenever those systems get their own MapReady-driven spawners — adding
    /// them here now would mean guessing an integration that does not exist yet (out of this task's
    /// scope by explicit instruction). LightDirector is still constructed and wired to
    /// OnAnyFuseCompleted so a later LightZone spawner can hand it a shutdown order without touching
    /// this class again; with an empty order today it is a documented no-op, not a missing connection.
    /// </summary>
    public class MainSceneWiring : MonoBehaviour
    {
        [SerializeField] private GameFlowController gameFlow;
        [SerializeField] private ObjectiveManager objectiveManager;
        [SerializeField] private MapExitController mapExit;
        [SerializeField] private FuseMiniGameController fuseMiniGame;

        [SerializeField, Tooltip("Intentionally empty until a LightZone spawner publishes real zones for the generated map — see class doc comment.")]
        private LightZone[] lightShutdownOrder = Array.Empty<LightZone>();

        private readonly LightDirector lightDirector = new LightDirector();
        private bool wired;

        public void Bind(GameFlowController flow, ObjectiveManager objectives, MapExitController exit,
            FuseMiniGameController miniGame)
        {
            gameFlow = flow;
            objectiveManager = objectives;
            mapExit = exit;
            fuseMiniGame = miniGame;
            Wire();
        }

        // Same Awake+Start double-attempt as SceneWiring — idempotent-guarded, safe either order.
        private void Awake() => Wire();
        private void Start() => Wire();

        private void Wire()
        {
            if (wired) return;
            if (gameFlow == null || objectiveManager == null || mapExit == null || fuseMiniGame == null) return;
            wired = true;

            lightDirector.SetShutdownOrder(lightShutdownOrder ?? Array.Empty<LightZone>());
            objectiveManager.OnAnyFuseCompleted += lightDirector.OnFuseCompleted;

            // All fuses complete -> open the exit -> MapExitController.Open() itself enters
            // FinalChaseState (TGD section 37/38) -- not duplicated here.
            objectiveManager.OnAllComplete += mapExit.Open;

            // Death mid-minigame (prompt section 27, same fix already made for the test scene's
            // SceneWiring — see chat): the Fuse popup has no way to know the player died on its own.
            gameFlow.OnStateChanged += stateName =>
            {
                if (stateName != nameof(DeathState)) return;
                fuseMiniGame.ForceCancel();
            };
        }
    }
}
