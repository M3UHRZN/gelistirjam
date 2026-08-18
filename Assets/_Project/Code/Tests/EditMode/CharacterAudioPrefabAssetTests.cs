using Game.Gameplay.Audio;
using Game.Gameplay.Monster;
using Game.Gameplay.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Gameplay.Tests
{
    public sealed class CharacterAudioPrefabAssetTests
    {
        private const string PlayerPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string MonsterPath = "Assets/_Project/Prefabs/Monster.prefab";

        [Test]
        public void PlayerPrefab_HasBoundDriverAndTwoDedicated2dSources()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            Assert.That(prefab, Is.Not.Null);
            CharacterAudioEmitter emitter = prefab.GetComponent<CharacterAudioEmitter>();
            PlayerAudioController controller = prefab.GetComponent<PlayerAudioController>();
            Assert.That(emitter, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);
            Assert.That(emitter.IsBound, Is.True);
            Assert.That(controller.IsBound, Is.True);
            Assert.That(emitter.FootstepSource, Is.Not.SameAs(emitter.VoiceSource));
            Assert.That(emitter.FootstepSource.playOnAwake, Is.False);
            Assert.That(emitter.VoiceSource.playOnAwake, Is.False);
            Assert.That(emitter.FootstepSource.spatialBlend, Is.Zero);
            Assert.That(emitter.VoiceSource.spatialBlend, Is.Zero);
        }

        [Test]
        public void MonsterPrefab_HasBoundDriverRigReferenceAndTwoDedicated3dSources()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.activeSelf, Is.False);
            CharacterAudioEmitter emitter = prefab.GetComponent<CharacterAudioEmitter>();
            MonsterAudioController controller = prefab.GetComponent<MonsterAudioController>();
            MonsterRuntimeRig rig = prefab.GetComponent<MonsterRuntimeRig>();
            Assert.That(emitter, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);
            Assert.That(rig, Is.Not.Null);
            Assert.That(emitter.IsBound, Is.True);
            Assert.That(controller.IsBound, Is.True);
            Assert.That(emitter.FootstepSource, Is.Not.SameAs(emitter.VoiceSource));
            Assert.That(emitter.FootstepSource.playOnAwake, Is.False);
            Assert.That(emitter.VoiceSource.playOnAwake, Is.False);
            Assert.That(emitter.FootstepSource.spatialBlend, Is.EqualTo(1f));
            Assert.That(emitter.VoiceSource.spatialBlend, Is.EqualTo(1f));
            Assert.That(emitter.FootstepSource.maxDistance,
                Is.GreaterThan(emitter.FootstepSource.minDistance));
            Assert.That(emitter.VoiceSource.maxDistance,
                Is.GreaterThan(emitter.VoiceSource.minDistance));

            var serializedRig = new SerializedObject(rig);
            Assert.That(serializedRig.FindProperty("audioController").objectReferenceValue,
                Is.SameAs(controller));
        }
    }
}
