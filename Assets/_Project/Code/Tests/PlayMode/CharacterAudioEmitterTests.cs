using Game.Gameplay.Audio;
using NUnit.Framework;
using UnityEngine;

namespace Game.Gameplay.Tests
{
    public sealed class CharacterAudioEmitterTests
    {
        [Test]
        public void BoundEmitter_UsesDedicatedChannelsAndRecordsSelectedClips()
        {
            var root = new GameObject("Emitter");
            AudioClip stepClip = AudioClip.Create("Step", 80, 1, 8000, false);
            AudioClip voiceClip = AudioClip.Create("Voice", 80, 1, 8000, false);
            try
            {
                AudioSource stepSource = root.AddComponent<AudioSource>();
                AudioSource voiceSource = root.AddComponent<AudioSource>();
                CharacterAudioEmitter emitter = root.AddComponent<CharacterAudioEmitter>();
                emitter.Bind(stepSource, voiceSource);

                Assert.That(emitter.IsBound, Is.True);
                Assert.That(emitter.PlayFootstep(
                    new AudioCueSet(new[] { stepClip }, 0.7f, 0.7f, 0.9f, 0.9f)), Is.True);
                Assert.That(emitter.PlayVoice(
                    new AudioCueSet(new[] { voiceClip }, 1f, 1f, 1.1f, 1.1f)), Is.True);
                Assert.That(emitter.LastFootstepClip, Is.SameAs(stepClip));
                Assert.That(emitter.LastVoiceClip, Is.SameAs(voiceClip));
                Assert.That(stepSource.pitch, Is.EqualTo(0.9f).Within(0.0001f));
                Assert.That(voiceSource.pitch, Is.EqualTo(1.1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(stepClip);
                UnityEngine.Object.DestroyImmediate(voiceClip);
            }
        }

        [Test]
        public void MissingSourceOrEmptyCue_ReturnsFalseWithoutException()
        {
            var root = new GameObject("Emitter");
            AudioClip voiceClip = AudioClip.Create("Voice", 8, 1, 8000, false);
            try
            {
                AudioSource stepSource = root.AddComponent<AudioSource>();
                CharacterAudioEmitter emitter = root.AddComponent<CharacterAudioEmitter>();
                emitter.Bind(stepSource, null);

                Assert.That(emitter.PlayFootstep(new AudioCueSet()), Is.False);
                Assert.That(emitter.PlayVoice(
                    new AudioCueSet(new[] { voiceClip })), Is.False);
                Assert.That(emitter.LastFootstepClip, Is.Null);
                Assert.That(emitter.LastVoiceClip, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(voiceClip);
            }
        }
    }
}
