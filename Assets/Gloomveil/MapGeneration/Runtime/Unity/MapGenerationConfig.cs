using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gloomveil.MapGeneration
{
    [CreateAssetMenu(
        fileName = "MapGenerationConfig",
        menuName = "Gloomveil/Map Generation/Generation Config")]
    public sealed class MapGenerationConfig : ScriptableObject
    {
        [Serializable]
        private sealed class FootprintDefinition
        {
            [SerializeField] private string id = "1x1";
            [SerializeField, Min(1)] private int widthUnits = 1;
            [SerializeField, Min(1)] private int depthUnits = 1;
            [SerializeField, Min(1)] private int weight = 1;
            [SerializeField] private bool requiredPerFloor;

            public FootprintDefinition(
                string id,
                int widthUnits,
                int depthUnits,
                int weight,
                bool requiredPerFloor)
            {
                this.id = id;
                this.widthUnits = widthUnits;
                this.depthUnits = depthUnits;
                this.weight = weight;
                this.requiredPerFloor = requiredPerFloor;
            }

            public FootprintSpec ToSpec() => new FootprintSpec(
                id,
                new GridSize2(widthUnits, depthUnits),
                weight,
                requiredPerFloor);
        }

        [Header("World Scale")]
        [SerializeField, Min(0.1f)] private float unitSize = 4f;
        [SerializeField, Min(0.1f)] private float floorHeight = 4f;
        [SerializeField] private MapModuleCatalog moduleCatalog;

        [Header("Layout")]
        [SerializeField, Min(8)] private int widthUnits = 16;
        [SerializeField, Min(8)] private int depthUnits = 16;
        [SerializeField, Min(2)] private int floorCount = 2;
        [SerializeField, Min(1)] private int minRoomsPerFloor = 7;
        [SerializeField, Min(1)] private int maxRoomsPerFloor = 9;
        [SerializeField, Min(0)] private int roomClearanceUnits = 1;
        [SerializeField, Min(0)] private int extraRoomLinksPerFloor = 2;
        [SerializeField, Min(2)] private int verticalLinksPerFloorPair = 2;
        [SerializeField, Min(0f)] private float minimumStairCenterSeparationUnits = 5f;
        [SerializeField, Min(1)] private int verticalTraversalCostUnits = 4;
        [SerializeField, Min(1)] private int fuseCount = 3;
        [SerializeField, Min(1)] private int safeZoneCount = 2;
        [SerializeField, Min(1)] private int maxLayoutAttempts = 12;
        [SerializeField, Min(1)] private int maxPlacementAttemptsPerArea = 64;

        [Header("Footprints")]
        [SerializeField] private FootprintDefinition[] roomFootprints =
        {
            new FootprintDefinition("1x2", 1, 2, 4, true),
            new FootprintDefinition("2x2", 2, 2, 3, true),
            new FootprintDefinition("2x3", 2, 3, 2, true)
        };
        [SerializeField] private FootprintDefinition[] stairFootprints =
        {
            new FootprintDefinition("STA_1x2", 1, 2, 3, false),
            new FootprintDefinition("STA_2x2", 2, 2, 1, false)
        };
        [SerializeField] private int[] corridorRunLengths = { 1, 2, 3 };

        public float UnitSize => unitSize;
        public float FloorHeight => floorHeight;
        public int FloorCount => floorCount;
        public MapModuleCatalog ModuleCatalog => moduleCatalog;

        public MapGenerationParameters ToParameters()
        {
            IReadOnlyList<FootprintSpec> rooms = (roomFootprints ?? Array.Empty<FootprintDefinition>())
                .Select(definition => definition.ToSpec())
                .ToArray();
            IReadOnlyList<FootprintSpec> stairs = (stairFootprints ?? Array.Empty<FootprintDefinition>())
                .Select(definition => definition.ToSpec())
                .ToArray();
            IReadOnlyList<int> lengths = new List<int>(corridorRunLengths ?? Array.Empty<int>());
            var result = new MapGenerationParameters(
                widthUnits,
                depthUnits,
                floorCount,
                minRoomsPerFloor,
                maxRoomsPerFloor,
                roomClearanceUnits,
                extraRoomLinksPerFloor,
                verticalLinksPerFloorPair,
                minimumStairCenterSeparationUnits,
                verticalTraversalCostUnits,
                fuseCount,
                safeZoneCount,
                maxLayoutAttempts,
                maxPlacementAttemptsPerArea,
                rooms,
                stairs,
                lengths);
            result.Validate();
            return result;
        }

        public void ConfigureGeneratedContent(MapModuleCatalog catalog, int floors)
        {
            moduleCatalog = catalog;
            floorCount = floors;
        }

        private void OnValidate()
        {
            try
            {
                ToParameters();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"MapGenerationConfig '{name}' is invalid: {exception.Message}",
                    this);
            }
        }
    }
}
