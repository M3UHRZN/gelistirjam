using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Gloomveil.MapGeneration.Tests
{
    public sealed class GeneratedMapPlayModeSmokeTests
    {
        private readonly List<Object> ownedObjects = new List<Object>();
        private ModularMapBuilder builder;

        [TearDown]
        public void TearDown()
        {
            builder?.DestroyGeneratedRoot();
            foreach (Object ownedObject in ownedObjects)
            {
                if (ownedObject != null)
                {
                    Object.DestroyImmediate(ownedObject);
                }
            }

            ownedObjects.Clear();
        }

        [UnityTest]
        public IEnumerator Build_TwoFloors_UsesOneObjectPerVisiblePlacement()
        {
            const int seed = 1042;
            MapLayout layout = GenerateAssignedLayout(floors: 2, seed);
            MapModuleCatalog catalog = CreateTemporaryCatalog();
            builder = new ModularMapBuilder(catalog, unitSize: 4f, floorHeight: 4f);

            GeneratedMapRuntimeData data = builder.Build(layout, seed);
            int expected =
                layout.Rooms.Count + layout.Stairs.Count + layout.CorridorModules.Count;

            Assert.That(data.Modules.Count, Is.EqualTo(expected));
            Assert.That(
                data.Modules.Count,
                Is.LessThan(layout.WidthUnits * layout.DepthUnits * 2));
            Assert.That(
                data.RoomModules.Select(module => module.CanonicalFootprintUnits),
                Does.Contain(new GridSize2(1, 2)));
            Assert.That(
                data.RoomModules.Select(module => module.CanonicalFootprintUnits),
                Does.Contain(new GridSize2(2, 2)));
            Assert.That(
                data.RoomModules.Select(module => module.CanonicalFootprintUnits),
                Does.Contain(new GridSize2(2, 3)));
            yield return null;
        }

        private static MapLayout GenerateAssignedLayout(int floors, int seed)
        {
            MapGenerationParameters parameters =
                MapGenerationParameters.CreateProductionDefaults(floors);
            MapLayout layout = new VariableFootprintLayoutGenerator().Generate(parameters, seed);
            new GameplayRoleAssigner().Assign(
                layout,
                parameters,
                new SeededRandom(seed, "roles"));
            MapValidationReport report = new MapLayoutValidator().Validate(layout, parameters);
            Assert.That(report.IsValid, Is.True, string.Join("\n", report.Errors));
            return layout;
        }

        private MapModuleCatalog CreateTemporaryCatalog()
        {
            var entries = new List<MapModuleEntry>
            {
                Entry("ROOM_1x2", MapModuleCategory.Room, new GridSize2(1, 2)),
                Entry("ROOM_2x2", MapModuleCategory.Room, new GridSize2(2, 2)),
                Entry("ROOM_2x3", MapModuleCategory.Room, new GridSize2(2, 3)),
                Entry("COR_STRAIGHT_1x1", MapModuleCategory.Corridor, new GridSize2(1, 1), CorridorShape.Straight),
                Entry("COR_STRAIGHT_1x2", MapModuleCategory.Corridor, new GridSize2(1, 2), CorridorShape.Straight),
                Entry("COR_STRAIGHT_1x3", MapModuleCategory.Corridor, new GridSize2(1, 3), CorridorShape.Straight),
                Entry("COR_CORNER", MapModuleCategory.Corridor, new GridSize2(1, 1), CorridorShape.Corner),
                Entry("COR_T", MapModuleCategory.Corridor, new GridSize2(1, 1), CorridorShape.TJunction),
                Entry("COR_CROSS", MapModuleCategory.Corridor, new GridSize2(1, 1), CorridorShape.CrossJunction),
                Entry("COR_DEAD", MapModuleCategory.Corridor, new GridSize2(1, 1), CorridorShape.DeadEnd),
                Entry("COR_DOOR", MapModuleCategory.Corridor, new GridSize2(1, 1), CorridorShape.DoorTransition),
                Entry("STA_1x2", MapModuleCategory.Stair, new GridSize2(1, 2)),
                Entry("STA_2x2", MapModuleCategory.Stair, new GridSize2(2, 2))
            };
            MapModuleCatalog catalog = ScriptableObject.CreateInstance<MapModuleCatalog>();
            catalog.name = "TemporaryMapModuleCatalog";
            catalog.SetEntries(entries);
            ownedObjects.Add(catalog);
            return catalog;
        }

        private MapModuleEntry Entry(
            string id,
            MapModuleCategory category,
            GridSize2 size,
            CorridorShape shape = CorridorShape.DeadEnd)
        {
            var prefab = new GameObject($"PF_{id}");
            ownedObjects.Add(prefab);
            return new MapModuleEntry(
                id,
                id,
                category,
                size,
                shape,
                RoomRoleMask.All,
                prefab,
                weight: 1,
                allowQuarterTurns: true,
                usesDynamicSockets: false);
        }
    }
}
