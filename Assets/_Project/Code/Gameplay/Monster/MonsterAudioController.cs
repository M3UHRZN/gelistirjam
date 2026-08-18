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
        [SerializeField] private bool lightweightMode;

        [Header("Footsteps")]
        [SerializeField] private AudioCueSet footsteps = new();
        [SerializeField, Min(0.01f)] private float distancePerStep = 1.8f;
        [SerializeField, Min(0f)] private float minimumMovementSpeed = 0.05f;

        [Header("Voice")]
        [SerializeField] private AudioCueSet yell = new();
        [SerializeField] private AudioCueSet attack = new();

        private readonly DistanceStepCadence footstepCadence = new();
        private bool subscribed;

        public bool IsBound => agent != null && emitter != null && emitter.IsBound
            && (lightweightMode || brain != null);

        public void Bind(MonsterBrain monsterBrain, NavMeshAgent navMeshAgent)
        {
            Unsubscribe();
            brain = monsterBrain;
            agent = navMeshAgent;
            lightweightMode = false;
            ResolveComponents();
            Subscribe();
        }

        public void BindLightweight(NavMeshAgent navMeshAgent, CharacterAudioEmitter audioEmitter)
        {
            Unsubscribe();
            brain = null;
            agent = navMeshAgent;
            emitter = audioEmitter;
            lightweightMode = true;
            ResetAudioState();
        }

        public bool PlayYell() => emitter != null && emitter.PlayVoice(yell);
        public bool PlayAttack() => emitter != null && emitter.PlayVoice(attack);

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
                    "MonsterAudioController requires a NavMeshAgent and bound CharacterAudioEmitter; full monster mode also requires MonsterBrain.",
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

        private void HandleConfirmedGazeStarted() => PlayYell();
        private void HandleKill() => PlayAttack();

        private void ResetAudioState()
        {
            footstepCadence.Reset();
            footsteps?.ResetHistory();
            yell?.ResetHistory();
            attack?.ResetHistory();
        }
    }
}
