using UnityEngine;

namespace Game.UI.MainMenu
{
    /// <summary>Wired to the OPTIONS page's Master Volume Slider.onValueChanged. Single source of
    /// truth for reading/writing AudioListener.volume from UI, since that's a static property a
    /// UnityEvent can't bind to directly.</summary>
    public class MasterVolumeController : MonoBehaviour
    {
        public float CurrentVolume => AudioListener.volume;

        public void SetVolume(float value) => AudioListener.volume = value;
    }
}
