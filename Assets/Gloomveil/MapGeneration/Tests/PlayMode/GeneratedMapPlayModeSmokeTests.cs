using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.AI.Navigation;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Gloomveil.MapGeneration.Tests
{
    public sealed class GeneratedMapPlayModeSmokeTests
    {
        private const string CatalogPath =
            "Assets/Gloomveil/MapGeneration/Data/GrayboxMapModuleCatalog.asset";

        [UnityTest]
        public IEnumerator Generate_BuildsNavigableMapsThenPublishes()
        {
            foreach (int floors in new[] { 2, 3, 4 })
            {
                MapGenerationFixture fixture = CreateFixture(floors);
                int publishCount = 0;
                Action<GeneratedMapRuntimeData> onReady = _ => publishCount++;
                fixture.Registry.MapReady += onReady;

                try
                {
                    fixture.Bootstrap.Generate(1042);
                    yield return null;

                    Assert.That(fixture.Registry.IsReady, Is.True, $"floors={floors}");
                    Assert.That(publishCount, Is.EqualTo(1), $"floors={floors}");
                    GeneratedMapRuntimeData data = fixture.Registry.Current;
                    int visiblePlacementCount =
                        data.RoomModules.Count +
                        data.CorridorModules.Count +
                        data.StairModules.Count;
                    Assert.That(data.Modules.Count, Is.EqualTo(visiblePlacementCount));
                    Assert.That(
                        data.Modules.Count,
                        Is.LessThan(data.WidthUnits * data.DepthUnits * floors));
                    Assert.That(
                        data.RoomModules.Select(module => module.CanonicalFootprintUnits),
                        Is.SupersetOf(new[]
                        {
                            new GridSize2(1, 2),
                            new GridSize2(2, 2),
                            new GridSize2(2, 3)
                        }));

                    AssertCompletePath(
                        data.StartAnchor,
                        data.ExitAnchor,
                        fixture.Surface.agentTypeID);
                    foreach (Transform fuse in data.FuseAnchors)
                    {
                        AssertCompletePath(
                            data.StartAnchor,
                            fuse,
                            fixture.Surface.agentTypeID);
                    }

                    foreach (GeneratedMapModule stair in data.StairModules)
                    {
                        AssertCompletePath(
                            data.StartAnchor,
                            stair.BottomLanding,
                            fixture.Surface.agentTypeID);
                        AssertCompletePath(
                            data.StartAnchor,
                            stair.TopLanding,
                            fixture.Surface.agentTypeID);
                        AssertCompletePath(
                            stair.BottomLanding,
                            stair.TopLanding,
                            fixture.Surface.agentTypeID);
                    }

                    if (floors == 2)
                    {
                        GameObject previousRoot = data.Root;
                        NavMeshData previousNavMeshData = fixture.Surface.navMeshData;
                        publishCount = 0;

                        fixture.Bootstrap.Generate(1042);

                        Assert.That(previousRoot.activeSelf, Is.False);
                        Assert.That(fixture.Registry.IsReady, Is.True);
                        Assert.That(publishCount, Is.EqualTo(1));
                        Assert.That(fixture.Registry.Current.Root, Is.Not.SameAs(previousRoot));
                        Assert.That(fixture.Surface.navMeshData, Is.Not.Null);
                        Assert.That(
                            fixture.Surface.navMeshData,
                            Is.Not.SameAs(previousNavMeshData));
                        yield return null;
                        Assert.That(previousRoot == null, Is.True);
                    }
                }
                finally
                {
                    fixture.Registry.MapReady -= onReady;
                    fixture.Dispose();
                }
            }
        }

        private static MapGenerationFixture CreateFixture(int floors)
        {
#if UNITY_EDITOR
            MapModuleCatalog catalog =
                AssetDatabase.LoadAssetAtPath<MapModuleCatalog>(CatalogPath);
#else
            MapModuleCatalog catalog = null;
#endif
            Assert.That(catalog, Is.Not.Null, CatalogPath);

            MapGenerationConfig config =
                ScriptableObject.CreateInstance<MapGenerationConfig>();
            config.ConfigureGeneratedContent(catalog, floors);

            var root = new GameObject($"MapGenerationFixture_{floors}");
            GeneratedMapRegistry registry = root.AddComponent<GeneratedMapRegistry>();
            var surfaceObject = new GameObject("GeneratedGeometrySurface");
            surfaceObject.transform.SetParent(root.transform, false);
            NavMeshSurface surface = surfaceObject.AddComponent<NavMeshSurface>();
            GeneratedNavMeshController navMeshController =
                root.AddComponent<GeneratedNavMeshController>();
            navMeshController.Configure(surface);
            MapGenerationBootstrap bootstrap = root.AddComponent<MapGenerationBootstrap>();
            bootstrap.Configure(config, navMeshController, registry);

            return new MapGenerationFixture(
                root,
                config,
                bootstrap,
                registry,
                surface);
        }

        private static void AssertCompletePath(
            Transform from,
            Transform to,
            int agentTypeId)
        {
            Assert.That(from, Is.Not.Null);
            Assert.That(to, Is.Not.Null);
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = agentTypeId,
                areaMask = NavMesh.AllAreas
            };
            Assert.That(
                NavMesh.SamplePosition(from.position, out NavMeshHit fromHit, 1.5f, filter),
                Is.True,
                $"Could not sample {from.name} at {from.position}.");
            Assert.That(
                NavMesh.SamplePosition(to.position, out NavMeshHit toHit, 1.5f, filter),
                Is.True,
                $"Could not sample {to.name} at {to.position}.");

            var path = new NavMeshPath();
            bool calculated = NavMesh.CalculatePath(
                fromHit.position,
                toHit.position,
                filter,
                path);
            Assert.That(calculated, Is.True, $"{from.name} -> {to.name}");
            Assert.That(
                path.status,
                Is.EqualTo(NavMeshPathStatus.PathComplete),
                $"{from.name} -> {to.name}: {path.status}");
        }

        private sealed class MapGenerationFixture : IDisposable
        {
            private readonly GameObject root;
            private readonly MapGenerationConfig config;

            public MapGenerationBootstrap Bootstrap { get; }
            public GeneratedMapRegistry Registry { get; }
            public NavMeshSurface Surface { get; }

            public MapGenerationFixture(
                GameObject root,
                MapGenerationConfig config,
                MapGenerationBootstrap bootstrap,
                GeneratedMapRegistry registry,
                NavMeshSurface surface)
            {
                this.root = root;
                this.config = config;
                Bootstrap = bootstrap;
                Registry = registry;
                Surface = surface;
            }

            public void Dispose()
            {
                Bootstrap.ClearGeneratedMap();
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
