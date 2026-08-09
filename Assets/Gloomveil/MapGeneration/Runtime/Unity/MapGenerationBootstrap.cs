using System;
using UnityEngine;

namespace Gloomveil.MapGeneration
{
    [DisallowMultipleComponent]
    public sealed class MapGenerationBootstrap : MonoBehaviour
    {
        [SerializeField] private MapGenerationConfig config;
        [SerializeField] private GeneratedNavMeshController navMeshController;
        [SerializeField] private GeneratedMapRegistry registry;
        [SerializeField] private bool generateOnStart;
        [SerializeField] private int startupSeed = 1042;

        private ModularMapBuilder builder;

        /// <summary>
        /// Set before loading a scene with a generate-on-start bootstrap to make it generate that
        /// seed instead of its serialized startupSeed, then self-clears — e.g. the Lobby's "start a
        /// new run" interaction sets a random value here right before loading MainGame, so each run
        /// gets a different map without needing a config asset or extra wiring.
        /// </summary>
        public static int? NextRunSeed;

        public MapGenerationConfig Config => config;
        public GeneratedNavMeshController NavMeshController => navMeshController;
        public GeneratedMapRegistry Registry => registry;
        public MapLayout LastLayout { get; private set; }
        public GeneratedMapRuntimeData LastRuntimeData { get; private set; }
        public RuntimeValidationReport LastRuntimeValidation { get; private set; }
        public string LastFailureStage { get; private set; }
        public string LastError { get; private set; }
        public int CurrentSeed { get; private set; }

        public void Configure(
            MapGenerationConfig generationConfig,
            GeneratedNavMeshController generatedNavMeshController,
            GeneratedMapRegistry generatedMapRegistry,
            bool generateAtStart = false,
            int initialSeed = 1042)
        {
            if (generationConfig == null)
            {
                throw new ArgumentNullException(nameof(generationConfig));
            }

            if (generatedNavMeshController == null)
            {
                throw new ArgumentNullException(nameof(generatedNavMeshController));
            }

            if (generatedMapRegistry == null)
            {
                throw new ArgumentNullException(nameof(generatedMapRegistry));
            }

            ClearGeneratedMap();
            config = generationConfig;
            navMeshController = generatedNavMeshController;
            registry = generatedMapRegistry;
            generateOnStart = generateAtStart;
            startupSeed = initialSeed;
        }

        public void Generate(int seed)
        {
            CurrentSeed = seed;
            LastFailureStage = null;
            LastError = null;
            string stage = "Initialize";

            try
            {
                RequireConfiguration();
                ClearGeneratedMap();

                stage = "GenerateSpatialLayout";
                MapGenerationParameters parameters = config.ToParameters();
                LastLayout = new VariableFootprintLayoutGenerator().Generate(parameters, seed);

                stage = "AssignRoomRoles";
                new GameplayRoleAssigner().Assign(
                    LastLayout,
                    parameters,
                    new SeededRandom(seed, "roles"));

                stage = "ValidateLayout";
                MapValidationReport layoutReport =
                    new MapLayoutValidator().Validate(LastLayout, parameters);
                if (!layoutReport.IsValid)
                {
                    throw new MapGenerationException(string.Join("\n", layoutReport.Errors));
                }

                stage = "ResolveAndBuildModules";
                builder = new ModularMapBuilder(
                    config.ModuleCatalog,
                    config.UnitSize,
                    config.FloorHeight,
                    navMeshController.ContentRoot);
                LastRuntimeData = builder.Build(LastLayout, seed);

                stage = "BuildNavMeshAndValidateRuntimePaths";
                LastRuntimeValidation =
                    navMeshController.RebuildAndValidate(LastRuntimeData);
                if (!LastRuntimeValidation.IsValid)
                {
                    throw new MapGenerationException(
                        string.Join("\n", LastRuntimeValidation.Errors));
                }

                stage = "Publish";
                registry.Publish(LastRuntimeData);
            }
            catch (Exception exception)
            {
                LastFailureStage = stage;
                LastError = exception.Message;
                registry?.Clear();
                navMeshController?.Clear();
                HandleFailedRoot();
                Debug.LogError(
                    $"Map generation failed: seed={seed} stage={stage}. {exception.Message}",
                    this);
            }
        }

        public void ClearGeneratedMap()
        {
            if (registry != null &&
                registry.TryGetCurrent(out GeneratedMapRuntimeData published) &&
                published.Root != null)
            {
                published.Root.SetActive(false);
            }

            if (builder?.GeneratedRoot != null)
            {
                builder.GeneratedRoot.SetActive(false);
            }

            registry?.Clear();
            navMeshController?.Clear();
            builder?.DestroyGeneratedRoot();
            DestroyOrphanedGeneratedRoots();
            builder = null;
            LastLayout = null;
            LastRuntimeData = null;
            LastRuntimeValidation = null;
        }

        private void DestroyOrphanedGeneratedRoots()
        {
            if (navMeshController == null)
            {
                return;
            }

            Transform contentRoot = navMeshController.ContentRoot;
            for (int index = contentRoot.childCount - 1; index >= 0; index--)
            {
                GameObject child = contentRoot.GetChild(index).gameObject;
                if (!child.name.StartsWith("GeneratedMap_", StringComparison.Ordinal))
                {
                    continue;
                }

                child.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private void Awake()
        {
            if (navMeshController == null)
            {
                navMeshController = GetComponent<GeneratedNavMeshController>();
            }

            if (registry == null)
            {
                registry = GetComponent<GeneratedMapRegistry>();
            }
        }

        private void Start()
        {
            if (generateOnStart)
            {
                int seed = NextRunSeed ?? startupSeed;
                NextRunSeed = null;
                Generate(seed);
            }
        }

        private void OnDestroy()
        {
            ClearGeneratedMap();
        }

        private void RequireConfiguration()
        {
            if (config == null)
            {
                throw new InvalidOperationException("MapGenerationBootstrap has no config.");
            }

            if (config.ModuleCatalog == null)
            {
                throw new InvalidOperationException("MapGenerationConfig has no module catalog.");
            }

            if (navMeshController == null)
            {
                throw new InvalidOperationException(
                    "MapGenerationBootstrap has no GeneratedNavMeshController.");
            }

            if (registry == null)
            {
                throw new InvalidOperationException(
                    "MapGenerationBootstrap has no GeneratedMapRegistry.");
            }
        }

        private void HandleFailedRoot()
        {
            GameObject failedRoot = builder?.GeneratedRoot ?? LastRuntimeData?.Root;
            if (failedRoot == null)
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            failedRoot.SetActive(false);
#else
            builder?.DestroyGeneratedRoot();
#endif
        }
    }
}
