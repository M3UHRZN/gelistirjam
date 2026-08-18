using System;
using System.Linq;
using System.Reflection;
using Game.Gameplay.Audio;
using Game.Gameplay.Monster;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Gameplay.Tests
{
    public sealed class MonsterAudioControllerTests
    {
        [Test]
        public void BrainEvents_PlayYellThenAttackThroughVoiceChannel()
        {
            using var fixture = new Fixture();
            fixture.Raise("OnConfirmedGazeStarted");
            Assert.That(fixture.Emitter.LastVoiceClip, Is.SameAs(fixture.Yell));

            fixture.Brain.RaiseKill();
            Assert.That(fixture.Emitter.LastVoiceClip, Is.SameAs(fixture.Attack));
        }

        [Test]
        public void MovingWithoutAnAiState_CanPlayGeneralFootstep()
        {
            using var fixture = new Fixture();
            fixture.TickFootsteps(2f, true, 0.1f);
            Assert.That(fixture.Brain.CurrentStateName, Is.EqualTo("-"));
            Assert.That(fixture.Emitter.LastFootstepClip, Is.SameAs(fixture.Step));
        }

        [Test]
        public void DisableAndReenable_LeavesExactlyOneKillSubscription()
        {
            using var fixture = new Fixture();
            fixture.Controller.enabled = false;
            fixture.Controller.enabled = true;
            fixture.Controller.enabled = false;
            fixture.Controller.enabled = true;

            Assert.That(fixture.SubscriptionCount("OnKill"), Is.EqualTo(1));
        }

        private sealed class Fixture : IDisposable
        {
            private const BindingFlags PrivateInstance =
                BindingFlags.Instance | BindingFlags.NonPublic;
            private readonly GameObject brainRoot;
            private readonly GameObject audioRoot;

            public Fixture()
            {
                brainRoot = new GameObject("InactiveMonsterBrain");
                brainRoot.SetActive(false);
                Agent = brainRoot.AddComponent<NavMeshAgent>();
                Brain = brainRoot.AddComponent<MonsterBrain>();

                audioRoot = new GameObject("MonsterAudio");
                audioRoot.SetActive(false);
                AudioSource footsteps = audioRoot.AddComponent<AudioSource>();
                AudioSource voice = audioRoot.AddComponent<AudioSource>();
                Emitter = audioRoot.AddComponent<CharacterAudioEmitter>();
                Emitter.Bind(footsteps, voice);
                Controller = audioRoot.AddComponent<MonsterAudioController>();
                Controller.Bind(Brain, Agent);

                Step = AudioClip.Create("MonsterStep", 80, 1, 8000, false);
                Yell = AudioClip.Create("MonsterYell", 80, 1, 8000, false);
                Attack = AudioClip.Create("MonsterAttack", 80, 1, 8000, false);
                SetField("footsteps", new AudioCueSet(new[] { Step }));
                SetField("yell", new AudioCueSet(new[] { Yell }));
                SetField("attack", new AudioCueSet(new[] { Attack }));
                SetField("distancePerStep", 0.1f);
                audioRoot.SetActive(true);
            }

            public NavMeshAgent Agent { get; }
            public MonsterBrain Brain { get; }
            public MonsterAudioController Controller { get; }
            public CharacterAudioEmitter Emitter { get; }
            public AudioClip Step { get; }
            public AudioClip Yell { get; }
            public AudioClip Attack { get; }

            public void Raise(string eventName)
            {
                var handlers = (Action)typeof(MonsterBrain)
                    .GetField(eventName, PrivateInstance)
                    ?.GetValue(Brain);
                handlers?.Invoke();
            }

            public int SubscriptionCount(string eventName)
            {
                var handlers = (Delegate)typeof(MonsterBrain)
                    .GetField(eventName, PrivateInstance)
                    ?.GetValue(Brain);
                return handlers?.GetInvocationList()
                    .Count(handler => ReferenceEquals(handler.Target, Controller)) ?? 0;
            }

            public void TickFootsteps(float speed, bool validMovement, float deltaTime) =>
                typeof(MonsterAudioController)
                    .GetMethod("TickFootsteps", PrivateInstance)
                    ?.Invoke(Controller, new object[] { speed, validMovement, deltaTime });

            private void SetField(string name, object value) =>
                typeof(MonsterAudioController).GetField(name, PrivateInstance)
                    ?.SetValue(Controller, value);

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(audioRoot);
                UnityEngine.Object.DestroyImmediate(brainRoot);
                UnityEngine.Object.DestroyImmediate(Step);
                UnityEngine.Object.DestroyImmediate(Yell);
                UnityEngine.Object.DestroyImmediate(Attack);
            }
        }
    }
}
