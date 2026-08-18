using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

namespace Gloomveil.MapGeneration
{
    public sealed class GeneratedMapRuntimeData
    {
        public GameObject Root { get; }
        public IReadOnlyList<GeneratedMapModule> Modules { get; }
        public IReadOnlyList<GeneratedMapModule> RoomModules { get; }
        public IReadOnlyList<GeneratedMapModule> CorridorModules { get; }
        public IReadOnlyList<GeneratedMapModule> StairModules { get; }
        public IReadOnlyList<Transform> StalkPoints { get; }
        public IReadOnlyList<Transform> FuseAnchors { get; }
        public IReadOnlyList<Transform> SafeZoneAnchors { get; }
        public IReadOnlyList<Transform> LightAnchors { get; }
        public IReadOnlyList<Transform> SpawnCandidates { get; }
        public GeneratedMapModule StartModule { get; }
        public GeneratedMapModule ExitModule { get; }
        public Transform StartAnchor { get; }
        public Transform ExitAnchor { get; }
        public int Seed { get; }
        public int WidthUnits { get; }
        public int DepthUnits { get; }
        public int FloorCount { get; }
        public string TopologySignature { get; }
        public MapTraversalGraph TraversalGraph { get; }
        public MinimapTopology MinimapTopology { get; }
        public float UnitSize { get; }
        public float FloorHeight { get; }

        public GeneratedMapRuntimeData(
            GameObject root,
            IEnumerable<GeneratedMapModule> modules,
            IEnumerable<Transform> stalkPoints,
            IEnumerable<Transform> fuseAnchors,
            IEnumerable<Transform> safeZoneAnchors,
            IEnumerable<Transform> lightAnchors,
            IEnumerable<Transform> spawnCandidates,
            GeneratedMapModule startModule,
            GeneratedMapModule exitModule,
            Transform startAnchor,
            Transform exitAnchor,
            int seed,
            int widthUnits,
            int depthUnits,
            int floorCount,
            string topologySignature,
            MapTraversalGraph traversalGraph = null,
            MinimapTopology minimapTopology = null,
            float unitSize = 1f,
            float floorHeight = 1f)
        {
            if (unitSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(unitSize));
            }

            if (floorHeight <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(floorHeight));
            }

            Root = root != null ? root : throw new ArgumentNullException(nameof(root));
            Modules = ReadOnly(modules, nameof(modules));
            RoomModules = Filter(Modules, MapModuleCategory.Room);
            CorridorModules = Filter(Modules, MapModuleCategory.Corridor);
            StairModules = Filter(Modules, MapModuleCategory.Stair);
            StalkPoints = ReadOnly(stalkPoints, nameof(stalkPoints));
            FuseAnchors = ReadOnly(fuseAnchors, nameof(fuseAnchors));
            SafeZoneAnchors = ReadOnly(safeZoneAnchors, nameof(safeZoneAnchors));
            LightAnchors = ReadOnly(lightAnchors, nameof(lightAnchors));
            SpawnCandidates = ReadOnly(spawnCandidates, nameof(spawnCandidates));
            StartModule = startModule;
            ExitModule = exitModule;
            StartAnchor = startAnchor;
            ExitAnchor = exitAnchor;
            Seed = seed;
            WidthUnits = widthUnits;
            DepthUnits = depthUnits;
            FloorCount = floorCount;
            TopologySignature = topologySignature ?? throw new ArgumentNullException(nameof(topologySignature));
            TraversalGraph = traversalGraph;
            MinimapTopology = minimapTopology;
            UnitSize = unitSize;
            FloorHeight = floorHeight;
        }

        private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> source, string argumentName) =>
            new ReadOnlyCollection<T>(
                new List<T>(source ?? throw new ArgumentNullException(argumentName)));

        private static IReadOnlyList<GeneratedMapModule> Filter(
            IEnumerable<GeneratedMapModule> modules,
            MapModuleCategory category) =>
            new ReadOnlyCollection<GeneratedMapModule>(
                modules.Where(module => module.Category == category).ToList());
    }
}
