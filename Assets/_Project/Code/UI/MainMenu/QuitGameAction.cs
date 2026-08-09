using UnityEngine;

namespace Game.UI.MainMenu
{
    /// <summary>Wired to the QUIT row's onConfirmed. Exits Play Mode in the Editor (Application.Quit
    /// is a no-op there), quits the built application otherwise.</summary>
    public class QuitGameAction : MonoBehaviour
    {
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
