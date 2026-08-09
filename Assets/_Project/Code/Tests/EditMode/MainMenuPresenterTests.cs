using Game.UI.MainMenu;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class MainMenuPresenterTests
    {
        [Test]
        public void ComputeNextIndex_MovesToNextRow_WhenAllInteractable()
        {
            bool[] mask = { true, true, true, true };

            int next = MainMenuPresenter.ComputeNextIndex(0, 1, mask);

            Assert.AreEqual(1, next);
        }

        [Test]
        public void ComputeNextIndex_SkipsDisabledRow()
        {
            // Row 1 (CONTINUE) is disabled; moving down from row 0 should land on row 2, not row 1.
            bool[] mask = { true, false, true, true };

            int next = MainMenuPresenter.ComputeNextIndex(0, 1, mask);

            Assert.AreEqual(2, next);
        }

        [Test]
        public void ComputeNextIndex_WrapsFromLastToFirst()
        {
            bool[] mask = { true, true, true, true };

            int next = MainMenuPresenter.ComputeNextIndex(3, 1, mask);

            Assert.AreEqual(0, next);
        }

        [Test]
        public void ComputeNextIndex_WrapsBackwardPastDisabledRow()
        {
            // From row 2, moving up (-1) should skip disabled row 1 and land on row 0.
            bool[] mask = { true, false, true, true };

            int next = MainMenuPresenter.ComputeNextIndex(2, -1, mask);

            Assert.AreEqual(0, next);
        }

        [Test]
        public void ComputeNextIndex_ReturnsCurrent_WhenNoRowIsInteractable()
        {
            bool[] mask = { false, false, false };

            int next = MainMenuPresenter.ComputeNextIndex(1, 1, mask);

            Assert.AreEqual(1, next);
        }

        [Test]
        public void ComputeNextIndex_ReturnsCurrent_WhenMaskIsEmpty()
        {
            int next = MainMenuPresenter.ComputeNextIndex(0, 1, new bool[0]);

            Assert.AreEqual(0, next);
        }
    }
}
