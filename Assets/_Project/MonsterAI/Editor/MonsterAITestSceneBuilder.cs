using System.Collections.Generic;
using Game.MonsterAI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Game.MonsterAI.Editor
{
    /// <summary>
    /// One-click graybox test scene for the monster AI: a ring corridor around a central room,
    /// with spurs to a start alcove, a dead end and a safe zone (TGD section 40, 54). Builds and
    /// wires every MonoBehaviour via Bind(...) calls instead of reflection, then bakes a NavMesh
    /// and saves the scene. Run again any time to rebuild from scratch.
    ///
    /// Actors (player/monster) are built while inactive and only SetActive(true) once every Bind()
    /// call has run — MonsterBrain.OnEnable and MonsterVisualDriver.OnEnable both touch bound
    /// references immediately, so activating before wiring would throw a NullReferenceException.
    /// </summary>
    public static class MonsterAITestSceneBuilder
    {
        private const string RootFolder = "Assets/_Project/MonsterAI";
        private const string ScenesFolder = RootFolder + "/Scenes";
        private const string DataFolder = RootFolder + "/Data";
        private const string ScenePath = ScenesFolder + "/MonsterAI_TestScene.unity";
        private const string ConfigAssetPath = DataFolder + "/MonsterConfig_Default.asset";
        private const string StalkSetAssetPath = DataFolder + "/StalkPointRuntimeSet.asset";
        private const string SafeSetAssetPath = DataFolder + "/SafeZoneRuntimeSet.asset";

        // Single place to retune the graybox before regenerating (Tools > Gloomveil > Build Monster AI Test Scene).
        // Once built, every wall/light/point below is a normal GameObject — drag it in the Scene view like
        // any other; you only need to touch these constants if you want a re-run to start from different values.
        private const float WallHeight = 3f;
        private const float WallThickness = 0.3f;
        private const float CorridorWidth = 3.2f;
        private const float WallCenterY = WallHeight * 0.5f;

        private struct Box
        {
            public Vector3 Position;
            public Vector3 Size;
            public Box(float x, float y, float z, float sx, float sy, float sz)
            {
                Position = new Vector3(x, y, z);
                Size = new Vector3(sx, sy, sz);
            }
        }

        [MenuItem("Tools/Gloomveil/Build Monster AI Test Scene")]
        public static void BuildScene()
        {
            EnsureFolders();

            MonsterConfig config = GetOrCreateAsset<MonsterConfig>(ConfigAssetPath);
            StalkPointRuntimeSet stalkSet = GetOrCreateAsset<StalkPointRuntimeSet>(StalkSetAssetPath);
            SafeZoneRuntimeSet safeSet = GetOrCreateAsset<SafeZoneRuntimeSet>(SafeSetAssetPath);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            Material wallMat = CreateMaterial("WallMaterial_Debug", new Color(0.22f, 0.22f, 0.25f));
            Material floorMat = CreateMaterial("FloorMaterial_Debug", new Color(0.12f, 0.12f, 0.13f));
            Material safeMat = CreateMaterial("SafeZoneMaterial_Debug", new Color(0.75f, 0.65f, 0.25f));
            Material monsterMat = CreateEmissiveMaterial("MonsterMaterial_Debug", new Color(0.12f, 0.12f, 0.16f));

            GameObject level = new GameObject("Level");

            // NavMeshSurface uses CollectObjects.Children — only its own children (Floor + Walls)
            // feed the bake, so the player/monster capsules never pollute the walkable mesh.
            GameObject navRoot = new GameObject("NavMeshSurface");
            navRoot.transform.SetParent(level.transform, false);
            NavMeshSurface surface = navRoot.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;

            BuildFloor(navRoot.transform, floorMat);
            BuildWalls(navRoot.transform, wallMat);

            List<Light> lightsRing = BuildRingLights(level.transform);
            List<Light> lightsStart = new List<Light> { CreatePointLight(level.transform, "Light_Start", new Vector3(8, 2.6f, -7), 6f, 1.1f) };
            List<Light> lightsDeadEnd = new List<Light> { CreatePointLight(level.transform, "Light_DeadEnd", new Vector3(8, 2.6f, 23), 6f, 1.1f) };
            List<Light> lightsSafe = new List<Light> { CreatePointLight(level.transform, "Light_SafeZone", new Vector3(-7, 2.6f, 8), 7f, 1.6f) };

            LightZone zoneRing = BuildLightZone(level.transform, "LightZone_Ring", lightsRing);
            LightZone zoneStart = BuildLightZone(level.transform, "LightZone_Start", lightsStart);
            LightZone zoneDeadEnd = BuildLightZone(level.transform, "LightZone_DeadEnd", lightsDeadEnd);
            LightZone zoneSafe = BuildLightZone(level.transform, "LightZone_Safe", lightsSafe);

            BuildSafeZone(level.transform, safeMat, safeSet);

            GameObject stalkRoot = new GameObject("StalkPoints");
            stalkRoot.transform.SetParent(level.transform, false);
            BuildStalkPoints(stalkRoot.transform, stalkSet);

            GameObject debugRoot = new GameObject("Debug");
            LightZoneTestToggler toggler = debugRoot.AddComponent<LightZoneTestToggler>();
            toggler.Bind(new[] { zoneRing, zoneStart, zoneDeadEnd, zoneSafe });

            // NavMesh must be baked before the agent (monster) is enabled and tries to path on it.
            surface.BuildNavMesh();

            GameObject player = BuildPlayer(new Vector3(8f, 1f, -7f));
            TestPlayerController playerController = player.GetComponent<TestPlayerController>();
            Camera playerCamera = player.GetComponentInChildren<Camera>(true);
            PlayerVisionModel visionModel = playerCamera.gameObject.AddComponent<PlayerVisionModel>();
            PlayerGazeDetector gazeDetector = playerCamera.gameObject.AddComponent<PlayerGazeDetector>();
            gazeDetector.Bind(playerCamera, config);

            GameObject monster = BuildMonster(new Vector3(8f, 1f, 8f), monsterMat, config, stalkSet, safeSet,
                player.transform, visionModel, gazeDetector);

            MonsterBrain brain = monster.GetComponent<MonsterBrain>();
            MonsterDebugGizmos gizmos = monster.AddComponent<MonsterDebugGizmos>();
            gizmos.Bind(brain);
            MonsterDebugHUD hud = debugRoot.AddComponent<MonsterDebugHUD>();
            hud.Bind(brain, playerController);

            Directory_CreateIfMissing(ScenesFolder);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Monster AI test scene built at {ScenePath}. Enter Play Mode to test: WASD to move, mouse to look, " +
                      "Shift to sprint, F for flashlight, 1-4 to kill light zones.");

            // Sanity check: if either count is 0 after a build, the Bind()/OnEnable registration
            // path is broken again — the monster silently has no destinations to navigate to.
            Debug.Log($"[Sanity] StalkPointRuntimeSet.Items = {stalkSet.Items.Count}, " +
                      $"SafeZoneRuntimeSet.Items = {safeSet.Items.Count}.");
        }

        // ---------- folders & assets ----------

        private static void EnsureFolders()
        {
            Directory_CreateIfMissing(RootFolder);
            Directory_CreateIfMissing(ScenesFolder);
            Directory_CreateIfMissing(DataFolder);
        }

        private static void Directory_CreateIfMissing(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath)) return;

            string parent = assetFolderPath.Substring(0, assetFolderPath.LastIndexOf('/'));
            string leaf = assetFolderPath.Substring(assetFolderPath.LastIndexOf('/') + 1);
            if (!AssetDatabase.IsValidFolder(parent)) Directory_CreateIfMissing(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static T GetOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            return mat;
        }

        // Emissive so MonsterVisualDriver's state-color feedback reads clearly under the scene's
        // near-black ambient lighting — a plain albedo tint is invisible in the dark (see chat).
        private static Material CreateEmissiveMaterial(string name, Color color)
        {
            Material mat = CreateMaterial(name, color);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color);
            return mat;
        }

        // ---------- lighting ----------

        private static void BuildLighting()
        {
            GameObject sunGO = new GameObject("Dim Ambient Sun");
            Light sun = sunGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.08f;
            sun.color = new Color(0.5f, 0.55f, 0.7f);
            sunGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.03f, 0.03f, 0.04f);
        }

        private static Light CreatePointLight(Transform parent, string name, Vector3 position, float range, float intensity)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
            light.color = new Color(1f, 0.85f, 0.65f);
            return light;
        }

        private static List<Light> BuildRingLights(Transform parent)
        {
            return new List<Light>
            {
                CreatePointLight(parent, "Light_Ring_SW", new Vector3(2, 2.6f, 2), 6f, 1f),
                CreatePointLight(parent, "Light_Ring_SE", new Vector3(14, 2.6f, 2), 6f, 1f),
                CreatePointLight(parent, "Light_Ring_NW", new Vector3(2, 2.6f, 14), 6f, 1f),
                CreatePointLight(parent, "Light_Ring_NE", new Vector3(14, 2.6f, 14), 6f, 1f),
                CreatePointLight(parent, "Light_CentralRoom", new Vector3(8, 2.6f, 8), 6f, 1f),
            };
        }

        private static LightZone BuildLightZone(Transform parent, string name, List<Light> lights)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LightZone zone = go.AddComponent<LightZone>();
            zone.Bind(lights.ToArray());
            zone.SetActive(true); // all zones start lit; the LightZoneTestToggler can kill them by hand
            return zone;
        }

        // ---------- geometry ----------

        private static void BuildFloor(Transform parent, Material material)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(parent, false);
            floor.transform.position = new Vector3(3f, -0.1f, 8f);
            floor.transform.localScale = new Vector3(26f, 0.2f, 40f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void BuildWalls(Transform parent, Material material)
        {
            GameObject wallsRoot = new GameObject("Walls");
            wallsRoot.transform.SetParent(parent, false);

            // Ring corridor: outer rect (1,1)-(15,15), inner room (4,4)-(12,12), ring width matches
            // CorridorWidth on every side. Every entry's Y position/height/thickness comes from the
            // three constants above the class body — retune those, not these lines, for a global change.
            float y = WallCenterY, h = WallHeight, t = WallThickness;
            var walls = new List<Box>
            {
                // Outer south (gap x6.4-9.6 -> Start spur)
                new Box(3.7f, y, 1f, 5.4f, h, t),
                new Box(12.3f, y, 1f, 5.4f, h, t),
                // Outer north (gap x6.4-9.6 -> Dead-end spur)
                new Box(3.7f, y, 15f, 5.4f, h, t),
                new Box(12.3f, y, 15f, 5.4f, h, t),
                // Outer west (gap z6.4-9.6 -> Safe zone spur)
                new Box(1f, y, 3.7f, t, h, 5.4f),
                new Box(1f, y, 12.3f, t, h, 5.4f),
                // Outer east (solid)
                new Box(15f, y, 8f, t, h, 14f),

                // Inner room south (gap x5.2-10.8 -> doorway into ring)
                new Box(5.2f, y, 4f, 2.4f, h, t),
                new Box(10.8f, y, 4f, 2.4f, h, t),
                // Inner room north/west/east (solid)
                new Box(8f, y, 12f, 8f, h, t),
                new Box(4f, y, 8f, t, h, 8f),
                new Box(12f, y, 8f, t, h, 8f),

                // Start spur (south) side walls
                new Box(6.4f, y, -2f, t, h, 6f),
                new Box(9.6f, y, -2f, t, h, 6f),
                // Start room
                new Box(8f, y, -9f, 6f, h, t),
                new Box(5f, y, -7f, t, h, 4f),
                new Box(11f, y, -7f, t, h, 4f),
                new Box(5.7f, y, -5f, 1.4f, h, t),
                new Box(10.3f, y, -5f, 1.4f, h, t),

                // Dead-end spur (north) side walls
                new Box(6.4f, y, 18f, t, h, 6f),
                new Box(9.6f, y, 18f, t, h, 6f),
                // Dead-end room
                new Box(8f, y, 25f, 6f, h, t),
                new Box(5f, y, 23f, t, h, 4f),
                new Box(11f, y, 23f, t, h, 4f),
                new Box(5.7f, y, 21f, 1.4f, h, t),
                new Box(10.3f, y, 21f, 1.4f, h, t),

                // Safe zone spur (west) side walls
                new Box(-2f, y, 6.4f, 6f, h, t),
                new Box(-2f, y, 9.6f, 6f, h, t),
                // Safe zone room
                new Box(-9f, y, 8f, t, h, 6f),
                new Box(-7f, y, 5f, 4f, h, t),
                new Box(-7f, y, 11f, 4f, h, t),
                new Box(-5f, y, 5.7f, t, h, 1.4f),
                new Box(-5f, y, 10.3f, t, h, 1.4f),
            };

            for (int i = 0; i < walls.Count; i++)
            {
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"Wall_{i:00}";
                wall.transform.SetParent(wallsRoot.transform, false);
                wall.transform.position = walls[i].Position;
                wall.transform.localScale = walls[i].Size;
                wall.GetComponent<MeshRenderer>().sharedMaterial = material;
                GameObjectUtility.SetStaticEditorFlags(wall, StaticEditorFlags.NavigationStatic | StaticEditorFlags.ContributeGI);
            }
        }

        private static void BuildSafeZone(Transform parent, Material material, SafeZoneRuntimeSet set)
        {
            GameObject go = new GameObject("SafeZone_Room");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(-7f, 1.5f, 8f);

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(3.2f, 3f, 5.2f);
            box.isTrigger = true;

            // Visible floor tint so the safe zone reads clearly in the editor/play view.
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "SafeZoneFloorTint";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, -1.35f, 0f);
            visual.transform.localScale = new Vector3(3.2f, 0.05f, 5.2f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;

            SafeZone safeZone = go.AddComponent<SafeZone>();
            safeZone.Bind(set);
        }

        private static void BuildStalkPoints(Transform parent, StalkPointRuntimeSet set)
        {
            AddStalkPoint(parent, set, "SP_RingCorner_SW", new Vector3(2, 0.1f, 2), true, false, false, false, 1f);
            AddStalkPoint(parent, set, "SP_RingCorner_SE", new Vector3(14, 0.1f, 2), true, false, false, false, 1f);
            AddStalkPoint(parent, set, "SP_RingCorner_NW", new Vector3(2, 0.1f, 14), true, false, false, false, 1f);
            AddStalkPoint(parent, set, "SP_RingCorner_NE", new Vector3(14, 0.1f, 14), true, false, false, false, 1f);
            AddStalkPoint(parent, set, "SP_Junction_StartSpur", new Vector3(8, 0.1f, 2), false, true, false, false, 0.9f);
            AddStalkPoint(parent, set, "SP_Junction_DeadEndSpur", new Vector3(8, 0.1f, 14), false, true, false, false, 0.9f);
            AddStalkPoint(parent, set, "SP_Junction_SafeZoneSpur", new Vector3(2, 0.1f, 8), false, true, false, true, 0.9f);
            AddStalkPoint(parent, set, "SP_CentralRoom", new Vector3(8, 0.1f, 8), false, false, false, false, 0.5f);
            AddStalkPoint(parent, set, "SP_StartSpurMid", new Vector3(8, 0.1f, -2), false, false, false, false, 0.8f);
            AddStalkPoint(parent, set, "SP_DeadEndRoom", new Vector3(8, 0.1f, 23), false, false, true, false, 0.85f);
            AddStalkPoint(parent, set, "SP_NearSafeZoneMouth", new Vector3(-3, 0.1f, 8), false, false, false, true, 0.7f);
            AddStalkPoint(parent, set, "SP_RingEastMid", new Vector3(14, 0.1f, 8), false, false, false, false, 0.9f);
        }

        private static void AddStalkPoint(Transform parent, StalkPointRuntimeSet set, string name, Vector3 position,
            bool nearCorner, bool junction, bool deadEnd, bool safeZoneAdjacent, float darkness)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            StalkPoint point = go.AddComponent<StalkPoint>();
            point.Configure(nearCorner, junction, deadEnd, safeZoneAdjacent, darkness);
            point.Bind(set);
        }

        // ---------- actors ----------

        private static GameObject BuildPlayer(Vector3 spawnPosition)
        {
            GameObject player = new GameObject("TestPlayer");
            player.transform.position = spawnPosition;

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 1f, 0f);
            controller.height = 1.8f;
            controller.radius = 0.35f;

            GameObject cameraPivot = new GameObject("CameraPivot");
            cameraPivot.transform.SetParent(player.transform, false);
            cameraPivot.transform.localPosition = new Vector3(0f, 1.65f, 0f);

            GameObject cameraGO = new GameObject("PlayerCamera");
            cameraGO.transform.SetParent(cameraPivot.transform, false);
            Camera camera = cameraGO.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            cameraGO.AddComponent<AudioListener>();

            GameObject flashlightGO = new GameObject("Flashlight");
            flashlightGO.transform.SetParent(cameraGO.transform, false);
            Light flashlight = flashlightGO.AddComponent<Light>();
            flashlight.type = LightType.Spot;
            flashlight.range = 12f;
            flashlight.spotAngle = 40f;
            flashlight.intensity = 3f;
            flashlight.enabled = false;

            FlashlightController flashlightController = player.AddComponent<FlashlightController>();
            flashlightController.Bind(flashlight);

            TestPlayerController testPlayerController = player.AddComponent<TestPlayerController>();
            testPlayerController.Bind(cameraPivot.transform, flashlightController);

            return player;
        }

        private static GameObject BuildMonster(Vector3 spawnPosition, Material material, MonsterConfig config,
            StalkPointRuntimeSet stalkSet, SafeZoneRuntimeSet safeSet, Transform player,
            PlayerVisionModel visionModel, PlayerGazeDetector gazeDetector)
        {
            GameObject monster = new GameObject("Monster");
            monster.SetActive(false); // stay inactive until every Bind() below has run — see class summary
            monster.transform.position = spawnPosition;

            GameObject bodyGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bodyGO.name = "Body";
            bodyGO.transform.SetParent(monster.transform, false);
            bodyGO.transform.localPosition = new Vector3(0f, 1f, 0f);
            bodyGO.transform.localScale = new Vector3(0.6f, 1.1f, 0.6f);
            Object.DestroyImmediate(bodyGO.GetComponent<Collider>());
            MeshRenderer bodyRenderer = bodyGO.GetComponent<MeshRenderer>();
            bodyRenderer.sharedMaterial = material;

            CapsuleCollider bodyCollider = monster.AddComponent<CapsuleCollider>();
            bodyCollider.center = new Vector3(0f, 1f, 0f);
            bodyCollider.height = 2.2f;
            bodyCollider.radius = 0.35f;

            // Pushed clearly forward of the capsule's own silhouette (radius maxes at 0.35 near the
            // neck) so player gaze/flashlight rays reach the eyes without clipping the monster's own
            // body collider first — that self-occlusion was why gaze looked "broken" (see chat).
            GameObject eyesGO = new GameObject("Eyes");
            eyesGO.transform.SetParent(monster.transform, false);
            eyesGO.transform.localPosition = new Vector3(0f, 1.9f, 0.5f);
            SphereCollider eyeCollider = eyesGO.AddComponent<SphereCollider>();
            eyeCollider.radius = 0.2f;
            eyeCollider.isTrigger = true;

            // Small visible marker so the player can actually see where to aim (bare capsule had none).
            Material eyeVisualMat = CreateEmissiveMaterial("MonsterEyeMaterial_Debug", new Color(1f, 0.9f, 0.1f));
            GameObject eyeVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eyeVisual.name = "EyeVisual";
            eyeVisual.transform.SetParent(eyesGO.transform, false);
            eyeVisual.transform.localScale = Vector3.one * 0.16f;
            Object.DestroyImmediate(eyeVisual.GetComponent<Collider>());
            eyeVisual.GetComponent<MeshRenderer>().sharedMaterial = eyeVisualMat;

            NavMeshAgent agent = monster.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 2.2f;
            agent.baseOffset = 0f;
            agent.acceleration = 12f;
            agent.angularSpeed = 720f;
            agent.stoppingDistance = 0.3f;

            MonsterNavigator navigator = monster.AddComponent<MonsterNavigator>();
            MonsterPerception perception = monster.AddComponent<MonsterPerception>();
            perception.Bind(player, visionModel);

            MonsterPositionEvaluator evaluator = monster.AddComponent<MonsterPositionEvaluator>();
            evaluator.Bind(stalkSet, safeSet, perception, player, monster.transform);

            MonsterAngerController angerController = monster.AddComponent<MonsterAngerController>();
            angerController.Bind(config);

            MonsterGazeReceiver gazeReceiver = eyesGO.AddComponent<MonsterGazeReceiver>();

            MonsterBrain brain = monster.AddComponent<MonsterBrain>();
            brain.Bind(config, perception, evaluator, angerController, navigator, gazeDetector, gazeReceiver, player);
            gazeReceiver.Bind(brain);

            MonsterVisualDriver visualDriver = bodyGO.AddComponent<MonsterVisualDriver>();
            visualDriver.Bind(bodyRenderer, brain);

            monster.SetActive(true); // all Bind() calls are done — safe to let Awake/OnEnable run now
            return monster;
        }
    }
}
