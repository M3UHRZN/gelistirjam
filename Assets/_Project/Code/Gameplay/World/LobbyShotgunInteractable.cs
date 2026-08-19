using Game.Gameplay.Interaction;
using UnityEngine;

namespace Game.Gameplay.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class LobbyShotgunInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string interactionPrompt = "TAKE SHOTGUN";
        [SerializeField, TextArea]
        private string responseMessage = "I wish I knew how to use a gun... it might be useful.";
        [SerializeField, Min(0f)] private float responseDuration = 3f;

        private float responseVisibleUntil;

        public bool CanInteract => true;

        public string InteractionPrompt =>
            Time.unscaledTime < responseVisibleUntil ? responseMessage : interactionPrompt;

        public void BeginInteract()
        {
            responseVisibleUntil = Time.unscaledTime + responseDuration;
        }

        public void EndInteract() { }
    }
}
