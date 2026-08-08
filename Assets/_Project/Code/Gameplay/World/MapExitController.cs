using System;
using Game.Core;
using Game.Gameplay.Player;
using Gloomveil.MapGeneration;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// Builds the run's exit at the generated map's Anchor_Exit when a map is published, and resolves
    /// the run when the player walks through an opened one.
    ///
    /// It knows nothing about *why* the exit opens — Open() is public and the composition root
    /// (SceneWiring) connects whatever tracks the run's objectives to it. That keeps this class free
    /// of the objective system, which is still a placeholder (TGD section 37 wants fuse completion
    /// to power the exit).
    ///
    /// The exit *room* is already chosen by the generator: GameplayRoleAssigner runs a weighted
    /// shortest-path search from Start (MapGraphSearch.WeightedDistancesFrom) and takes the farthest
    /// room, which MapLayoutValidator then enforces as a hard invariant (TGD section 46). Nothing is
    /// re-derived here — this component only consumes the published anchor, keeping map generation
    /// one-directional (same contract as PlayerSpawnGate).
    ///
    /// The older ExitController still serves the hand-built monster AI test scene, where the exit
    /// sits at a hardcoded position and no generated map exists.
    /// </summary>
    [DisallowMultipleComponent]
    public class MapExitController : MonoBehaviour
    {
        [SerializeField] private GeneratedMapRegistry registry;
        [SerializeField] private GameFlowController gameFlow;

        [SerializeField, Tooltip("Spawned at Anchor_Exit. Optional — a bare trigger volume is built when empty.")]
        private GameObject exitPrefab;

        [SerializeField, Tooltip("Size of the trigger built when the prefab carries no trigger collider of its own.")]
        private Vector3 fallbackTriggerSize = new Vector3(2f, 2.5f, 1f);

        [SerializeField, Tooltip("Local centre of that fallback trigger, relative to the exit anchor.")]
        private Vector3 fallbackTriggerCenter = new Vector3(0f, 1.25f, 0f);

        [SerializeField, Tooltip("Child of the exit prefab that physically blocks the doorway while closed. Deactivated on Open(). Leave empty to skip.")]
        private string blockerChildName = "ExitBlocker";

        [SerializeField, Tooltip("When off, reaching the exit only raises OnPlayerReachedExit and does not end the run — hook a next-map transition there instead.")]
        private bool completeRunOnReach = true;

        private ExitTriggerRelay relay;
        private bool subscribed;

        public event Action OnExitOpened;

        /// <summary>
        /// Raised the moment the player passes through an opened exit. Today the only outcome is
        /// VictoryState; a later "load the next map" step can subscribe here and turn
        /// completeRunOnReach off to take over without this class knowing about levels.
        /// </summary>
        public event Action OnPlayerReachedExit;

        public bool IsOpen { get; private set; }
        public Transform SpawnedExit { get; private set; }

        public void Bind(
            GeneratedMapRegistry mapRegistry,
            GameFlowController flow,
            GameObject prefab = null)
        {
            registry = mapRegistry;
            gameFlow = flow;
            if (prefab != null)
            {
                exitPrefab = prefab;
            }

            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        private void OnEnable() => Subscribe();

        private void Start() => Subscribe();

        private void OnDisable()
        {
            if (!subscribed)
            {
                return;
            }

            registry.MapReady -= OnMapReady;
            subscribed = false;
        }

        private void OnDestroy() => DetachRelay();

        // Same OnEnable+Start double-attempt as PlayerSpawnGate: whichever runs first with both
        // references present wins, and the guard makes the second call a no-op.
        private void Subscribe()
        {
            if (subscribed || registry == null || gameFlow == null)
            {
                return;
            }

            subscribed = true;
            registry.MapReady += OnMapReady;
            if (registry.TryGetCurrent(out GeneratedMapRuntimeData data))
            {
                BuildExit(data);
            }
        }

        private void OnMapReady(GeneratedMapRuntimeData data) => BuildExit(data);

        private void BuildExit(GeneratedMapRuntimeData data)
        {
            ClearSpawnedExit();

            // A freshly published map is a fresh run: the old exit's open state must not carry over,
            // or a regenerated map (next level) would start with its exit already unlocked.
            IsOpen = false;

            if (data.ExitAnchor == null)
            {
                Debug.LogError(
                    $"Generated map (seed {data.Seed}) published no Anchor_Exit — no exit was built.",
                    this);
                return;
            }

            GameObject instance;
            if (exitPrefab != null)
            {
                instance = Instantiate(exitPrefab, data.ExitAnchor, false);
                instance.name = exitPrefab.name;
            }
            else
            {
                instance = new GameObject("Exit");
                instance.transform.SetParent(data.ExitAnchor, false);
            }

            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            SpawnedExit = instance.transform;

            AttachRelay(FindOrCreateTrigger(instance));
        }

        private Collider FindOrCreateTrigger(GameObject instance)
        {
            foreach (Collider candidate in instance.GetComponentsInChildren<Collider>(true))
            {
                if (candidate.isTrigger)
                {
                    return candidate;
                }
            }

            var triggerObject = new GameObject("ExitTrigger");
            triggerObject.transform.SetParent(instance.transform, false);
            BoxCollider box = triggerObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = fallbackTriggerSize;
            box.center = fallbackTriggerCenter;
            return box;
        }

        private void AttachRelay(Collider trigger)
        {
            relay = trigger.GetComponent<ExitTriggerRelay>();
            if (relay == null)
            {
                relay = trigger.gameObject.AddComponent<ExitTriggerRelay>();
            }

            relay.Entered += HandleTriggerEntered;
        }

        private void DetachRelay()
        {
            if (relay == null)
            {
                return;
            }

            relay.Entered -= HandleTriggerEntered;
            relay = null;
        }

        private void ClearSpawnedExit()
        {
            DetachRelay();
            if (SpawnedExit == null)
            {
                return;
            }

            Destroy(SpawnedExit.gameObject);
            SpawnedExit = null;
        }

        /// <summary>
        /// Unlocks the exit and enters FinalChase. Called by whatever decides the run's objectives
        /// are done — SceneWiring connects that today.
        /// </summary>
        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            OpenBlocker();
            OnExitOpened?.Invoke();
            gameFlow.ChangeState(gameFlow.FinalChaseState);
        }

        private void OpenBlocker()
        {
            if (SpawnedExit == null || string.IsNullOrEmpty(blockerChildName))
            {
                return;
            }

            foreach (Transform child in SpawnedExit.GetComponentsInChildren<Transform>(true))
            {
                if (child != SpawnedExit && child.name == blockerChildName)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private void HandleTriggerEntered(Collider other)
        {
            if (!IsOpen || !IsPlayer(other))
            {
                return;
            }

            OnPlayerReachedExit?.Invoke();
            if (completeRunOnReach)
            {
                gameFlow.ChangeState(gameFlow.VictoryState);
            }
        }

        // No tag or layer scheme exists in this project yet, so the player is identified by its
        // controller component. Both rigs count: PlayerController (Rigidbody + CapsuleCollider) is
        // the real one, TestPlayerController is a CharacterController. ExitController only checks
        // for CharacterController, which is why it never fires for the real player rig.
        private static bool IsPlayer(Collider other) =>
            other.GetComponentInParent<PlayerController>() != null ||
            other.GetComponentInParent<CharacterController>() != null;
    }
}
