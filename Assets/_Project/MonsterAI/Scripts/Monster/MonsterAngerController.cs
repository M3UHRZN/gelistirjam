using System;
using UnityEngine;

namespace Game.MonsterAI
{
    /// <summary>Tracks Anger and derives movement speed from it (TGD section 19-21).</summary>
    public class MonsterAngerController : MonoBehaviour
    {
        [SerializeField] private MonsterConfig config;

        public int Anger { get; private set; }
        public event Action<int> OnAngerChanged;

        public void Bind(MonsterConfig monsterConfig) => config = monsterConfig;

        public float CurrentSpeed => config != null ? config.GetSpeedForAnger(Anger) : 0f;
        public float CommitSpeed => config != null ? config.GetCommitSpeed(Anger) : 0f;

        public void AddAnger(int amount = 1)
        {
            if (config == null) return;
            int clamped = Mathf.Clamp(Anger + amount, 0, config.MaxAnger);
            if (clamped == Anger) return;
            Anger = clamped;
            OnAngerChanged?.Invoke(Anger);
        }
    }
}
