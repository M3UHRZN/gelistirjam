using System.Collections.Generic;
using Game.Gameplay.Entrance;
using NUnit.Framework;

namespace Game.Gameplay.Tests
{
    public sealed class EntranceMonsterTrialStateMachineTests
    {
        public enum TrialOperation
        {
            Begin,
            Repel,
            CatchPlayer,
            CompleteRetreat,
            AbortSetup,
        }

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

        [TestCaseSource(nameof(InvalidOperationCases))]
        public void InvalidOperation_ReturnsFalseAndLeavesStateUnchanged(
            EntranceMonsterTrialState startingState,
            TrialOperation operation)
        {
            EntranceMonsterTrialStateMachine trial = CreateInState(startingState);

            Assert.That(Apply(trial, operation), Is.False);
            Assert.That(trial.State, Is.EqualTo(startingState));
        }

        private static IEnumerable<TestCaseData> InvalidOperationCases()
        {
            yield return Invalid(EntranceMonsterTrialState.Ready, TrialOperation.Repel);
            yield return Invalid(EntranceMonsterTrialState.Ready, TrialOperation.CatchPlayer);
            yield return Invalid(EntranceMonsterTrialState.Ready, TrialOperation.CompleteRetreat);
            yield return Invalid(EntranceMonsterTrialState.Ready, TrialOperation.AbortSetup);

            yield return Invalid(EntranceMonsterTrialState.Approaching, TrialOperation.Begin);
            yield return Invalid(EntranceMonsterTrialState.Approaching, TrialOperation.CompleteRetreat);

            yield return Invalid(EntranceMonsterTrialState.Retreating, TrialOperation.Begin);
            yield return Invalid(EntranceMonsterTrialState.Retreating, TrialOperation.Repel);
            yield return Invalid(EntranceMonsterTrialState.Retreating, TrialOperation.CatchPlayer);
            yield return Invalid(EntranceMonsterTrialState.Retreating, TrialOperation.AbortSetup);

            yield return Invalid(EntranceMonsterTrialState.PlayerCaught, TrialOperation.Begin);
            yield return Invalid(EntranceMonsterTrialState.PlayerCaught, TrialOperation.Repel);
            yield return Invalid(EntranceMonsterTrialState.PlayerCaught, TrialOperation.CatchPlayer);
            yield return Invalid(EntranceMonsterTrialState.PlayerCaught, TrialOperation.CompleteRetreat);
            yield return Invalid(EntranceMonsterTrialState.PlayerCaught, TrialOperation.AbortSetup);

            yield return Invalid(EntranceMonsterTrialState.Completed, TrialOperation.Begin);
            yield return Invalid(EntranceMonsterTrialState.Completed, TrialOperation.Repel);
            yield return Invalid(EntranceMonsterTrialState.Completed, TrialOperation.CatchPlayer);
            yield return Invalid(EntranceMonsterTrialState.Completed, TrialOperation.CompleteRetreat);
            yield return Invalid(EntranceMonsterTrialState.Completed, TrialOperation.AbortSetup);
        }

        private static TestCaseData Invalid(EntranceMonsterTrialState state, TrialOperation operation)
        {
            return new TestCaseData(state, operation).SetName($"Invalid_{state}_{operation}");
        }

        private static EntranceMonsterTrialStateMachine CreateInState(EntranceMonsterTrialState state)
        {
            var trial = new EntranceMonsterTrialStateMachine();
            switch (state)
            {
                case EntranceMonsterTrialState.Ready:
                    return trial;
                case EntranceMonsterTrialState.Approaching:
                    Assert.That(trial.TryBegin(), Is.True);
                    return trial;
                case EntranceMonsterTrialState.Retreating:
                    Assert.That(trial.TryBegin(), Is.True);
                    Assert.That(trial.TryRepel(), Is.True);
                    return trial;
                case EntranceMonsterTrialState.PlayerCaught:
                    Assert.That(trial.TryBegin(), Is.True);
                    Assert.That(trial.TryCatchPlayer(), Is.True);
                    return trial;
                case EntranceMonsterTrialState.Completed:
                    Assert.That(trial.TryBegin(), Is.True);
                    Assert.That(trial.TryRepel(), Is.True);
                    Assert.That(trial.TryCompleteRetreat(), Is.True);
                    return trial;
                default:
                    Assert.Fail($"No test setup exists for state {state}.");
                    return trial;
            }
        }

        private static bool Apply(EntranceMonsterTrialStateMachine trial, TrialOperation operation)
        {
            switch (operation)
            {
                case TrialOperation.Begin:
                    return trial.TryBegin();
                case TrialOperation.Repel:
                    return trial.TryRepel();
                case TrialOperation.CatchPlayer:
                    return trial.TryCatchPlayer();
                case TrialOperation.CompleteRetreat:
                    return trial.TryCompleteRetreat();
                case TrialOperation.AbortSetup:
                    return trial.TryAbortSetup();
                default:
                    Assert.Fail($"No test invocation exists for operation {operation}.");
                    return false;
            }
        }
    }
}
