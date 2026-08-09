# Monster Confirmed-Gaze Yell Design

## Goal

When the player establishes confirmed eye contact with the monster, the monster gains one configured spotted-anger increment and plays the non-looping `Yelling` animation exactly once for that continuous gaze.

## Behavior Contract

- Eye contact becomes confirmed after `MonsterConfig.GazeConfirmDurationSeconds` (currently 0.2 seconds).
- The rising edge of confirmed gaze adds `MonsterConfig.AngerPerSpotted` (currently 1) exactly once.
- Holding the same gaze does not repeatedly apply the initial spotted cost or restart the yell.
- Breaking gaze rearms the reaction; a later confirmed gaze can add anger and yell again.
- The existing `MonsterStaredownController` may still add periodic anger during a prolonged Shy staredown. That is a separate held-staredown mechanic.
- Kill/attack remains higher priority than yelling.

## Architecture

`MonsterBrain` owns gaze confirmation and anger, so it becomes the single publisher of `OnConfirmedGazeStarted`. This removes the duplicate one-time anger writes currently split between `InterestedState` and `ObservedState`. State classes continue deciding movement and aggression transitions but no longer own the initial gaze cost.

`MonsterAnimationController` observes `OnConfirmedGazeStarted` alongside the existing AI state and kill events. It queues a `Yell` Animator trigger until the Animator is initialized, while `Attack` clears any pending yell and remains the first Any State transition. Animation stays presentation-only: root motion remains disabled and the animation controller never drives the NavMeshAgent.

## Mecanim Contract

- Parameter: `Yell` (`Trigger`).
- State: `Yell`, motion `Assets/animations/Yelling.anim`, loop disabled.
- Transition: `Any State -> Yell`, no exit time, short fixed crossfade, no self-transition.
- Transition: `Yell -> Locomotion`, exit time near the end of the clip.
- Existing `Any State -> Attack` stays ahead of the yell transition.

## Verification

- PlayMode integration test proves one continuous confirmed gaze adds one anger point and enters `Yell`.
- The same test proves holding gaze does not add another initial point or retrigger.
- EditMode asset test proves the `Yell` parameter/state/motion/transition contract.
- Existing monster EditMode and PlayMode suites remain green.

## Scope Boundary

No scream audio is added because no audio asset was provided. `OnConfirmedGazeStarted` is an appropriate future hook for sound without coupling audio to AI logic.
