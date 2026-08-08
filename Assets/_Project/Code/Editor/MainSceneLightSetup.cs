// Assets/_Project/Code/Editor/MainSceneLightSetup.cs
using Game.Gameplay;
using Game.Gameplay.World;
using Gloomveil.MapGeneration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// One-click setup that wires MapLightGate and MapSafeZoneGate into the currently-open main
    /// procedural scene, mirroring MainSceneFuseSetup.cs — purely ADDITIVE, safe to re-run (every
    /// step checks for an existing object/component first). Requires MainSceneWiring to already exist
    /// (run "Setup Main Scene Fuse System" first if it doesn't) since MapLightGate needs its
    /// LightDirector instance.
    /// </summary>
    public static class MainSceneLightSetup
    {
        private const string SafeZoneRuntimeSetPath = "Assets/_Project/Data/SafeZoneRuntimeSet.asset";

        [MenuItem("Tools/Gloomveil/Setup Main Scene Light System")]
        public static void Run()
        {
            GeneratedMapRegistry registry = Object.FindFirstObjectByType<GeneratedMapRegistry>();
            MainSceneWiring wiring = Object.FindFirstObjectByType<MainSceneWiring>();

            if (registry == null || wiring == null)
            {
                Debug.LogError("Setup Main Scene Light System: missing GeneratedMapRegistry or " +
                                "MainSceneWiring in the active scene. Run 'Setup Main Scene Fuse " +
                                "System' first, and open the main scene.");
                return;
            }

            SafeZoneRuntimeSet safeZoneSet =
                AssetDatabase.LoadAssetAtPath<SafeZoneRuntimeSet>(SafeZoneRuntimeSetPath);
            if (safeZoneSet == null)
            {
                Debug.LogError(
                    $"Setup Main Scene Light System: no SafeZoneRuntimeSet asset at {SafeZoneRuntimeSetPath}.");
                return;
            }

            MapLightGate lightGate = EnsureLightGate();
            MapSafeZoneGate safeZoneGate = EnsureSafeZoneGate();

            lightGate.Bind(registry, wiring);
            safeZoneGate.Bind(registry, safeZoneSet);

            EditorUtility.SetDirty(lightGate);
            EditorUtility.SetDirty(safeZoneGate);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("Main scene Light system wired: MapLightGate, MapSafeZoneGate.");
        }

        private static MapLightGate EnsureLightGate()
        {
            MapLightGate existing = Object.FindFirstObjectByType<MapLightGate>();
            if (existing != null) return existing;

            GameObject go = new GameObject("LightGate");
            return go.AddComponent<MapLightGate>();
        }

        private static MapSafeZoneGate EnsureSafeZoneGate()
        {
            MapSafeZoneGate existing = Object.FindFirstObjectByType<MapSafeZoneGate>();
            if (existing != null) return existing;

            GameObject go = new GameObject("SafeZoneGate");
            return go.AddComponent<MapSafeZoneGate>();
        }
    }
}
