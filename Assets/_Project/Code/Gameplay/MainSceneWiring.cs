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
    /// MapExitController (not the hand-built-scene ExitController), and no Monster yet. Anger/
    /// fuse-awareness wiring for MonsterBrain is deliberately left for whenever a monster spawner
    /// exists — adding it here now would mean guessing an integration that does not exist yet (out of
    /// scope by explicit instruction). LightDirector is constructed here and exposed publicly so
    /// MapLightGate (a MapReady-driven spawner, same pattern as MapFuseGate/MapExitController) can
    /// hand it a real, seeded shutdown order once the generated map's LightAnchors exist — see
    /// MapLightGate.cs.
    /// </summary>
    public class MainSceneWiring : MonoBehaviour
    {
        [SerializeField] private GameFlowController gameFlow;
        [SerializeField] private ObjectiveManager objectiveManager;
        [SerializeField] private MapExitController mapExit;
        [SerializeField] private FuseMiniGameController fuseMiniGame;
        [SerializeField, Tooltip("Target percentage of the non-SafeZone LightZone pool still active after fuse 1, 2, 3, ... (TGD section 32). The last entry is reused for any fuse beyond the array's length.")]
        private float[] shutdownTargetPercents = { 75f, 45f, 15f };

        private readonly LightDirector lightDirector = new LightDirector();
        private bool wired;

        public LightDirector LightDirector => lightDirector;

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

            objectiveManager.OnAnyFuseCompleted += lightDirector.OnFuseCompleted;
            lightDirector.SetShutdownTargetPercents(shutdownTargetPercents);

            // All fuses complete -> open the exit -> MapExitController.Open() itself enters
            // FinalChaseState (TGD section 37/38) -- not duplicated here.
            objectiveManager.OnAllComplete += mapExit.Open;

            // Final Chase (TGD section 38): kill the remaining lights. Mirrors SceneWiring's
            // FinalChase handler minus the Monster-specific lines (IsRelentless/AggressiveState) —
            // there is no Monster in the main scene yet.
            gameFlow.OnStateChanged += stateName =>
            {
                if (stateName != nameof(FinalChaseState)) return;
                lightDirector.ShutdownAll();
            };

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
