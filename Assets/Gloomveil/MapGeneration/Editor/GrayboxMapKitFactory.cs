using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Gloomveil.MapGeneration.Editor
{
    public static class GrayboxMapKitFactory
    {
        private const string PrefabFolder =
            "Assets/Gloomveil/MapGeneration/Prefabs/Graybox";
        private const string DataFolder =
            "Assets/Gloomveil/MapGeneration/Data";
        private const string CatalogPath =
            DataFolder + "/GrayboxMapModuleCatalog.asset";
        private const float UnitSize = 4f;
        private const float FloorHeight = 4f;
        private const float WallThickness = 0.6f;
        private const float WallHeight = 3.2f;
        private const float CorridorClearWidth = 2.8f;
        private const float DoorWidth = 1.6f;
        private const float DoorHeight = 3f;
        private const float LintelHeight = WallHeight - DoorHeight;

        private static readonly CardinalDirection[] CardinalDirections =
        {
            CardinalDirection.North,
            CardinalDirection.East,
            CardinalDirection.South,
            CardinalDirection.West
        };

        [MenuItem("Tools/Gloomveil/Map/Create Graybox Kit")]
        public static void CreateGrayboxKit()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(DataFolder);

            var entries = new List<MapModuleEntry>
            {
                CreateRoomEntry("PF_ROOM_1x2_01", "ROOM_1x2", new GridSize2(1, 2)),
                CreateRoomEntry("PF_ROOM_2x2_01", "ROOM_2x2", new GridSize2(2, 2)),
                CreateRoomEntry("PF_ROOM_2x3_01", "ROOM_2x3", new GridSize2(2, 3)),
                CreateCorridorEntry(
                    "PF_COR_Straight_1x1_01",
                    "COR_Straight_1x1",
                    new GridSize2(1, 1),
                    CorridorShape.Straight),
                CreateCorridorEntry(
                    "PF_COR_Straight_1x2_01",
                    "COR_Straight_1x2",
                    new GridSize2(1, 2),
                    CorridorShape.Straight),
                CreateCorridorEntry(
                    "PF_COR_Straight_1x3_01",
                    "COR_Straight_1x3",
                    new GridSize2(1, 3),
                    CorridorShape.Straight),
                CreateCorridorEntry(
                    "PF_COR_Corner_1x1_01",
                    "COR_Corner_1x1",
                    new GridSize2(1, 1),
                    CorridorShape.Corner),
                CreateCorridorEntry(
                    "PF_COR_TJunction_1x1_01",
                    "COR_TJunction_1x1",
                    new GridSize2(1, 1),
                    CorridorShape.TJunction),
                CreateCorridorEntry(
                    "PF_COR_CrossJunction_1x1_01",
                    "COR_CrossJunction_1x1",
                    new GridSize2(1, 1),
                    CorridorShape.CrossJunction),
                CreateCorridorEntry(
                    "PF_COR_DeadEnd_1x1_01",
                    "COR_DeadEnd_1x1",
                    new GridSize2(1, 1),
                    CorridorShape.DeadEnd),
                CreateCorridorEntry(
                    "PF_COR_DoorTransition_1x1_01",
                    "COR_DoorTransition_1x1",
                    new GridSize2(1, 1),
                    CorridorShape.DoorTransition),
                CreateStairEntry(
                    "PF_STA_Straight_1x2_01",
                    "STA_Straight_1x2",
                    new GridSize2(1, 2),
                    switchback: false),
                CreateStairEntry(
                    "PF_STA_Switchback_2x2_01",
                    "STA_Switchback_2x2",
                    new GridSize2(2, 2),
                    switchback: true)
            };

            MapModuleCatalog catalog =
                AssetDatabase.LoadAssetAtPath<MapModuleCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MapModuleCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.SetEntries(entries);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            IReadOnlyList<string> errors = MapModuleCatalogValidator.Validate(catalog);
            if (errors.Count > 0)
            {
                foreach (string error in errors)
                {
                    Debug.LogError($"{CatalogPath}: {error}", catalog);
                }

                throw new InvalidOperationException(
                    $"Graybox kit validation failed with {errors.Count} error(s).");
            }

            Debug.Log(
                $"Created/updated 13 graybox prefabs and '{CatalogPath}' with no validation errors.",
                catalog);
        }

        private static MapModuleEntry CreateRoomEntry(
            string prefabName,
            string familyId,
            GridSize2 size)
        {
            var root = new GameObject(prefabName);
            try
            {
                CreateFloor(root.transform, size);
                root.AddComponent<ModuleSocketController>();
                foreach (SocketKey socket in AllBoundarySockets(size))
                {
                    CreateSocket(root.transform, size, socket);
                    CreateDynamicDoorPair(root.transform, size, socket);
                }

                CreateRoomShell(root.transform, size);

                CreateAnchor(root.transform, "Anchor_PlayerStart", MapAnchorKind.PlayerStart, new Vector3(0f, 0f, 0f));
                CreateAnchor(root.transform, "Anchor_Exit", MapAnchorKind.Exit, new Vector3(0.5f, 0f, 0.5f));
                CreateAnchor(root.transform, "Anchor_Fuse_00", MapAnchorKind.Fuse, new Vector3(-0.5f, 0f, 0.5f));
                CreateAnchor(root.transform, "Anchor_SafeZone", MapAnchorKind.SafeZone, new Vector3(0f, 0f, -0.5f));
                CreateCommonAnchors(root.transform, size);
                GameObject prefab = SavePrefab(root, prefabName);
                return new MapModuleEntry(
                    prefabName,
                    familyId,
                    MapModuleCategory.Room,
                    size,
                    CorridorShape.DeadEnd,
                    RoomRoleMask.All,
                    prefab,
                    weight: 1,
                    allowQuarterTurns: true,
                    usesDynamicSockets: true);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static MapModuleEntry CreateCorridorEntry(
            string prefabName,
            string familyId,
            GridSize2 size,
            CorridorShape shape)
        {
            var root = new GameObject(prefabName);
            try
            {
                CreateFloor(root.transform, size);
                foreach (SocketKey socket in CorridorSockets(shape))
                {
                    CreateSocket(root.transform, size, socket);
                }

                CreateCorridorShell(root.transform, size, shape);

                CreateCommonAnchors(root.transform, size);
                GameObject prefab = SavePrefab(root, prefabName);
                return new MapModuleEntry(
                    prefabName,
                    familyId,
                    MapModuleCategory.Corridor,
                    size,
                    shape,
                    RoomRoleMask.None,
                    prefab,
                    weight: 1,
                    allowQuarterTurns: true,
                    usesDynamicSockets: false);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static MapModuleEntry CreateStairEntry(
            string prefabName,
            string familyId,
            GridSize2 size,
            bool switchback)
        {
            var root = new GameObject(prefabName);
            try
            {
                if (switchback)
                {
                    CreateSwitchbackStair(root.transform);
                }
                else
                {
                    CreateStraightStair(root.transform);
                }

                SocketKey bottom = new SocketKey(CardinalDirection.South, 0, 0);
                SocketKey top = switchback
                    ? new SocketKey(CardinalDirection.South, 1, 1)
                    : new SocketKey(CardinalDirection.North, 0, 1);
                CreateStairSocket(root.transform, size, bottom, topLanding: false);
                CreateStairSocket(root.transform, size, top, topLanding: true);
                CreateStairExteriorShell(root.transform, size, bottom, top);
                CreateAnchor(
                    root.transform,
                    "Landing_Bottom",
                    MapAnchorKind.StairBottom,
                    ExpectedSocketPosition(size, bottom));
                CreateAnchor(
                    root.transform,
                    "Landing_Top",
                    MapAnchorKind.StairTop,
                    ExpectedSocketPosition(size, top));
                CreateAnchor(root.transform, "StalkPoint_StairLanding", MapAnchorKind.StalkPoint, new Vector3(0f, 2f, 0f));
                CreateAnchor(root.transform, "LightAnchor", MapAnchorKind.Light, new Vector3(0f, 3f, 0f));
                GameObject prefab = SavePrefab(root, prefabName);
                return new MapModuleEntry(
                    prefabName,
                    familyId,
                    MapModuleCategory.Stair,
                    size,
                    CorridorShape.DeadEnd,
                    RoomRoleMask.None,
                    prefab,
                    weight: 1,
                    allowQuarterTurns: true,
                    usesDynamicSockets: false);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateStraightStair(Transform root)
        {
            CreateFloor(root, new GridSize2(1, 2));
            const float angle = -27.035f;
            CreateRamp(root, "RampCollider", new Vector3(0f, 2f, 0f), angle, 8.8f);
            CreateVisualBlock(
                root,
                "RampVisual",
                new Vector3(0f, 2f, 0f),
                new Vector3(2.8f, 0.16f, 8.8f),
                Quaternion.Euler(angle, 0f, 0f),
                removeCollider: true);
            CreateVisualBlock(
                root,
                "TopLandingVisual",
                new Vector3(0f, 3.9f, 3.6f),
                new Vector3(2.8f, 0.2f, 0.8f),
                Quaternion.identity,
                removeCollider: false);
        }

        private static void CreateSwitchbackStair(Transform root)
        {
            CreateFloor(root, new GridSize2(2, 2));
            const float verticalRise = 2f;
            const float lowerHorizontalRun = 5.8f;
            const float upperHorizontalRun = 5.1f;
            float lowerAngle = Mathf.Atan2(verticalRise, lowerHorizontalRun) * Mathf.Rad2Deg;
            float upperAngle = Mathf.Atan2(verticalRise, upperHorizontalRun) * Mathf.Rad2Deg;
            float lowerLength = Mathf.Sqrt(
                (lowerHorizontalRun * lowerHorizontalRun) + (verticalRise * verticalRise));
            float upperLength = Mathf.Sqrt(
                (upperHorizontalRun * upperHorizontalRun) + (verticalRise * verticalRise));
            float lowerCenterY = 1f - (0.08f * Mathf.Cos(lowerAngle * Mathf.Deg2Rad));
            float upperCenterY = 3f - (0.08f * Mathf.Cos(upperAngle * Mathf.Deg2Rad));

            CreateRamp(
                root,
                "RampCollider_Lower",
                new Vector3(-2f, lowerCenterY, -1f),
                -lowerAngle,
                lowerLength);
            CreateRamp(
                root,
                "RampCollider_Upper",
                new Vector3(2f, upperCenterY, -0.65f),
                upperAngle,
                upperLength);
            CreateVisualBlock(
                root,
                "RampVisual_Lower",
                new Vector3(-2f, lowerCenterY, -1f),
                new Vector3(2.8f, 0.16f, lowerLength),
                Quaternion.Euler(-lowerAngle, 0f, 0f),
                removeCollider: true);
            CreateVisualBlock(
                root,
                "RampVisual_Upper",
                new Vector3(2f, upperCenterY, -0.65f),
                new Vector3(2.8f, 0.16f, upperLength),
                Quaternion.Euler(upperAngle, 0f, 0f),
                removeCollider: true);
            CreateVisualBlock(
                root,
                "LandingCollider",
                new Vector3(0f, 1.9f, 2.95f),
                new Vector3(6.8f, 0.2f, 2.1f),
                Quaternion.identity,
                removeCollider: false);
            CreateVisualBlock(
                root,
                "TopLandingVisual",
                new Vector3(2f, 3.9f, -3.6f),
                new Vector3(2.8f, 0.2f, 0.8f),
                Quaternion.identity,
                removeCollider: false);
        }

        private static void CreateRamp(
            Transform root,
            string name,
            Vector3 position,
            float angle,
            float length)
        {
            var ramp = new GameObject(name);
            ramp.transform.SetParent(root, false);
            ramp.transform.localPosition = position;
            ramp.transform.localRotation = Quaternion.Euler(angle, 0f, 0f);
            var collider = ramp.AddComponent<BoxCollider>();
            collider.size = new Vector3(2.8f, 0.16f, length);
        }

        private static void CreateFloor(Transform root, GridSize2 size)
        {
            CreateVisualBlock(
                root,
                "Graybox_Floor",
                new Vector3(0f, -0.1f, 0f),
                new Vector3(size.Width * UnitSize, 0.2f, size.Depth * UnitSize),
                Quaternion.identity,
                removeCollider: false);
        }

        private static void CreateRoomShell(Transform root, GridSize2 size)
        {
            const string prefix = "RoomShell";
            SocketKey[] sockets = AllBoundarySockets(size).ToArray();
            CreatePerimeterCornerPosts(root, size, 0f, prefix);
            foreach (CardinalDirection side in CardinalDirections)
            {
                float[] openingCenters = sockets
                    .Where(socket => socket.Side == side)
                    .Select(socket => SocketTangentPosition(size, socket))
                    .OrderBy(position => position)
                    .ToArray();
                CreateSideWallWithOpenings(
                    root,
                    size,
                    side,
                    0f,
                    DoorWidth,
                    openingCenters,
                    prefix);
            }

            foreach (SocketKey socket in sockets)
            {
                CreateBoundaryBlockAtSocket(
                    root,
                    $"{prefix}_Lintel_{SocketSuffix(socket)}",
                    size,
                    socket,
                    DoorWidth,
                    LintelHeight,
                    DoorHeight + (LintelHeight * 0.5f),
                    removeCollider: false);
            }
        }

        private static void CreateCorridorShell(
            Transform root,
            GridSize2 size,
            CorridorShape shape)
        {
            const string prefix = "CorridorShell";
            SocketKey[] sockets = CorridorSockets(shape).ToArray();
            CreatePerimeterCornerPosts(root, size, 0f, prefix);
            foreach (CardinalDirection side in CardinalDirections)
            {
                float[] openingCenters = sockets
                    .Where(socket => socket.Side == side)
                    .Select(socket => SocketTangentPosition(size, socket))
                    .OrderBy(position => position)
                    .ToArray();
                CreateSideWallWithOpenings(
                    root,
                    size,
                    side,
                    0f,
                    CorridorClearWidth,
                    openingCenters,
                    prefix);
            }

            if (shape == CorridorShape.DoorTransition)
            {
                CreateInlineDoorFrame(root);
            }
        }

        private static void CreateStairExteriorShell(
            Transform root,
            GridSize2 size,
            SocketKey bottom,
            SocketKey top)
        {
            CreateStairLevelShell(root, size, bottom, 0f, "StairShell_Lower");
            CreateStairLevelShell(root, size, top, FloorHeight, "StairShell_Upper");
        }

        private static void CreateStairLevelShell(
            Transform root,
            GridSize2 size,
            SocketKey openSocket,
            float baseY,
            string prefix)
        {
            CreatePerimeterCornerPosts(root, size, baseY, prefix);
            foreach (CardinalDirection side in CardinalDirections)
            {
                float[] openingCenters = side == openSocket.Side
                    ? new[] { SocketTangentPosition(size, openSocket) }
                    : Array.Empty<float>();
                CreateSideWallWithOpenings(
                    root,
                    size,
                    side,
                    baseY,
                    CorridorClearWidth,
                    openingCenters,
                    prefix);
            }
        }

        private static void CreatePerimeterCornerPosts(
            Transform root,
            GridSize2 size,
            float baseY,
            string prefix)
        {
            float x = (size.Width * UnitSize * 0.5f) - (WallThickness * 0.5f);
            float z = (size.Depth * UnitSize * 0.5f) - (WallThickness * 0.5f);
            float y = baseY + (WallHeight * 0.5f);
            Vector3 scale = new Vector3(WallThickness, WallHeight, WallThickness);
            CreateVisualBlock(
                root,
                $"{prefix}_Corner_NW",
                new Vector3(-x, y, z),
                scale,
                Quaternion.identity,
                removeCollider: false);
            CreateVisualBlock(
                root,
                $"{prefix}_Corner_NE",
                new Vector3(x, y, z),
                scale,
                Quaternion.identity,
                removeCollider: false);
            CreateVisualBlock(
                root,
                $"{prefix}_Corner_SW",
                new Vector3(-x, y, -z),
                scale,
                Quaternion.identity,
                removeCollider: false);
            CreateVisualBlock(
                root,
                $"{prefix}_Corner_SE",
                new Vector3(x, y, -z),
                scale,
                Quaternion.identity,
                removeCollider: false);
        }

        private static void CreateSideWallWithOpenings(
            Transform root,
            GridSize2 size,
            CardinalDirection side,
            float baseY,
            float openingWidth,
            IReadOnlyList<float> openingCenters,
            string prefix)
        {
            float halfSpan = SideTangentSpan(size, side) * 0.5f;
            float minimum = -halfSpan + WallThickness;
            float maximum = halfSpan - WallThickness;
            float cursor = minimum;
            int segmentIndex = 0;
            foreach (float center in openingCenters.OrderBy(position => position))
            {
                float openingMinimum = center - (openingWidth * 0.5f);
                float openingMaximum = center + (openingWidth * 0.5f);
                if (openingMinimum < minimum - 0.001f || openingMaximum > maximum + 0.001f)
                {
                    throw new InvalidOperationException(
                        $"{prefix} opening on {side} falls outside the footprint wall span.");
                }

                if (openingMinimum > cursor + 0.001f)
                {
                    CreateSideWallSpan(
                        root,
                        size,
                        side,
                        baseY,
                        cursor,
                        openingMinimum,
                        $"{prefix}_Wall_{DirectionToken(side)}_{segmentIndex:00}");
                    segmentIndex++;
                }

                cursor = Mathf.Max(cursor, openingMaximum);
            }

            if (maximum > cursor + 0.001f)
            {
                CreateSideWallSpan(
                    root,
                    size,
                    side,
                    baseY,
                    cursor,
                    maximum,
                    $"{prefix}_Wall_{DirectionToken(side)}_{segmentIndex:00}");
            }
        }

        private static void CreateSideWallSpan(
            Transform root,
            GridSize2 size,
            CardinalDirection side,
            float baseY,
            float tangentMinimum,
            float tangentMaximum,
            string name)
        {
            float length = tangentMaximum - tangentMinimum;
            float tangentCenter = (tangentMinimum + tangentMaximum) * 0.5f;
            float halfWidth = size.Width * UnitSize * 0.5f;
            float halfDepth = size.Depth * UnitSize * 0.5f;
            float normalInset = WallThickness * 0.5f;
            float y = baseY + (WallHeight * 0.5f);
            Vector3 position;
            Vector3 scale;
            switch (side)
            {
                case CardinalDirection.North:
                    position = new Vector3(tangentCenter, y, halfDepth - normalInset);
                    scale = new Vector3(length, WallHeight, WallThickness);
                    break;
                case CardinalDirection.East:
                    position = new Vector3(halfWidth - normalInset, y, tangentCenter);
                    scale = new Vector3(WallThickness, WallHeight, length);
                    break;
                case CardinalDirection.South:
                    position = new Vector3(tangentCenter, y, -halfDepth + normalInset);
                    scale = new Vector3(length, WallHeight, WallThickness);
                    break;
                default:
                    position = new Vector3(-halfWidth + normalInset, y, tangentCenter);
                    scale = new Vector3(WallThickness, WallHeight, length);
                    break;
            }

            CreateVisualBlock(root, name, position, scale, Quaternion.identity, removeCollider: false);
        }

        private static void CreateBoundaryBlockAtSocket(
            Transform root,
            string name,
            GridSize2 size,
            SocketKey socket,
            float width,
            float height,
            float centerHeight,
            bool removeCollider)
        {
            Quaternion rotation = DirectionRotation(socket.Side);
            Vector3 outward = rotation * Vector3.forward;
            Vector3 position = ExpectedSocketPosition(size, socket) +
                               new Vector3(0f, centerHeight, 0f) -
                               (outward * (WallThickness * 0.5f));
            CreateVisualBlock(
                root,
                name,
                position,
                new Vector3(width, height, WallThickness),
                rotation,
                removeCollider);
        }

        private static void CreateInlineDoorFrame(Transform root)
        {
            float jambWidth = (CorridorClearWidth - DoorWidth) * 0.5f;
            float jambOffset = (DoorWidth * 0.5f) + (jambWidth * 0.5f);
            CreateVisualBlock(
                root,
                "DoorFrame_Jamb_Left",
                new Vector3(-jambOffset, WallHeight * 0.5f, 0f),
                new Vector3(jambWidth, WallHeight, WallThickness),
                Quaternion.identity,
                removeCollider: false);
            CreateVisualBlock(
                root,
                "DoorFrame_Jamb_Right",
                new Vector3(jambOffset, WallHeight * 0.5f, 0f),
                new Vector3(jambWidth, WallHeight, WallThickness),
                Quaternion.identity,
                removeCollider: false);
            CreateVisualBlock(
                root,
                "DoorFrame_Lintel",
                new Vector3(0f, DoorHeight + (LintelHeight * 0.5f), 0f),
                new Vector3(DoorWidth, LintelHeight, WallThickness),
                Quaternion.identity,
                removeCollider: false);
        }

        private static float SideTangentSpan(GridSize2 size, CardinalDirection side) =>
            side == CardinalDirection.North || side == CardinalDirection.South
                ? size.Width * UnitSize
                : size.Depth * UnitSize;

        private static float SocketTangentPosition(GridSize2 size, SocketKey socket)
        {
            Vector3 position = ExpectedSocketPosition(size, socket);
            return socket.Side == CardinalDirection.North ||
                   socket.Side == CardinalDirection.South
                ? position.x
                : position.z;
        }

        private static GameObject CreateVisualBlock(
            Transform root,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            bool removeCollider)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(root, false);
            block.transform.localPosition = localPosition;
            block.transform.localRotation = localRotation;
            block.transform.localScale = localScale;
            if (removeCollider)
            {
                UnityEngine.Object.DestroyImmediate(block.GetComponent<Collider>());
            }

            return block;
        }

        private static void CreateSocket(
            Transform root,
            GridSize2 size,
            SocketKey socket)
        {
            var anchor = new GameObject(SocketName(socket));
            anchor.transform.SetParent(root, false);
            anchor.transform.localPosition = ExpectedSocketPosition(size, socket);
            anchor.transform.localRotation = DirectionRotation(socket.Side);
        }

        private static void CreateStairSocket(
            Transform root,
            GridSize2 size,
            SocketKey socket,
            bool topLanding)
        {
            var anchor = new GameObject(
                $"Socket_{(topLanding ? "Top" : "Bottom")}_" +
                $"{DirectionToken(socket.Side)}_{socket.Index:00}");
            anchor.transform.SetParent(root, false);
            anchor.transform.localPosition = ExpectedSocketPosition(size, socket);
            anchor.transform.localRotation = DirectionRotation(socket.Side);
        }

        private static void CreateDynamicDoorPair(
            Transform root,
            GridSize2 size,
            SocketKey socket)
        {
            string suffix = SocketSuffix(socket);
            var portal = new GameObject($"Portal_{suffix}");
            portal.transform.SetParent(root, false);
            portal.transform.localPosition = ExpectedSocketPosition(size, socket);
            portal.transform.localRotation = DirectionRotation(socket.Side);
            portal.SetActive(false);
            CreateBoundaryBlockAtSocket(
                root,
                $"WallCap_{suffix}",
                size,
                socket,
                DoorWidth,
                DoorHeight,
                DoorHeight * 0.5f,
                removeCollider: false);
        }

        private static void CreateCommonAnchors(Transform root, GridSize2 size)
        {
            float halfWidth = size.Width * UnitSize * 0.25f;
            float halfDepth = size.Depth * UnitSize * 0.25f;
            CreateAnchor(root, "StalkPoint_Corner", MapAnchorKind.StalkPoint, new Vector3(-halfWidth, 0f, halfDepth));
            CreateAnchor(root, "StalkPoint_DoorSide", MapAnchorKind.StalkPoint, new Vector3(halfWidth, 0f, halfDepth));
            CreateAnchor(root, "MonsterSpawnCandidate", MapAnchorKind.SpawnCandidate, new Vector3(0f, 0f, -halfDepth));
            CreateAnchor(root, "LightAnchor", MapAnchorKind.Light, new Vector3(0f, 3f, 0f));
            CreateAnchor(root, "PropSocket", MapAnchorKind.PropSocket, new Vector3(halfWidth, 0f, -halfDepth));
        }

        private static void CreateAnchor(
            Transform root,
            string name,
            MapAnchorKind kind,
            Vector3 localPosition)
        {
            var anchorObject = new GameObject(name);
            anchorObject.transform.SetParent(root, false);
            anchorObject.transform.localPosition = localPosition;
            anchorObject.AddComponent<MapAnchor>().Configure(kind);
        }

        private static IEnumerable<SocketKey> AllBoundarySockets(GridSize2 size)
        {
            for (int index = 0; index < size.Width; index++)
            {
                yield return new SocketKey(CardinalDirection.North, index, 0);
                yield return new SocketKey(CardinalDirection.South, index, 0);
            }

            for (int index = 0; index < size.Depth; index++)
            {
                yield return new SocketKey(CardinalDirection.East, index, 0);
                yield return new SocketKey(CardinalDirection.West, index, 0);
            }
        }

        private static IEnumerable<SocketKey> CorridorSockets(CorridorShape shape)
        {
            yield return new SocketKey(CardinalDirection.North, 0, 0);
            if (shape == CorridorShape.DeadEnd)
            {
                yield break;
            }

            if (shape == CorridorShape.Straight || shape == CorridorShape.DoorTransition)
            {
                yield return new SocketKey(CardinalDirection.South, 0, 0);
                yield break;
            }

            yield return new SocketKey(CardinalDirection.East, 0, 0);
            if (shape == CorridorShape.Corner)
            {
                yield break;
            }

            if (shape == CorridorShape.TJunction)
            {
                yield return new SocketKey(CardinalDirection.West, 0, 0);
                yield break;
            }

            yield return new SocketKey(CardinalDirection.South, 0, 0);
            yield return new SocketKey(CardinalDirection.West, 0, 0);
        }

        private static Vector3 ExpectedSocketPosition(GridSize2 size, SocketKey socket)
        {
            float y = socket.FloorOffset * FloorHeight;
            switch (socket.Side)
            {
                case CardinalDirection.North:
                    return new Vector3(
                        (socket.Index + 0.5f - (size.Width / 2f)) * UnitSize,
                        y,
                        size.Depth * UnitSize * 0.5f);
                case CardinalDirection.East:
                    return new Vector3(
                        size.Width * UnitSize * 0.5f,
                        y,
                        (socket.Index + 0.5f - (size.Depth / 2f)) * UnitSize);
                case CardinalDirection.South:
                    return new Vector3(
                        (socket.Index + 0.5f - (size.Width / 2f)) * UnitSize,
                        y,
                        -size.Depth * UnitSize * 0.5f);
                default:
                    return new Vector3(
                        -size.Width * UnitSize * 0.5f,
                        y,
                        (socket.Index + 0.5f - (size.Depth / 2f)) * UnitSize);
            }
        }

        private static Quaternion DirectionRotation(CardinalDirection direction) =>
            Quaternion.Euler(0f, (int)direction * 90f, 0f);

        private static string SocketName(SocketKey socket)
        {
            string suffix = SocketSuffix(socket);
            return socket.FloorOffset == 0
                ? $"Socket_{suffix}"
                : $"Socket_Top_{DirectionToken(socket.Side)}_{socket.Index:00}";
        }

        private static string SocketSuffix(SocketKey socket) =>
            socket.FloorOffset == 0
                ? $"{DirectionToken(socket.Side)}_{socket.Index:00}"
                : $"Top_{DirectionToken(socket.Side)}_{socket.Index:00}";

        private static string DirectionToken(CardinalDirection direction)
        {
            switch (direction)
            {
                case CardinalDirection.North:
                    return "N";
                case CardinalDirection.East:
                    return "E";
                case CardinalDirection.South:
                    return "S";
                default:
                    return "W";
            }
        }

        private static GameObject SavePrefab(GameObject root, string prefabName)
        {
            string path = $"{PrefabFolder}/{prefabName}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success || saved == null)
            {
                throw new InvalidOperationException($"Could not save graybox prefab '{path}'.");
            }

            return saved;
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }
    }
}
