using UnityEditor;
using UnityEngine;

namespace Gloomveil.MapGeneration.Editor
{
    [CustomEditor(typeof(MapGenerationBootstrap))]
    public sealed class MapGenerationBootstrapEditor : UnityEditor.Editor
    {
        private SerializedProperty startupSeed;

        private void OnEnable()
        {
            startupSeed = serializedObject.FindProperty("startupSeed");
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (!GUILayout.Button("Generate Map"))
                {
                    return;
                }

                serializedObject.ApplyModifiedProperties();
                var bootstrap = (MapGenerationBootstrap)target;
                bootstrap.Generate(startupSeed.intValue);
                SceneView.RepaintAll();
            }
        }
    }
}
