using System;
using Game.Gameplay.Interaction;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// Forwards trigger entries and E-hold interactions to a listener that does not live on the
    /// same GameObject. MapExitController is a persistent scene component, but the collider it
    /// reacts to is spawned at runtime under the generated map root — Unity delivers OnTriggerEnter
    /// only to components on the collider's own GameObject, and PlayerInteraction's raycast needs
    /// an IInteractable on that same GameObject too, so both need this bridge.
    /// </summary>
    [DisallowMultipleComponent]
    public class ExitTriggerRelay : MonoBehaviour, IInteractable
    {
        public event Action<Collider> Entered;
        public event Action Interacted;

        /// <summary>Set by MapExitController once the exit is unlocked — closed/locked doors don't respond to E.</summary>
        public bool CanInteract { get; set; }

        public string InteractionPrompt => "E - ÇIKIŞ";

        private void OnTriggerEnter(Collider other) => Entered?.Invoke(other);

        public void BeginInteract() => Interacted?.Invoke();

        public void EndInteract() { }
    }
}
