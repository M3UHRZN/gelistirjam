using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>Exposes world-space velocity without coupling AI to a concrete player controller.</summary>
    public interface IPlayerVelocitySource
    {
        Vector3 Velocity { get; }
    }
}
