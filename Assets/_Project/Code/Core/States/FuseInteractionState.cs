namespace Game.Core
{
    /// <summary>
    /// Player is busy with a fuse minigame (TGD section 4, 35). Movement is restricted for the
    /// duration. Entered by FuseBox.BeginInteract, exited back to ExplorationState by
    /// FuseBox.EndInteract (both on complete and on cancel — see chat: Unity MCP validation pass,
    /// this comment previously said "nothing transitions into or out of this state yet", which went
    /// stale once FuseBox landed).
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
