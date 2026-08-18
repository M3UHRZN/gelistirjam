namespace Game.Gameplay.Entrance
{
    public enum EntranceMonsterTrialState
    {
        Ready,
        Approaching,
        Retreating,
        PlayerCaught,
        Completed,
    }

    public sealed class EntranceMonsterTrialStateMachine
    {
        public EntranceMonsterTrialState State { get; private set; } = EntranceMonsterTrialState.Ready;

        public bool TryBegin() => TryTransition(EntranceMonsterTrialState.Ready, EntranceMonsterTrialState.Approaching);
        public bool TryRepel() => TryTransition(EntranceMonsterTrialState.Approaching, EntranceMonsterTrialState.Retreating);
        public bool TryCatchPlayer() => TryTransition(EntranceMonsterTrialState.Approaching, EntranceMonsterTrialState.PlayerCaught);
        public bool TryCompleteRetreat() => TryTransition(EntranceMonsterTrialState.Retreating, EntranceMonsterTrialState.Completed);
        public bool TryAbortSetup() => TryTransition(EntranceMonsterTrialState.Approaching, EntranceMonsterTrialState.Ready);

        private bool TryTransition(EntranceMonsterTrialState expected, EntranceMonsterTrialState next)
        {
            if (State != expected) return false;
            State = next;
            return true;
        }
    }
}
