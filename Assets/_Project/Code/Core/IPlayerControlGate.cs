namespace Game.Core
{
    /// <summary>
    /// Lets Core-layer game-flow states enable/disable player control without Core referencing a
    /// concrete Gameplay type — dependencies point inward only (MASTER_PROMPT.md section 9).
    /// Gameplay's TestPlayerController implements this; the composition root (scene builder) wires
    /// the concrete instance into GameManager.Bind().
    /// </summary>
    public interface IPlayerControlGate
    {
        void SetControlEnabled(bool enabled);
    }
}
