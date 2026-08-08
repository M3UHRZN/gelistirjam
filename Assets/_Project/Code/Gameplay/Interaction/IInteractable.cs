namespace Game.Gameplay.Interaction
{
    /// <summary>Contract for anything the player can hold-interact with via PlayerInteraction's forward ray (TGD section 49).</summary>
    public interface IInteractable
    {
        bool CanInteract { get; }
        string InteractionPrompt { get; }

        /// <summary>Player pressed and is holding the interact key while aiming at this object.</summary>
        void BeginInteract();

        /// <summary>Player released the interact key, looked away, or moved out of range.</summary>
        void EndInteract();
    }
}
