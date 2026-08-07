using Game.Core;
using Game.Gameplay.Monster;
using Game.Gameplay.Objectives;
using Game.Gameplay.World;
using UnityEngine;

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
        [SerializeField] private GameManager gameManager;
        [SerializeField] private ObjectiveManager objectiveManager;
        [SerializeField] private ExitController exitController;
        [SerializeField] private LightZone[] lightShutdownOrder;
        [SerializeField] private MonsterConfig config;

        private readonly LightDirector lightDirector = new LightDirector();
        private bool wired;

        public void Bind(MonsterBrain monsterBrain, GameManager manager, ObjectiveManager objectives,
            ExitController exit, LightZone[] shutdownOrder, MonsterConfig monsterConfig)
        {
            brain = monsterBrain;
            gameManager = manager;
            objectiveManager = objectives;
            exitController = exit;
            lightShutdownOrder = shutdownOrder;
            config = monsterConfig;
            Wire();
        }

        // Both fire attempts, same reliability reasoning as SafeZone/FuseBox's OnEnable+Start
        // fallback pattern (see chat) — Wire() is idempotent-guarded so this is always safe.
        private void Awake() => Wire();
        private void Start() => Wire();

        private void Wire()
        {
            if (wired) return;
            if (brain == null || gameManager == null || objectiveManager == null || exitController == null || config == null) return;
            wired = true;

            lightDirector.SetShutdownOrder(lightShutdownOrder);

            brain.OnKill += () => gameManager.ChangeState(gameManager.DeathState);
            objectiveManager.OnAnyFuseCompleted += lightDirector.OnFuseCompleted;
            objectiveManager.OnAllComplete += exitController.Open;

            // FinalChase (TGD section 38): kill the remaining lights and force a guaranteed,
            // relentless chase — see MonsterBrain.IsRelentless for why Anger alone isn't enough.
            gameManager.OnStateChanged += stateName =>
            {
                if (stateName != nameof(FinalChaseState)) return;
                lightDirector.ShutdownAll();
                brain.Anger.AddAnger(config.MaxAnger);
                brain.IsRelentless = true;
                brain.ChangeState(brain.AggressiveState);
            };
        }
    }
}
