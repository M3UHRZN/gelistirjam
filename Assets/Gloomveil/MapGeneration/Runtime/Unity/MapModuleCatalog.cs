using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Gloomveil.MapGeneration
{
    [CreateAssetMenu(
        fileName = "MapModuleCatalog",
        menuName = "Gloomveil/Map Generation/Module Catalog")]
    public sealed class MapModuleCatalog : ScriptableObject
    {
        [SerializeField] private List<MapModuleEntry> entries = new List<MapModuleEntry>();

        public IReadOnlyList<MapModuleEntry> Entries =>
            new ReadOnlyCollection<MapModuleEntry>(entries);

        public void SetEntries(IEnumerable<MapModuleEntry> newEntries)
        {
            entries = new List<MapModuleEntry>(
                newEntries ?? throw new ArgumentNullException(nameof(newEntries)));
        }
    }
}
