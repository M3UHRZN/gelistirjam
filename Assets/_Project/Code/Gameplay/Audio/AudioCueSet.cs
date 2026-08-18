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
