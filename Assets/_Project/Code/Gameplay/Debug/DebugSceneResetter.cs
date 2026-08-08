using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Game.Gameplay.DebugTools
{
    /// <summary>
    /// Dev-only: press R to instantly reload the active scene, resetting every monster/player/game
    /// state back to spawn without stopping and re-entering Play Mode in the Editor (see chat — the
    /// Editor's own Stop/Play toggle proved slow and occasionally got stuck mid-transition while
    /// playtesting the AI, especially when the Editor window lost focus). Same shipping-build guard as
    /// MonsterDebugHUD/MonsterDebugGizmos.
    /// </summary>
    public class DebugSceneResetter : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.rKey.wasPressedThisFrame) return;

#if UNITY_EDITOR
            // EditorSceneManager.LoadSceneInPlayMode (not the runtime SceneManager.LoadScene) is the
            // documented API for exactly this case: reloading a scene during Play Mode in the Editor
            // without requiring it to be in Build Settings (MonsterAI_TestScene isn't). Confirmed via
            // Unity's own ScriptReference — see chat.
            EditorSceneManager.LoadSceneInPlayMode(SceneManager.GetActiveScene().path,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            // In an actual DEVELOPMENT_BUILD the scene must be in Build Settings anyway, so a plain
            // build-index reload is both simpler and correct there.
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
#endif
        }
#endif
    }
}
