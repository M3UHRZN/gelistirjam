using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// The shared spring-damper integrator used by every HUD motion component.
    /// </summary>
    public static class HudSpring
    {
        public const float MaxTimeStep = 1f / 30f;

        public static void Step(ref Vector2 offset, ref Vector2 velocity, Vector2 target,
            float stiffness, float damping, float deltaTime)
        {
            float dt = Mathf.Min(deltaTime, MaxTimeStep);
            Vector2 acceleration = (target - offset) * stiffness - velocity * damping;
            velocity += acceleration * dt;
            offset += velocity * dt;
        }

        public static void Step(ref float offset, ref float velocity, float target,
            float stiffness, float damping, float deltaTime)
        {
            float dt = Mathf.Min(deltaTime, MaxTimeStep);
            float acceleration = (target - offset) * stiffness - velocity * damping;
            velocity += acceleration * dt;
            offset += velocity * dt;
        }
    }
}
