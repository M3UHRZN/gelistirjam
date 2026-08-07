using System;
using UnityEngine;

namespace Gloomveil.MapGeneration
{
    [DisallowMultipleComponent]
    public sealed class GeneratedMapRegistry : MonoBehaviour
    {
        private GeneratedMapRuntimeData current;

        public event Action<GeneratedMapRuntimeData> MapReady;

        public bool IsReady => current != null;
        public GeneratedMapRuntimeData Current => current ?? throw new InvalidOperationException(
            "No generated map has been published.");

        public bool TryGetCurrent(out GeneratedMapRuntimeData data)
        {
            data = current;
            return data != null;
        }

        public void Publish(GeneratedMapRuntimeData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (current != null)
            {
                throw new InvalidOperationException(
                    "A generated map is already published. Clear it before publishing again.");
            }

            current = data;
            MapReady?.Invoke(data);
        }

        public void Clear()
        {
            current = null;
        }
    }
}
