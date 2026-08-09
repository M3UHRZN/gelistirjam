using Game.Gameplay.Player;
using Game.Gameplay.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// One-click setup for the Lobby scene: player spawn, a floor, a light, and the same Exit
    /// prefab used in-run, standing open in front of the player. Interacting with it starts a fresh
    /// run on a new random seed (LobbyNewGameInteractable). Additive/idempotent like
    /// MainSceneFuseSetup.cs — safe to re-run.
    /// </summary>
    public static class LobbySceneBuilder
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string ExitPrefabPath = "Assets/_Project/Prefabs/Exit.prefab";

        [MenuItem("Tools/Gloomveil/Setup Lobby Scene")]
        public static void Run()
        {
            string activeSceneName = EditorSceneManager.GetActiveScene().name;
            if (activeSceneName != "Lobby")
            {
                Debug.LogError("Setup Lobby Scene: the active scene is " +
                                $"'{activeSceneName}', not 'Lobby'. Open Lobby.unity before " +
                                "running this tool — it saves the scene. Aborting.");
                return;
            }

            EnsureFloor();
            EnsureLight();
            EnsurePlayer();
            EnsureExitDoor();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("Lobby scene set up: floor, light, player spawn, and the exit door (walk up " +
                       "and hold E to start a new run). Enter Play Mode to test.");
        }

        private static void EnsureFloor()
        {
            if (GameObject.Find("Floor") != null) return;

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(10f, 1f, 10f);
        }

        private static void EnsureLight()
        {
            if (Object.FindFirstObjectByType<Light>() != null) return;

            var lightGo = new GameObject("LobbyLight");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.85f, 0.85f, 0.9f);
            light.intensity = 5f;
            light.range = 12f;
            lightGo.transform.position = new Vector3(0f, 3f, 2f);
        }

        private static void EnsurePlayer()
        {
            if (Object.FindFirstObjectByType<PlayerController>() != null) return;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Setup Lobby Scene: player prefab not found at '{PlayerPrefabPath}'.");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            // Player faces -Z (default forward) toward the door placed at +Z below.
            instance.transform.SetPositionAndRotation(new Vector3(0f, 0f, -2f), Quaternion.identity);
        }

        private static void EnsureExitDoor()
        {
            if (Object.FindFirstObjectByType<LobbyNewGameInteractable>() != null) return;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExitPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Setup Lobby Scene: exit prefab not found at '{ExitPrefabPath}'.");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "LobbyExitDoor";
            // Facing back toward the player spawn at (0,0,-2): rotate 180 so its front (+Z, per
            // MapExitController's own convention for this prefab) points at -Z, into the lobby.
            instance.transform.SetPositionAndRotation(new Vector3(0f, 0f, 2f), Quaternion.Euler(0f, 180f, 0f));

            Transform trigger = instance.transform.Find("ExitTrigger");
            if (trigger == null)
            {
                Debug.LogError("Setup Lobby Scene: Exit.prefab has no 'ExitTrigger' child — " +
                                "LobbyNewGameInteractable was not added.");
                return;
            }

            trigger.gameObject.AddComponent<LobbyNewGameInteractable>();
        }
    }
}
