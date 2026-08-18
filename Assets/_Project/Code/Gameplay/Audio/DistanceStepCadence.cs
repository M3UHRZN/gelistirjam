using UnityEngine;

namespace Game.Gameplay.Audio
{
    public sealed class DistanceStepCadence
    {
        public float AccumulatedDistance { get; private set; }

        public bool Advance(
            float speed,
            float deltaTime,
            float distancePerStep,
            bool active)
        {
            if (!active || speed <= 0f)
            {
                Reset();
                return false;
            }

            if (deltaTime <= 0f) return false;

            float threshold = Mathf.Max(0.01f, distancePerStep);
            AccumulatedDistance += speed * deltaTime;
            if (AccumulatedDistance < threshold) return false;

            AccumulatedDistance %= threshold;
            return true;
        }

        public void Reset() => AccumulatedDistance = 0f;
    }
}
