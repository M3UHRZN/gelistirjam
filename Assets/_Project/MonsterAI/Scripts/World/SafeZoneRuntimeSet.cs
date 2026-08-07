using System.Collections.Generic;
using UnityEngine;

namespace Game.MonsterAI
{
    /// <summary>Self-registering collection of active SafeZones. Avoids FindObjectsOfType scans.</summary>
    [CreateAssetMenu(fileName = "SafeZoneRuntimeSet", menuName = "Gloomveil/Safe Zone Set")]
    public class SafeZoneRuntimeSet : ScriptableObject
    {
        private readonly List<SafeZone> items = new List<SafeZone>();
        public IReadOnlyList<SafeZone> Items => items;

        public void Register(SafeZone zone)
        {
            if (!items.Contains(zone)) items.Add(zone);
        }

        public void Unregister(SafeZone zone) => items.Remove(zone);

        private void OnEnable() => items.Clear();
    }
}
