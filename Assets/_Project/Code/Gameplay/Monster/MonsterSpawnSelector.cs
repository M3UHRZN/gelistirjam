using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Monster
{
    public enum MonsterSpawnTier
    {
        OrdinaryCandidate,
        RelaxedCandidate,
        StalkFallback,
    }

    public enum MonsterSpawnSelectionReason
    {
        None,
        SeededDistant,
        OrdinaryFarthest,
        RelaxedFarthest,
        StalkFarthest,
    }

    public readonly struct MonsterSpawnOption
    {
        public MonsterSpawnOption(
            string stableKey,
            Vector3 position,
            float yaw,
            float pathLength,
            MonsterSpawnTier tier)
        {
            StableKey = stableKey;
            Position = position;
            Yaw = yaw;
            PathLength = pathLength;
            Tier = tier;
        }

        public string StableKey { get; }
        public Vector3 Position { get; }
        public float Yaw { get; }
        public float PathLength { get; }
        public MonsterSpawnTier Tier { get; }
    }

    /// <summary>
    /// Pure deterministic selection policy. World/NavMesh validation is deliberately performed by
    /// the composition gate, which passes only reachable options into this class.
    /// </summary>
    public static class MonsterSpawnSelector
    {
        private const int SeedSalt = 0x4D4F4E53;

        public static bool TrySelect(
            IReadOnlyList<MonsterSpawnOption> options,
            int seed,
            float minimumPathDistance,
            out MonsterSpawnOption selected,
            out MonsterSpawnSelectionReason reason)
        {
            if (options == null || options.Count == 0)
            {
                selected = default;
                reason = MonsterSpawnSelectionReason.None;
                return false;
            }

            var distantOrdinary = new List<MonsterSpawnOption>();
            for (int i = 0; i < options.Count; i++)
            {
                MonsterSpawnOption option = options[i];
                if (option.Tier == MonsterSpawnTier.OrdinaryCandidate
                    && option.PathLength >= minimumPathDistance)
                {
                    distantOrdinary.Add(option);
                }
            }

            distantOrdinary.Sort(CompareDistantFirst);
            if (distantOrdinary.Count > 0)
            {
                int candidateCount = Math.Min(3, distantOrdinary.Count);
                int index = new System.Random(seed ^ SeedSalt).Next(candidateCount);
                selected = distantOrdinary[index];
                reason = MonsterSpawnSelectionReason.SeededDistant;
                return true;
            }

            if (TryGetFarthest(options, MonsterSpawnTier.OrdinaryCandidate, out selected))
            {
                reason = MonsterSpawnSelectionReason.OrdinaryFarthest;
                return true;
            }

            if (TryGetFarthest(options, MonsterSpawnTier.RelaxedCandidate, out selected))
            {
                reason = MonsterSpawnSelectionReason.RelaxedFarthest;
                return true;
            }

            if (TryGetFarthest(options, MonsterSpawnTier.StalkFallback, out selected))
            {
                reason = MonsterSpawnSelectionReason.StalkFarthest;
                return true;
            }

            selected = default;
            reason = MonsterSpawnSelectionReason.None;
            return false;
        }

        private static bool TryGetFarthest(
            IReadOnlyList<MonsterSpawnOption> options,
            MonsterSpawnTier tier,
            out MonsterSpawnOption selected)
        {
            bool found = false;
            selected = default;

            for (int i = 0; i < options.Count; i++)
            {
                MonsterSpawnOption candidate = options[i];
                if (candidate.Tier != tier) continue;
                if (!found || CompareDistantFirst(candidate, selected) < 0)
                {
                    selected = candidate;
                    found = true;
                }
            }

            return found;
        }

        private static int CompareDistantFirst(MonsterSpawnOption left, MonsterSpawnOption right)
        {
            int distanceOrder = right.PathLength.CompareTo(left.PathLength);
            return distanceOrder != 0
                ? distanceOrder
                : string.Compare(left.StableKey, right.StableKey, StringComparison.Ordinal);
        }
    }
}
