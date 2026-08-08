using System.Collections.Generic;

namespace Game.Gameplay.World
{
    /// <summary>
    /// Progressively shuts down LightZones as fuses complete (TGD section 31-32). Zones are shut
    /// down in the order they're registered — design that order deliberately (less-critical zones
    /// first, anything near the exit last) when building the real level. SafeZone-linked zones
    /// should simply never be added to this list.
    /// </summary>
    public class LightDirector
    {
        private readonly List<LightZone> zonesToShutdown = new List<LightZone>();
        private int nextIndexToShutdown;

        public void SetShutdownOrder(IReadOnlyList<LightZone> orderedZones)
        {
            zonesToShutdown.Clear();
            zonesToShutdown.AddRange(orderedZones);
            nextIndexToShutdown = 0;
        }

        /// <summary>Called once per completed fuse — flickers (TGD section 31: warning, not an instant cut) then shuts down the next zone in the configured order.</summary>
        public void OnFuseCompleted()
        {
            if (nextIndexToShutdown >= zonesToShutdown.Count) return;
            zonesToShutdown[nextIndexToShutdown].Flicker();
            nextIndexToShutdown++;
        }

        /// <summary>Final chase (TGD section 38): kill every zone this director controls, in one pass.</summary>
        public void ShutdownAll()
        {
            for (int i = 0; i < zonesToShutdown.Count; i++)
            {
                zonesToShutdown[i].SetActive(false);
            }
            nextIndexToShutdown = zonesToShutdown.Count;
        }
    }
}
