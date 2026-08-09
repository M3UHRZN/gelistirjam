using Game.Gameplay.Monster;
using NUnit.Framework;
using UnityEngine;

namespace Game.Gameplay.Tests
{
    public sealed class GeneratedStalkPointMetadataTests
    {
        [TestCase("StalkPoint_Corner", "PF_TCOR_Straight_1x1_01", true, false, false, true)]
        [TestCase("StalkPoint_DoorSide", "PF_TCOR_TJunction_1x1_01", false, true, false, true)]
        [TestCase("StalkPoint_DoorSide", "PF_TCOR_CrossJunction_1x1_01", false, true, false, true)]
        [TestCase("StalkPoint_DoorSide", "PF_TCOR_DeadEnd_1x1_01", false, false, true, true)]
        [TestCase("StalkPoint_StairLanding", "PF_TSTA_Straight_1x2_01", false, false, false, false)]
        public void Infer_UsesAnchorAndModuleNames(
            string anchorName,
            string catalogEntryId,
            bool nearCorner,
            bool junction,
            bool deadEnd,
            bool ambushAllowed)
        {
            GeneratedStalkPointMetadata metadata =
                GeneratedStalkPointMetadata.Infer(anchorName, catalogEntryId);

            Assert.That(metadata.IsNearCorner, Is.EqualTo(nearCorner));
            Assert.That(metadata.IsJunction, Is.EqualTo(junction));
            Assert.That(metadata.IsDeadEnd, Is.EqualTo(deadEnd));
            Assert.That(metadata.AmbushAllowed, Is.EqualTo(ambushAllowed));
        }

        [TestCase(-1f, 0f)]
        [TestCase(0.35f, 0.35f)]
        [TestCase(2f, 1f)]
        public void ConfigureStaticDarkness_ClampsToUnitRange(float input, float expected)
        {
            var gameObject = new GameObject("StalkPoint");
            try
            {
                StalkPoint point = gameObject.AddComponent<StalkPoint>();
                point.ConfigureStaticDarkness(input);

                Assert.That(point.DarknessLevel, Is.EqualTo(expected).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
