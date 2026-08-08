using System;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// Forwards trigger entries to a listener that does not live on the same GameObject.
    /// MapExitController is a persistent scene component, but the collider it reacts to is
    /// spawned at runtime under the generated map root — Unity delivers OnTriggerEnter only to
    /// components on the collider's own GameObject, so the two need this bridge.
    /// </summary>
    [DisallowMultipleComponent]
    public class ExitTriggerRelay : MonoBehaviour
    {
        public event Action<Collider> Entered;

        private void OnTriggerEnter(Collider other) => Entered?.Invoke(other);
    }
}
