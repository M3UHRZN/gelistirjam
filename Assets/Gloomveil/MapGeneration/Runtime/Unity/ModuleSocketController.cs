using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gloomveil.MapGeneration
{
    [DisallowMultipleComponent]
    public sealed class ModuleSocketController : MonoBehaviour
    {
        private readonly Dictionary<SocketKey, GameObject> portals =
            new Dictionary<SocketKey, GameObject>();
        private readonly Dictionary<SocketKey, GameObject> wallCaps =
            new Dictionary<SocketKey, GameObject>();
        private bool cacheBuilt;

        public void Apply(IReadOnlyCollection<SocketKey> openSockets)
        {
            if (openSockets == null)
            {
                throw new ArgumentNullException(nameof(openSockets));
            }

            EnsureCache();
            var open = new HashSet<SocketKey>(openSockets);
            var keys = new HashSet<SocketKey>(portals.Keys);
            keys.UnionWith(wallCaps.Keys);
            foreach (SocketKey key in keys)
            {
                if (!portals.TryGetValue(key, out GameObject portal) ||
                    !wallCaps.TryGetValue(key, out GameObject wallCap))
                {
                    throw new MapGenerationException(
                        $"Dynamic module '{name}' socket {key} requires both Portal and WallCap children.");
                }

                bool isOpen = open.Contains(key);
                portal.SetActive(isOpen);
                wallCap.SetActive(!isOpen);
            }
        }

        public IReadOnlyList<SocketKey> GetDeclaredSockets()
        {
            var result = new List<SocketKey>();
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (TryParse(child.name, "Socket_", out SocketKey key))
                {
                    result.Add(key);
                }
            }

            result.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));
            return result;
        }

        public void RebuildCache()
        {
            portals.Clear();
            wallCaps.Clear();
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (TryParse(child.name, "Portal_", out SocketKey portalKey))
                {
                    AddUnique(portals, portalKey, child.gameObject, "Portal");
                }

                if (TryParse(child.name, "WallCap_", out SocketKey capKey))
                {
                    AddUnique(wallCaps, capKey, child.gameObject, "WallCap");
                }
            }

            cacheBuilt = true;
        }

        public static bool TryParse(string childName, string prefix, out SocketKey key)
        {
            key = default;
            if (string.IsNullOrEmpty(childName) ||
                string.IsNullOrEmpty(prefix) ||
                !childName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            string[] tokens = childName.Substring(prefix.Length).Split('_');
            int floorOffset = 0;
            int sideIndex = 0;
            int numberIndex = 1;
            if (tokens.Length == 3 &&
                (tokens[0] == "Bottom" || tokens[0] == "Top"))
            {
                floorOffset = tokens[0] == "Top" ? 1 : 0;
                sideIndex = 1;
                numberIndex = 2;
            }

            if (tokens.Length <= numberIndex ||
                !TryDirection(tokens[sideIndex], out CardinalDirection direction) ||
                !int.TryParse(tokens[numberIndex], out int index) ||
                index < 0)
            {
                return false;
            }

            key = new SocketKey(direction, index, floorOffset);
            return true;
        }

        private void EnsureCache()
        {
            if (!cacheBuilt)
            {
                RebuildCache();
            }
        }

        private static void AddUnique(
            IDictionary<SocketKey, GameObject> destination,
            SocketKey key,
            GameObject value,
            string kind)
        {
            if (destination.ContainsKey(key))
            {
                throw new MapGenerationException(
                    $"Duplicate {kind} child for socket {key} under '{value.transform.root.name}'.");
            }

            destination.Add(key, value);
        }

        private static bool TryDirection(string token, out CardinalDirection direction)
        {
            switch (token)
            {
                case "N":
                case "North":
                    direction = CardinalDirection.North;
                    return true;
                case "E":
                case "East":
                    direction = CardinalDirection.East;
                    return true;
                case "S":
                case "South":
                    direction = CardinalDirection.South;
                    return true;
                case "W":
                case "West":
                    direction = CardinalDirection.West;
                    return true;
                default:
                    direction = default;
                    return false;
            }
        }
    }
}
