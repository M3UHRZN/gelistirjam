using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Gloomveil.MapGeneration.Editor
{
    public static class MapModuleCatalogValidator
    {
        private const float UnitSize = 4f;
        private const float FloorHeight = 4f;
        private const float PositionTolerance = 0.03f;

        [MenuItem("Tools/Gloomveil/Map/Validate Module Catalog")]
        private static void ValidateAllCatalogs()
        {
            string[] guids = AssetDatabase.FindAssets("t:MapModuleCatalog");
            if (guids.Length == 0)
            {
                Debug.LogError("No MapModuleCatalog asset exists in the project.");
                return;
            }

            int errorCount = 0;
            foreach (string guid in guids.OrderBy(value => value, StringComparer.Ordinal))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MapModuleCatalog catalog = AssetDatabase.LoadAssetAtPath<MapModuleCatalog>(path);
                IReadOnlyList<string> errors = Validate(catalog);
                foreach (string error in errors)
                {
                    Debug.LogError($"{path}: {error}", catalog);
                }

                errorCount += errors.Count;
            }

            if (errorCount == 0)
            {
                Debug.Log($"Validated {guids.Length} map module catalog(s): no errors.");
            }
        }

        public static IReadOnlyList<string> Validate(MapModuleCatalog catalog)
        {
            var errors = new List<string>();
            if (catalog == null)
            {
                errors.Add("Catalog reference is null.");
                return errors;
            }

            MapModuleEntry[] entries = catalog.Entries.Where(entry => entry != null).ToArray();
            if (entries.Length != catalog.Entries.Count)
            {
                errors.Add("Catalog contains a null entry.");
            }

            foreach (MapModuleEntry entry in entries)
            {
                ValidateEntry(entry, errors);
            }

            foreach (IGrouping<string, MapModuleEntry> duplicate in entries
                         .Where(entry => !string.IsNullOrWhiteSpace(entry.CatalogEntryId))
                         .GroupBy(entry => entry.CatalogEntryId, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                errors.Add($"CatalogEntryId '{duplicate.Key}' is duplicated.");
            }

            foreach (IGrouping<string, MapModuleEntry> family in entries
                         .Where(entry => !string.IsNullOrWhiteSpace(entry.FamilyId))
                         .GroupBy(entry => entry.FamilyId, StringComparer.Ordinal))
            {
                ValidateFamily(family, errors);
            }

            ValidateRequiredP0(entries, errors);
            return errors;
        }

        private static void ValidateEntry(MapModuleEntry entry, ICollection<string> errors)
        {
            string label = string.IsNullOrWhiteSpace(entry.CatalogEntryId)
                ? "<blank entry>"
                : entry.CatalogEntryId;
            if (string.IsNullOrWhiteSpace(entry.CatalogEntryId))
            {
                errors.Add("An entry has a blank CatalogEntryId.");
            }

            if (string.IsNullOrWhiteSpace(entry.FamilyId))
            {
                errors.Add($"{label}: FamilyId is blank.");
            }

            if (entry.Prefab == null)
            {
                errors.Add($"{label}: Prefab is missing.");
                return;
            }

            if (entry.Weight < 1)
            {
                errors.Add($"{label}: Weight must be at least 1.");
            }

            GridSize2 size;
            try
            {
                size = entry.CanonicalFootprintUnits;
            }
            catch (Exception exception)
            {
                errors.Add($"{label}: Invalid footprint: {exception.Message}");
                return;
            }

            Transform root = entry.Prefab.transform;
            if (!Approximately(root.localScale, Vector3.one))
            {
                errors.Add($"{label}: prefab root scale must be (1,1,1).");
            }

            if (root.localPosition.sqrMagnitude > PositionTolerance * PositionTolerance)
            {
                errors.Add($"{label}: prefab root pivot must be centred at local origin.");
            }

            Dictionary<SocketKey, Transform> sockets = ReadSockets(entry.Prefab, label, errors);
            ValidateExpectedSockets(entry, sockets, errors);
            foreach (KeyValuePair<SocketKey, Transform> socket in sockets)
            {
                ValidateSocketTransform(label, size, socket.Key, socket.Value, errors);
            }

            if (entry.UsesDynamicSockets)
            {
                ValidateDynamicPairs(entry, sockets.Keys, errors);
            }

            if (entry.Category == MapModuleCategory.Room)
            {
                RequireChild(entry, "Anchor_PlayerStart", errors);
                RequireChild(entry, "Anchor_Exit", errors);
                RequireChild(entry, "Anchor_Fuse_00", errors);
                RequireChild(entry, "Anchor_SafeZone", errors);
            }

            if (entry.Category == MapModuleCategory.Stair)
            {
                ValidateStair(entry, sockets, errors);
            }

            ValidateQuarterTurnGeometry(entry, size, sockets, errors);
        }

        private static void ValidateFamily(
            IGrouping<string, MapModuleEntry> family,
            ICollection<string> errors)
        {
            MapModuleEntry first = family.First();
            string firstSockets = SocketSignature(first.Prefab);
            foreach (MapModuleEntry entry in family.Skip(1))
            {
                if (entry.Category != first.Category ||
                    entry.CanonicalFootprintUnits != first.CanonicalFootprintUnits ||
                    entry.CorridorShape != first.CorridorShape ||
                    entry.UsesDynamicSockets != first.UsesDynamicSockets ||
                    SocketSignature(entry.Prefab) != firstSockets)
                {
                    errors.Add(
                        $"FamilyId '{family.Key}' variants disagree on category, footprint, shape, " +
                        "socket set, or dynamic-socket policy.");
                    return;
                }
            }
        }

        private static void ValidateRequiredP0(
            IReadOnlyCollection<MapModuleEntry> entries,
            ICollection<string> errors)
        {
            RequireStructural(entries, MapModuleCategory.Room, new GridSize2(1, 2), null, errors);
            RequireStructural(entries, MapModuleCategory.Room, new GridSize2(2, 2), null, errors);
            RequireStructural(entries, MapModuleCategory.Room, new GridSize2(2, 3), null, errors);
            RequireStructural(entries, MapModuleCategory.Corridor, new GridSize2(1, 1), CorridorShape.Straight, errors);
            RequireStructural(entries, MapModuleCategory.Corridor, new GridSize2(1, 2), CorridorShape.Straight, errors);
            RequireStructural(entries, MapModuleCategory.Corridor, new GridSize2(1, 3), CorridorShape.Straight, errors);
            RequireStructural(entries, MapModuleCategory.Corridor, new GridSize2(1, 1), CorridorShape.Corner, errors);
            RequireStructural(entries, MapModuleCategory.Corridor, new GridSize2(1, 1), CorridorShape.TJunction, errors);
            RequireStructural(entries, MapModuleCategory.Corridor, new GridSize2(1, 1), CorridorShape.CrossJunction, errors);
            RequireStructural(entries, MapModuleCategory.Corridor, new GridSize2(1, 1), CorridorShape.DeadEnd, errors);
            RequireStructural(entries, MapModuleCategory.Corridor, new GridSize2(1, 1), CorridorShape.DoorTransition, errors);
            RequireStructural(entries, MapModuleCategory.Stair, new GridSize2(1, 2), null, errors);
            RequireStructural(entries, MapModuleCategory.Stair, new GridSize2(2, 2), null, errors);
        }

        private static void RequireStructural(
            IEnumerable<MapModuleEntry> entries,
            MapModuleCategory category,
            GridSize2 size,
            CorridorShape? shape,
            ICollection<string> errors)
        {
            bool found = entries.Any(entry =>
                entry.Category == category &&
                entry.CanonicalFootprintUnits == size &&
                (!shape.HasValue || entry.CorridorShape == shape.Value));
            if (!found)
            {
                errors.Add(
                    $"Missing P0 entry: {category} {size}" +
                    (shape.HasValue ? $" {shape.Value}." : "."));
            }
        }

        private static Dictionary<SocketKey, Transform> ReadSockets(
            GameObject prefab,
            string label,
            ICollection<string> errors)
        {
            var result = new Dictionary<SocketKey, Transform>();
            foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (!ModuleSocketController.TryParse(child.name, "Socket_", out SocketKey key))
                {
                    continue;
                }

                if (result.ContainsKey(key))
                {
                    errors.Add($"{label}: socket {key} is duplicated.");
                }
                else
                {
                    result.Add(key, child);
                }
            }

            return result;
        }

        private static void ValidateExpectedSockets(
            MapModuleEntry entry,
            IReadOnlyDictionary<SocketKey, Transform> actual,
            ICollection<string> errors)
        {
            HashSet<SocketKey> expected = ExpectedSockets(entry);
            foreach (SocketKey missing in expected.Where(key => !actual.ContainsKey(key)))
            {
                errors.Add($"{entry.CatalogEntryId}: required socket {missing} is missing.");
            }

            foreach (SocketKey unexpected in actual.Keys.Where(key => !expected.Contains(key)))
            {
                errors.Add($"{entry.CatalogEntryId}: socket {unexpected} is outside its structural contract.");
            }
        }

        private static HashSet<SocketKey> ExpectedSockets(MapModuleEntry entry)
        {
            var result = new HashSet<SocketKey>();
            GridSize2 size = entry.CanonicalFootprintUnits;
            if (entry.Category == MapModuleCategory.Room)
            {
                for (int index = 0; index < size.Width; index++)
                {
                    result.Add(new SocketKey(CardinalDirection.North, index, 0));
                    result.Add(new SocketKey(CardinalDirection.South, index, 0));
                }

                for (int index = 0; index < size.Depth; index++)
                {
                    result.Add(new SocketKey(CardinalDirection.East, index, 0));
                    result.Add(new SocketKey(CardinalDirection.West, index, 0));
                }

                return result;
            }

            if (entry.Category == MapModuleCategory.Stair)
            {
                result.Add(new SocketKey(CardinalDirection.South, 0, 0));
                result.Add(entry.CanonicalFootprintUnits == new GridSize2(2, 2)
                    ? new SocketKey(CardinalDirection.South, 1, 1)
                    : new SocketKey(CardinalDirection.North, 0, 1));
                return result;
            }

            switch (entry.CorridorShape)
            {
                case CorridorShape.Straight:
                case CorridorShape.DoorTransition:
                    result.Add(new SocketKey(CardinalDirection.North, 0, 0));
                    result.Add(new SocketKey(CardinalDirection.South, 0, 0));
                    break;
                case CorridorShape.Corner:
                    result.Add(new SocketKey(CardinalDirection.North, 0, 0));
                    result.Add(new SocketKey(CardinalDirection.East, 0, 0));
                    break;
                case CorridorShape.TJunction:
                    result.Add(new SocketKey(CardinalDirection.North, 0, 0));
                    result.Add(new SocketKey(CardinalDirection.East, 0, 0));
                    result.Add(new SocketKey(CardinalDirection.West, 0, 0));
                    break;
                case CorridorShape.CrossJunction:
                    result.Add(new SocketKey(CardinalDirection.North, 0, 0));
                    result.Add(new SocketKey(CardinalDirection.East, 0, 0));
                    result.Add(new SocketKey(CardinalDirection.South, 0, 0));
                    result.Add(new SocketKey(CardinalDirection.West, 0, 0));
                    break;
                default:
                    result.Add(new SocketKey(CardinalDirection.North, 0, 0));
                    break;
            }

            return result;
        }

        private static void ValidateSocketTransform(
            string label,
            GridSize2 size,
            SocketKey key,
            Transform socket,
            ICollection<string> errors)
        {
            Vector3 expected = ExpectedSocketPosition(size, key);
            if ((socket.localPosition - expected).sqrMagnitude >
                PositionTolerance * PositionTolerance)
            {
                errors.Add(
                    $"{label}: socket {key} is at {socket.localPosition}; expected {expected}.");
            }

            Vector3 expectedFacing = DirectionVector(key.Side);
            Vector3 actualFacing = socket.localRotation * Vector3.forward;
            if (Vector3.Dot(actualFacing.normalized, expectedFacing) < 0.98f)
            {
                errors.Add($"{label}: socket {key} faces inward or sideways.");
            }
        }

        private static Vector3 ExpectedSocketPosition(GridSize2 size, SocketKey key)
        {
            float y = key.FloorOffset * FloorHeight;
            switch (key.Side)
            {
                case CardinalDirection.North:
                    return new Vector3(
                        (key.Index + 0.5f - (size.Width / 2f)) * UnitSize,
                        y,
                        size.Depth * UnitSize * 0.5f);
                case CardinalDirection.East:
                    return new Vector3(
                        size.Width * UnitSize * 0.5f,
                        y,
                        (key.Index + 0.5f - (size.Depth / 2f)) * UnitSize);
                case CardinalDirection.South:
                    return new Vector3(
                        (key.Index + 0.5f - (size.Width / 2f)) * UnitSize,
                        y,
                        -size.Depth * UnitSize * 0.5f);
                case CardinalDirection.West:
                    return new Vector3(
                        -size.Width * UnitSize * 0.5f,
                        y,
                        (key.Index + 0.5f - (size.Depth / 2f)) * UnitSize);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static Vector3 DirectionVector(CardinalDirection direction)
        {
            switch (direction)
            {
                case CardinalDirection.North:
                    return Vector3.forward;
                case CardinalDirection.East:
                    return Vector3.right;
                case CardinalDirection.South:
                    return Vector3.back;
                case CardinalDirection.West:
                    return Vector3.left;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        private static void ValidateDynamicPairs(
            MapModuleEntry entry,
            IEnumerable<SocketKey> sockets,
            ICollection<string> errors)
        {
            if (entry.Prefab.GetComponent<ModuleSocketController>() == null)
            {
                errors.Add(
                    $"{entry.CatalogEntryId}: dynamic prefab root needs a " +
                    $"{nameof(ModuleSocketController)} component.");
            }

            Transform[] children = entry.Prefab.GetComponentsInChildren<Transform>(true);
            foreach (SocketKey socket in sockets)
            {
                string suffix = SocketSuffix(socket);
                Transform portal = children.FirstOrDefault(child =>
                    child.name == $"Portal_{suffix}");
                Transform wallCap = children.FirstOrDefault(child =>
                    child.name == $"WallCap_{suffix}");
                if (portal == null || wallCap == null)
                {
                    errors.Add(
                        $"{entry.CatalogEntryId}: dynamic socket {socket} lacks Portal_{suffix} " +
                        $"or WallCap_{suffix}.");
                    continue;
                }

                if (portal.gameObject.activeSelf)
                {
                    errors.Add(
                        $"{entry.CatalogEntryId}: Portal_{suffix} must be inactive by default.");
                }

                if (portal.GetComponentsInChildren<Collider>(true)
                    .Any(collider => collider.enabled && !collider.isTrigger))
                {
                    errors.Add(
                        $"{entry.CatalogEntryId}: Portal_{suffix} must not block navigation.");
                }

                if (!wallCap.gameObject.activeSelf)
                {
                    errors.Add(
                        $"{entry.CatalogEntryId}: WallCap_{suffix} must be active by default.");
                }

                BoxCollider capCollider = wallCap.GetComponentsInChildren<BoxCollider>(true)
                    .FirstOrDefault(collider => collider.enabled && !collider.isTrigger);
                if (capCollider == null)
                {
                    errors.Add(
                        $"{entry.CatalogEntryId}: WallCap_{suffix} needs an enabled, non-trigger " +
                        "BoxCollider.");
                }
            }
        }

        private static void ValidateStair(
            MapModuleEntry entry,
            IReadOnlyDictionary<SocketKey, Transform> sockets,
            ICollection<string> errors)
        {
            SocketKey bottom = new SocketKey(CardinalDirection.South, 0, 0);
            SocketKey top = entry.CanonicalFootprintUnits == new GridSize2(2, 2)
                ? new SocketKey(CardinalDirection.South, 1, 1)
                : new SocketKey(CardinalDirection.North, 0, 1);
            if (sockets.TryGetValue(bottom, out Transform bottomTransform) &&
                sockets.TryGetValue(top, out Transform topTransform) &&
                Math.Abs((topTransform.localPosition.y - bottomTransform.localPosition.y) - FloorHeight) >
                PositionTolerance)
            {
                errors.Add($"{entry.CatalogEntryId}: stair must span exactly {FloorHeight} m.");
            }

            if (entry.CanonicalFootprintUnits == new GridSize2(2, 2))
            {
                ValidateRampCollider(entry, "RampCollider_Lower", 2f, errors);
                ValidateRampCollider(entry, "RampCollider_Upper", 2f, errors);
                ValidateLandingCollider(entry, "LandingCollider", errors);
            }
            else
            {
                ValidateRampCollider(entry, "RampCollider", FloorHeight, errors);
            }

            RequireChild(entry, "Landing_Bottom", errors);
            RequireChild(entry, "Landing_Top", errors);
        }

        private static void ValidateRampCollider(
            MapModuleEntry entry,
            string childName,
            float expectedRise,
            ICollection<string> errors)
        {
            BoxCollider collider = RequireNavigableBoxCollider(entry, childName, errors);
            if (collider == null)
            {
                return;
            }

            float width = collider.size.x * Math.Abs(collider.transform.lossyScale.x);
            if (width + PositionTolerance < 2.8f)
            {
                errors.Add(
                    $"{entry.CatalogEntryId}: '{childName}' clear width is {width:0.###} m; " +
                    "expected at least 2.8 m.");
            }

            Vector3 forward = collider.transform.forward.normalized;
            float length = collider.size.z * Math.Abs(collider.transform.lossyScale.z);
            float rise = Math.Abs(forward.y) * length;
            if (Math.Abs(rise - expectedRise) > 0.15f)
            {
                errors.Add(
                    $"{entry.CatalogEntryId}: '{childName}' rises {rise:0.###} m; " +
                    $"expected {expectedRise:0.###} m from one continuous ramp collider.");
            }
        }

        private static BoxCollider RequireNavigableBoxCollider(
            MapModuleEntry entry,
            string childName,
            ICollection<string> errors)
        {
            Transform[] matches = entry.Prefab.GetComponentsInChildren<Transform>(true)
                .Where(child => child.name == childName)
                .ToArray();
            if (matches.Length != 1)
            {
                errors.Add(
                    $"{entry.CatalogEntryId}: expected exactly one '{childName}' child; " +
                    $"found {matches.Length}.");
                return null;
            }

            BoxCollider collider = matches[0].GetComponents<BoxCollider>()
                .FirstOrDefault(candidate => candidate.enabled && !candidate.isTrigger);
            if (collider == null)
            {
                errors.Add(
                    $"{entry.CatalogEntryId}: '{childName}' needs an enabled, non-trigger " +
                    "BoxCollider.");
            }

            return collider;
        }

        private static void ValidateLandingCollider(
            MapModuleEntry entry,
            string childName,
            ICollection<string> errors)
        {
            BoxCollider collider = RequireNavigableBoxCollider(entry, childName, errors);
            if (collider == null)
            {
                return;
            }

            float flatness = Math.Abs(Vector3.Dot(
                collider.transform.up.normalized,
                Vector3.up));
            if (flatness < 0.98f)
            {
                errors.Add($"{entry.CatalogEntryId}: '{childName}' must be horizontal.");
            }

            float spanX = collider.size.x * Math.Abs(collider.transform.lossyScale.x);
            float spanZ = collider.size.z * Math.Abs(collider.transform.lossyScale.z);
            float usableWidth = Math.Max(spanX, spanZ);
            if (usableWidth + PositionTolerance < 2.8f)
            {
                errors.Add(
                    $"{entry.CatalogEntryId}: '{childName}' usable width is " +
                    $"{usableWidth:0.###} m; expected at least 2.8 m.");
            }
        }

        private static void ValidateQuarterTurnGeometry(
            MapModuleEntry entry,
            GridSize2 size,
            IReadOnlyDictionary<SocketKey, Transform> sockets,
            ICollection<string> errors)
        {
            Transform root = entry.Prefab.transform;
            Collider[] colliders = entry.Prefab.GetComponentsInChildren<Collider>(true);
            for (int turns = 0; turns < 4; turns++)
            {
                Quaternion rotation = Quaternion.Euler(0f, turns * 90f, 0f);
                GridSize2 rotatedSize = size.Rotate(turns);
                ValidateRotatedSockets(
                    entry.CatalogEntryId,
                    root,
                    size,
                    rotatedSize,
                    sockets,
                    turns,
                    rotation,
                    errors);
                ValidateRotatedColliderEnvelope(
                    entry.CatalogEntryId,
                    root,
                    rotatedSize,
                    colliders,
                    turns,
                    rotation,
                    errors);
            }
        }

        private static void ValidateRotatedSockets(
            string label,
            Transform root,
            GridSize2 canonicalSize,
            GridSize2 rotatedSize,
            IReadOnlyDictionary<SocketKey, Transform> sockets,
            int turns,
            Quaternion rotation,
            ICollection<string> errors)
        {
            foreach (KeyValuePair<SocketKey, Transform> socket in sockets)
            {
                SocketKey rotatedKey = socket.Key.Rotate(canonicalSize, turns);
                Vector3 localPosition = root.InverseTransformPoint(socket.Value.position);
                Vector3 actualPosition = rotation * localPosition;
                Vector3 expectedPosition = ExpectedSocketPosition(rotatedSize, rotatedKey);
                if ((actualPosition - expectedPosition).sqrMagnitude >
                    PositionTolerance * PositionTolerance)
                {
                    errors.Add(
                        $"{label}: socket {socket.Key} at quarter-turn {turns} lands at " +
                        $"{actualPosition}; expected {rotatedKey} at {expectedPosition}.");
                }

                Vector3 localFacing = root.InverseTransformDirection(socket.Value.forward);
                Vector3 actualFacing = rotation * localFacing;
                Vector3 expectedFacing = DirectionVector(rotatedKey.Side);
                if (Vector3.Dot(actualFacing.normalized, expectedFacing) < 0.98f)
                {
                    errors.Add(
                        $"{label}: socket {socket.Key} faces incorrectly after quarter-turn {turns} " +
                        $"(expected {rotatedKey.Side}).");
                }
            }
        }

        private static void ValidateRotatedColliderEnvelope(
            string label,
            Transform root,
            GridSize2 rotatedSize,
            IEnumerable<Collider> colliders,
            int turns,
            Quaternion rotation,
            ICollection<string> errors)
        {
            float halfWidth = rotatedSize.Width * UnitSize * 0.5f + PositionTolerance;
            float halfDepth = rotatedSize.Depth * UnitSize * 0.5f + PositionTolerance;
            foreach (Collider collider in colliders)
            {
                foreach (Vector3 localCorner in ColliderCornersInRootSpace(collider, root))
                {
                    Vector3 corner = rotation * localCorner;
                    if (corner.x >= -halfWidth && corner.x <= halfWidth &&
                        corner.z >= -halfDepth && corner.z <= halfDepth)
                    {
                        continue;
                    }

                    errors.Add(
                        $"{label}: collider '{collider.name}' protrudes beyond the {rotatedSize} " +
                        $"footprint/socket boundary at quarter-turn {turns}.");
                    break;
                }
            }
        }

        private static IEnumerable<Vector3> ColliderCornersInRootSpace(
            Collider collider,
            Transform root)
        {
            if (collider is BoxCollider box)
            {
                Vector3 halfSize = box.size * 0.5f;
                for (int x = -1; x <= 1; x += 2)
                {
                    for (int y = -1; y <= 1; y += 2)
                    {
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 point = box.center + Vector3.Scale(
                                halfSize,
                                new Vector3(x, y, z));
                            yield return root.InverseTransformPoint(
                                box.transform.TransformPoint(point));
                        }
                    }
                }

                yield break;
            }

            Bounds bounds = collider.bounds;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 point = bounds.center + Vector3.Scale(
                            bounds.extents,
                            new Vector3(x, y, z));
                        yield return root.InverseTransformPoint(point);
                    }
                }
            }
        }

        private static void RequireChild(
            MapModuleEntry entry,
            string childName,
            ICollection<string> errors)
        {
            bool found = entry.Prefab.GetComponentsInChildren<Transform>(true)
                .Any(child => child.name == childName);
            if (!found)
            {
                errors.Add($"{entry.CatalogEntryId}: required child '{childName}' is missing.");
            }
        }

        private static string SocketSignature(GameObject prefab)
        {
            if (prefab == null)
            {
                return "<missing>";
            }

            return string.Join(
                ",",
                prefab.GetComponentsInChildren<Transform>(true)
                    .Where(child => ModuleSocketController.TryParse(
                        child.name,
                        "Socket_",
                        out _))
                    .Select(child => child.name)
                    .OrderBy(name => name, StringComparer.Ordinal));
        }

        private static string SocketSuffix(SocketKey key)
        {
            string side;
            switch (key.Side)
            {
                case CardinalDirection.North:
                    side = "N";
                    break;
                case CardinalDirection.East:
                    side = "E";
                    break;
                case CardinalDirection.South:
                    side = "S";
                    break;
                default:
                    side = "W";
                    break;
            }

            if (key.FloorOffset == 0)
            {
                return $"{side}_{key.Index:00}";
            }

            return $"Top_{side}_{key.Index:00}";
        }

        private static bool Approximately(Vector3 left, Vector3 right) =>
            (left - right).sqrMagnitude <= PositionTolerance * PositionTolerance;
    }
}
