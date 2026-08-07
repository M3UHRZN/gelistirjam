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
        [SerializeField] private int seed;
        [SerializeField] private int originX;
        [SerializeField] private int originZ;
        [SerializeField] private int canonicalWidthUnits;
        [SerializeField] private int canonicalDepthUnits;
        [SerializeField] private int rotatedWidthUnits;
        [SerializeField] private int rotatedDepthUnits;
        [SerializeField] private Transform bottomLanding;
        [SerializeField] private Transform topLanding;

        public int PlacementId => placementId;
        public string CatalogEntryId => catalogEntryId;
        public string FamilyId => familyId;
        public MapModuleCategory Category => category;
        public RoomRole RoomRole => roomRole;
        public int MinFloor => minFloor;
        public int MaxFloor => maxFloor;
        public int Seed => seed;
        public GridCoord3 Origin => new GridCoord3(originX, originZ, minFloor);
        public GridSize2 CanonicalFootprintUnits =>
            new GridSize2(canonicalWidthUnits, canonicalDepthUnits);
        public GridSize2 RotatedFootprintUnits =>
            new GridSize2(rotatedWidthUnits, rotatedDepthUnits);
        public Transform BottomLanding => bottomLanding;
        public Transform TopLanding => topLanding;

        public void Initialize(MapModuleSelection selection, int generationSeed)
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
            seed = generationSeed;
            originX = selection.Origin.X;
            originZ = selection.Origin.Z;
            canonicalWidthUnits = selection.CanonicalSize.Width;
            canonicalDepthUnits = selection.CanonicalSize.Depth;
            rotatedWidthUnits = selection.RotatedSize.Width;
            rotatedDepthUnits = selection.RotatedSize.Depth;
        }

        public void SetStairLandings(Transform bottom, Transform top)
        {
            bottomLanding = bottom;
            topLanding = top;
        }
    }
}
