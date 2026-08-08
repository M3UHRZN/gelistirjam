using System;
using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Objectives;
using Gloomveil.MapGeneration;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// Spawns a FuseBox at each of the generated map's Anchor_Fuse markers when a map is published.
    /// Same subscribe-to-MapReady pattern as PlayerSpawnGate/MapExitController (see chat: Fuse Memory
    /// Minigame main-scene integration) — map generation reaches gameplay only through
    /// GeneratedMapRegistry and neutral anchors, never a hardcoded scene reference. The number of
    /// fuses is whatever GameplayRoleAssigner/MapGenerationParameters.FuseCount actually produced —
    /// nothing here assumes exactly 3.
    /// </summary>
    [DisallowMultipleComponent]
    public class MapFuseGate : MonoBehaviour
    {
        [SerializeField] private GeneratedMapRegistry registry;
        [SerializeField] private GameFlowController gameFlow;
        [SerializeField] private ObjectiveManager objectiveManager;
        [SerializeField] private FuseMiniGameController miniGame;

        [SerializeField, Tooltip("The scene's CinemachineBrain, handed to each spawned tablet presenter. Spawned prefabs cannot hold scene references, so the gate passes it through.")]
        private CinemachineBrain cinemachineBrain;

        [SerializeField, Tooltip("Optional visual spawned at each anchor. A plain placeholder cube is built when empty.")]
        private GameObject fuseBoxPrefab;

        [SerializeField, Tooltip("Mount each fuse on a closed wall socket instead of leaving it at the room's centre anchor.")]
        private bool placeOnClosedWall = true;

        [SerializeField, Tooltip("Height of the visual's bottom above the room floor/socket level.")]
        private float mountHeight = 1.25f;

        [SerializeField, Tooltip("Gap between the visual's back face and the wall's inner face.")]
        private float wallClearance = 0.02f;

        [SerializeField, Tooltip("Final wall-frame nudge: X along the wall, Y upward, Z into the room.")]
        private Vector3 wallOffset;

        [SerializeField, Tooltip("Local Euler correction applied after the visual faces into the room. Use this to correct the tablet model's authored axes.")]
        private Vector3 rotationOffsetEuler;

        [SerializeField, Tooltip("Round counts assigned to fuse anchors in order, cycling if there are more anchors than entries (TGD section 33 default: 1, 2, 3 = 6 total rounds).")]
        private int[] roundsPattern = { 1, 2, 3 };

        private readonly List<FuseBox> spawned = new List<FuseBox>();
        private bool subscribed;

        public IReadOnlyList<FuseBox> SpawnedFuseBoxes => spawned;

        public void Bind(GeneratedMapRegistry mapRegistry, GameFlowController flow,
            ObjectiveManager objectives, FuseMiniGameController fuseMiniGame, CinemachineBrain brain)
        {
            registry = mapRegistry;
            gameFlow = flow;
            objectiveManager = objectives;
            miniGame = fuseMiniGame;
            cinemachineBrain = brain;
            if (isActiveAndEnabled) Subscribe();
        }

        // Same OnEnable+Start double-attempt as PlayerSpawnGate/MapExitController: whichever runs
        // first with every reference present wins, the guard makes the second a no-op.
        private void OnEnable() => Subscribe();
        private void Start() => Subscribe();

        private void OnDisable()
        {
            if (!subscribed) return;
            registry.MapReady -= OnMapReady;
            subscribed = false;
        }

        private void Subscribe()
        {
            if (subscribed || registry == null || gameFlow == null || objectiveManager == null || miniGame == null) return;

            subscribed = true;
            registry.MapReady += OnMapReady;
            if (registry.TryGetCurrent(out GeneratedMapRuntimeData data))
            {
                SpawnAll(data);
            }
        }

        private void OnMapReady(GeneratedMapRuntimeData data) => SpawnAll(data);

        private void SpawnAll(GeneratedMapRuntimeData data)
        {
            // A freshly published map is a fresh run: old fuse instances (and their Complete state)
            // must not carry over — same "fresh run" reasoning as MapExitController.BuildExit resetting
            // IsOpen on every new map.
            ClearSpawned();

            if (data.FuseAnchors.Count == 0)
            {
                Debug.LogWarning(
                    $"Generated map (seed {data.Seed}) published no Fuse anchors — no fuse objectives were built.",
                    this);
                objectiveManager.SetRequiredCount(0);
                return;
            }

            for (int i = 0; i < data.FuseAnchors.Count; i++)
            {
                Transform anchor = data.FuseAnchors[i];
                if (anchor == null) continue;

                Transform socket = null;
                Vector3 intoRoom = default;
                Renderer wallSurface = null;
                bool mounted = placeOnClosedWall &&
                               TryResolveWallPlacement(anchor, out socket, out intoRoom, out wallSurface);
                if (placeOnClosedWall && !mounted)
                {
                    GeneratedMapModule module = anchor.GetComponentInParent<GeneratedMapModule>();
                    string moduleName = module != null ? module.name : "<none>";
                    Debug.LogWarning(
                        $"MapFuseGate could not resolve a closed wall for anchor '{anchor.name}' " +
                        $"in module '{moduleName}'; using the anchor fallback.",
                        this);
                }

                GameObject instance = fuseBoxPrefab != null
                    ? Instantiate(fuseBoxPrefab, transform, false)
                    : BuildDefaultFuseVisual();
                if (fuseBoxPrefab != null)
                {
                    instance.name = fuseBoxPrefab.name;
                }

                Quaternion baseRotation = mounted
                    ? Quaternion.LookRotation(intoRoom, Vector3.up)
                    : anchor.rotation;
                instance.transform.SetPositionAndRotation(
                    mounted ? socket.position : anchor.position,
                    baseRotation * Quaternion.Euler(rotationOffsetEuler));

                if (mounted)
                {
                    MountAgainstWall(instance, socket.position.y, intoRoom, wallSurface);
                }

                if (instance.GetComponent<Collider>() == null)
                {
                    BoxCollider box = instance.AddComponent<BoxCollider>();
                    box.size = Vector3.one * 0.6f;
                }

                FuseBox fuseBox = instance.GetComponent<FuseBox>();
                if (fuseBox == null) fuseBox = instance.AddComponent<FuseBox>();

                FuseTabletPresenter presenter = instance.GetComponent<FuseTabletPresenter>();
                if (presenter != null) presenter.Bind(cinemachineBrain);

                int rounds = roundsPattern != null && roundsPattern.Length > 0
                    ? roundsPattern[i % roundsPattern.Length]
                    : 1;
                fuseBox.SetRoundsRequired(rounds);
                fuseBox.Bind(gameFlow, objectiveManager, miniGame, presenter);
                spawned.Add(fuseBox);
            }

            objectiveManager.SetRequiredCount(spawned.Count);
        }

        // Parented to this component's own transform (always identity scale), not the anchor itself:
        // generated room/corridor modules are sometimes placed with a mirrored/negative axis scale,
        // which a child inherits as its lossyScale — a BoxCollider built on top of that logs "does not
        // support negative scale or size" and its physics become undefined (found live via Unity MCP
        // testing — see chat). Position/rotation still match the anchor exactly; only the scale
        // inheritance is avoided.
        private GameObject BuildDefaultFuseVisual()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "FuseBox";
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(0.5f, 0.7f, 0.3f);
            return go;
        }

        private static bool TryResolveWallPlacement(
            Transform anchor,
            out Transform socket,
            out Vector3 intoRoom,
            out Renderer wallSurface)
        {
            socket = null;
            intoRoom = default;
            wallSurface = null;

            GeneratedMapModule module = anchor.GetComponentInParent<GeneratedMapModule>();
            if (module == null)
            {
                return false;
            }

            var sockets = new Dictionary<SocketKey, Transform>();
            var cappedKeys = new HashSet<SocketKey>();
            var openDoorways = new List<Vector3>();
            foreach (Transform child in module.GetComponentsInChildren<Transform>(true))
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

                if (score > bestScore ||
                    (Mathf.Approximately(score, bestScore) && socket != null &&
                     string.CompareOrdinal(entry.Value.name, socket.name) < 0))
                {
                    bestScore = score;
                    socket = entry.Value;
                }
            }

            if (socket == null)
            {
                return false;
            }

            Vector3 outward = Vector3.ProjectOnPlane(socket.forward, Vector3.up);
            if (outward.sqrMagnitude < 0.001f)
            {
                socket = null;
                return false;
            }

            intoRoom = -outward.normalized;
            wallSurface = FindWallRenderer(module.transform, socket);
            return true;
        }

        private static Renderer FindWallRenderer(Transform module, Transform socket)
        {
            string suffix = socket.name.Substring("Socket_".Length);
            foreach (Transform child in module.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "WallCap_" + suffix)
                {
                    return child.GetComponentInChildren<Renderer>(true);
                }
            }

            return null;
        }

        private void MountAgainstWall(
            GameObject instance,
            float floorY,
            Vector3 intoRoom,
            Renderer wallSurface)
        {
            Vector3 shift = intoRoom * wallClearance;
            if (TryGetWorldBounds(instance, out Bounds bounds))
            {
                shift = Vector3.up * ((floorY + mountHeight) - bounds.min.y);
                if (wallSurface != null)
                {
                    float wallInnerFace = Vector3.Dot(wallSurface.bounds.center, intoRoom) +
                                          ProjectedExtent(wallSurface.bounds, intoRoom);
                    float visualBackFace = Vector3.Dot(bounds.center, intoRoom) -
                                           ProjectedExtent(bounds, intoRoom);
                    shift += intoRoom * (wallInnerFace + wallClearance - visualBackFace);
                }
                else
                {
                    shift += intoRoom * wallClearance;
                }
            }

            instance.transform.position += shift + WallFrameOffset(intoRoom);
        }

        private Vector3 WallFrameOffset(Vector3 intoRoom)
        {
            Vector3 alongWall = Vector3.Cross(Vector3.up, intoRoom).normalized;
            return (alongWall * wallOffset.x) +
                   (Vector3.up * wallOffset.y) +
                   (intoRoom * wallOffset.z);
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

        private void ClearSpawned()
        {
            // A minigame open on a fuse that is about to be destroyed would leave its canvas parented
            // to a dead transform and its coroutine waiting for input that can never arrive.
            if (miniGame != null) miniGame.ForceCancel();

            foreach (FuseBox box in spawned)
            {
                if (box != null) Destroy(box.gameObject);
            }
            spawned.Clear();
        }
    }
}
