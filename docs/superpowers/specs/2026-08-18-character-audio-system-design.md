# Gloomveil Character Audio System Design

**Date:** 2026-08-18
**Status:** Approved in chat

## Goal

Add a small character-audio layer for the player and monster so the remaining work is Inspector authoring: assign clips, tune volume and pitch variation, and adjust each `AudioSource`'s spatial falloff. The system must automatically drive footsteps and the existing monster yell/attack moments without putting playback logic into movement or AI classes.

## Scope

The first version provides:

- player walk and sprint footstep clip pools;
- optional player sprint-breath and exhausted-breath clip pools;
- monster footstep clips during all movement states;
- monster yell clips on confirmed gaze;
- a monster attack clip pool when the existing kill/attack event begins;
- Inspector-owned volume, pitch, `AudioSource`, mixer output, and 3D attenuation settings.

An empty cue is a supported configuration and stays silent without logging an error. This lets audio be added incrementally.

## Non-goals

- surface or material-specific footsteps;
- adaptive music, ambience, or gameplay SFX migration;
- a global singleton, service locator, or game-wide event bus;
- runtime-created one-shot objects or audio-source pooling;
- exact foot-contact synchronization through animation events;
- changes to the existing flashlight, fuse, menu, or death-sequence audio paths;
- scene or map-generation changes.

If exact attack-impact or foot-contact timing becomes necessary later, a small animation-event bridge can call the same emitter API without replacing this design.

## Existing Hooks

`PlayerController` already exposes `CurrentSpeed`, `IsGrounded`, `IsSprinting`, and `IsExhausted`. The player audio driver can observe these presentation values without changing movement authority.

`MonsterBrain` already publishes `OnConfirmedGazeStarted` and `OnKill`. `MonsterAnimationController` consumes the same events to trigger `Yell` and `Attack`, so audio can observe them independently. `MonsterNavigator` owns the `NavMeshAgent`, whose velocity provides monster footstep cadence. `MonsterRuntimeRig` initializes the spawned monster while its prefab is inactive and is the correct place to bind the monster audio driver before activation.

## Approaches Considered

### 1. Prefab-local emitter plus thin character drivers — selected

A reusable emitter owns playback details. Player and monster drivers own only their trigger rules. This keeps 2D/3D settings visible on the relevant prefab and uses component composition plus C# events where the project already exposes them.

### 2. Global audio manager with cue IDs

This centralizes lookup, but it makes actor-local 3D attenuation and overlapping footsteps/voice harder to reason about. The project has only one player and one runtime monster, so the extra global routing does not earn its complexity.

### 3. Direct playback in movement and AI classes

This uses fewer files initially, but couples presentation to `PlayerController`, `MonsterBrain`, and AI states. It would also spread clip fields across gameplay-authoritative components. This option is rejected.

## Architecture

### `AudioCueSet`

A serializable value shown inline in the Inspector. It contains:

- an `AudioClip[]` pool;
- minimum and maximum volume scale;
- minimum and maximum pitch;
- immediate-repeat avoidance when at least two valid clips exist.

Null clip entries are ignored. Runtime selection may use `UnityEngine.Random` because sound variation has no gameplay or seeded-map effect. Ranges are normalized during validation so reversed Inspector values cannot produce invalid playback settings.

### `DistanceStepCadence`

A small plain-C# helper shared by both drivers. While movement is active, it accumulates `speed * deltaTime` and emits one step when the configured distance-per-step is crossed. It resets while stopped or, for the player, airborne. It emits at most once per frame so a frame hitch cannot create a burst of stacked footsteps.

Distance-based cadence is preferred over a timer because walk, sprint, anger-driven monster speed, and movement ramps then stay naturally proportional without separate timing code.

### `CharacterAudioEmitter`

A reusable prefab component with serialized references to two dedicated sources:

- `footstepSource` for movement cues;
- `voiceSource` for yell, attack, and breathing cues.

It exposes narrow methods for playing an `AudioCueSet` through either channel. It selects a valid clip, applies the cue's pitch, and calls `PlayOneShot` with the selected volume scale. It does not decide when a cue should play.

The sources are authored on the prefab rather than created at runtime. `playOnAwake` is disabled. Player sources are configured as 2D. Monster sources are configured as 3D; the user remains the owner of volume, spatial blend, rolloff mode, minimum distance, maximum distance, spread, doppler, and optional mixer-group settings.

### `PlayerAudioController`

A component on `Player.prefab` references `PlayerController` and `CharacterAudioEmitter`. It owns these Inspector cues and tuning values:

- walk footsteps and walk distance-per-step;
- sprint footsteps and sprint distance-per-step;
- optional sprint breathing and interval;
- optional exhausted breathing and interval;
- minimum movement speed.

During `Update`, grounded movement advances the footstep cadence. `IsSprinting` chooses the sprint or walk cue and distance. Sprint breathing plays at its configured interval; exhausted breathing takes priority while `IsExhausted` is true. Stopping, leaving the ground, disabling control, or disabling the component resets the relevant cadence/timers.

The controller reads presentation state only. It never writes velocity, stamina, grounding, input, or player-control state.

### `MonsterAudioController`

A component on `Monster.prefab` references `MonsterBrain`, the monster `NavMeshAgent`, and `CharacterAudioEmitter`. It owns:

- general monster footsteps and distance-per-step;
- minimum movement speed;
- gaze-yell cues;
- attack cues.

Any valid NavMesh movement above the threshold advances footstep cadence, regardless of the current AI state. This deliberately implements the approved requirement that monster footsteps are general rather than final-chase-only.

The controller subscribes to `MonsterBrain.OnConfirmedGazeStarted` for yell and `MonsterBrain.OnKill` for the attack cue. It subscribes in `OnEnable` and unsubscribes symmetrically in `OnDisable`. The existing `DeathSequenceController.deathSound` remains the separate player-death sting; this design does not play two monster cues simultaneously from the single kill event.

### `MonsterRuntimeRig` integration

`MonsterRuntimeRig` gains a serialized `MonsterAudioController` reference. During inactive-prefab initialization it resolves the component and binds the already-resolved brain and agent before the monster is activated. A missing required audio component makes rig initialization fail with the same explicit validation style used for the animation controller. Empty clip pools do not fail initialization.

No `MainSceneWiring` change is needed because every dependency is local to the spawned monster prefab. No map-generation type references the audio layer.

## Data Flow

Player footsteps:

`PlayerController state -> PlayerAudioController cadence -> CharacterAudioEmitter -> player footstep AudioSource`

Monster footsteps:

`NavMeshAgent velocity -> MonsterAudioController cadence -> CharacterAudioEmitter -> monster footstep AudioSource`

Monster yell and attack:

`MonsterBrain event -> MonsterAudioController -> CharacterAudioEmitter -> monster voice AudioSource`

The gameplay publishers do not know that audio exists.

## Failure Handling

- Empty or all-null clip pools are silent no-ops.
- Missing required component/source references produce one descriptive initialization error and disable that driver; no per-frame log spam is allowed.
- Disabling a driver clears cadence and timers and removes event subscriptions.
- A cue pool with one valid clip may repeat it; a pool with multiple clips avoids selecting the immediately previous valid clip.
- A disabled or off-NavMesh agent is treated as stationary.

## Assets and Files

Expected new runtime files:

- `Assets/_Project/Code/Gameplay/Audio/AudioCueSet.cs`
- `Assets/_Project/Code/Gameplay/Audio/DistanceStepCadence.cs`
- `Assets/_Project/Code/Gameplay/Audio/CharacterAudioEmitter.cs`
- `Assets/_Project/Code/Gameplay/Player/PlayerAudioController.cs`
- `Assets/_Project/Code/Gameplay/Monster/MonsterAudioController.cs`

Expected modified runtime assets/files:

- `Assets/_Project/Code/Gameplay/Monster/MonsterRuntimeRig.cs`
- `Assets/_Project/Prefabs/Player.prefab`
- `Assets/_Project/Prefabs/Monster.prefab`

Focused EditMode and PlayMode test files will be added under the existing `_Project/Code/Tests` layout. `Assets/Scenes/MainGame.unity` and `Assets/Gloomveil/MapGeneration/Data/DefaultMapGenerationConfig.asset` are explicitly outside this change and their current user edits must be preserved.

## Verification

Focused automated coverage must prove:

- distance cadence emits only after enough grounded/moving distance and resets when inactive;
- walk and sprint select their own player cue pools;
- monster movement can emit footsteps independent of AI state;
- one confirmed-gaze rising edge produces one yell request;
- the kill event produces one attack request;
- disable/re-enable does not duplicate monster event subscriptions;
- empty cue pools and null entries remain safe;
- multi-clip pools avoid an immediate repeat;
- the two prefabs contain the required controllers, emitters, dedicated sources, and serialized references.

After Unity recompiles, run the focused EditMode and PlayMode suites, inspect the Console for new errors, and perform a short Play Mode check. Runtime acceptance requires hearing player walk/sprint cadence, hearing monster movement from a tunable 3D position, and hearing yell once when confirmed gaze begins. Final loudness and attenuation are artistic Inspector tuning, not automated-test assertions.

## Acceptance Criteria

- The user can add or replace every scoped sound by assigning clips on `Player.prefab` or `Monster.prefab`; no code edit is required.
- Monster footsteps play during any actual monster movement.
- Monster footstep and voice distance/falloff remain directly adjustable on prefab `AudioSource` components.
- Player walk and sprint cadences are distinct and Inspector-tunable.
- Existing movement, AI, animation, death, flashlight, fuse, menu, map, and scene behavior remains unchanged.
