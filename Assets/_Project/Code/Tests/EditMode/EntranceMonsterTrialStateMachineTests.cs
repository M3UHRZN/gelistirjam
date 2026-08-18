using Game.Gameplay.Entrance;
using NUnit.Framework;

namespace Game.Gameplay.Tests
{
    public sealed class EntranceMonsterTrialStateMachineTests
    {
        [Test]
        public void BeginThenRepelThenComplete_IsOneShot()
        {
            var trial = new EntranceMonsterTrialStateMachine();

            Assert.That(trial.TryBegin(), Is.True);
            Assert.That(trial.TryRepel(), Is.True);
            Assert.That(trial.TryCompleteRetreat(), Is.True);
            Assert.That(trial.State, Is.EqualTo(EntranceMonsterTrialState.Completed));
            Assert.That(trial.TryBegin(), Is.False);
        }

        [Test]
        public void CatchPlayer_IsLegalOnlyWhileApproaching()
        {
            var trial = new EntranceMonsterTrialStateMachine();

            Assert.That(trial.TryCatchPlayer(), Is.False);
            Assert.That(trial.TryBegin(), Is.True);
            Assert.That(trial.TryCatchPlayer(), Is.True);
            Assert.That(trial.State, Is.EqualTo(EntranceMonsterTrialState.PlayerCaught));
            Assert.That(trial.TryRepel(), Is.False);
        }

        [Test]
        public void SetupAbort_ReturnsApproachingTrialToReady()
        {
            var trial = new EntranceMonsterTrialStateMachine();

            Assert.That(trial.TryBegin(), Is.True);
            Assert.That(trial.TryAbortSetup(), Is.True);
            Assert.That(trial.State, Is.EqualTo(EntranceMonsterTrialState.Ready));
            Assert.That(trial.TryBegin(), Is.True);
        }
    }
}
