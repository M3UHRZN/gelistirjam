using System.Collections.Generic;
using UnityEngine;

namespace Game.MonsterAI
{
    /// <summary>Self-registering collection of active StalkPoints. Avoids FindObjectsOfType scans.</summary>
    [CreateAssetMenu(fileName = "StalkPointRuntimeSet", menuName = "Gloomveil/Stalk Point Set")]
    public class StalkPointRuntimeSet : ScriptableObject
    {
        private readonly List<StalkPoint> items = new List<StalkPoint>();
        public IReadOnlyList<StalkPoint> Items => items;

        public void Register(StalkPoint point)
        {
            if (!items.Contains(point)) items.Add(point);
        }

        public void Unregister(StalkPoint point) => items.Remove(point);

        // Guards against stale entries carried over between play sessions with Domain Reload disabled.
        private void OnEnable() => items.Clear();
    }
}
