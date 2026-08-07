using System;
using UnityEngine;

namespace Gloomveil.MapGeneration
{
    [Serializable]
    public sealed class MapModuleEntry
    {
        [SerializeField] private string catalogEntryId = string.Empty;
        [SerializeField] private string familyId = string.Empty;
        [SerializeField] private MapModuleCategory category;
        [SerializeField, Min(1)] private int canonicalWidthUnits = 1;
        [SerializeField, Min(1)] private int canonicalDepthUnits = 1;
        [SerializeField] private CorridorShape corridorShape;
        [SerializeField] private RoomRoleMask allowedRoomRoles = RoomRoleMask.All;
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(1)] private int weight = 1;
        [SerializeField] private bool allowQuarterTurns = true;
        [SerializeField] private bool usesDynamicSockets;

        public string CatalogEntryId => catalogEntryId;
        public string FamilyId => familyId;
        public MapModuleCategory Category => category;
        public GridSize2 CanonicalFootprintUnits =>
            new GridSize2(canonicalWidthUnits, canonicalDepthUnits);
        public CorridorShape CorridorShape => corridorShape;
        public RoomRoleMask AllowedRoomRoles => allowedRoomRoles;
        public GameObject Prefab => prefab;
        public int Weight => weight;
        public bool AllowQuarterTurns => allowQuarterTurns;
        public bool UsesDynamicSockets => usesDynamicSockets;

        public MapModuleEntry(
            string catalogEntryId,
            string familyId,
            MapModuleCategory category,
            GridSize2 canonicalFootprintUnits,
            CorridorShape corridorShape,
            RoomRoleMask allowedRoomRoles,
            GameObject prefab,
            int weight,
            bool allowQuarterTurns,
            bool usesDynamicSockets)
        {
            this.catalogEntryId = catalogEntryId;
            this.familyId = familyId;
            this.category = category;
            canonicalWidthUnits = canonicalFootprintUnits.Width;
            canonicalDepthUnits = canonicalFootprintUnits.Depth;
            this.corridorShape = corridorShape;
            this.allowedRoomRoles = allowedRoomRoles;
            this.prefab = prefab;
            this.weight = weight;
            this.allowQuarterTurns = allowQuarterTurns;
            this.usesDynamicSockets = usesDynamicSockets;
        }
    }
}
