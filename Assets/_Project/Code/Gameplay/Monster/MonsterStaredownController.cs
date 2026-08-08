using UnityEngine;

namespace Game.Gameplay.Monster
{
    /// <summary>
    /// Staredown detection/anger-accrual pulled out of ShyState (TGD section 12, wiki "Brackens will
    /// stop retreating and enter a stare down... allowing anger to fill"). Was previously four fields
    /// and ~20 lines interleaved with flee-timer and fake-retreat logic inside ShyState itself, which
    /// made the state hard to read and impossible to reuse if another gaze-freeze state ever needs the
    /// same "roll a chance to start staring back, then drip anger while it holds" behavior (see chat —
    /// pure extraction, no behavior change: same two-phase check-then-accumulate ordering as before).
    /// Not a MonoBehaviour — owned and ticked by the state that uses it, same lifetime as that state's
    /// own fields were.
    /// </summary>
    public class MonsterStaredownController
    {
        private readonly MonsterBrain brain;
        private float checkTimer;
        private float angerTimer;

        public MonsterStaredownController(MonsterBrain brain) => this.brain = brain;

        /// <summary>True once a staredown roll has succeeded and hasn't been ended yet.</summary>
        public bool IsActive { get; private set; }

        /// <summary>Call once per Enter() / whenever the owning state (re)starts fleeing unwatched.</summary>
        public void Reset()
        {
            IsActive = false;
            checkTimer = 0f;
            angerTimer = 0f;
        }

        /// <summary>
        /// Call every Tick while the player is currently gazing at the monster. Returns true the exact
        /// frame a staredown begins (so the caller can e.g. stop the NavMeshAgent), same as the
        /// original inline ShyState logic.
        /// </summary>
        public bool TickWhileGazed(float deltaTime)
        {
            bool justStarted = false;

            if (!IsActive)
            {
                checkTimer -= deltaTime;
                if (checkTimer <= 0f)
                {
                    checkTimer = brain.Config.StaredownCheckInterval;
                    if (Random.value < brain.Config.StaredownChance)
                    {
                        IsActive = true;
                        angerTimer = 0f;
                        justStarted = true;
                    }
                }
            }

            if (IsActive)
            {
                angerTimer += deltaTime;
                if (angerTimer >= brain.Config.StaredownAngerInterval)
                {
                    brain.Anger.AddAnger(1);
                    angerTimer = 0f;
                }
            }

            return justStarted;
        }

        /// <summary>Call when gaze breaks (or the owning state exits) to clear the staredown.</summary>
        public void EndStaredown()
        {
            IsActive = false;
            checkTimer = 0f;
        }
    }
}
