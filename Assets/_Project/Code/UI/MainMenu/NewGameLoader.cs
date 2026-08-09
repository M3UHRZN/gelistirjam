using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI.MainMenu
{
    /// <summary>Wired to the NEW GAME row's onConfirmed. Scene name is a field, not hardcoded, so
    /// retargeting it never needs a code change.</summary>
    public class NewGameLoader : MonoBehaviour
    {
        [SerializeField] private string sceneName = "MainGame";

        public void Load() => SceneManager.LoadScene(sceneName);
    }
}
