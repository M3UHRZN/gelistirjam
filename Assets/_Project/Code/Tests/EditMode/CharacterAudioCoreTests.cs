using Game.Gameplay.Audio;
using NUnit.Framework;
using UnityEngine;

namespace Game.Gameplay.Tests
{
    public sealed class CharacterAudioCoreTests
    {
        private AudioClip first;
        private AudioClip second;

        [SetUp]
        public void SetUp()
        {
            first = AudioClip.Create("First", 80, 1, 8000, false);
            second = AudioClip.Create("Second", 80, 1, 8000, false);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
        }

        [Test]
        public void EmptyAndNullOnlyCueSets_AreSilent()
        {
            Assert.That(new AudioCueSet().TrySelect(out _, out _, out _), Is.False);
            Assert.That(new AudioCueSet(new AudioClip[] { null, null })
                .TrySelect(out _, out _, out _), Is.False);
        }

        [Test]
        public void TwoClipCueSet_AvoidsImmediateRepeat()
        {
            var cues = new AudioCueSet(new[] { first, null, second });
            Assert.That(cues.TrySelect(out AudioClip previous, out _, out _), Is.True);

            for (int index = 0; index < 12; index++)
            {
                Assert.That(cues.TrySelect(out AudioClip current, out _, out _), Is.True);
                Assert.That(current, Is.Not.SameAs(previous));
                previous = current;
            }
        }

        [Test]
        public void ReversedVariationRanges_AreNormalizedAtSelection()
        {
            var cues = new AudioCueSet(new[] { first }, 1.2f, 0.8f, 1.1f, 0.9f);
            Assert.That(cues.TrySelect(out _, out float volume, out float pitch), Is.True);
            Assert.That(volume, Is.InRange(0.8f, 1.2f));
            Assert.That(pitch, Is.InRange(0.9f, 1.1f));
        }

        [Test]
        public void Cadence_EmitsAfterTravelDistanceAndResetsWhenInactive()
        {
            var cadence = new DistanceStepCadence();
            Assert.That(cadence.Advance(2f, 0.2f, 1f, true), Is.False);
            Assert.That(cadence.Advance(2f, 0.31f, 1f, true), Is.True);

            Assert.That(cadence.Advance(2f, 0.2f, 1f, false), Is.False);
            Assert.That(cadence.AccumulatedDistance, Is.Zero);
            Assert.That(cadence.Advance(2f, 0.3f, 1f, true), Is.False);
        }

        [Test]
        public void Cadence_FrameHitchEmitsAtMostOnceAndKeepsOnlyRemainder()
        {
            var cadence = new DistanceStepCadence();
            Assert.That(cadence.Advance(9f, 0.5f, 1f, true), Is.True);
            Assert.That(cadence.AccumulatedDistance, Is.EqualTo(0.5f).Within(0.0001f));
        }
    }
}
