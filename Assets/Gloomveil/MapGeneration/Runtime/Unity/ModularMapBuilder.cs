using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gloomveil.MapGeneration
{
    public sealed class ModularMapBuilder
    {
        private readonly MapModuleResolver resolver;
        private readonly float unitSize;
        private readonly float floorHeight;
        private readonly Transform parent;
        private GameObject generatedRoot;

        public GameObject GeneratedRoot => generatedRoot;

        public ModularMapBuilder(
            MapModuleCatalog catalog,
            float unitSize,
            float floorHeight,
            Transform parent = null)
        {
            if (unitSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(unitSize));
            }

            if (floorHeight <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(floorHeight));
            }

            resolver = new MapModuleResolver(catalog);
            this.unitSize = unitSize;
            this.floorHeight = floorHeight;
            this.parent = parent;
        }

        public GeneratedMapRuntimeData Build(MapLayout layout, int seed)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            DestroyGeneratedRoot();
            generatedRoot = new GameObject($"GeneratedMap_{seed}");
            if (parent != null)
            {
                generatedRoot.transform.SetParent(parent, false);
            }

            var modules = new List<GeneratedMapModule>();
            foreach (PlacedRoom room in layout.Rooms.OrderBy(room => room.PlacementId))
            {
                MapModuleSelection selection = resolver.ResolveRoom(
                    room,
                    new SeededRandom(seed, $"module-room-{room.PlacementId}"));
                modules.Add(Instantiate(selection, seed));
            }

            foreach (CorridorModulePlacement corridor in layout.CorridorModules
                         .OrderBy(corridor => corridor.PlacementId))
            {
                MapModuleSelection selection = resolver.ResolveCorridor(
                    corridor,
                    new SeededRandom(seed, $"module-corridor-{corridor.PlacementId}"));
                modules.Add(Instantiate(selection, seed));
            }

            foreach (PlacedStair stair in layout.Stairs.OrderBy(stair => stair.PlacementId))
            {
                MapModuleSelection selection = resolver.ResolveStair(
                    stair,
                    new SeededRandom(seed, $"module-stair-{stair.PlacementId}"));
                modules.Add(Instantiate(selection, seed));
            }

            return CollectRuntimeData(layout, modules, seed);
        }

        public void DestroyGeneratedRoot()
        {
            if (generatedRoot == null)
            {
                return;
            }

            generatedRoot.SetActive(false);
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(generatedRoot);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(generatedRoot);
            }

            generatedRoot = null;
        }

        private GeneratedMapModule Instantiate(MapModuleSelection selection, int seed)
        {
            GameObject instance = UnityEngine.Object.Instantiate(
                selection.Prefab,
                generatedRoot.transform);
            instance.name = $"{selection.CatalogEntryId}__{selection.PlacementId}";

            GridSize2 size = selection.RotatedSize;
            instance.transform.localPosition = new Vector3(
                (selection.Origin.X + (size.Width * 0.5f)) * unitSize,
                selection.MinFloor * floorHeight,
                (selection.Origin.Z + (size.Depth * 0.5f)) * unitSize);
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            if (selection.UsesDynamicSockets)
            {
                ModuleSocketController socketController =
                    instance.GetComponent<ModuleSocketController>();
                if (socketController == null)
                {
                    throw new MapGenerationException(
                        $"Dynamic prefab '{selection.Prefab.name}' has no ModuleSocketController.");
                }

                socketController.Apply(selection.OpenLocalSockets);
            }

            instance.transform.localRotation = Quaternion.Euler(
                0f,
                selection.QuarterTurns * 90f,
                0f);
            GeneratedMapModule generated = instance.GetComponent<GeneratedMapModule>();
            if (generated == null)
            {
                generated = instance.AddComponent<GeneratedMapModule>();
            }

            generated.Initialize(selection, seed);
            if (selection.Category == MapModuleCategory.Stair)
            {
                MapAnchor[] anchors = instance.GetComponentsInChildren<MapAnchor>(true);
                generated.SetStairLandings(
                    anchors.FirstOrDefault(anchor => anchor.Kind == MapAnchorKind.StairBottom)?.transform,
                    anchors.FirstOrDefault(anchor => anchor.Kind == MapAnchorKind.StairTop)?.transform);
            }

            return generated;
        }

        private GeneratedMapRuntimeData CollectRuntimeData(
            MapLayout layout,
            IReadOnlyList<GeneratedMapModule> modules,
            int seed)
        {
            var stalkPoints = new List<Transform>();
            var fuseAnchors = new List<Transform>();
            var safeAnchors = new List<Transform>();
            var lightAnchors = new List<Transform>();
            var spawnCandidates = new List<Transform>();
            GeneratedMapModule startModule = null;
            GeneratedMapModule exitModule = null;
            Transform startAnchor = null;
            Transform exitAnchor = null;

            foreach (GeneratedMapModule module in modules)
            {
                MapAnchor[] anchors = module.GetComponentsInChildren<MapAnchor>(true);
                stalkPoints.AddRange(anchors
                    .Where(anchor => anchor.Kind == MapAnchorKind.StalkPoint)
                    .Select(anchor => anchor.transform));
                lightAnchors.AddRange(anchors
                    .Where(anchor => anchor.Kind == MapAnchorKind.Light)
                    .Select(anchor => anchor.transform));
                spawnCandidates.AddRange(anchors
                    .Where(anchor => anchor.Kind == MapAnchorKind.SpawnCandidate)
                    .Select(anchor => anchor.transform));

                if (module.RoomRole == RoomRole.Start)
                {
                    startModule = module;
                    startAnchor = anchors
                        .FirstOrDefault(anchor => anchor.Kind == MapAnchorKind.PlayerStart)
                        ?.transform;
                }
                else if (module.RoomRole == RoomRole.Exit)
                {
                    exitModule = module;
                    exitAnchor = anchors
                        .FirstOrDefault(anchor => anchor.Kind == MapAnchorKind.Exit)
                        ?.transform;
                }
                else if (module.RoomRole == RoomRole.Fuse)
                {
                    Transform fuse = anchors
                        .FirstOrDefault(anchor => anchor.Kind == MapAnchorKind.Fuse)
                        ?.transform;
                    if (fuse != null)
                    {
                        fuseAnchors.Add(fuse);
                    }
                }
                else if (module.RoomRole == RoomRole.SafeZone)
                {
                    Transform safe = anchors
                        .FirstOrDefault(anchor => anchor.Kind == MapAnchorKind.SafeZone)
                        ?.transform;
                    if (safe != null)
                    {
                        safeAnchors.Add(safe);
                    }
                }
            }

            return new GeneratedMapRuntimeData(
                generatedRoot,
                modules,
                stalkPoints,
                fuseAnchors,
                safeAnchors,
                lightAnchors,
                spawnCandidates,
                startModule,
                exitModule,
                startAnchor,
                exitAnchor,
                seed,
                layout.WidthUnits,
                layout.DepthUnits,
                layout.FloorCount,
                layout.CreateSignature());
        }
    }
}
