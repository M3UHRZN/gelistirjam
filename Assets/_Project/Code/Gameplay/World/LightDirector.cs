using System;
using System.Collections.Generic;

namespace Game.Gameplay.World
{
    /// <summary>
    /// Progressively shuts down LightZones as fuses complete (TGD section 31-32). Zones are shut
    /// down in the order they're registered — design that order deliberately (less-critical zones
    /// first, anything near the exit last) when building the real level. SafeZone-linked zones
    /// should simply never be added to this list.
    ///
    /// Shutdown pace is driven by a percentage-of-pool curve rather than a fixed zone count: each
    /// completed fuse targets "this many percent of the pool should still be active," which is what
    /// makes "Fuse 3: most of the map is dark" (TGD section 32) hold true whether the pool has 5
    /// zones (hand-built test scenes) or 60+ (the procedural main scene) — a fixed "one zone per
    /// fuse" pace only reads as intended at the small end of that range.
    /// </summary>
    public class LightDirector
    {
        private static readonly float[] DefaultShutdownTargetPercents = { 75f, 45f, 15f };

        private readonly List<LightZone> zonesToShutdown = new List<LightZone>();
        private float[] shutdownTargetPercents = DefaultShutdownTargetPercents;
        private int nextIndexToShutdown;
        private int fuseCompletionsSeen;

        public void SetShutdownOrder(IReadOnlyList<LightZone> orderedZones)
        {
            zonesToShutdown.Clear();
            zonesToShutdown.AddRange(orderedZones);
            nextIndexToShutdown = 0;
            fuseCompletionsSeen = 0;
        }

        /// <summary>
        /// Overrides the default per-fuse "percent of pool still active" curve. Each entry is the
        /// target active percentage after that fuse completes (entry 0 -> after fuse 1, entry 1 ->
        /// after fuse 2, ...); if more fuses complete than the curve has entries, the last entry is
        /// reused. No-ops on a null or empty array so callers can't accidentally clear the curve.
        /// </summary>
        public void SetShutdownTargetPercents(float[] targetActivePercents)
        {
            if (targetActivePercents == null || targetActivePercents.Length == 0) return;
            shutdownTargetPercents = targetActivePercents;
        }

        /// <summary>
        /// Called once per completed fuse — flickers (TGD section 31: warning, not an instant cut)
        /// however many zones are needed to bring the pool down to this stage's target active
        /// percentage, then leaves the rest untouched until the next fuse.
        /// </summary>
        public void OnFuseCompleted()
        {
            fuseCompletionsSeen++;
            int stageIndex = Math.Min(fuseCompletionsSeen, shutdownTargetPercents.Length) - 1;
            float targetActivePercent = shutdownTargetPercents[stageIndex];

            int targetActiveCount = (int)Math.Round(
                zonesToShutdown.Count * (targetActivePercent / 100.0), MidpointRounding.AwayFromZero);
            int targetDisabledCount = zonesToShutdown.Count - targetActiveCount;

            while (nextIndexToShutdown < targetDisabledCount && nextIndexToShutdown < zonesToShutdown.Count)
            {
                zonesToShutdown[nextIndexToShutdown].Flicker();
                nextIndexToShutdown++;
            }
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
