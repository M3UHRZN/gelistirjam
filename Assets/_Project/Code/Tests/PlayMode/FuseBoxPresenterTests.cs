using System;
using System.Collections;
using Game.Core;
using Game.Gameplay.Objectives;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

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
                fixture.Box.BeginInteract();

                Assert.That(fixture.Presenter.ActivateCount, Is.EqualTo(1));
                Assert.That(fixture.Presenter.DeactivateCount, Is.EqualTo(0));
            }
            finally
            {
                fixture.Dispose();
            }

            yield break;
        }

        /// <summary>
        /// A genuine in-flight cancel (ForceCancel while RunFuse is mid-coroutine, waiting on the
        /// player), not the ValidateConfiguration bail-out surrogate this suite used before the fixture
        /// wired a valid minigame UI — that surrogate exercised a real but differently-named path (see
        /// code review on this task: label vs. coverage mismatch). With a valid panel/buttons, Begin()
        /// actually starts RunFuse, so BeginInteract() returns while the coroutine is suspended
        /// (IsActive == true) and ForceCancel() has a real window to act in.
        /// </summary>
        [UnityTest]
        public IEnumerator Cancel_DeactivatesWithoutPlayingCompletion()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Box.BeginInteract();
                fixture.MiniGame.ForceCancel();
                yield return WaitUntilInactive(fixture.MiniGame);

                Assert.That(fixture.Presenter.CompletionCount, Is.EqualTo(0));
                Assert.That(fixture.Presenter.DeactivateCount, Is.EqualTo(1));
                Assert.That(fixture.Box.State, Is.EqualTo(FuseBoxState.Incomplete));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        /// <summary>
        /// The regression test for this task's guard fix (see FuseBox.FinishInteraction's doc comment).
        /// Pins the fix by construction: the flow is forced into FinalChaseState (standing in for "the
        /// last fuse opened the exit") strictly *between* BeginInteract() and the cancel resolving, then
        /// ForceCancel() runs a real async cancel (IsActive only flips false once RunFuse's coroutine
        /// actually unwinds through ClosePanel/onCancelled — this is not the synchronous
        /// ValidateConfiguration bail-out, so there is a genuine multi-frame window here). Against the
        /// pre-fix `CurrentStateName != nameof(DeathState)` guard this fails (FinalChaseState != Death
        /// is true, so it gets stomped back to ExplorationState); against the corrected
        /// `CurrentStateName == nameof(FuseInteractionState)` guard it passes. Verified both ways live
        /// via Unity MCP — see task-5-report.md's fix report.
        /// </summary>
        [UnityTest]
        public IEnumerator FinishInteraction_DoesNotLeaveFinalChase()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Box.BeginInteract();
                Assert.That(fixture.GameFlow.CurrentStateName, Is.EqualTo(nameof(FuseInteractionState)));
                Assert.That(fixture.MiniGame.IsActive, Is.True);

                // Stand in for "the last fuse opened the exit": something else moved the flow on
                // while the fuse was finishing.
                fixture.GameFlow.ChangeState(fixture.GameFlow.FinalChaseState);

                fixture.MiniGame.ForceCancel();
                yield return WaitUntilInactive(fixture.MiniGame);

                Assert.That(fixture.GameFlow.CurrentStateName, Is.EqualTo(nameof(FinalChaseState)));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        /// <summary>
        /// Happy-path coverage only: FuseInteractionState != DeathState was already true under the
        /// pre-fix guard, so this scenario can't tell the old and new guards apart (that's
        /// FinishInteraction_DoesNotLeaveFinalChase's job, above). This test exists to confirm the
        /// ordinary "cancel out of an in-progress fuse" case still lands back in Exploration.
        /// </summary>
        [UnityTest]
        public IEnumerator FinishInteraction_ReturnsToExplorationFromFuseInteraction()
        {
            Fixture fixture = CreateFixture();

            try
            {
                fixture.Box.BeginInteract();
                Assert.That(fixture.GameFlow.CurrentStateName, Is.EqualTo(nameof(FuseInteractionState)));

                fixture.MiniGame.ForceCancel();
                yield return WaitUntilInactive(fixture.MiniGame);

                Assert.That(fixture.GameFlow.CurrentStateName, Is.EqualTo(nameof(ExplorationState)));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        /// <summary>
        /// Pumps frames until the minigame coroutine has actually unwound (RunFuse's Cancelled branch
        /// calls ClosePanel then sets IsActive = false before invoking onCancelled), rather than
        /// assuming one frame is always enough. Fails loudly instead of hanging if it never resolves.
        /// </summary>
        private static IEnumerator WaitUntilInactive(FuseMiniGameController miniGame, int maxFrames = 30)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                if (!miniGame.IsActive) yield break;
                yield return null;
            }

            Assert.Fail($"FuseMiniGameController was still active after {maxFrames} frames following ForceCancel().");
        }

        private static Fixture CreateFixture()
        {
            var gameFlowObject = new GameObject("FuseBoxPresenterFixture_GameFlow");
            GameFlowController gameFlow = gameFlowObject.AddComponent<GameFlowController>();

            var objectivesObject = new GameObject("FuseBoxPresenterFixture_Objectives");
            ObjectiveManager objectives = objectivesObject.AddComponent<ObjectiveManager>();

            var miniGameObject = new GameObject("FuseBoxPresenterFixture_MiniGame", typeof(Canvas));
            FuseMiniGameController miniGame = miniGameObject.AddComponent<FuseMiniGameController>();
            WireMinimalValidUi(miniGame, miniGameObject.transform);

            var screenAnchorObject = new GameObject("FuseBoxPresenterFixture_ScreenAnchor");

            var boxObject = new GameObject("FuseBoxPresenterFixture_Box", typeof(BoxCollider));
            FuseBox box = boxObject.AddComponent<FuseBox>();

            var presenter = new SpyPresenter(screenAnchorObject.transform);
            box.Bind(gameFlow, objectives, miniGame, presenter);

            return new Fixture(gameFlowObject, objectivesObject, miniGameObject, boxObject, screenAnchorObject,
                gameFlow, miniGame, box, presenter);
        }

        /// <summary>
        /// Wires just enough UI for FuseMiniGameController.ValidateConfiguration() to pass — a panel
        /// GameObject and 4 distinct Button/Image pairs — so Begin() actually starts the RunFuse
        /// coroutine instead of bailing synchronously (see chat: code review on this task found the
        /// suite's earlier reliance on that synchronous bail-out left the guard fix unpinned). A Canvas
        /// is required after the world-space migration; EventSystem and raycasting infrastructure remain
        /// unused because these tests only exercise ForceCancel()/timeouts. panelGroup is left unbound
        /// (both null-checked in FuseMiniGameController) so no CanvasGroup fades run; fadeDuration is
        /// zeroed anyway for defence if that ever changes.
        ///
        /// One known gap: because this uses AddComponent&lt;FuseMiniGameController&gt;() on an already-
        /// active GameObject, Awake() (which wires each button's onClick listener) has already run by
        /// the time these fields are assigned below, so the buttons' onClick listeners are never
        /// connected. That only matters for tests that simulate the player pressing a button — none of
        /// these do; every test here drives the flow through ForceCancel().
        /// </summary>
        private static void WireMinimalValidUi(FuseMiniGameController miniGame, Transform parent)
        {
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(parent, false);

            var buttons = new Button[4];
            var images = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                var buttonObject = new GameObject("Button_" + i, typeof(RectTransform));
                buttonObject.transform.SetParent(panel.transform, false);
                Image image = buttonObject.AddComponent<Image>();
                Button button = buttonObject.AddComponent<Button>();
                button.targetGraphic = image;
                buttons[i] = button;
                images[i] = image;
            }

            var serialized = new SerializedObject(miniGame);
            serialized.FindProperty("worldCanvas").objectReferenceValue = parent.GetComponent<Canvas>();
            serialized.FindProperty("panelRoot").objectReferenceValue = panel;

            SerializedProperty buttonsProp = serialized.FindProperty("buttons");
            SerializedProperty imagesProp = serialized.FindProperty("buttonImages");
            buttonsProp.arraySize = buttons.Length;
            imagesProp.arraySize = images.Length;
            for (int i = 0; i < buttons.Length; i++)
            {
                buttonsProp.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
                imagesProp.GetArrayElementAtIndex(i).objectReferenceValue = images[i];
            }

            serialized.FindProperty("fadeDuration").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Counts calls instead of exercising Cinemachine: this suite is about FuseBox's decisions,
        /// and FuseTabletPresenterTests already covers the real implementation.
        /// </summary>
        private sealed class SpyPresenter : IFuseViewPresenter
        {
            private readonly Transform screenAnchor;

            public SpyPresenter(Transform screenAnchor)
            {
                this.screenAnchor = screenAnchor;
            }

            public int ActivateCount { get; private set; }
            public int DeactivateCount { get; private set; }
            public int CompletionCount { get; private set; }

            public Transform ScreenAnchor => screenAnchor;

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
            private readonly GameObject screenAnchorObject;

            public GameFlowController GameFlow { get; }
            public FuseMiniGameController MiniGame { get; }
            public FuseBox Box { get; }
            public SpyPresenter Presenter { get; }

            public Fixture(
                GameObject gameFlowObject,
                GameObject objectivesObject,
                GameObject miniGameObject,
                GameObject boxObject,
                GameObject screenAnchorObject,
                GameFlowController gameFlow,
                FuseMiniGameController miniGame,
                FuseBox box,
                SpyPresenter presenter)
            {
                this.gameFlowObject = gameFlowObject;
                this.objectivesObject = objectivesObject;
                this.miniGameObject = miniGameObject;
                this.boxObject = boxObject;
                this.screenAnchorObject = screenAnchorObject;
                GameFlow = gameFlow;
                MiniGame = miniGame;
                Box = box;
                Presenter = presenter;
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(boxObject);
                UnityEngine.Object.DestroyImmediate(screenAnchorObject);
                UnityEngine.Object.DestroyImmediate(miniGameObject);
                UnityEngine.Object.DestroyImmediate(objectivesObject);
                UnityEngine.Object.DestroyImmediate(gameFlowObject);
            }
        }
    }
}
