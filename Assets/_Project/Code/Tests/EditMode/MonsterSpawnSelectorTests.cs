using System.Linq;
using Game.Gameplay.Monster;
using NUnit.Framework;
using UnityEngine;

namespace Game.Gameplay.Tests
{
    public sealed class MonsterSpawnSelectorTests
    {
        [Test]
        public void TrySelect_IsOrderIndependentAndSeededWithinThreeMostDistantOrdinaryOptions()
        {
            MonsterSpawnOption[] options =
            {
                Option("A", 30f, MonsterSpawnTier.OrdinaryCandidate),
                Option("B", 40f, MonsterSpawnTier.OrdinaryCandidate),
                Option("C", 50f, MonsterSpawnTier.OrdinaryCandidate),
                Option("D", 60f, MonsterSpawnTier.OrdinaryCandidate),
                Option("Fuse", 80f, MonsterSpawnTier.RelaxedCandidate),
            };

            Assert.That(MonsterSpawnSelector.TrySelect(options, 1042, 24f, out var first, out var reason), Is.True);
            Assert.That(MonsterSpawnSelector.TrySelect(options.Reverse().ToArray(), 1042, 24f, out var second, out _), Is.True);
            Assert.That(second.StableKey, Is.EqualTo(first.StableKey));
            Assert.That(new[] { "B", "C", "D" }, Does.Contain(first.StableKey));
            Assert.That(reason, Is.EqualTo(MonsterSpawnSelectionReason.SeededDistant));
        }

        [Test]
        public void TrySelect_WhenOrdinaryOptionsAreBelowMinimum_UsesFarthestOrdinary()
        {
            MonsterSpawnOption[] options =
            {
                Option("Near", 8f, MonsterSpawnTier.OrdinaryCandidate),
                Option("Far", 18f, MonsterSpawnTier.OrdinaryCandidate),
                Option("Relaxed", 80f, MonsterSpawnTier.RelaxedCandidate),
            };

            Assert.That(MonsterSpawnSelector.TrySelect(options, 3, 24f, out var selected, out var reason), Is.True);
            Assert.That(selected.StableKey, Is.EqualTo("Far"));
            Assert.That(reason, Is.EqualTo(MonsterSpawnSelectionReason.OrdinaryFarthest));
        }

        [Test]
        public void TrySelect_WithoutOrdinaryOptions_UsesFarthestRelaxedOption()
        {
            MonsterSpawnOption[] options =
            {
                Option("RelaxedNear", 12f, MonsterSpawnTier.RelaxedCandidate),
                Option("RelaxedFar", 31f, MonsterSpawnTier.RelaxedCandidate),
                Option("Stalk", 90f, MonsterSpawnTier.StalkFallback),
            };

            Assert.That(MonsterSpawnSelector.TrySelect(options, 7, 24f, out var selected, out var reason), Is.True);
            Assert.That(selected.StableKey, Is.EqualTo("RelaxedFar"));
            Assert.That(reason, Is.EqualTo(MonsterSpawnSelectionReason.RelaxedFarthest));
        }

        [Test]
        public void TrySelect_WithOnlyStalkOptions_UsesFarthestStalkOption()
        {
            MonsterSpawnOption[] options =
            {
                Option("StalkNear", 10f, MonsterSpawnTier.StalkFallback),
                Option("StalkFar", 42f, MonsterSpawnTier.StalkFallback),
            };

            Assert.That(MonsterSpawnSelector.TrySelect(options, 11, 24f, out var selected, out var reason), Is.True);
            Assert.That(selected.StableKey, Is.EqualTo("StalkFar"));
            Assert.That(reason, Is.EqualTo(MonsterSpawnSelectionReason.StalkFarthest));
        }

        [Test]
        public void TrySelect_WithNoOptions_ReturnsFalse()
        {
            Assert.That(MonsterSpawnSelector.TrySelect(
                System.Array.Empty<MonsterSpawnOption>(),
                1042,
                24f,
                out _,
                out var reason), Is.False);
            Assert.That(reason, Is.EqualTo(MonsterSpawnSelectionReason.None));
        }

        private static MonsterSpawnOption Option(string key, float pathLength, MonsterSpawnTier tier) =>
            new MonsterSpawnOption(
                key,
                new Vector3(pathLength, 0f, 0f),
                0f,
                pathLength,
                tier);
    }
}
