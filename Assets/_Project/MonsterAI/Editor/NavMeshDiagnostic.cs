using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Game.MonsterAI.Editor
{
    /// <summary>
    /// Temporary diagnostic (see chat): prints an ASCII top-down map of NavMesh coverage so wall-gap
    /// math errors can be found without opening the Editor's Scene view. Explicitly opens the built
    /// scene first — NavMesh.SamplePosition only sees whatever scene is currently loaded in this
    /// process, and running this from a fresh `-executeMethod` invocation (a different process than
    /// the one that ran BuildScene) starts with nothing loaded at all.
    /// </summary>
    public static class NavMeshDiagnostic
    {
        private const string ScenePath = "Assets/_Project/MonsterAI/Scenes/MonsterAI_TestScene.unity";

        [MenuItem("Tools/Gloomveil/Diagnose NavMesh (prints ASCII map to log)")]
        public static void PrintAsciiMap()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            const float step = 0.5f;
            const float sampleTolerance = 0.5f; // floor top is at y=0 exactly; query at y=0.1
            float xMin = -11f, xMax = 17f, zMin = -11f, zMax = 27f;

            var sb = new StringBuilder();
            sb.AppendLine("NavMesh ASCII map ('#' = walkable, '.' = not on navmesh). X grows right, Z grows up (rows printed north-to-south).");

            for (float z = zMax; z >= zMin; z -= step)
            {
                var row = new StringBuilder();
                for (float x = xMin; x <= xMax; x += step)
                {
                    bool onMesh = NavMesh.SamplePosition(new Vector3(x, 0.1f, z), out _, sampleTolerance, NavMesh.AllAreas);
                    row.Append(onMesh ? '#' : '.');
                }
                sb.AppendLine(row.ToString());
            }

            Debug.Log(sb.ToString());

            // Connectivity check: can every key room-anchor reach every other via a single NavMeshPath?
            CheckConnectivity(new Vector3(8, 1, -7), "StartRoom");
            CheckConnectivity(new Vector3(8, 1, 8), "CentralRoom");
            CheckConnectivity(new Vector3(8, 1, 23), "DeadEndRoom");
            CheckConnectivity(new Vector3(-7, 1, 8), "SafeZoneRoom");
            CheckConnectivity(new Vector3(2, 1, 2), "RingCornerSW");
            CheckConnectivity(new Vector3(14, 1, 14), "RingCornerNE");
        }

        private static void CheckConnectivity(Vector3 point, string label)
        {
            bool onMesh = NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas);
            if (!onMesh)
            {
                Debug.LogError($"[NavMeshDiagnostic] {label} at {point} is NOT on the navmesh at all (nearest within 2m not found).");
                return;
            }

            NavMeshPath path = new NavMeshPath();
            bool reachableFromOrigin = NavMesh.CalculatePath(new Vector3(8, 1, -7), hit.position, NavMesh.AllAreas, path);
            Debug.Log($"[NavMeshDiagnostic] {label}: onMesh={onMesh} snappedTo={hit.position} pathFromStartRoom={path.status} reachable={reachableFromOrigin}");
        }
    }
}
