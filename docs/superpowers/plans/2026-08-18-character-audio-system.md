# Character Audio System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build Inspector-authored player and monster audio components for general footsteps, player breathing, monster gaze-yell, and monster attack cues.

**Architecture:** A prefab-local `CharacterAudioEmitter` owns playback while thin player and monster controllers decide when cues fire. Shared `AudioCueSet` and `DistanceStepCadence` types keep clip selection and distance-based step timing reusable; `MonsterAudioController` observes existing `MonsterBrain` events and is bound by `MonsterRuntimeRig` before prefab activation.

**Tech Stack:** Unity 6000.3.13f1, C#/.NET, Unity `AudioSource`, Unity Test Framework with NUnit, Unity MCP for compilation/tests and prefab authoring.

**Spec:** `docs/superpowers/specs/2026-08-18-character-audio-system-design.md`

## Global Constraints

- Read the spec before starting and keep this plan scoped to player and monster character audio.
- Do not add packages, a singleton, a service locator, an event bus, surface detection, adaptive music, or runtime-created audio objects.
- Audio clips, cue variation, mixer output, volume, and spatial settings remain Inspector-authored.
- Empty or all-null cue pools are valid silent configurations.
- Monster footsteps run during every actual movement state, not only final chase.
- Do not put `AudioSource.Play*` calls in `PlayerController`, `MonsterBrain`, `MonsterNavigator`, or AI states.
- Do not change flashlight, fuse, menu, death-sequence, scene, map-generation, or gameplay-authoritative behavior.
- Preserve the existing user changes in `Assets/Scenes/MainGame.unity` and `Assets/Gloomveil/MapGeneration/Data/DefaultMapGenerationConfig.asset`; never stage either file.
- Author dedicated sources and serialized references in `Player.prefab` and `Monster.prefab`; do not rely on runtime fallback creation.
- Before every Unity MCP action, select the current `gelistirjam` Editor instance and wait for `mcpforunity://editor/state` to report `data.advice.ready_for_tools=true`.
- After script edits, request an AssetDatabase refresh/compile and read the Console before starting tests.
- `run_tests` is asynchronous: retain its `job_id`, poll `get_test_job` to a terminal result, and report the actual passed/failed counts.
- Stage only the files named by the current task, including Unity-generated `.meta` files for newly added scripts.

---

## File Structure

### New runtime files

- `Assets/_Project/Code/Gameplay/Audio/AudioCueSet.cs` — Inspector-serializable clip pool and variation selection.
- `Assets/_Project/Code/Gameplay/Audio/DistanceStepCadence.cs` — engine-small distance accumulator shared by both character drivers.
- `Assets/_Project/Code/Gameplay/Audio/CharacterAudioEmitter.cs` — the only new class that calls `AudioSource.PlayOneShot`.
- `Assets/_Project/Code/Gameplay/Player/PlayerAudioController.cs` — reads player presentation state and schedules steps/breaths.
- `Assets/_Project/Code/Gameplay/Monster/MonsterAudioController.cs` — reads monster movement and observes gaze/kill events.

### Modified runtime files/assets

- `Assets/_Project/Code/Gameplay/Monster/MonsterRuntimeRig.cs` — validates and binds monster audio before activation.
- `Assets/_Project/Prefabs/Player.prefab` — authored 2D sources, emitter, and player driver.
- `Assets/_Project/Prefabs/Monster.prefab` — authored 3D sources, emitter, monster driver, and rig reference.

### New tests

- `Assets/_Project/Code/Tests/EditMode/CharacterAudioCoreTests.cs`
- `Assets/_Project/Code/Tests/EditMode/CharacterAudioPrefabAssetTests.cs`
- `Assets/_Project/Code/Tests/PlayMode/CharacterAudioEmitterTests.cs`
- `Assets/_Project/Code/Tests/PlayMode/PlayerAudioControllerTests.cs`
- `Assets/_Project/Code/Tests/PlayMode/MonsterAudioControllerTests.cs`

### Existing tests updated for the stricter rig contract

- `Assets/_Project/Code/Tests/PlayMode/MonsterRuntimeRigTests.cs`
- `Assets/_Project/Code/Tests/PlayMode/MonsterConfirmedGazeTests.cs`
- `Assets/_Project/Code/Tests/PlayMode/MapMonsterGateTests.cs`
- `Assets/_Project/Code/Tests/PlayMode/MainSceneWiringMonsterTests.cs`

---

### Task 1: Cue selection and distance cadence

**Files:**

- Create: `Assets/_Project/Code/Gameplay/Audio/AudioCueSet.cs`
- Create: `Assets/_Project/Code/Gameplay/Audio/DistanceStepCadence.cs`
- Test: `Assets/_Project/Code/Tests/EditMode/CharacterAudioCoreTests.cs`

**Interfaces:**

- Consumes: `UnityEngine.AudioClip`, `UnityEngine.Random`, `Mathf`.
- Produces: `AudioCueSet.TrySelect(out AudioClip clip, out float volumeScale, out float pitch)`, `AudioCueSet.NormalizeRanges()`, `AudioCueSet.ResetHistory()`, `DistanceStepCadence.Advance(float speed, float deltaTime, float distancePerStep, bool active)`, and `DistanceStepCadence.Reset()`.

- [ ] **Step 1: Write the failing core tests**

Create `CharacterAudioCoreTests.cs` with the complete contract below:

```csharp
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
```

- [ ] **Step 2: Run the core test fixture and verify the expected compile failure**

Use Unity MCP `run_tests` with `mode="EditMode"` and `test_names="Game.Gameplay.Tests.CharacterAudioCoreTests"`.

Expected: FAIL before execution because `Game.Gameplay.Audio`, `AudioCueSet`, and `DistanceStepCadence` do not exist.

- [ ] **Step 3: Implement `AudioCueSet`**

Create `AudioCueSet.cs`:

```csharp
using System;
using UnityEngine;

namespace Game.Gameplay.Audio
{
    [Serializable]
    public sealed class AudioCueSet
    {
        [SerializeField] private AudioClip[] clips = Array.Empty<AudioClip>();
        [SerializeField, Min(0f)] private float minimumVolume = 1f;
        [SerializeField, Min(0f)] private float maximumVolume = 1f;
        [SerializeField, Min(0.01f)] private float minimumPitch = 1f;
        [SerializeField, Min(0.01f)] private float maximumPitch = 1f;

        [NonSerialized] private int lastClipIndex = -1;

        public AudioCueSet() { }

        public AudioCueSet(
            AudioClip[] sourceClips,
            float minVolume = 1f,
            float maxVolume = 1f,
            float minPitch = 1f,
            float maxPitch = 1f)
        {
            clips = sourceClips ?? Array.Empty<AudioClip>();
            minimumVolume = minVolume;
            maximumVolume = maxVolume;
            minimumPitch = minPitch;
            maximumPitch = maxPitch;
        }

        public bool TrySelect(
            out AudioClip clip,
            out float volumeScale,
            out float pitch)
        {
            clip = null;
            volumeScale = 1f;
            pitch = 1f;

            int validCount = CountValidClips();
            if (validCount == 0) return false;
            NormalizeRanges();

            int selectedOrdinal;
            if (validCount > 1 && IsLastClipValid())
            {
                int lastOrdinal = GetValidOrdinal(lastClipIndex);
                selectedOrdinal = UnityEngine.Random.Range(0, validCount - 1);
                if (selectedOrdinal >= lastOrdinal) selectedOrdinal++;
            }
            else
            {
                selectedOrdinal = UnityEngine.Random.Range(0, validCount);
            }

            lastClipIndex = GetIndexForValidOrdinal(selectedOrdinal);
            clip = clips[lastClipIndex];

            volumeScale = UnityEngine.Random.Range(minimumVolume, maximumVolume);
            pitch = UnityEngine.Random.Range(minimumPitch, maximumPitch);
            return true;
        }

        public void NormalizeRanges()
        {
            float volumeLow = Mathf.Max(0f, Mathf.Min(minimumVolume, maximumVolume));
            float volumeHigh = Mathf.Max(volumeLow, Mathf.Max(minimumVolume, maximumVolume));
            minimumVolume = volumeLow;
            maximumVolume = volumeHigh;

            float pitchLow = Mathf.Max(0.01f, Mathf.Min(minimumPitch, maximumPitch));
            float pitchHigh = Mathf.Max(pitchLow, Mathf.Max(minimumPitch, maximumPitch));
            minimumPitch = pitchLow;
            maximumPitch = pitchHigh;
        }

        public void ResetHistory() => lastClipIndex = -1;

        private int CountValidClips()
        {
            if (clips == null) return 0;
            int count = 0;
            for (int index = 0; index < clips.Length; index++)
            {
                if (clips[index] != null) count++;
            }
            return count;
        }

        private bool IsLastClipValid() => clips != null
            && lastClipIndex >= 0
            && lastClipIndex < clips.Length
            && clips[lastClipIndex] != null;

        private int GetValidOrdinal(int clipIndex)
        {
            int ordinal = 0;
            for (int index = 0; index < clipIndex; index++)
            {
                if (clips[index] != null) ordinal++;
            }
            return ordinal;
        }

        private int GetIndexForValidOrdinal(int targetOrdinal)
        {
            int ordinal = 0;
            for (int index = 0; index < clips.Length; index++)
            {
                if (clips[index] == null) continue;
                if (ordinal == targetOrdinal) return index;
                ordinal++;
            }
            throw new InvalidOperationException("Valid audio clip ordinal was not found.");
        }
    }
}
```

- [ ] **Step 4: Implement `DistanceStepCadence`**

Create `DistanceStepCadence.cs`:

```csharp
using UnityEngine;

namespace Game.Gameplay.Audio
{
    public sealed class DistanceStepCadence
    {
        public float AccumulatedDistance { get; private set; }

        public bool Advance(
            float speed,
            float deltaTime,
            float distancePerStep,
            bool active)
        {
            if (!active || speed <= 0f)
            {
                Reset();
                return false;
            }

            if (deltaTime <= 0f) return false;

            float threshold = Mathf.Max(0.01f, distancePerStep);
            AccumulatedDistance += speed * deltaTime;
            if (AccumulatedDistance < threshold) return false;

            AccumulatedDistance %= threshold;
            return true;
        }

        public void Reset() => AccumulatedDistance = 0f;
    }
}
```

- [ ] **Step 5: Refresh Unity and run the core tests**

Request `refresh_unity(scope="scripts", compile="request", wait_for_ready=true)`, read Console errors, then rerun `Game.Gameplay.Tests.CharacterAudioCoreTests` in EditMode.

Expected: 5 tests PASS and no new compiler errors.

- [ ] **Step 6: Commit Task 1**

```powershell
git add -- Assets/_Project/Code/Gameplay/Audio.meta Assets/_Project/Code/Gameplay/Audio/AudioCueSet.cs Assets/_Project/Code/Gameplay/Audio/AudioCueSet.cs.meta Assets/_Project/Code/Gameplay/Audio/DistanceStepCadence.cs Assets/_Project/Code/Gameplay/Audio/DistanceStepCadence.cs.meta Assets/_Project/Code/Tests/EditMode/CharacterAudioCoreTests.cs Assets/_Project/Code/Tests/EditMode/CharacterAudioCoreTests.cs.meta
git commit -m "feat: add character audio cue primitives"
```

---

### Task 2: Shared character audio emitter

**Files:**

- Create: `Assets/_Project/Code/Gameplay/Audio/CharacterAudioEmitter.cs`
- Test: `Assets/_Project/Code/Tests/PlayMode/CharacterAudioEmitterTests.cs`

**Interfaces:**

- Consumes: `AudioCueSet.TrySelect(...)` from Task 1 and two authored `AudioSource` references.
- Produces: `CharacterAudioEmitter.Bind(AudioSource footstep, AudioSource voice)`, `IsBound`, `PlayFootstep(AudioCueSet)`, `PlayVoice(AudioCueSet)`, `LastFootstepClip`, and `LastVoiceClip`.

- [ ] **Step 1: Write the failing emitter tests**

Create `CharacterAudioEmitterTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the emitter fixture and verify the expected compile failure**

Run PlayMode test `Game.Gameplay.Tests.CharacterAudioEmitterTests` with `init_timeout=120000`.

Expected: FAIL before execution because `CharacterAudioEmitter` does not exist.

- [ ] **Step 3: Implement the emitter**

Create `CharacterAudioEmitter.cs`:

```csharp
using UnityEngine;

namespace Game.Gameplay.Audio
{
    [DisallowMultipleComponent]
    public sealed class CharacterAudioEmitter : MonoBehaviour
    {
        [SerializeField] private AudioSource footstepSource;
        [SerializeField] private AudioSource voiceSource;

        public bool IsBound => footstepSource != null && voiceSource != null;
        public AudioSource FootstepSource => footstepSource;
        public AudioSource VoiceSource => voiceSource;
        public AudioClip LastFootstepClip { get; private set; }
        public AudioClip LastVoiceClip { get; private set; }

        public void Bind(AudioSource footstep, AudioSource voice)
        {
            footstepSource = footstep;
            voiceSource = voice;
        }

        public bool PlayFootstep(AudioCueSet cue) =>
            Play(footstepSource, cue, true);

        public bool PlayVoice(AudioCueSet cue) =>
            Play(voiceSource, cue, false);

        private bool Play(AudioSource source, AudioCueSet cue, bool footstep)
        {
            if (source == null || cue == null
                || !cue.TrySelect(out AudioClip clip, out float volume, out float pitch))
            {
                return false;
            }

            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
            if (footstep) LastFootstepClip = clip;
            else LastVoiceClip = clip;
            return true;
        }
    }
}
```

- [ ] **Step 4: Refresh, inspect Console, and run the emitter tests**

Compile through `refresh_unity`, confirm no new Console errors, then run `CharacterAudioEmitterTests` in PlayMode.

Expected: 2 tests PASS.

- [ ] **Step 5: Commit Task 2**

```powershell
git add -- Assets/_Project/Code/Gameplay/Audio/CharacterAudioEmitter.cs Assets/_Project/Code/Gameplay/Audio/CharacterAudioEmitter.cs.meta Assets/_Project/Code/Tests/PlayMode/CharacterAudioEmitterTests.cs Assets/_Project/Code/Tests/PlayMode/CharacterAudioEmitterTests.cs.meta
git commit -m "feat: add character audio emitter"
```

---

### Task 3: Player footsteps and breathing

**Files:**

- Create: `Assets/_Project/Code/Gameplay/Player/PlayerAudioController.cs`
- Test: `Assets/_Project/Code/Tests/PlayMode/PlayerAudioControllerTests.cs`

**Interfaces:**

- Consumes: `PlayerController.CurrentSpeed`, `IsGrounded`, `IsSprinting`, `IsExhausted`, `CharacterAudioEmitter`, `AudioCueSet`, and `DistanceStepCadence`.
- Produces: `PlayerAudioController.Bind(PlayerController player, CharacterAudioEmitter emitter)` and `PlayerAudioController.IsBound`.

- [ ] **Step 1: Write failing player-audio tests**

Create `PlayerAudioControllerTests.cs`. The fixture creates the prefab-local topology in memory, uses reflection only to drive `PlayerController`'s existing private-set presentation properties, and calls the driver's private deterministic tick directly:

```csharp
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
```

- [ ] **Step 2: Run the player fixture and verify the expected compile failure**

Run PlayMode test `Game.Gameplay.Tests.PlayerAudioControllerTests`.

Expected: FAIL before execution because `PlayerAudioController` does not exist.

- [ ] **Step 3: Implement `PlayerAudioController`**

Create `PlayerAudioController.cs`:

```csharp
using Game.Gameplay.Audio;
using UnityEngine;

namespace Game.Gameplay.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerAudioController : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private CharacterAudioEmitter emitter;

        [Header("Footsteps")]
        [SerializeField] private AudioCueSet walkFootsteps = new();
        [SerializeField] private AudioCueSet sprintFootsteps = new();
        [SerializeField, Min(0.01f)] private float walkDistancePerStep = 1.7f;
        [SerializeField, Min(0.01f)] private float sprintDistancePerStep = 2f;
        [SerializeField, Min(0f)] private float minimumMovementSpeed = 0.05f;

        [Header("Breathing")]
        [SerializeField] private AudioCueSet sprintBreathing = new();
        [SerializeField] private AudioCueSet exhaustedBreathing = new();
        [SerializeField, Min(0.05f)] private float sprintBreathInterval = 2.2f;
        [SerializeField, Min(0.05f)] private float exhaustedBreathInterval = 1.4f;

        private readonly DistanceStepCadence footstepCadence = new();
        private float breathTimer;

        public bool IsBound => player != null && emitter != null && emitter.IsBound;

        public void Bind(PlayerController targetPlayer, CharacterAudioEmitter targetEmitter)
        {
            player = targetPlayer;
            emitter = targetEmitter;
        }

        private void Awake() => ResolveComponents();

        private void OnValidate()
        {
            walkDistancePerStep = Mathf.Max(0.01f, walkDistancePerStep);
            sprintDistancePerStep = Mathf.Max(0.01f, sprintDistancePerStep);
            minimumMovementSpeed = Mathf.Max(0f, minimumMovementSpeed);
            sprintBreathInterval = Mathf.Max(0.05f, sprintBreathInterval);
            exhaustedBreathInterval = Mathf.Max(0.05f, exhaustedBreathInterval);
            walkFootsteps?.NormalizeRanges();
            sprintFootsteps?.NormalizeRanges();
            sprintBreathing?.NormalizeRanges();
            exhaustedBreathing?.NormalizeRanges();
        }

        private void OnEnable()
        {
            ResolveComponents();
            if (!IsBound)
            {
                Debug.LogError(
                    "PlayerAudioController requires PlayerController and a bound CharacterAudioEmitter.",
                    this);
                enabled = false;
                return;
            }
            ResetAudioState();
        }

        private void OnDisable() => ResetAudioState();
        private void Update() => TickAudio(Time.deltaTime);

        private void ResolveComponents()
        {
            if (player == null) player = GetComponent<PlayerController>();
            if (emitter == null) emitter = GetComponent<CharacterAudioEmitter>();
        }

        private void TickAudio(float deltaTime)
        {
            if (!IsBound || !player.enabled)
            {
                ResetAudioState();
                return;
            }

            bool canStep = player.IsGrounded && player.CurrentSpeed > minimumMovementSpeed;
            float stepDistance = player.IsSprinting
                ? sprintDistancePerStep
                : walkDistancePerStep;
            if (footstepCadence.Advance(
                    player.CurrentSpeed,
                    deltaTime,
                    stepDistance,
                    canStep))
            {
                emitter.PlayFootstep(player.IsSprinting ? sprintFootsteps : walkFootsteps);
            }

            TickBreathing(deltaTime);
        }

        private void TickBreathing(float deltaTime)
        {
            AudioCueSet cue;
            float interval;
            if (player.IsExhausted)
            {
                cue = exhaustedBreathing;
                interval = exhaustedBreathInterval;
            }
            else if (player.IsSprinting)
            {
                cue = sprintBreathing;
                interval = sprintBreathInterval;
            }
            else
            {
                breathTimer = 0f;
                return;
            }

            breathTimer -= Mathf.Max(0f, deltaTime);
            if (breathTimer > 0f) return;

            emitter.PlayVoice(cue);
            breathTimer = Mathf.Max(0.05f, interval);
        }

        private void ResetAudioState()
        {
            footstepCadence.Reset();
            breathTimer = 0f;
            walkFootsteps?.ResetHistory();
            sprintFootsteps?.ResetHistory();
            sprintBreathing?.ResetHistory();
            exhaustedBreathing?.ResetHistory();
        }
    }
}
```

- [ ] **Step 4: Refresh, inspect Console, and run player-audio tests**

Compile, read Console errors, and run `PlayerAudioControllerTests` in PlayMode.

Expected: 3 tests PASS. If Unity reports an unrelated cursor-state warning from `PlayerController`, isolate it without changing movement behavior.

- [ ] **Step 5: Commit Task 3**

```powershell
git add -- Assets/_Project/Code/Gameplay/Player/PlayerAudioController.cs Assets/_Project/Code/Gameplay/Player/PlayerAudioController.cs.meta Assets/_Project/Code/Tests/PlayMode/PlayerAudioControllerTests.cs Assets/_Project/Code/Tests/PlayMode/PlayerAudioControllerTests.cs.meta
git commit -m "feat: add player character audio"
```

---

### Task 4: Monster audio events, footsteps, and runtime rig binding

**Files:**

- Create: `Assets/_Project/Code/Gameplay/Monster/MonsterAudioController.cs`
- Modify: `Assets/_Project/Code/Gameplay/Monster/MonsterRuntimeRig.cs:14-77`
- Test: `Assets/_Project/Code/Tests/PlayMode/MonsterAudioControllerTests.cs`
- Modify test fixture: `Assets/_Project/Code/Tests/PlayMode/MonsterRuntimeRigTests.cs:20-124`
- Modify test fixture: `Assets/_Project/Code/Tests/PlayMode/MonsterConfirmedGazeTests.cs:75-151`
- Modify test fixture: `Assets/_Project/Code/Tests/PlayMode/MapMonsterGateTests.cs:459-485`
- Modify test fixture: `Assets/_Project/Code/Tests/PlayMode/MainSceneWiringMonsterTests.cs:197-248`

**Interfaces:**

- Consumes: `MonsterBrain.OnConfirmedGazeStarted`, `MonsterBrain.OnKill`, `NavMeshAgent.velocity`, `CharacterAudioEmitter`, `AudioCueSet`, and `DistanceStepCadence`.
- Produces: `MonsterAudioController.Bind(MonsterBrain brain, NavMeshAgent agent)` and `MonsterAudioController.IsBound`; `MonsterRuntimeRig.Initialize(...)` now requires and binds the controller.

- [ ] **Step 1: Write failing monster-audio tests**

Create `MonsterAudioControllerTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the monster fixture and verify the expected compile failure**

Run PlayMode test `Game.Gameplay.Tests.MonsterAudioControllerTests`.

Expected: FAIL before execution because `MonsterAudioController` does not exist.

- [ ] **Step 3: Implement `MonsterAudioController`**

Create `MonsterAudioController.cs`:

```csharp
using Game.Gameplay.Audio;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Gameplay.Monster
{
    [DisallowMultipleComponent]
    public sealed class MonsterAudioController : MonoBehaviour
    {
        [SerializeField] private MonsterBrain brain;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private CharacterAudioEmitter emitter;

        [Header("Footsteps")]
        [SerializeField] private AudioCueSet footsteps = new();
        [SerializeField, Min(0.01f)] private float distancePerStep = 1.8f;
        [SerializeField, Min(0f)] private float minimumMovementSpeed = 0.05f;

        [Header("Voice")]
        [SerializeField] private AudioCueSet yell = new();
        [SerializeField] private AudioCueSet attack = new();

        private readonly DistanceStepCadence footstepCadence = new();
        private bool subscribed;

        public bool IsBound => brain != null && agent != null
            && emitter != null && emitter.IsBound;

        public void Bind(MonsterBrain monsterBrain, NavMeshAgent navMeshAgent)
        {
            Unsubscribe();
            brain = monsterBrain;
            agent = navMeshAgent;
            ResolveComponents();
            Subscribe();
        }

        private void Awake() => ResolveComponents();

        private void OnValidate()
        {
            distancePerStep = Mathf.Max(0.01f, distancePerStep);
            minimumMovementSpeed = Mathf.Max(0f, minimumMovementSpeed);
            footsteps?.NormalizeRanges();
            yell?.NormalizeRanges();
            attack?.NormalizeRanges();
        }

        private void OnEnable()
        {
            ResolveComponents();
            if (!IsBound)
            {
                Debug.LogError(
                    "MonsterAudioController requires MonsterBrain, NavMeshAgent, and a bound CharacterAudioEmitter.",
                    this);
                enabled = false;
                return;
            }
            Subscribe();
            ResetAudioState();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ResetAudioState();
        }

        private void Update()
        {
            bool validMovement = agent != null && agent.enabled && agent.isOnNavMesh;
            float speed = validMovement ? agent.velocity.magnitude : 0f;
            TickFootsteps(speed, validMovement, Time.deltaTime);
        }

        private void ResolveComponents()
        {
            if (brain == null) brain = GetComponent<MonsterBrain>();
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (emitter == null) emitter = GetComponent<CharacterAudioEmitter>();
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled || subscribed || brain == null) return;
            brain.OnConfirmedGazeStarted += HandleConfirmedGazeStarted;
            brain.OnKill += HandleKill;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || brain == null) return;
            brain.OnConfirmedGazeStarted -= HandleConfirmedGazeStarted;
            brain.OnKill -= HandleKill;
            subscribed = false;
        }

        private void TickFootsteps(float speed, bool validMovement, float deltaTime)
        {
            bool moving = validMovement && speed > minimumMovementSpeed;
            if (footstepCadence.Advance(speed, deltaTime, distancePerStep, moving))
            {
                emitter.PlayFootstep(footsteps);
            }
        }

        private void HandleConfirmedGazeStarted() => emitter.PlayVoice(yell);
        private void HandleKill() => emitter.PlayVoice(attack);

        private void ResetAudioState()
        {
            footstepCadence.Reset();
            footsteps?.ResetHistory();
            yell?.ResetHistory();
            attack?.ResetHistory();
        }
    }
}
```

- [ ] **Step 4: Tighten `MonsterRuntimeRig` with a failing missing-audio test**

In `MonsterRuntimeRigTests.cs`, change the fixture constructor to:

```csharp
public RigFixture(bool includeGazeReceiver, bool includeAudioController = true)
```

When `includeAudioController` is true, create the local audio topology before adding `MonsterRuntimeRig`:

```csharp
AudioSource footstepSource = Root.AddComponent<AudioSource>();
AudioSource voiceSource = Root.AddComponent<AudioSource>();
CharacterAudioEmitter emitter = Root.AddComponent<CharacterAudioEmitter>();
emitter.Bind(footstepSource, voiceSource);
Root.AddComponent<MonsterAudioController>();
```

Add the test:

```csharp
[Test]
public void Initialize_MissingAudioControllerFailsAndKeepsRootInactive()
{
    using var fixture = new RigFixture(
        includeGazeReceiver: true,
        includeAudioController: false);

    bool initialized = fixture.Rig.Initialize(
        fixture.Config,
        fixture.Player.transform,
        fixture.VelocitySource,
        fixture.GazeDetector,
        fixture.SafeZones,
        fixture.StalkPoints,
        fixture.VisionModel);

    Assert.That(initialized, Is.False);
    Assert.That(fixture.Rig.IsInitialized, Is.False);
    Assert.That(fixture.Root.activeSelf, Is.False);
}
```

Extend `Initialize_BindsCompleteInactiveRig` with:

```csharp
Assert.That(fixture.Root.GetComponent<MonsterAudioController>().IsBound, Is.True);
```

Run `MonsterRuntimeRigTests` and verify the new missing-audio test FAILS because the current rig does not require audio.

- [ ] **Step 5: Bind audio in `MonsterRuntimeRig`**

Add the serialized component beside `animationController`:

```csharp
[SerializeField] private MonsterAudioController audioController;
```

Include `audioController == null` in the required-reference condition. Replace the current animation bind/check with:

```csharp
var agent = navigator.GetComponent<UnityEngine.AI.NavMeshAgent>();
animationController.Bind(animator, brain, agent);
audioController.Bind(brain, agent);

if (!animationController.IsBound || !audioController.IsBound) return false;
```

Resolve the new component in `ResolveComponents()`:

```csharp
if (audioController == null) audioController = GetComponent<MonsterAudioController>();
```

- [ ] **Step 6: Update every existing complete-rig fixture**

Add `using Game.Gameplay.Audio;` to each test file below. Before each fixture adds `MonsterRuntimeRig`, add two sources, bind an emitter, and add the audio controller.

In `MonsterConfirmedGazeTests.GazeFixture`, immediately after `MonsterVisualDriver`:

```csharp
AudioSource footstepSource = Root.AddComponent<AudioSource>();
AudioSource voiceSource = Root.AddComponent<AudioSource>();
CharacterAudioEmitter emitter = Root.AddComponent<CharacterAudioEmitter>();
emitter.Bind(footstepSource, voiceSource);
Root.AddComponent<MonsterAudioController>();
```

In `MapMonsterGateTests.BuildMonsterPrefab`, immediately after `MonsterVisualDriver`:

```csharp
AudioSource footstepSource = root.AddComponent<AudioSource>();
AudioSource voiceSource = root.AddComponent<AudioSource>();
CharacterAudioEmitter emitter = root.AddComponent<CharacterAudioEmitter>();
emitter.Bind(footstepSource, voiceSource);
root.AddComponent<MonsterAudioController>();
```

In `MainSceneWiringMonsterTests.ActiveMonsterFixture`, immediately after `MonsterVisualDriver`:

```csharp
AudioSource footstepSource = root.AddComponent<AudioSource>();
AudioSource voiceSource = root.AddComponent<AudioSource>();
CharacterAudioEmitter emitter = root.AddComponent<CharacterAudioEmitter>();
emitter.Bind(footstepSource, voiceSource);
root.AddComponent<MonsterAudioController>();
```

In `MonsterRuntimeRigTests.RigFixture`, keep the conditional audio block from Step 4 before `MonsterRuntimeRig` is added.

- [ ] **Step 7: Refresh and run monster-focused regression tests**

Compile and read Console errors. Run this PlayMode test set:

```text
Game.Gameplay.Tests.MonsterAudioControllerTests
Game.Gameplay.Tests.MonsterRuntimeRigTests
Game.Gameplay.Tests.MonsterConfirmedGazeTests
Game.Gameplay.Tests.MapMonsterGateTests
Game.Gameplay.Tests.MainSceneWiringMonsterTests
```

Expected: all selected tests PASS; the exact count is recorded from the terminal test job. No `NavMeshAgent` warning may be attributed to audio without isolating its source.

- [ ] **Step 8: Commit Task 4**

Stage only the new monster-audio files, `MonsterRuntimeRig`, and the four named test fixtures, including new `.meta` files. Confirm `git diff --cached --name-only` excludes `MainGame.unity` and `DefaultMapGenerationConfig.asset`, then commit:

```powershell
git add -- Assets/_Project/Code/Gameplay/Monster/MonsterAudioController.cs Assets/_Project/Code/Gameplay/Monster/MonsterAudioController.cs.meta Assets/_Project/Code/Gameplay/Monster/MonsterRuntimeRig.cs Assets/_Project/Code/Tests/PlayMode/MonsterAudioControllerTests.cs Assets/_Project/Code/Tests/PlayMode/MonsterAudioControllerTests.cs.meta Assets/_Project/Code/Tests/PlayMode/MonsterRuntimeRigTests.cs Assets/_Project/Code/Tests/PlayMode/MonsterConfirmedGazeTests.cs Assets/_Project/Code/Tests/PlayMode/MapMonsterGateTests.cs Assets/_Project/Code/Tests/PlayMode/MainSceneWiringMonsterTests.cs
git diff --cached --name-only
git commit -m "feat: add monster character audio"
```

---

### Task 5: Author prefab sources and validate the complete system

**Files:**

- Modify: `Assets/_Project/Prefabs/Player.prefab`
- Modify: `Assets/_Project/Prefabs/Monster.prefab`
- Test: `Assets/_Project/Code/Tests/EditMode/CharacterAudioPrefabAssetTests.cs`

**Interfaces:**

- Consumes: all runtime components from Tasks 1-4 and the existing `PlayerController`, `MonsterBrain`, `NavMeshAgent`, and `MonsterRuntimeRig` prefab components.
- Produces: fully serialized player and monster prefab audio topology; clips remain empty for user assignment.

- [ ] **Step 1: Write the failing prefab contract tests**

Create `CharacterAudioPrefabAssetTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the prefab tests and verify they fail for missing components**

Run EditMode test `Game.Gameplay.Tests.CharacterAudioPrefabAssetTests`.

Expected: both tests FAIL because the production prefabs do not yet contain the new components/sources.

- [ ] **Step 3: Author `Player.prefab` through the live Unity Editor**

After Unity reports ready, use `execute_code(action="execute", compiler="roslyn")` with this method body:

```csharp
string path = "Assets/_Project/Prefabs/Player.prefab";
GameObject root = PrefabUtility.LoadPrefabContents(path);
try
{
    Transform audioRoot = root.transform.Find("CharacterAudio");
    if (audioRoot == null)
    {
        var audioObject = new GameObject("CharacterAudio");
        audioObject.transform.SetParent(root.transform, false);
        audioRoot = audioObject.transform;
    }

    Transform stepTransform = audioRoot.Find("Footsteps");
    if (stepTransform == null)
    {
        var stepObject = new GameObject("Footsteps");
        stepObject.transform.SetParent(audioRoot, false);
        stepTransform = stepObject.transform;
    }

    Transform voiceTransform = audioRoot.Find("Voice");
    if (voiceTransform == null)
    {
        var voiceObject = new GameObject("Voice");
        voiceObject.transform.SetParent(audioRoot, false);
        voiceTransform = voiceObject.transform;
    }

    AudioSource footsteps = stepTransform.GetComponent<AudioSource>();
    if (footsteps == null) footsteps = stepTransform.gameObject.AddComponent<AudioSource>();
    AudioSource voice = voiceTransform.GetComponent<AudioSource>();
    if (voice == null) voice = voiceTransform.gameObject.AddComponent<AudioSource>();
    foreach (AudioSource source in new[] { footsteps, voice })
    {
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
    }

    var emitter = root.GetComponent<Game.Gameplay.Audio.CharacterAudioEmitter>();
    if (emitter == null)
        emitter = root.AddComponent<Game.Gameplay.Audio.CharacterAudioEmitter>();
    emitter.Bind(footsteps, voice);

    var playerAudio = root.GetComponent<Game.Gameplay.Player.PlayerAudioController>();
    if (playerAudio == null)
        playerAudio = root.AddComponent<Game.Gameplay.Player.PlayerAudioController>();
    playerAudio.Bind(root.GetComponent<Game.Gameplay.Player.PlayerController>(), emitter);
    playerAudio.enabled = true;

    EditorUtility.SetDirty(emitter);
    EditorUtility.SetDirty(playerAudio);
    PrefabUtility.SaveAsPrefabAsset(root, path);
    return "Player character audio authored";
}
finally
{
    PrefabUtility.UnloadPrefabContents(root);
}
```

Do not assign any clip automatically. The empty cue pools are intentional Inspector slots for the user.

- [ ] **Step 4: Author `Monster.prefab` through the live Unity Editor**

Use `execute_code(action="execute", compiler="roslyn")` with this method body:

```csharp
string path = "Assets/_Project/Prefabs/Monster.prefab";
GameObject root = PrefabUtility.LoadPrefabContents(path);
try
{
    Transform audioRoot = root.transform.Find("CharacterAudio");
    if (audioRoot == null)
    {
        var audioObject = new GameObject("CharacterAudio");
        audioObject.transform.SetParent(root.transform, false);
        audioRoot = audioObject.transform;
    }

    Transform stepTransform = audioRoot.Find("Footsteps");
    if (stepTransform == null)
    {
        var stepObject = new GameObject("Footsteps");
        stepObject.transform.SetParent(audioRoot, false);
        stepTransform = stepObject.transform;
    }

    Transform voiceTransform = audioRoot.Find("Voice");
    if (voiceTransform == null)
    {
        var voiceObject = new GameObject("Voice");
        voiceObject.transform.SetParent(audioRoot, false);
        voiceTransform = voiceObject.transform;
    }

    AudioSource footsteps = stepTransform.GetComponent<AudioSource>();
    if (footsteps == null) footsteps = stepTransform.gameObject.AddComponent<AudioSource>();
    footsteps.playOnAwake = false;
    footsteps.loop = false;
    footsteps.spatialBlend = 1f;
    footsteps.rolloffMode = AudioRolloffMode.Logarithmic;
    footsteps.minDistance = 1.5f;
    footsteps.maxDistance = 18f;
    footsteps.dopplerLevel = 0f;

    AudioSource voice = voiceTransform.GetComponent<AudioSource>();
    if (voice == null) voice = voiceTransform.gameObject.AddComponent<AudioSource>();
    voice.playOnAwake = false;
    voice.loop = false;
    voice.spatialBlend = 1f;
    voice.rolloffMode = AudioRolloffMode.Logarithmic;
    voice.minDistance = 3f;
    voice.maxDistance = 30f;
    voice.dopplerLevel = 0f;

    var emitter = root.GetComponent<Game.Gameplay.Audio.CharacterAudioEmitter>();
    if (emitter == null)
        emitter = root.AddComponent<Game.Gameplay.Audio.CharacterAudioEmitter>();
    emitter.Bind(footsteps, voice);

    var monsterAudio = root.GetComponent<Game.Gameplay.Monster.MonsterAudioController>();
    if (monsterAudio == null)
        monsterAudio = root.AddComponent<Game.Gameplay.Monster.MonsterAudioController>();
    monsterAudio.Bind(
        root.GetComponent<Game.Gameplay.Monster.MonsterBrain>(),
        root.GetComponent<UnityEngine.AI.NavMeshAgent>());
    monsterAudio.enabled = true;

    var rig = root.GetComponent<Game.Gameplay.Monster.MonsterRuntimeRig>();
    var serializedRig = new SerializedObject(rig);
    serializedRig.FindProperty("audioController").objectReferenceValue = monsterAudio;
    serializedRig.ApplyModifiedPropertiesWithoutUndo();

    EditorUtility.SetDirty(emitter);
    EditorUtility.SetDirty(monsterAudio);
    EditorUtility.SetDirty(rig);
    PrefabUtility.SaveAsPrefabAsset(root, path);
    return "Monster character audio authored";
}
finally
{
    PrefabUtility.UnloadPrefabContents(root);
}
```

- [ ] **Step 5: Inspect serialized prefab results before tests**

Use `manage_prefabs(action="get_hierarchy")` and `manage_prefabs(action="get_info")` for both prefab paths. Confirm:

- each prefab has `CharacterAudio/Footsteps` and `CharacterAudio/Voice`;
- root components include the correct driver and `CharacterAudioEmitter`;
- `Monster.prefab` remains inactive;
- no scene object was created or changed.

- [ ] **Step 6: Run prefab, focused, and assembly-level verification**

Run `CharacterAudioPrefabAssetTests` in EditMode first. Then run:

```text
EditMode assembly: Game.Gameplay.EditModeTests
PlayMode assembly: Game.Gameplay.PlayModeTests
```

Expected: all tests in both jobs PASS. Record exact totals from terminal job results. If a pre-existing failure is caused by the user's dirty scene/config, isolate and report it rather than altering those files.

Read the Unity Console after both jobs. There must be no new character-audio errors, duplicate-listener errors, or missing-reference errors.

- [ ] **Step 7: Inspect the final diff and commit Task 5**

Run:

```powershell
git diff --check
git status --short
git diff -- Assets/_Project/Prefabs/Player.prefab Assets/_Project/Prefabs/Monster.prefab Assets/_Project/Code/Tests/EditMode/CharacterAudioPrefabAssetTests.cs
```

Stage only both prefabs plus `CharacterAudioPrefabAssetTests.cs` and its `.meta`. Confirm the two user-owned dirty files remain unstaged, then commit:

```powershell
git add -- Assets/_Project/Prefabs/Player.prefab Assets/_Project/Prefabs/Monster.prefab Assets/_Project/Code/Tests/EditMode/CharacterAudioPrefabAssetTests.cs Assets/_Project/Code/Tests/EditMode/CharacterAudioPrefabAssetTests.cs.meta
git diff --cached --name-only
git commit -m "feat: wire character audio prefabs"
```

- [ ] **Step 8: Hand off Inspector authoring and runtime acceptance accurately**

Tell the user exactly where to assign clips:

- `Player.prefab > PlayerAudioController`: Walk Footsteps, Sprint Footsteps, Sprint Breathing, Exhausted Breathing.
- `Monster.prefab > MonsterAudioController`: Footsteps, Yell, Attack.
- `Player.prefab > CharacterAudio/Footsteps|Voice`: 2D source volume/mixer settings.
- `Monster.prefab > CharacterAudio/Footsteps|Voice`: 3D volume, rolloff, Min Distance, and Max Distance.

If the cue pools are still empty, report code, prefab, and automated-test verification as complete but auditory Play Mode acceptance as pending clip assignment. Do not claim that footsteps or yell were heard until clips were assigned and a Play Mode run actually produced them.
