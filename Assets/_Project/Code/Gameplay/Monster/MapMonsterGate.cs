using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Gameplay.Player;
using Game.Gameplay.World;
using Gloomveil.MapGeneration;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Generated-gameplay composition gate. It waits for the light and SafeZone projections of the
    /// same published map, converts neutral stalk anchors, selects a reachable seeded spawn, then
    /// activates one fully initialized monster.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapMonsterGate : MonoBehaviour
    {
        [SerializeField] private GeneratedMapRegistry registry;
        [SerializeField] private MonsterRuntimeRig monsterPrefab;
        [SerializeField] private MonsterConfig config;
        [SerializeField] private PlayerController player;
        [SerializeField] private PlayerGazeDetector gazeDetector;
        [SerializeField] private PlayerVisionModel visionModel;
        [SerializeField] private SafeZoneRuntimeSet safeZoneSet;
        [SerializeField] private StalkPointRuntimeSet stalkPointSet;
        [SerializeField] private MapLightGate lightGate;
        [SerializeField] private MapSafeZoneGate safeZoneGate;
        [SerializeField] private MainSceneWiring wiring;

        [SerializeField, Min(0f)] private float minimumInitialPathDistance = 24f;
        [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 2f;

        private readonly List<StalkPoint> spawnedStalkPoints = new();
        private readonly List<StalkPoint> addedStalkPoints = new();
        private MonsterRuntimeRig spawnedRig;
        private GeneratedMapRuntimeData pendingData;
        private GeneratedMapRuntimeData builtFor;
        private bool subscribed;

        public MonsterBrain SpawnedBrain => spawnedRig != null ? spawnedRig.Brain : null;
        public int SpawnCount => spawnedRig != null ? 1 : 0;
        public IReadOnlyList<StalkPoint> SpawnedStalkPoints => spawnedStalkPoints;
        public float SelectedSpawnPathDistance { get; private set; }

        public void Bind(
            GeneratedMapRegistry mapRegistry,
            MonsterRuntimeRig prefab,
            MonsterConfig monsterConfig,
            PlayerController playerController,
            PlayerGazeDetector detector,
            PlayerVisionModel playerVisionModel,
            SafeZoneRuntimeSet safeZones,
            StalkPointRuntimeSet stalkPoints,
            MapLightGate generatedLightGate,
            MapSafeZoneGate generatedSafeZoneGate,
            MainSceneWiring sceneWiring)
        {
            Unsubscribe();

            registry = mapRegistry;
            monsterPrefab = prefab;
            config = monsterConfig;
            player = playerController;
            gazeDetector = detector;
            visionModel = playerVisionModel;
            safeZoneSet = safeZones;
            stalkPointSet = stalkPoints;
            lightGate = generatedLightGate;
            safeZoneGate = generatedSafeZoneGate;
            wiring = sceneWiring;

            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable() => Subscribe();
        private void Start() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void OnDestroy()
        {
            Unsubscribe();
            CleanupSpawnedContent();
        }

        private void Subscribe()
        {
            if (subscribed || registry == null || monsterPrefab == null || config == null
                || player == null || gazeDetector == null || visionModel == null
                || safeZoneSet == null || stalkPointSet == null || lightGate == null
                || safeZoneGate == null || wiring == null)
            {
                return;
            }

            subscribed = true;
            registry.MapReady += HandleMapReady;
            registry.MapCleared += HandleMapCleared;
            lightGate.BuildCompleted += HandleDependencyBuildCompleted;
            safeZoneGate.BuildCompleted += HandleDependencyBuildCompleted;

            if (registry.TryGetCurrent(out GeneratedMapRuntimeData data)) HandleMapReady(data);
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;

            registry.MapReady -= HandleMapReady;
            registry.MapCleared -= HandleMapCleared;
            lightGate.BuildCompleted -= HandleDependencyBuildCompleted;
            safeZoneGate.BuildCompleted -= HandleDependencyBuildCompleted;
            subscribed = false;
        }

        private void HandleMapReady(GeneratedMapRuntimeData data)
        {
            if (data == null || ReferenceEquals(builtFor, data)) return;

            if (pendingData != null && !ReferenceEquals(pendingData, data))
            {
                CleanupSpawnedContent();
                builtFor = null;
            }

            pendingData = data;
            TryBuildPending();
        }

        private void HandleDependencyBuildCompleted(GeneratedMapRuntimeData data)
        {
            if (ReferenceEquals(pendingData, data)) TryBuildPending();
        }

        private void HandleMapCleared(GeneratedMapRuntimeData data)
        {
            if (!ReferenceEquals(pendingData, data) && !ReferenceEquals(builtFor, data)) return;

            CleanupSpawnedContent();
            pendingData = null;
            builtFor = null;
        }

        private void TryBuildPending()
        {
            if (pendingData == null || ReferenceEquals(builtFor, pendingData) || spawnedRig != null) return;
            if (!lightGate.IsBuiltFor(pendingData) || !safeZoneGate.IsBuiltFor(pendingData)) return;

            BuildFor(pendingData);
        }

        private void BuildFor(GeneratedMapRuntimeData data)
        {
            if (monsterPrefab.gameObject.activeSelf)
            {
                Debug.LogError(
                    "MapMonsterGate requires an inactive MonsterRuntimeRig prefab so every dependency " +
                    "is bound before MonsterBrain.OnEnable runs.",
                    monsterPrefab);
                builtFor = data;
                return;
            }

            ConvertStalkPoints(data);
            List<MonsterSpawnOption> options = CollectSpawnOptions(data);
            if (!MonsterSpawnSelector.TrySelect(
                    options,
                    data.Seed,
                    minimumInitialPathDistance,
                    out MonsterSpawnOption selected,
                    out MonsterSpawnSelectionReason reason))
            {
                Debug.LogError(
                    $"Generated map seed {data.Seed} has no complete NavMesh path to a legal monster " +
                    "spawn or stalk anchor.",
                    this);
                builtFor = data;
                return;
            }

            MonsterRuntimeRig instance = Instantiate(
                monsterPrefab,
                selected.Position,
                Quaternion.Euler(0f, selected.Yaw, 0f),
                transform);
            instance.name = $"Monster_{data.Seed}_{selected.StableKey}";

            if (!instance.Initialize(
                    config,
                    player.transform,
                    player,
                    gazeDetector,
                    safeZoneSet,
                    stalkPointSet,
                    visionModel))
            {
                Debug.LogError(
                    $"Monster runtime rig failed initialization for map seed {data.Seed}; spawn aborted.",
                    instance);
                DestroyRuntimeObject(instance.gameObject);
                ClearStalkPoints();
                builtFor = data;
                return;
            }

            spawnedRig = instance;
            SelectedSpawnPathDistance = selected.PathLength;
            builtFor = data;
            wiring.BindMonster(instance.Brain);
            instance.gameObject.SetActive(true);

            Debug.Log(
                $"Spawned monster for seed {data.Seed}: {reason}, path " +
                $"{selected.PathLength:0.0}m, key {selected.StableKey}.",
                instance);
        }

        private void ConvertStalkPoints(GeneratedMapRuntimeData data)
        {
            ClearStalkPoints();

            foreach (Transform anchor in data.StalkPoints)
            {
                if (anchor == null) continue;

                GeneratedMapModule module = anchor.GetComponentInParent<GeneratedMapModule>();
                GeneratedStalkPointMetadata metadata = GeneratedStalkPointMetadata.Infer(
                    anchor.name,
                    module != null ? module.CatalogEntryId : string.Empty);

                StalkPoint point = anchor.GetComponent<StalkPoint>();
                if (point == null)
                {
                    point = anchor.gameObject.AddComponent<StalkPoint>();
                    addedStalkPoints.Add(point);
                }

                point.ConfigureTopology(
                    metadata.IsNearCorner,
                    metadata.IsJunction,
                    metadata.IsDeadEnd,
                    metadata.AmbushAllowed);

                LightZone zone = module != null && lightGate.TryGetLightZone(module, out LightZone found)
                    ? found
                    : null;
                if (module != null && zone == null) point.ConfigureStaticDarkness(1f);

                point.enabled = true;
                point.Bind(safeZoneSet, zone, stalkPointSet);
                spawnedStalkPoints.Add(point);
            }
        }

        private List<MonsterSpawnOption> CollectSpawnOptions(GeneratedMapRuntimeData data)
        {
            var options = new List<MonsterSpawnOption>(
                data.SpawnCandidates.Count + data.StalkPoints.Count);
            if (data.StartAnchor == null
                || !NavMesh.SamplePosition(
                    data.StartAnchor.position,
                    out NavMeshHit startHit,
                    navMeshSampleRadius,
                    NavMesh.AllAreas))
            {
                return options;
            }

            foreach (Transform candidate in data.SpawnCandidates)
            {
                if (candidate == null || !TryGetSpawnTier(candidate, out MonsterSpawnTier tier)) continue;
                TryAddOption(options, startHit.position, candidate, tier, "spawn");
            }

            foreach (Transform stalkAnchor in data.StalkPoints)
            {
                if (stalkAnchor == null) continue;
                TryAddOption(
                    options,
                    startHit.position,
                    stalkAnchor,
                    MonsterSpawnTier.StalkFallback,
                    "stalk");
            }

            return options;
        }

        private static bool TryGetSpawnTier(Transform candidate, out MonsterSpawnTier tier)
        {
            GeneratedMapModule module = candidate.GetComponentInParent<GeneratedMapModule>();
            RoomRole role = module != null ? module.RoomRole : RoomRole.None;

            switch (role)
            {
                case RoomRole.None:
                    tier = MonsterSpawnTier.OrdinaryCandidate;
                    return true;
                case RoomRole.Fuse:
                case RoomRole.Exit:
                    tier = MonsterSpawnTier.RelaxedCandidate;
                    return true;
                case RoomRole.Start:
                case RoomRole.SafeZone:
                default:
                    tier = default;
                    return false;
            }
        }

        private void TryAddOption(
            List<MonsterSpawnOption> options,
            Vector3 startPosition,
            Transform source,
            MonsterSpawnTier tier,
            string sourceKind)
        {
            if (!NavMesh.SamplePosition(
                    source.position,
                    out NavMeshHit hit,
                    navMeshSampleRadius,
                    NavMesh.AllAreas))
            {
                return;
            }

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(startPosition, hit.position, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete)
            {
                return;
            }

            float pathLength = CalculatePathLength(path);
            string stableKey = string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1}|{2:F3},{3:F3},{4:F3}",
                sourceKind,
                BuildHierarchyPath(source),
                hit.position.x,
                hit.position.y,
                hit.position.z);
            options.Add(new MonsterSpawnOption(
                stableKey,
                hit.position,
                source.eulerAngles.y,
                pathLength,
                tier));
        }

        private static float CalculatePathLength(NavMeshPath path)
        {
            Vector3[] corners = path.corners;
            float length = 0f;
            for (int i = 1; i < corners.Length; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }
            return length;
        }

        private static string BuildHierarchyPath(Transform source)
        {
            var names = new Stack<string>();
            Transform current = source;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }
            return string.Join("/", names);
        }

        private void CleanupSpawnedContent()
        {
            if (spawnedRig != null)
            {
                wiring?.UnbindMonster(spawnedRig.Brain);
                GameObject root = spawnedRig.gameObject;
                spawnedRig = null;
                root.SetActive(false);
                DestroyRuntimeObject(root);
            }

            ClearStalkPoints();
            SelectedSpawnPathDistance = 0f;
        }

        private void ClearStalkPoints()
        {
            foreach (StalkPoint point in spawnedStalkPoints)
            {
                if (point != null) stalkPointSet?.Unregister(point);
            }

            foreach (StalkPoint point in addedStalkPoints)
            {
                if (point == null) continue;
                point.enabled = false;
                DestroyRuntimeObject(point);
            }

            spawnedStalkPoints.Clear();
            addedStalkPoints.Clear();
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
