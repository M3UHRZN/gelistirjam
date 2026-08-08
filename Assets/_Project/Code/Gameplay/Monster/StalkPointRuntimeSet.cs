using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>Self-registering collection of hand-placed StalkPoints, mirrors SafeZoneRuntimeSet's pattern.</summary>
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

        private void OnEnable() => items.Clear();
    }
}
