using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

using Game.Gameplay.World;
namespace Game.Gameplay.DebugTools
{
    /// <summary>
    /// Debug-only: lets the tester manually kill light zones with number keys to feel out
    /// "Darkness Belongs to the Monster" (TGD section 2.3) before the real LightDirector/Fuse
    /// progression system exists. Not part of the shipping game loop.
    /// </summary>
    public class LightZoneTestToggler : MonoBehaviour
    {
        [SerializeField] private LightZone[] zones;

        public void Bind(LightZone[] lightZones) => zones = lightZones;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || zones == null) return;

            ToggleIfPressed(keyboard.digit1Key, 0);
            ToggleIfPressed(keyboard.digit2Key, 1);
            ToggleIfPressed(keyboard.digit3Key, 2);
            ToggleIfPressed(keyboard.digit4Key, 3);
            ToggleIfPressed(keyboard.digit5Key, 4);
        }

        private void ToggleIfPressed(KeyControl key, int zoneIndex)
        {
            if (key.wasPressedThisFrame && zoneIndex < zones.Length && zones[zoneIndex] != null)
            {
                zones[zoneIndex].Toggle();
            }
        }
    }
}
