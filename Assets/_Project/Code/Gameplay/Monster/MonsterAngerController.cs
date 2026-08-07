using System;
using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Persistent anger meter (Bracken wiki, section "Enraged"). Never resets on its own — the only
    /// way it drains is by being in Aggressive and losing 1/sec (AggressiveState), or contact-killing
    /// the player. Higher anger means a higher chance of escalating to Aggressive the next time a
    /// staredown ends (see InterestedState/ShyState) — this is the probabilistic "aggro roll".
    /// </summary>
    public class MonsterAngerController : MonoBehaviour
    {
        [SerializeField] private MonsterConfig config;

        public int Anger { get; private set; }
        public event Action<int> OnAngerChanged;

        /// <summary>0..1 chance of an aggro roll succeeding at the current anger level.</summary>
        public float AggroChance => config != null && config.MaxAnger > 0 ? (float)Anger / config.MaxAnger : 0f;

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
