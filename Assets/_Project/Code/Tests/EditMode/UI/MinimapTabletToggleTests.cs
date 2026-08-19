using Game.UI.Minimap;
using NUnit.Framework;

namespace Game.Tests.EditMode.UI
{
    public class MinimapTabletToggleTests
    {
        [Test]
        public void Toggle_AlternatesOpeningThenClosing()
        {
            var toggle = new MinimapTabletToggle();

            Assert.That(toggle.Toggle(), Is.EqualTo(MinimapTabletCommand.Opening));
            Assert.That(toggle.Toggle(), Is.EqualTo(MinimapTabletCommand.Closing));
        }
    }
}
