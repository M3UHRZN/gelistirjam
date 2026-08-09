using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Gameplay.Tests
{
    public sealed class MonsterAnimationAssetTests
    {
        private const string ControllerPath = "Assets/_Project/Animations/MonsterAnimator.controller";
        private const string PrefabPath = "Assets/_Project/Prefabs/Monster.prefab";

        [Test]
        public void ProductionController_ExposesRuntimeContractAndAttackTransition()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.That(controller, Is.Not.Null, "Production monster Animator Controller is missing.");

            AssertParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            AssertParameter(controller, "Moving", AnimatorControllerParameterType.Bool);
            AssertParameter(controller, "Aggressive", AnimatorControllerParameterType.Bool);
            AssertParameter(controller, "Retreating", AnimatorControllerParameterType.Bool);
            AssertParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
            AssertParameter(controller, "Yell", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            Assert.That(stateMachine.defaultState.name, Is.EqualTo("Locomotion"));

            AnimatorState attack = stateMachine.states.Select(child => child.state)
                .SingleOrDefault(state => state.name == "Attack");
            Assert.That(attack, Is.Not.Null);
            Assert.That(attack.motion, Is.TypeOf<AnimationClip>());
            Assert.That(attack.motion.name, Is.EqualTo("attack"));

            bool hasAttackTrigger = stateMachine.anyStateTransitions.Any(transition =>
                transition.destinationState == attack
                && transition.conditions.Any(condition => condition.parameter == "Attack"));
            Assert.That(hasAttackTrigger, Is.True, "Any State must reach Attack through the Attack trigger.");

            AnimatorState yell = stateMachine.states.Select(child => child.state)
                .SingleOrDefault(state => state.name == "Yell");
            Assert.That(yell, Is.Not.Null);
            Assert.That(yell.motion, Is.TypeOf<AnimationClip>());
            Assert.That(yell.motion.name, Is.EqualTo("Yelling"));
            Assert.That(((AnimationClip)yell.motion).isLooping, Is.False, "Yelling must be a one-shot clip.");

            bool hasYellTrigger = stateMachine.anyStateTransitions.Any(transition =>
                transition.destinationState == yell
                && transition.conditions.Any(condition => condition.parameter == "Yell"));
            Assert.That(hasYellTrigger, Is.True, "Any State must reach Yell through the Yell trigger.");

            int attackTransitionIndex = System.Array.FindIndex(stateMachine.anyStateTransitions,
                transition => transition.destinationState == attack);
            int yellTransitionIndex = System.Array.FindIndex(stateMachine.anyStateTransitions,
                transition => transition.destinationState == yell);
            Assert.That(attackTransitionIndex, Is.LessThan(yellTransitionIndex),
                "Attack must keep higher Any State transition priority than Yell.");

            AssertStateHasClip(stateMachine, "Locomotion");
            AssertLoopingState(stateMachine, "Stalk", "Running Crawl");
            AssertLoopingState(stateMachine, "Retreat", "Run Backward");
            AssertLoopingState(stateMachine, "Chase", "Zombie Running");
        }

        [Test]
        public void ProductionPrefab_HasModelAnimatorAndPresentationBridge()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.activeSelf, Is.False,
                "The spawn gate must initialize the monster before its first OnEnable callbacks.");

            Animator animator = prefab.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController,
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)));
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(prefab.GetComponent("MonsterAnimationController"), Is.Not.Null);

            SkinnedMeshRenderer modelRenderer = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.That(modelRenderer, Is.Not.Null);
            Assert.That(modelRenderer.gameObject.activeSelf, Is.True);
        }

        private static void AssertParameter(
            AnimatorController controller,
            string parameterName,
            AnimatorControllerParameterType parameterType)
        {
            AnimatorControllerParameter parameter = controller.parameters
                .SingleOrDefault(candidate => candidate.name == parameterName);
            Assert.That(parameter, Is.Not.Null, $"Missing Animator parameter: {parameterName}");
            Assert.That(parameter.type, Is.EqualTo(parameterType));
        }

        private static void AssertLoopingState(
            AnimatorStateMachine stateMachine,
            string stateName,
            string clipName)
        {
            AnimatorState state = stateMachine.states.Select(child => child.state)
                .SingleOrDefault(candidate => candidate.name == stateName);
            Assert.That(state, Is.Not.Null, $"Missing Animator state: {stateName}");
            Assert.That(state.motion, Is.TypeOf<AnimationClip>());
            Assert.That(state.motion.name, Is.EqualTo(clipName));
            Assert.That(((AnimationClip)state.motion).isLooping, Is.True, $"{clipName} must loop.");
        }

        private static void AssertStateHasClip(AnimatorStateMachine stateMachine, string stateName)
        {
            AnimatorState state = stateMachine.states.Select(child => child.state)
                .SingleOrDefault(candidate => candidate.name == stateName);
            Assert.That(state, Is.Not.Null, $"Missing Animator state: {stateName}");
            Assert.That(state.motion, Is.TypeOf<AnimationClip>());
        }
    }
}
