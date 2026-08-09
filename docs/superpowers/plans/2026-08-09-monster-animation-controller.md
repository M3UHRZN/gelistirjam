# Monster Animation Controller Implementation Plan

> **For Codex:** Execute this plan inline on the existing `enemy-implement-to-game` feature branch. Preserve the user's imported model, material, texture, and clip files.

**Goal:** Wire the imported monster model and attack animation into the runtime-spawned monster without coupling Mecanim state names to the AI state machine.

**Architecture:** `MonsterBrain` remains gameplay authority. A `MonsterAnimationController` presentation component subscribes to brain state changes and feeds stable Animator parameters. The Animator Controller contains a future-ready locomotion state plus a one-shot attack state; NavMesh movement supplies speed/moving values and root motion stays disabled.

**Tech Stack:** Unity 6, C#, Mecanim `AnimatorController`, Unity Test Framework.

---

### Task 1: Lock the asset and prefab contract with a failing EditMode test

**Files:**
- Create: `Assets/_Project/Code/Tests/EditMode/MonsterAnimationAssetTests.cs`

1. Assert the production Animator Controller exists and exposes `Speed`, `Moving`, `Aggressive`, and `Attack` with the intended types.
2. Assert its default state is `Locomotion`, its `Attack` state uses the imported `attack` clip, and a trigger transition reaches it.
3. Assert `Monster.prefab` contains an Animator, assigns the controller, disables root motion, and contains `MonsterAnimationController`.
4. Run the focused EditMode test and confirm it fails before production assets exist.

### Task 2: Implement the runtime presentation bridge

**Files:**
- Create: `Assets/_Project/Code/Gameplay/Monster/MonsterAnimationController.cs`
- Modify: `Assets/_Project/Code/Gameplay/Monster/MonsterRuntimeRig.cs`

1. Add hashed Animator parameters and serialized Animator/brain/NavMeshAgent references.
2. Subscribe safely across enable/disable and explicit runtime binding.
3. Update damped speed, moving, and aggressive parameters from live AI/navigation state.
4. Fire `Attack` once on entry to `KillState`; never drive gameplay or root motion from animation.
5. Bind and validate the component from `MonsterRuntimeRig` before the spawned monster is enabled.

### Task 3: Build the Mecanim asset and wire the production prefab

**Files:**
- Create: `Assets/_Project/Animations/MonsterAnimator.controller`
- Modify: `Assets/_Project/Prefabs/Monster.prefab`

1. Create `Locomotion` as the default state and `Attack` using `Assets/animations/attack.anim`.
2. Add `Any State -> Attack` trigger transition and `Attack -> Locomotion` exit-time transition.
3. Instantiate `monsterV2.fbx` below the prefab root, assign the controller, disable root motion, and keep the placeholder renderer inactive as a fallback/debug reference.
4. Bind serialized references on `MonsterAnimationController` and `MonsterRuntimeRig`.

### Task 4: Verify compilation, assets, and runtime behavior

1. Refresh Unity and confirm there are no compile errors.
2. Run the focused EditMode contract test, then all EditMode tests.
3. Run the relevant monster PlayMode tests.
4. Inspect the spawned prefab live: controller assigned, root motion disabled, model renderer active, attack transition reachable.
5. Review `git diff` and stage/commit only files required by this task, including the imported attack clip dependency but excluding unrelated user material/texture changes.
