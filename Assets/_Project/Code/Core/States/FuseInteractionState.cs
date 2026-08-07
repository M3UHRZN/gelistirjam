namespace Game.Core
{
    /// <summary>
    /// Player is busy with a fuse minigame (TGD section 4, 35). Movement is restricted for the
    /// duration. Nothing transitions into or out of this state yet — the fuse system (Faz 2) will
    /// call GameManager.ChangeState(FuseInteractionState) on interact, and back to Exploration on
    /// minigame complete/cancel.
    /// </summary>
    public class FuseInteractionState : IGameState
    {
        private readonly GameManager manager;

        public FuseInteractionState(GameManager manager) => this.manager = manager;

        public void Enter()
        {
            manager.SetPlayerControlEnabled(false);
        }

        public void Tick(float deltaTime) { }

        public void Exit()
        {
            manager.SetPlayerControlEnabled(true);
        }
    }
}
