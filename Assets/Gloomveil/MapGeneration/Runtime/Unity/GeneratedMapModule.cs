using System;
using UnityEngine;

namespace Gloomveil.MapGeneration
{
    [DisallowMultipleComponent]
    public sealed class GeneratedMapModule : MonoBehaviour
    {
        [SerializeField] private int placementId;
        [SerializeField] private string catalogEntryId = string.Empty;
        [SerializeField] private string familyId = string.Empty;
        [SerializeField] private MapModuleCategory category;
        [SerializeField] private RoomRole roomRole;
        [SerializeField] private int minFloor;
        [SerializeField] private int maxFloor;

        public int PlacementId => placementId;
        public string CatalogEntryId => catalogEntryId;
        public string FamilyId => familyId;
        public MapModuleCategory Category => category;
        public RoomRole RoomRole => roomRole;
        public int MinFloor => minFloor;
        public int MaxFloor => maxFloor;

        public void Initialize(MapModuleSelection selection)
        {
            if (selection == null)
            {
                throw new ArgumentNullException(nameof(selection));
            }

            placementId = selection.PlacementId;
            catalogEntryId = selection.CatalogEntryId;
            familyId = selection.FamilyId;
            category = selection.Category;
            roomRole = selection.RoomRole;
            minFloor = selection.MinFloor;
            maxFloor = selection.MaxFloor;
        }
    }
}
