using Game.Core;
using Game.Gameplay.Monster;
using Game.Gameplay.Objectives;
using Game.Gameplay.World;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Gameplay
{
    /// <summary>
    /// Real runtime composition-root wiring (MASTER_PROMPT.md section 9's "persistent bootstrap
    /// scene loads services"). The editor scene builder used to set up these cross-system event
    /// subscriptions itself, directly inside the -executeMethod script — but C# event subscriptions
    /// are NOT serialized by Unity, so that wiring only existed for the remainder of the exact same
    /// Editor session BuildScene() ran in. Reopening the saved scene (or the whole project) silently
    /// dropped death, fuse-triggered light shutdown, the exit, and FinalChase entirely, even though
    /// every component looked correctly wired in the Inspector — only the events connecting them were
    /// lost, not the direct references. This component redoes that wiring every time the scene
    /// actually loads, which is the only place it can reliably live (see chat).
    /// </summary>
    public class SceneWiring : MonoBehaviour
    {
        [SerializeField] private MonsterBrain brain;
        [SerializeField, FormerlySerializedAs("gameManager")] private GameFlowController gameFlow;
        [SerializeField] private ObjectiveManager objectiveManager;
        [SerializeField, Tooltip("Hand-placed exit of the AI test scene. Leave empty in a generated-map scene.")]
        private ExitController exitController;
        [SerializeField, Tooltip("Exit built from the generated map's Anchor_Exit. Leave empty in the AI test scene.")]
        private MapExitController mapExitController;
        [SerializeField] private LightZone[] lightShutdownOrder;
        [SerializeField] private MonsterConfig config;
        [SerializeField] private FuseMiniGameController fuseMiniGame;

        private readonly LightDirector lightDirector = new LightDirector();
        private bool wired;

        public void Bind(MonsterBrain monsterBrain, GameFlowController flow, ObjectiveManager objectives,
            ExitController exit, LightZone[] shutdownOrder, MonsterConfig monsterConfig,
            FuseMiniGameController miniGame, MapExitController mapExit = null)
        {
            brain = monsterBrain;
            gameFlow = flow;
            objectiveManager = objectives;
            exitController = exit;
            mapExitController = mapExit;
            lightShutdownOrder = shutdownOrder;
            config = monsterConfig;
            fuseMiniGame = miniGame;
            Wire();
        }

        // Both fire attempts, same reliability reasoning as SafeZone/FuseBox's OnEnable+Start
        // fallback pattern (see chat) — Wire() is idempotent-guarded so this is always safe.
        private void Awake() => Wire();
        private void Start() => Wire();

        private void Wire()
        {
            if (wired) return;
            // exitController is deliberately NOT required: a procedurally generated scene has no
            // hand-placed exit, its exit is built at runtime by MapExitController from the map's
            // Anchor_Exit. Requiring it here would drop everything else in this method — including
            // the FinalChase reaction below, which both exits depend on.
            if (brain == null || gameFlow == null || objectiveManager == null || config == null || fuseMiniGame == null) return;
            wired = true;

            lightDirector.SetShutdownOrder(lightShutdownOrder);

            brain.OnKill += () => gameFlow.ChangeState(gameFlow.DeathState);
            objectiveManager.OnAnyFuseCompleted += lightDirector.OnFuseCompleted;
            // MapExitController deliberately does not know about objectives, so the objective ->
            // exit link is made here rather than inside it (TGD section 37).
            if (exitController != null) objectiveManager.OnAllComplete += exitController.Open;
            if (mapExitController != null) objectiveManager.OnAllComplete += mapExitController.Open;

            // TGD section 36: the monster must be aware of fuse interactions, not just that player
            // control is locked. See FuseBox.OnInteractionStarted/Stopped -> ObjectiveManager forward.
            objectiveManager.OnFuseInteractionStarted += position => brain.SetFuseAwareness(true, position);
            objectiveManager.OnFuseInteractionStopped += () => brain.SetFuseAwareness(false, Vector3.zero);

            // FinalChase: kill the remaining lights and begin continuous player-aware stalking.
            // Deliberately does NOT touch Anger: the TGD is explicit that "Monster'ın current Anger
            // değeri korunur" (preserved) precisely so that "oyuncu run boyunca monster'a ne kadar çok
            // baktıysa final o kadar zordur" — a player who avoided eye contact all run should reach
            // an easier final chase, not a forced-maximum one. AddAnger(MaxAnger) used to run here,
            // which overwrote that carried-over value instead of preserving it (see chat) — the
            // Final pressure now comes from lights out, shorter reposition cooldown and closer
            // candidate points rather than replacing the game's stalking identity with direct chase.
            gameFlow.OnStateChanged += stateName =>
            {
                if (stateName != nameof(FinalChaseState)) return;
                lightDirector.ShutdownAll();
                brain.BeginFinalHunt();
            };

            // Death mid-minigame (prompt section 27, found live via Unity MCP testing — see chat): the
            // Fuse popup has no way to know the player died on its own, so it just sat open forever,
            // fighting visually with the death sequence. ForceCancel runs the same graceful
            // close/cursor-restore path as an ESC press.
            gameFlow.OnStateChanged += stateName =>
            {
                if (stateName != nameof(DeathState)) return;
                fuseMiniGame.ForceCancel();
            };
        }
    }
}
