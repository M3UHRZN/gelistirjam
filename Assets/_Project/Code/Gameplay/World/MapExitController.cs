using System;
using System.Collections.Generic;
using Game.Core;
using Gloomveil.MapGeneration;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// Builds the run's exit inside the generated map's exit room when a map is published, and
    /// resolves the run when the player walks through an opened one. The exit is set into one of the
    /// room's walled-off sockets (see TryResolveWallPlacement); Anchor_Exit is only the fallback,
    /// because the kit places it at the room's centre.
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

        [SerializeField, Tooltip("Spawned into the exit room. Optional — a bare trigger volume is built when empty.")]
        private GameObject exitPrefab;

        [SerializeField, Tooltip("Set the exit into one of the exit room's walled-off sockets instead of Anchor_Exit, which sits in the middle of the room.")]
        private bool placeInWall = true;

        [SerializeField, Tooltip("Extra yaw applied after aligning with the wall. Use if the prefab's front ends up facing the wrong way.")]
        private float yawOffsetDegrees;

        [SerializeField, Tooltip("Gap left between the wall surface and the back of the exit.")]
        private float wallClearance = 0.02f;

        [SerializeField, Tooltip("Manual nudge in the wall's own frame, applied last: X slides along the wall, Y up, Z into the room. Leave at zero to use the automatic fit.")]
        private Vector3 wallOffset;

        [SerializeField, Tooltip("Shrink the exit uniformly if it is taller than the wall it sits in, so it never pokes through the ceiling. Never enlarges it.")]
        private bool fitToWallHeight = true;

        [SerializeField, Tooltip("Size of the trigger built when the prefab carries no trigger collider of its own.")]
        private Vector3 fallbackTriggerSize = new Vector3(2f, 2.5f, 1f);

        [SerializeField, Tooltip("Local centre of that fallback trigger, relative to the spawn point.")]
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

            Transform parent = data.ExitModule != null ? data.ExitModule.transform : data.ExitAnchor;
            var position = Vector3.zero;
            var rotation = Quaternion.identity;
            var intoRoom = Vector3.forward;
            Renderer wallSurface = null;
            bool inWall = placeInWall &&
                          TryResolveWallPlacement(data, out position, out rotation, out intoRoom, out wallSurface);
            if (!inWall)
            {
                position = data.ExitAnchor.position;
                rotation = data.ExitAnchor.rotation;
            }

            GameObject instance;
            if (exitPrefab != null)
            {
                instance = Instantiate(exitPrefab, parent, false);
                instance.name = exitPrefab.name;
                // The prefab's own local rotation is authoring data (exitdoor.prefab carries a -90°
                // X from its FBX import); overwriting it with identity lays the door on its side.
                // Its local position, on the other hand, is a leftover world offset and is dropped.
                rotation *= exitPrefab.transform.localRotation;
            }
            else
            {
                instance = new GameObject("Exit");
                instance.transform.SetParent(parent, false);
            }

            instance.transform.SetPositionAndRotation(position, rotation);
            SpawnedExit = instance.transform;

            if (inWall)
            {
                ShrinkToWallHeight(instance, wallSurface);
                SitOnFloorAgainstWall(instance, position.y, intoRoom, wallSurface);
                instance.transform.position += WallFrameOffset(intoRoom);
            }

            AttachRelay(FindOrCreateTrigger(instance));
        }

        /// <summary>
        /// Turns wallOffset into a world nudge: X along the wall, Y up, Z into the room. Expressed in
        /// the wall's frame rather than world space so the same value reads the same on every seed,
        /// whichever compass direction the chosen wall happens to face.
        /// </summary>
        private Vector3 WallFrameOffset(Vector3 intoRoom)
        {
            if (wallOffset == Vector3.zero)
            {
                return Vector3.zero;
            }

            Vector3 alongWall = Vector3.Cross(Vector3.up, intoRoom).normalized;
            return (alongWall * wallOffset.x) + (Vector3.up * wallOffset.y) + (intoRoom * wallOffset.z);
        }

        /// <summary>
        /// Picks a socket of the exit room that the generator walled off (its WallCap child is
        /// active, so nothing connects through it) and returns a placement flush with that wall.
        /// Anchor_Exit itself is no use here — the kit puts it at the room's centre, which leaves the
        /// exit standing in open floor. Among the walled sockets the one farthest from the room's
        /// open doorways wins, so the way out is never right next to the way in.
        /// </summary>
        private bool TryResolveWallPlacement(
            GeneratedMapRuntimeData data,
            out Vector3 position,
            out Quaternion rotation,
            out Vector3 intoRoom,
            out Renderer wallSurface)
        {
            position = default;
            rotation = default;
            intoRoom = default;
            wallSurface = null;
            if (data.ExitModule == null)
            {
                return false;
            }

            var sockets = new Dictionary<SocketKey, Transform>();
            var cappedKeys = new HashSet<SocketKey>();
            var openDoorways = new List<Vector3>();
            foreach (Transform child in data.ExitModule.GetComponentsInChildren<Transform>(true))
            {
                if (ModuleSocketController.TryParse(child.name, "Socket_", out SocketKey socketKey))
                {
                    sockets[socketKey] = child;
                }
                else if (ModuleSocketController.TryParse(child.name, "WallCap_", out SocketKey capKey))
                {
                    if (child.gameObject.activeInHierarchy)
                    {
                        cappedKeys.Add(capKey);
                    }
                }
                else if (ModuleSocketController.TryParse(child.name, "Portal_", out SocketKey _) &&
                         child.gameObject.activeInHierarchy)
                {
                    openDoorways.Add(child.position);
                }
            }

            Transform chosen = null;
            float bestScore = float.NegativeInfinity;
            foreach (KeyValuePair<SocketKey, Transform> entry in sockets)
            {
                if (!cappedKeys.Contains(entry.Key))
                {
                    continue;
                }

                float score = float.MaxValue;
                foreach (Vector3 doorway in openDoorways)
                {
                    score = Mathf.Min(score, Vector3.Distance(entry.Value.position, doorway));
                }

                // Ordinal name order breaks ties so the same seed always picks the same wall.
                if (score > bestScore ||
                    (Mathf.Approximately(score, bestScore) && chosen != null &&
                     string.CompareOrdinal(entry.Value.name, chosen.name) < 0))
                {
                    bestScore = score;
                    chosen = entry.Value;
                }
            }

            if (chosen == null)
            {
                return false;
            }

            Vector3 outward = Vector3.ProjectOnPlane(chosen.forward, Vector3.up);
            if (outward.sqrMagnitude < 0.001f)
            {
                return false;
            }

            outward.Normalize();
            intoRoom = -outward;
            position = chosen.position;
            // The prefab's face normal is its local +X once its authored rotation is applied, so aim
            // +X into the room: LookRotation puts +Z on intoRoom, the -90° yaw swings +X onto it.
            rotation = Quaternion.LookRotation(intoRoom, Vector3.up) *
                       Quaternion.Euler(0f, -90f + yawOffsetDegrees, 0f);
            wallSurface = FindWallRenderer(data.ExitModule.transform, chosen);
            return true;
        }

        private static Renderer FindWallRenderer(Transform module, Transform socket)
        {
            string suffix = socket.name.Substring("Socket_".Length);
            Transform cap = null;
            foreach (Transform child in module.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "WallCap_" + suffix)
                {
                    cap = child;
                    break;
                }
            }

            return cap != null ? cap.GetComponentInChildren<Renderer>(true) : null;
        }

        /// <summary>
        /// exitdoor.prefab stands 3.37 m while the kit's walls are 3 m, so left alone it pushes
        /// through the ceiling. Scales uniformly to match the wall, never up.
        /// </summary>
        private void ShrinkToWallHeight(GameObject instance, Renderer wallSurface)
        {
            if (!fitToWallHeight || wallSurface == null || !TryGetWorldBounds(instance, out Bounds bounds))
            {
                return;
            }

            float wallHeight = wallSurface.bounds.size.y;
            if (bounds.size.y <= wallHeight || bounds.size.y <= 0.0001f)
            {
                return;
            }

            instance.transform.localScale *= wallHeight / bounds.size.y;
        }

        /// <summary>
        /// Nudges the spawned exit so it rests on the floor and its back sits against the wall's
        /// inner face. Measured from renderer bounds rather than hardcoded offsets, so swapping the
        /// prefab for a differently sized door needs no retuning.
        /// </summary>
        private void SitOnFloorAgainstWall(GameObject instance, float floorY, Vector3 intoRoom, Renderer wallSurface)
        {
            if (!TryGetWorldBounds(instance, out Bounds bounds))
            {
                return;
            }

            Vector3 shift = Vector3.up * (floorY - bounds.min.y);
            if (wallSurface != null)
            {
                float wallInnerFace = Vector3.Dot(wallSurface.bounds.center, intoRoom) +
                                      ProjectedExtent(wallSurface.bounds, intoRoom);
                float exitBackFace = Vector3.Dot(bounds.center, intoRoom) - ProjectedExtent(bounds, intoRoom);
                shift += intoRoom * (wallInnerFace + wallClearance - exitBackFace);
            }

            instance.transform.position += shift;
        }

        private static bool TryGetWorldBounds(GameObject instance, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (any)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                else
                {
                    bounds = renderer.bounds;
                    any = true;
                }
            }

            return any;
        }

        private static float ProjectedExtent(Bounds bounds, Vector3 direction) =>
            Mathf.Abs(direction.x) * bounds.extents.x +
            Mathf.Abs(direction.y) * bounds.extents.y +
            Mathf.Abs(direction.z) * bounds.extents.z;

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

            relay.CanInteract = IsOpen;
            relay.Interacted += HandleInteracted;
        }

        private void DetachRelay()
        {
            if (relay == null)
            {
                return;
            }

            relay.Interacted -= HandleInteracted;
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
            if (relay != null)
            {
                relay.CanInteract = true;
            }

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

        /// <summary>
        /// Fired by ExitTriggerRelay.Interacted — the player held E while aiming at the (unlocked)
        /// exit. Reaching the exit no longer ends the run by itself (see chat: exit now requires an
        /// explicit interact instead of auto-completing on trigger enter); the door only responds
        /// once IsOpen, so this can't fire before FinalChase starts.
        /// </summary>
        private void HandleInteracted()
        {
            if (!IsOpen)
            {
                return;
            }

            OnPlayerReachedExit?.Invoke();
            if (completeRunOnReach)
            {
                gameFlow.ChangeState(gameFlow.VictoryState);
            }
        }
    }
}
