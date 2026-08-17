using System;
using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>TGD section 20 anger brackets — not consumed everywhere yet, but Stage is available for any state/tuning that wants tiered behavior instead of the raw 0..1 AggroChance.</summary>
    public enum AngerStage { Calm, Wary, Agitated, Furious }

    /// <summary>
    /// Persistent anger meter (Bracken wiki, section "Enraged"). Never resets on its own — the only
    /// way it drains is by being in Aggressive and losing 1/sec (AggressiveState), or contact-killing
    /// the player. Anger still scales movement/tuning brackets, but player gaze no longer consumes it
    /// as a probabilistic attack roll; a gaze reaction is always a Shy retreat.
    /// </summary>
    public class MonsterAngerController : MonoBehaviour
    {
        [SerializeField] private MonsterConfig config;

        public int Anger { get; private set; }
        public event Action<int> OnAngerChanged;

        /// <summary>0..1 chance of an aggro roll succeeding at the current anger level.</summary>
        public float AggroChance => config != null && config.MaxAnger > 0 ? (float)Anger / config.MaxAnger : 0f;

        /// <summary>TGD section 20's four-tier anger bracket, computed as quarters of MaxAnger so it scales with tuning instead of hardcoding "0-2/3-5/6-8/9+" against a fixed 12-point scale.</summary>
        public AngerStage Stage
        {
            get
            {
                float t = AggroChance;
                if (t < 0.25f) return AngerStage.Calm;
                if (t < 0.5f) return AngerStage.Wary;
                if (t < 0.75f) return AngerStage.Agitated;
                return AngerStage.Furious;
            }
        }

        public void Bind(MonsterConfig monsterConfig) => config = monsterConfig;

        public void AddAnger(int amount)
        {
            if (config == null || amount == 0) return;
            int clamped = Mathf.Clamp(Anger + amount, 0, config.MaxAnger);
            if (clamped == Anger) return;
            Anger = clamped;
            OnAngerChanged?.Invoke(Anger);
        }
    }
}
