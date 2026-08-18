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
