using System;
using System.Reflection;
using Game.Gameplay.Audio;
using Game.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;

namespace Game.Gameplay.Tests
{
    public sealed class PlayerAudioControllerTests
    {
        [Test]
        public void GroundedWalkAndSprint_SelectDifferentStepPools()
        {
            using var fixture = new Fixture();
            fixture.SetState(2f, true, false, false);
            fixture.Tick(0.1f);
            Assert.That(fixture.Emitter.LastFootstepClip, Is.SameAs(fixture.Walk));

            fixture.Controller.enabled = false;
            fixture.Controller.enabled = true;
            fixture.SetState(2f, true, true, false);
            fixture.Tick(0.1f);
            Assert.That(fixture.Emitter.LastFootstepClip, Is.SameAs(fixture.Sprint));
        }

        [Test]
        public void AirborneMovement_DoesNotPlayFootstep()
        {
            using var fixture = new Fixture();
            fixture.SetState(5f, false, true, false);
            fixture.Tick(1f);
            Assert.That(fixture.Emitter.LastFootstepClip, Is.Null);
        }

        [Test]
        public void ExhaustedBreathing_TakesPriorityOverSprintBreathing()
        {
            using var fixture = new Fixture();
            fixture.SetState(2f, true, true, true);
            fixture.Tick(0.01f);
            Assert.That(fixture.Emitter.LastVoiceClip, Is.SameAs(fixture.ExhaustedBreath));
        }

        private sealed class Fixture : IDisposable
        {
            private const BindingFlags PrivateInstance =
                BindingFlags.Instance | BindingFlags.NonPublic;

            public Fixture()
            {
                Root = new GameObject("PlayerAudioFixture");
                Root.SetActive(false);
                Player = Root.AddComponent<PlayerController>();
                AudioSource footsteps = Root.AddComponent<AudioSource>();
                AudioSource voice = Root.AddComponent<AudioSource>();
                Emitter = Root.AddComponent<CharacterAudioEmitter>();
                Emitter.Bind(footsteps, voice);
                Controller = Root.AddComponent<PlayerAudioController>();
                Controller.Bind(Player, Emitter);

                Walk = AudioClip.Create("Walk", 80, 1, 8000, false);
                Sprint = AudioClip.Create("Sprint", 80, 1, 8000, false);
                SprintBreath = AudioClip.Create("SprintBreath", 80, 1, 8000, false);
                ExhaustedBreath = AudioClip.Create("ExhaustedBreath", 80, 1, 8000, false);

                SetField("walkFootsteps", new AudioCueSet(new[] { Walk }));
                SetField("sprintFootsteps", new AudioCueSet(new[] { Sprint }));
                SetField("sprintBreathing", new AudioCueSet(new[] { SprintBreath }));
                SetField("exhaustedBreathing", new AudioCueSet(new[] { ExhaustedBreath }));
                SetField("walkDistancePerStep", 0.1f);
                SetField("sprintDistancePerStep", 0.1f);
                SetField("sprintBreathInterval", 0.1f);
                SetField("exhaustedBreathInterval", 0.1f);
                Root.SetActive(true);
            }

            public GameObject Root { get; }
            public PlayerController Player { get; }
            public PlayerAudioController Controller { get; }
            public CharacterAudioEmitter Emitter { get; }
            public AudioClip Walk { get; }
            public AudioClip Sprint { get; }
            public AudioClip SprintBreath { get; }
            public AudioClip ExhaustedBreath { get; }

            public void SetState(float speed, bool grounded, bool sprinting, bool exhausted)
            {
                SetProperty(nameof(PlayerController.CurrentSpeed), speed);
                SetProperty(nameof(PlayerController.IsGrounded), grounded);
                SetProperty(nameof(PlayerController.IsSprinting), sprinting);
                typeof(PlayerController).GetField("isExhausted", PrivateInstance)
                    ?.SetValue(Player, exhausted);
            }

            public void Tick(float deltaTime) =>
                typeof(PlayerAudioController).GetMethod("TickAudio", PrivateInstance)
                    ?.Invoke(Controller, new object[] { deltaTime });

            private void SetField(string name, object value) =>
                typeof(PlayerAudioController).GetField(name, PrivateInstance)
                    ?.SetValue(Controller, value);

            private void SetProperty(string name, object value) =>
                typeof(PlayerController).GetProperty(name)
                    ?.GetSetMethod(true)
                    ?.Invoke(Player, new[] { value });

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
                UnityEngine.Object.DestroyImmediate(Walk);
                UnityEngine.Object.DestroyImmediate(Sprint);
                UnityEngine.Object.DestroyImmediate(SprintBreath);
                UnityEngine.Object.DestroyImmediate(ExhaustedBreath);
            }
        }
    }
}
