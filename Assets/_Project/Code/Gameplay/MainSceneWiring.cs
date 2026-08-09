using Game.Core;
using Game.Gameplay.Monster;
using Game.Gameplay.Objectives;
using Game.Gameplay.World;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Composition-root wiring for the real procedural main scene (see chat: Fuse Memory Minigame
    /// main-scene integration). Mirrors SceneWiring — the MonsterAI_TestScene's composition root —
    /// but adapted to what the main scene actually has: GameFlowController (not a second GameManager),
    /// MapExitController (not the hand-built-scene ExitController). The generated monster arrives
    /// after MapReady, so its event ownership is replaceable rather than serialized into the scene.
    /// LightDirector is constructed here and exposed publicly so
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
        private MonsterBrain brain;
        private bool wired;

        public LightDirector LightDirector => lightDirector;
        public MonsterBrain BoundMonster => brain;

        public void Bind(GameFlowController flow, ObjectiveManager objectives, MapExitController exit,
            FuseMiniGameController miniGame)
        {
            gameFlow = flow;
            objectiveManager = objectives;
            mapExit = exit;
            fuseMiniGame = miniGame;
            Wire();
        }

        public void BindMonster(MonsterBrain monsterBrain)
        {
            if (ReferenceEquals(brain, monsterBrain)) return;
            if (brain != null) brain.OnKill -= HandleMonsterKill;

            brain = monsterBrain;
            if (brain == null) return;

            brain.OnKill += HandleMonsterKill;
            ApplyCurrentStateToMonster();
        }

        public void UnbindMonster(MonsterBrain monsterBrain)
        {
            if (!ReferenceEquals(brain, monsterBrain)) return;

            brain.OnKill -= HandleMonsterKill;
            brain = null;
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
            objectiveManager.OnFuseInteractionStarted += HandleFuseStarted;
            objectiveManager.OnFuseInteractionStopped += HandleFuseStopped;

            gameFlow.OnStateChanged += HandleGameStateChanged;
        }

        private void HandleMonsterKill()
        {
            if (gameFlow != null) gameFlow.ChangeState(gameFlow.DeathState);
        }

        private void HandleFuseStarted(Vector3 position) =>
            brain?.SetFuseAwareness(true, position);

        private void HandleFuseStopped() =>
            brain?.SetFuseAwareness(false, Vector3.zero);

        private void HandleGameStateChanged(string stateName)
        {
            if (stateName == nameof(FinalChaseState))
            {
                lightDirector.ShutdownAll();
                ApplyCurrentStateToMonster();
            }

            // Death mid-minigame (prompt section 27, same fix already made for the test scene's
            // SceneWiring — see chat): the Fuse popup has no way to know the player died on its own.
            if (stateName == nameof(DeathState)) fuseMiniGame.ForceCancel();
        }

        private void ApplyCurrentStateToMonster()
        {
            if (brain == null || gameFlow == null
                || gameFlow.CurrentStateName != nameof(FinalChaseState))
            {
                return;
            }

            brain.IsRelentless = true;
            brain.ChangeState(brain.AggressiveState);
        }

        private void OnDestroy()
        {
            if (wired)
            {
                objectiveManager.OnAnyFuseCompleted -= lightDirector.OnFuseCompleted;
                objectiveManager.OnAllComplete -= mapExit.Open;
                objectiveManager.OnFuseInteractionStarted -= HandleFuseStarted;
                objectiveManager.OnFuseInteractionStopped -= HandleFuseStopped;
                gameFlow.OnStateChanged -= HandleGameStateChanged;
            }

            if (brain != null) brain.OnKill -= HandleMonsterKill;
        }
    }
}
