namespace Game.Core
{
    /// <summary>
    /// All fuses complete, exit is open (TGD section 4, 38). Entered by an exit controller's
    /// Open() -> ChangeState(FinalChaseState) once every fuse reports complete
    /// (MapExitController for a generated map, ExitController in the AI test scene).
    /// This class itself stays empty on purpose: it
    /// exists as a named marker on the state machine, and the actual FinalChase side effects (kill
    /// the remaining lights and begin MonsterBrain's continuous stalking hunt) are wired as a
    /// listener on GameFlowController.OnStateChanged in SceneWiring.Wire(), matching every other
    /// system-to-system hookup in this project (event/query boundary, not a direct command — see
    /// chat / MASTER_PROMPT.md section 62.7). Was previously documented as "nothing triggers this
    /// yet", which was accurate when written but went stale once SceneWiring's listener was added —
    /// corrected here (see chat: Unity MCP validation pass).
    /// </summary>
    public class FinalChaseState : IGameState
    {
        private readonly GameFlowController flow;

        public FinalChaseState(GameFlowController flow) => this.flow = flow;

        public void Enter() { }

        public void Tick(float deltaTime) { }

        public void Exit() { }
    }
}
