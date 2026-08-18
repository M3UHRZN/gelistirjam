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
