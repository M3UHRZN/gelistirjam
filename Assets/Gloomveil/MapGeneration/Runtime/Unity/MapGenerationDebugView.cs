using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Gloomveil.MapGeneration
{
    [DisallowMultipleComponent]
    public sealed class MapGenerationDebugView : MonoBehaviour
    {
        [SerializeField] private MapGenerationBootstrap bootstrap;
        [SerializeField, Min(0f)] private float lineHeightOffset = 0.08f;
        [SerializeField] private bool showModuleLabels = true;

        public void Configure(MapGenerationBootstrap mapBootstrap)
        {
            bootstrap = mapBootstrap != null
                ? mapBootstrap
                : throw new ArgumentNullException(nameof(mapBootstrap));
        }

        private void OnDrawGizmos()
        {
            MapGenerationBootstrap source = bootstrap != null
                ? bootstrap
                : GetComponent<MapGenerationBootstrap>();
            if (source == null ||
                source.LastLayout == null ||
                source.LastRuntimeData == null ||
                source.Config == null)
            {
                return;
            }

            DrawRooms(source);
            DrawCorridors(source);
            DrawStairs(source);
            DrawInvalidPaths(source.LastRuntimeValidation);
            DrawEditorLabels(source);
        }

        private void DrawRooms(MapGenerationBootstrap source)
        {
            foreach (GeneratedMapModule module in source.LastRuntimeData.RoomModules)
            {
                DrawFlatBounds(
                    module,
                    module.RotatedFootprintUnits,
                    source.Config.UnitSize,
                    RoleColor(module.RoomRole));
            }
        }

        private void DrawCorridors(MapGenerationBootstrap source)
        {
            foreach (GeneratedMapModule module in source.LastRuntimeData.CorridorModules)
            {
                DrawFlatBounds(
                    module,
                    module.RotatedFootprintUnits,
                    source.Config.UnitSize,
                    new Color(0.35f, 0.65f, 1f, 1f));

                CorridorModulePlacement placement = source.LastLayout.CorridorModules
                    .FirstOrDefault(candidate => candidate.PlacementId == module.PlacementId);
                if (placement == null)
                {
                    continue;
                }

                Gizmos.color = new Color(0.15f, 0.85f, 1f, 1f);
                Vector3 localCenter = Vector3.up * lineHeightOffset;
                foreach (SocketKey socket in placement.OpenLocalSockets)
                {
                    Vector3 localSocket = SocketLocalPosition(
                        placement.CanonicalSize,
                        socket,
                        source.Config.UnitSize,
                        source.Config.FloorHeight);
                    localSocket.y += lineHeightOffset;
                    Gizmos.DrawLine(
                        module.transform.TransformPoint(localCenter),
                        module.transform.TransformPoint(localSocket));
                }
            }
        }

        private void DrawStairs(MapGenerationBootstrap source)
        {
            Gizmos.color = Color.cyan;
            foreach (GeneratedMapModule stair in source.LastRuntimeData.StairModules)
            {
                Vector3 size = new Vector3(
                    stair.RotatedFootprintUnits.Width * source.Config.UnitSize,
                    source.Config.FloorHeight,
                    stair.RotatedFootprintUnits.Depth * source.Config.UnitSize);
                Vector3 center = stair.transform.position +
                    (Vector3.up * source.Config.FloorHeight * 0.5f);
                Gizmos.DrawWireCube(center, size);

                if (stair.BottomLanding != null && stair.TopLanding != null)
                {
                    Gizmos.DrawLine(
                        stair.BottomLanding.position,
                        stair.TopLanding.position);
                    Gizmos.DrawWireSphere(stair.BottomLanding.position, 0.18f);
                    Gizmos.DrawWireSphere(stair.TopLanding.position, 0.18f);
                }
            }
        }

        private static void DrawInvalidPaths(RuntimeValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            Gizmos.color = Color.red;
            foreach (RuntimePathCheck check in report.PathChecks.Where(check => !check.IsComplete))
            {
                if (check.SourcePosition == Vector3.zero &&
                    check.TargetPosition == Vector3.zero)
                {
                    continue;
                }

                Gizmos.DrawLine(check.SourcePosition, check.TargetPosition);
                Gizmos.DrawWireSphere(check.SourcePosition, 0.25f);
                Gizmos.DrawWireSphere(check.TargetPosition, 0.25f);
            }
        }

        private void DrawFlatBounds(
            GeneratedMapModule module,
            GridSize2 footprint,
            float unitSize,
            Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawWireCube(
                module.transform.position + (Vector3.up * lineHeightOffset),
                new Vector3(
                    footprint.Width * unitSize,
                    0.04f,
                    footprint.Depth * unitSize));
        }

        private static Vector3 SocketLocalPosition(
            GridSize2 size,
            SocketKey socket,
            float unitSize,
            float floorHeight)
        {
            float y = socket.FloorOffset * floorHeight;
            switch (socket.Side)
            {
                case CardinalDirection.North:
                    return new Vector3(
                        (socket.Index + 0.5f - (size.Width / 2f)) * unitSize,
                        y,
                        size.Depth * unitSize * 0.5f);
                case CardinalDirection.East:
                    return new Vector3(
                        size.Width * unitSize * 0.5f,
                        y,
                        (socket.Index + 0.5f - (size.Depth / 2f)) * unitSize);
                case CardinalDirection.South:
                    return new Vector3(
                        (socket.Index + 0.5f - (size.Width / 2f)) * unitSize,
                        y,
                        -size.Depth * unitSize * 0.5f);
                default:
                    return new Vector3(
                        -size.Width * unitSize * 0.5f,
                        y,
                        (socket.Index + 0.5f - (size.Depth / 2f)) * unitSize);
            }
        }

        private static Color RoleColor(RoomRole role)
        {
            switch (role)
            {
                case RoomRole.Start:
                    return Color.green;
                case RoomRole.Exit:
                    return new Color(1f, 0.2f, 0.8f, 1f);
                case RoomRole.Fuse:
                    return Color.yellow;
                case RoomRole.SafeZone:
                    return new Color(0.25f, 1f, 0.55f, 1f);
                default:
                    return Color.white;
            }
        }

        private static IReadOnlyList<SocketKey> GetOpenSockets(
            MapLayout layout,
            GeneratedMapModule module,
            out int quarterTurns)
        {
            quarterTurns = 0;
            if (module.Category == MapModuleCategory.Room)
            {
                PlacedRoom room = layout.Rooms
                    .FirstOrDefault(candidate => candidate.PlacementId == module.PlacementId);
                if (room != null)
                {
                    quarterTurns = room.QuarterTurns;
                    return room.OpenLocalSockets;
                }
            }
            else if (module.Category == MapModuleCategory.Corridor)
            {
                CorridorModulePlacement corridor = layout.CorridorModules
                    .FirstOrDefault(candidate => candidate.PlacementId == module.PlacementId);
                if (corridor != null)
                {
                    quarterTurns = corridor.QuarterTurns;
                    return corridor.OpenLocalSockets;
                }
            }
            else
            {
                PlacedStair stair = layout.Stairs
                    .FirstOrDefault(candidate => candidate.PlacementId == module.PlacementId);
                if (stair != null)
                {
                    quarterTurns = stair.QuarterTurns;
                    var sockets = new List<SocketKey>(2);
                    if (stair.BottomPort.HasValue)
                    {
                        sockets.Add(stair.BottomPort.Value.LocalSocket);
                    }

                    if (stair.TopPort.HasValue)
                    {
                        sockets.Add(stair.TopPort.Value.LocalSocket);
                    }

                    return sockets;
                }
            }

            return Array.Empty<SocketKey>();
        }

        private void DrawEditorLabels(MapGenerationBootstrap source)
        {
#if UNITY_EDITOR
            Vector3 summaryPosition = source.transform.position + Vector3.up;
            Handles.Label(
                summaryPosition,
                $"Map seed={source.CurrentSeed} floors={source.LastRuntimeData.FloorCount}");

            if (showModuleLabels)
            {
                foreach (GeneratedMapModule stair in source.LastRuntimeData.StairModules)
                {
                    if (stair.BottomLanding != null)
                    {
                        Handles.Label(
                            stair.BottomLanding.position,
                            $"P{stair.PlacementId} lower F{stair.MinFloor}");
                    }

                    if (stair.TopLanding != null)
                    {
                        Handles.Label(
                            stair.TopLanding.position,
                            $"P{stair.PlacementId} upper F{stair.MaxFloor}");
                    }
                }
            }

            GameObject selected = Selection.activeGameObject;
            GeneratedMapModule module = selected != null
                ? selected.GetComponentInParent<GeneratedMapModule>()
                : null;
            if (module == null || !source.LastRuntimeData.Modules.Contains(module))
            {
                return;
            }

            IReadOnlyList<SocketKey> sockets = GetOpenSockets(
                source.LastLayout,
                module,
                out int quarterTurns);
            string socketList = sockets.Count == 0
                ? "none"
                : string.Join(", ", sockets.Select(socket => socket.ToString()));
            Handles.Label(
                module.transform.position + (Vector3.up * 1.5f),
                $"PlacementId: {module.PlacementId}\n" +
                $"CatalogEntryId: {module.CatalogEntryId}\n" +
                $"Canonical: {module.CanonicalFootprintUnits}\n" +
                $"Rotated: {module.RotatedFootprintUnits}\n" +
                $"QuarterTurns: {quarterTurns}\n" +
                $"OpenSockets: {socketList}\n" +
                $"Seed: {module.Seed}  Floors: {source.LastRuntimeData.FloorCount}");
#endif
        }
    }
}
