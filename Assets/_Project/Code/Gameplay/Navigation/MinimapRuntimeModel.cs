using System;
using System.Collections.Generic;
using System.Linq;
using Game.Gameplay.Objectives;
using Game.Gameplay.World;
using Gloomveil.MapGeneration;
using UnityEngine;

namespace Game.Gameplay.Navigation
{
    [DisallowMultipleComponent]
    public sealed class MinimapRuntimeModel : MonoBehaviour
    {
        [SerializeField] private GeneratedMapRegistry registry;
        [SerializeField] private Transform player;
        [SerializeField] private MapFuseGate fuseGate;
        [SerializeField] private MapExitController exitController;
        [SerializeField, Min(0.02f)] private float refreshInterval = 0.1f;

        private bool subscribed;
        private float nextRefreshTime;

        public MinimapRuntimeSnapshot Current { get; private set; } = MinimapRuntimeSnapshot.Empty;
        public event Action<MinimapRuntimeSnapshot> SnapshotChanged;

        public void Bind(
            GeneratedMapRegistry mapRegistry,
            Transform playerTransform,
            MapFuseGate mapFuseGate,
            MapExitController mapExitController)
        {
            Unsubscribe();
            registry = mapRegistry;
            player = playerTransform;
            fuseGate = mapFuseGate;
            exitController = mapExitController;

            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        private void OnEnable() => Subscribe();
        private void Start() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, refreshInterval);
            RefreshNow();
        }

        public void RefreshNow()
        {
            if (registry == null || player == null ||
                !registry.TryGetCurrent(out GeneratedMapRuntimeData data) ||
                data.MinimapTopology == null || data.TraversalGraph == null)
            {
                Publish(MinimapRuntimeSnapshot.Empty);
                return;
            }

            Vector2 playerPosition = WorldToMap(data, player.position);
            int playerFloor = WorldToFloor(data, player.position);
            float playerYaw = PlayerYawInMapSpace(data);
            MapTraversalNode? playerNode = FindNearestNode(
                data.TraversalGraph,
                playerPosition,
                playerFloor);

            bool hasFuseDirection = false;
            Vector2 fuseDirection = Vector2.zero;
            IReadOnlyList<MinimapRoutePoint> exitRoute = Array.Empty<MinimapRoutePoint>();

            if (exitController != null && exitController.IsOpen)
            {
                exitRoute = BuildExitRoute(data, playerPosition, playerFloor, playerNode);
            }
            else
            {
                hasFuseDirection = TryBuildFuseDirection(
                    data,
                    playerPosition,
                    playerNode,
                    out fuseDirection);
            }

            Publish(new MinimapRuntimeSnapshot(
                data.MinimapTopology,
                playerFloor,
                playerPosition,
                playerYaw,
                hasFuseDirection,
                fuseDirection,
                exitRoute));
        }

        private void Subscribe()
        {
            if (subscribed || registry == null)
            {
                return;
            }

            subscribed = true;
            registry.MapReady += HandleMapReady;
            registry.MapCleared += HandleMapCleared;
            if (exitController != null)
            {
                exitController.OnExitOpened += HandleExitOpened;
            }

            RefreshNow();
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            registry.MapReady -= HandleMapReady;
            registry.MapCleared -= HandleMapCleared;
            if (exitController != null)
            {
                exitController.OnExitOpened -= HandleExitOpened;
            }

            subscribed = false;
        }

        private void HandleMapReady(GeneratedMapRuntimeData _) => RefreshNow();

        private void HandleMapCleared(GeneratedMapRuntimeData _) =>
            Publish(MinimapRuntimeSnapshot.Empty);

        private void HandleExitOpened() => RefreshNow();

        private bool TryBuildFuseDirection(
            GeneratedMapRuntimeData data,
            Vector2 playerPosition,
            MapTraversalNode? playerNode,
            out Vector2 direction)
        {
            direction = Vector2.zero;
            if (fuseGate == null || playerNode == null)
            {
                return false;
            }

            IReadOnlyDictionary<int, int> distances = MapGraphSearch.WeightedDistancesFrom(
                data.TraversalGraph,
                playerNode.Value.Id);
            FuseBox selectedFuse = null;
            MapTraversalNode? selectedNode = null;
            int selectedDistance = int.MaxValue;
            float selectedStraightDistance = float.MaxValue;

            foreach (FuseBox fuse in fuseGate.SpawnedFuseBoxes)
            {
                if (fuse == null || fuse.State == FuseBoxState.Complete)
                {
                    continue;
                }

                Vector2 fusePosition = WorldToMap(data, fuse.transform.position);
                int fuseFloor = WorldToFloor(data, fuse.transform.position);
                MapTraversalNode? fuseNode = FindNearestNode(
                    data.TraversalGraph,
                    fusePosition,
                    fuseFloor);
                if (fuseNode == null ||
                    !distances.TryGetValue(fuseNode.Value.Id, out int graphDistance) ||
                    graphDistance == int.MaxValue)
                {
                    continue;
                }

                float straightDistance = (fusePosition - playerPosition).sqrMagnitude;
                if (graphDistance < selectedDistance ||
                    (graphDistance == selectedDistance && straightDistance < selectedStraightDistance))
                {
                    selectedFuse = fuse;
                    selectedNode = fuseNode;
                    selectedDistance = graphDistance;
                    selectedStraightDistance = straightDistance;
                }
            }

            if (selectedFuse == null || selectedNode == null)
            {
                return false;
            }

            IReadOnlyList<MapTraversalNode> path = MapGraphSearch.WeightedShortestPath(
                data.TraversalGraph,
                playerNode.Value.Id,
                selectedNode.Value.Id);
            Vector2 target = path.Count > 1
                ? NodePosition(path[1])
                : WorldToMap(data, selectedFuse.transform.position);
            direction = target - playerPosition;
            return direction.sqrMagnitude > 0.0001f;
        }

        private IReadOnlyList<MinimapRoutePoint> BuildExitRoute(
            GeneratedMapRuntimeData data,
            Vector2 playerPosition,
            int playerFloor,
            MapTraversalNode? playerNode)
        {
            if (playerNode == null || data.ExitModule == null)
            {
                return Array.Empty<MinimapRoutePoint>();
            }

            MapTraversalNode? exitNode = data.TraversalGraph.Nodes
                .Where(node => node.Kind == MapTraversalNodeKind.Room)
                .Cast<MapTraversalNode?>()
                .FirstOrDefault(node => node.Value.OwnerPlacementId == data.ExitModule.PlacementId);
            if (exitNode == null)
            {
                Vector3 exitWorldPosition = exitController != null && exitController.SpawnedExit != null
                    ? exitController.SpawnedExit.position
                    : data.ExitAnchor.position;
                exitNode = FindNearestNode(
                    data.TraversalGraph,
                    WorldToMap(data, exitWorldPosition),
                    WorldToFloor(data, exitWorldPosition));
            }

            if (exitNode == null)
            {
                return Array.Empty<MinimapRoutePoint>();
            }

            IReadOnlyList<MapTraversalNode> path = MapGraphSearch.WeightedShortestPath(
                data.TraversalGraph,
                playerNode.Value.Id,
                exitNode.Value.Id);
            if (path.Count == 0)
            {
                return Array.Empty<MinimapRoutePoint>();
            }

            var route = new List<MinimapRoutePoint>(path.Count + 2)
            {
                new MinimapRoutePoint(playerPosition, playerFloor)
            };
            route.AddRange(path.Select(node =>
                new MinimapRoutePoint(NodePosition(node), node.Coord.Floor)));

            Transform exitTransform = exitController != null && exitController.SpawnedExit != null
                ? exitController.SpawnedExit
                : data.ExitAnchor;
            if (exitTransform != null)
            {
                route.Add(new MinimapRoutePoint(
                    WorldToMap(data, exitTransform.position),
                    WorldToFloor(data, exitTransform.position)));
            }

            return route;
        }

        private static MapTraversalNode? FindNearestNode(
            MapTraversalGraph graph,
            Vector2 mapPosition,
            int floor)
        {
            MapTraversalNode? nearest = null;
            float nearestDistance = float.MaxValue;
            foreach (MapTraversalNode node in graph.Nodes)
            {
                if (node.Coord.Floor != floor)
                {
                    continue;
                }

                float distance = (NodePosition(node) - mapPosition).sqrMagnitude;
                if (distance < nearestDistance ||
                    (Mathf.Approximately(distance, nearestDistance) &&
                     (nearest == null || node.Id < nearest.Value.Id)))
                {
                    nearest = node;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private static Vector2 NodePosition(MapTraversalNode node) =>
            new Vector2(node.Coord.X + 0.5f, node.Coord.Z + 0.5f);

        private static Vector2 WorldToMap(GeneratedMapRuntimeData data, Vector3 worldPosition)
        {
            Vector3 local = data.Root.transform.InverseTransformPoint(worldPosition);
            return new Vector2(local.x / data.UnitSize, local.z / data.UnitSize);
        }

        private static int WorldToFloor(GeneratedMapRuntimeData data, Vector3 worldPosition)
        {
            Vector3 local = data.Root.transform.InverseTransformPoint(worldPosition);
            int floor = Mathf.RoundToInt(local.y / data.FloorHeight);
            return Mathf.Clamp(floor, 0, data.FloorCount - 1);
        }

        private float PlayerYawInMapSpace(GeneratedMapRuntimeData data)
        {
            Vector3 localForward = data.Root.transform.InverseTransformDirection(player.forward);
            localForward.y = 0f;
            return localForward.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(localForward.x, localForward.z) * Mathf.Rad2Deg
                : 0f;
        }

        private void Publish(MinimapRuntimeSnapshot snapshot)
        {
            Current = snapshot;
            SnapshotChanged?.Invoke(snapshot);
        }
    }
}
