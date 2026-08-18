using Game.Gameplay.Interaction;
using UnityEngine;

namespace Game.Gameplay.Entrance
{
    public interface IEntranceMonsterTrialStarter
    {
        EntranceMonsterTrialState State { get; }
        bool TryStart();
    }

    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class EntranceMonsterTrialButton : MonoBehaviour, IInteractable
    {
        [SerializeField] private EntranceMonsterTrialController controller;
        [SerializeField] private string interactionPrompt = "DON'T";

        private IEntranceMonsterTrialStarter injectedStarter;

        public bool CanInteract
        {
            get
            {
                var starter = ResolveStarter();
                return starter != null && starter.State == EntranceMonsterTrialState.Ready;
            }
        }

        public string InteractionPrompt => interactionPrompt;

        public void Bind(IEntranceMonsterTrialStarter starter)
        {
            injectedStarter = starter;
        }

        public void BeginInteract()
        {
            var starter = ResolveStarter();
            if (starter == null || starter.State != EntranceMonsterTrialState.Ready) return;

            starter.TryStart();
        }

        public void EndInteract()
        {
        }

        private IEntranceMonsterTrialStarter ResolveStarter()
        {
            return injectedStarter ?? controller;
        }
    }
}
