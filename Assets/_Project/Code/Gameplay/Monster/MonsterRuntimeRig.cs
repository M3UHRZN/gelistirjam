using Game.Gameplay.Player;
using Game.Gameplay.World;
using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Composition boundary for a spawned monster. The gate creates the prefab inactive, then this
    /// component validates and binds every runtime dependency before the first OnEnable callback.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterRuntimeRig : MonoBehaviour
    {
        [SerializeField] private MonsterBrain brain;
        [SerializeField] private MonsterNavigator navigator;
        [SerializeField] private MonsterPerception perception;
        [SerializeField] private MonsterAngerController anger;
        [SerializeField] private MonsterGazeReceiver gazeReceiver;
        [SerializeField] private MonsterVisualDriver visualDriver;
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private MonsterAnimationController animationController;
        [SerializeField] private MonsterAudioController audioController;
        [SerializeField] private Animator animator;

        public MonsterBrain Brain => brain;
        public MonsterPerception Perception => perception;
        public bool IsInitialized { get; private set; }

        public bool Initialize(
            MonsterConfig config,
            Transform player,
            IPlayerVelocitySource velocitySource,
            PlayerGazeDetector gazeDetector,
            SafeZoneRuntimeSet safeZones,
            StalkPointRuntimeSet stalkPoints,
            PlayerVisionModel visionModel)
        {
            IsInitialized = false;
            ResolveComponents();

            Camera playerCamera = gazeDetector != null ? gazeDetector.GetComponent<Camera>() : null;
            if (config == null || player == null || velocitySource == null || playerCamera == null
                || safeZones == null || stalkPoints == null || visionModel == null
                || brain == null || navigator == null || perception == null || anger == null
                || gazeReceiver == null || visualDriver == null || bodyRenderer == null
                || animationController == null || audioController == null || animator == null)
            {
                return false;
            }

            perception.Bind(player, velocitySource);
            navigator.Bind(safeZones);
            anger.Bind(config);
            gazeDetector.Bind(playerCamera, config);
            gazeReceiver.Bind(brain);
            brain.Bind(config, perception, anger, navigator, gazeDetector, gazeReceiver, player);
            brain.BindStalkSystems(stalkPoints, visionModel);
            visionModel.Bind(player, perception);
            visualDriver.Bind(bodyRenderer, brain);
            var agent = navigator.GetComponent<UnityEngine.AI.NavMeshAgent>();
            animationController.Bind(animator, brain, agent);
            audioController.Bind(brain, agent);

            if (!animationController.IsBound || !audioController.IsBound) return false;

            IsInitialized = true;
            return true;
        }

        private void ResolveComponents()
        {
            if (brain == null) brain = GetComponent<MonsterBrain>();
            if (navigator == null) navigator = GetComponent<MonsterNavigator>();
            if (perception == null) perception = GetComponent<MonsterPerception>();
            if (anger == null) anger = GetComponent<MonsterAngerController>();
            if (gazeReceiver == null) gazeReceiver = GetComponentInChildren<MonsterGazeReceiver>(true);
            if (visualDriver == null) visualDriver = GetComponent<MonsterVisualDriver>();
            if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<Renderer>(true);
            if (animationController == null) animationController = GetComponent<MonsterAnimationController>();
            if (audioController == null) audioController = GetComponent<MonsterAudioController>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
        }
    }
}
