using System;
using System.Collections;
using Game.Core;
using Game.Gameplay.Objectives;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Gameplay.Tests
{
    public sealed class FuseBoxPresenterTests
    {
        [UnityTest]
        public IEnumerator BeginInteract_ActivatesThePresenter()
        {
            Fixture fixture = CreateFixture();

            try
            {
                // The fixture's minigame has no UI wired, so Begin() fails ValidateConfiguration and
                // invokes onCancelled synchronously, inside this same call, before BeginInteract()
                // returns — Activate() (called just before miniGame.Begin) still fires exactly once,
                // which is what this test verifies. Deactivate-on-cancel is Cancel_DeactivatesWithout
                // PlayingCompletion's job below, not this one's.
                LogAssert.Expect(LogType.Error, "[FuseMiniGameController] Missing required references (buttons/panel) — cannot start. See Inspector.");
                fixture.Box.BeginInteract();

                Assert.That(fixture.Presenter.ActivateCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator Cancel_DeactivatesWithoutPlayingCompletion()
        {
            Fixture fixture = CreateFixture();

            try
            {
                LogAssert.Expect(LogType.Error, "[FuseMiniGameController] Missing required references (buttons/panel) — cannot start. See Inspector.");
                fixture.Box.BeginInteract();
                fixture.MiniGame.ForceCancel();
                yield return null;

                Assert.That(fixture.Presenter.CompletionCount, Is.EqualTo(0));
                Assert.That(fixture.Presenter.DeactivateCount, Is.EqualTo(1));
                Assert.That(fixture.Box.State, Is.EqualTo(FuseBoxState.Incomplete));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator FinishInteraction_DoesNotLeaveFinalChase()
        {
            Fixture fixture = CreateFixture();

            try
            {
                LogAssert.Expect(LogType.Error, "[FuseMiniGameController] Missing required references (buttons/panel) — cannot start. See Inspector.");
                fixture.Box.BeginInteract();

                // Stand in for "the last fuse opened the exit": something else moved the flow on
                // while the fuse was finishing.
                fixture.GameFlow.ChangeState(fixture.GameFlow.FinalChaseState);

                fixture.MiniGame.ForceCancel();
                yield return null;

                Assert.That(fixture.GameFlow.CurrentStateName, Is.EqualTo(nameof(FinalChaseState)));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator FinishInteraction_ReturnsToExplorationFromFuseInteraction()
        {
            Fixture fixture = CreateFixture();

            try
            {
                // As above: the fixture's unconfigured minigame bails synchronously inside Begin(), so
                // FinishInteraction() (and its FuseInteractionState -> ExplorationState transition) has
                // already run by the time BeginInteract() returns — there is no observable intermediate
                // "still in FuseInteractionState" moment to assert on here. ForceCancel()/yield below is
                // then a no-op (documents intent, stays correct once Task 6 wires real UI); the outcome
                // this test protects — landing back in ExplorationState, not stuck elsewhere — still holds.
                LogAssert.Expect(LogType.Error, "[FuseMiniGameController] Missing required references (buttons/panel) — cannot start. See Inspector.");
                fixture.Box.BeginInteract();

                fixture.MiniGame.ForceCancel();
                yield return null;

                Assert.That(fixture.GameFlow.CurrentStateName, Is.EqualTo(nameof(ExplorationState)));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static Fixture CreateFixture()
        {
            var gameFlowObject = new GameObject("FuseBoxPresenterFixture_GameFlow");
            GameFlowController gameFlow = gameFlowObject.AddComponent<GameFlowController>();

            var objectivesObject = new GameObject("FuseBoxPresenterFixture_Objectives");
            ObjectiveManager objectives = objectivesObject.AddComponent<ObjectiveManager>();

            var miniGameObject = new GameObject("FuseBoxPresenterFixture_MiniGame");
            FuseMiniGameController miniGame = miniGameObject.AddComponent<FuseMiniGameController>();

            var boxObject = new GameObject("FuseBoxPresenterFixture_Box", typeof(BoxCollider));
            FuseBox box = boxObject.AddComponent<FuseBox>();

            var presenter = new SpyPresenter();
            box.Bind(gameFlow, objectives, miniGame, presenter);

            return new Fixture(gameFlowObject, objectivesObject, miniGameObject, boxObject,
                gameFlow, miniGame, box, presenter);
        }

        /// <summary>
        /// Counts calls instead of exercising Cinemachine: this suite is about FuseBox's decisions,
        /// and FuseTabletPresenterTests already covers the real implementation.
        /// </summary>
        private sealed class SpyPresenter : IFuseViewPresenter
        {
            public int ActivateCount { get; private set; }
            public int DeactivateCount { get; private set; }
            public int CompletionCount { get; private set; }

            public Transform ScreenAnchor => null;

            public void Activate() => ActivateCount++;

            public void Deactivate() => DeactivateCount++;

            public IEnumerator PlayCompletion()
            {
                CompletionCount++;
                yield break;
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject gameFlowObject;
            private readonly GameObject objectivesObject;
            private readonly GameObject miniGameObject;
            private readonly GameObject boxObject;

            public GameFlowController GameFlow { get; }
            public FuseMiniGameController MiniGame { get; }
            public FuseBox Box { get; }
            public SpyPresenter Presenter { get; }

            public Fixture(
                GameObject gameFlowObject,
                GameObject objectivesObject,
                GameObject miniGameObject,
                GameObject boxObject,
                GameFlowController gameFlow,
                FuseMiniGameController miniGame,
                FuseBox box,
                SpyPresenter presenter)
            {
                this.gameFlowObject = gameFlowObject;
                this.objectivesObject = objectivesObject;
                this.miniGameObject = miniGameObject;
                this.boxObject = boxObject;
                GameFlow = gameFlow;
                MiniGame = miniGame;
                Box = box;
                Presenter = presenter;
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(boxObject);
                UnityEngine.Object.DestroyImmediate(miniGameObject);
                UnityEngine.Object.DestroyImmediate(objectivesObject);
                UnityEngine.Object.DestroyImmediate(gameFlowObject);
            }
        }
    }
}
