using System;
using System.Collections.Generic;
using Game.Core;
using Game.Gameplay.Objectives;
using Gloomveil.MapGeneration;
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

        [SerializeField, Tooltip("Optional visual spawned at each anchor. A plain placeholder cube is built when empty.")]
        private GameObject fuseBoxPrefab;

        [SerializeField, Tooltip("Round counts assigned to fuse anchors in order, cycling if there are more anchors than entries (TGD section 33 default: 1, 2, 3 = 6 total rounds).")]
        private int[] roundsPattern = { 1, 2, 3 };

        private readonly List<FuseBox> spawned = new List<FuseBox>();
        private bool subscribed;

        public IReadOnlyList<FuseBox> SpawnedFuseBoxes => spawned;

        public void Bind(GeneratedMapRegistry mapRegistry, GameFlowController flow,
            ObjectiveManager objectives, FuseMiniGameController fuseMiniGame)
        {
            registry = mapRegistry;
            gameFlow = flow;
            objectiveManager = objectives;
            miniGame = fuseMiniGame;
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

                GameObject instance = fuseBoxPrefab != null
                    ? Instantiate(fuseBoxPrefab, anchor.position, anchor.rotation, anchor)
                    : BuildDefaultFuseVisual(anchor);

                if (instance.GetComponent<Collider>() == null)
                {
                    BoxCollider box = instance.AddComponent<BoxCollider>();
                    box.size = Vector3.one * 0.6f;
                }

                FuseBox fuseBox = instance.GetComponent<FuseBox>();
                if (fuseBox == null) fuseBox = instance.AddComponent<FuseBox>();

                int rounds = roundsPattern != null && roundsPattern.Length > 0
                    ? roundsPattern[i % roundsPattern.Length]
                    : 1;
                fuseBox.SetRoundsRequired(rounds);
                fuseBox.Bind(gameFlow, objectiveManager, miniGame);
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
        private GameObject BuildDefaultFuseVisual(Transform anchor)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "FuseBox";
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            go.transform.localScale = new Vector3(0.5f, 0.7f, 0.3f);
            return go;
        }

        private void ClearSpawned()
        {
            foreach (FuseBox box in spawned)
            {
                if (box != null) Destroy(box.gameObject);
            }
            spawned.Clear();
        }
    }
}
