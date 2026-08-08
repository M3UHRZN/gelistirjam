using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

using Game.Core;
using Game.Gameplay;
using Game.Gameplay.Monster;
using Game.Gameplay.Player;
using Game.Gameplay.World;
using Game.Gameplay.DebugTools;
using Game.Gameplay.Interaction;
using Game.Gameplay.Objectives;
namespace Game.Editor
{
    /// <summary>
    /// One-click graybox test scene for the Bracken-style monster AI: a ring corridor around a
    /// central room, with spurs to a start alcove, a dead end and a safe zone. The monster no longer
    /// needs hand-placed StalkPoints (removed — see chat, full AI rewrite) since it now patrols and
    /// flees via random NavMesh sampling instead of a scored candidate list. Builds and wires every
    /// MonoBehaviour via Bind(...) calls instead of reflection, then bakes a NavMesh and saves the
    /// scene. Run again any time to rebuild from scratch.
    ///
    /// Actors (player/monster) are built while inactive and only SetActive(true) once every Bind()
    /// call has run — MonsterBrain.OnEnable and MonsterVisualDriver.OnEnable both touch bound
    /// references immediately, so activating before wiring would throw a NullReferenceException.
    /// </summary>
    public static class MonsterAITestSceneBuilder
    {
        // Paths follow the MASTER_PROMPT §10 project skeleton (see chat: Faz 0 architecture migration).
        private const string ProjectRoot = "Assets/_Project";
        private const string ScenesFolder = ProjectRoot + "/Scenes/Levels";
        private const string DataFolder = ProjectRoot + "/Data";
        private const string ScenePath = ScenesFolder + "/MonsterAI_TestScene.unity";
        private const string ConfigAssetPath = DataFolder + "/MonsterConfig_Default.asset";
        private const string SafeSetAssetPath = DataFolder + "/SafeZoneRuntimeSet.asset";
        private const string StalkSetAssetPath = DataFolder + "/StalkPointRuntimeSet.asset";

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
            SafeZoneRuntimeSet safeSet = GetOrCreateAsset<SafeZoneRuntimeSet>(SafeSetAssetPath);
            StalkPointRuntimeSet stalkSet = GetOrCreateAsset<StalkPointRuntimeSet>(StalkSetAssetPath);

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
            List<Light> lightsEastWing = new List<Light>
            {
                CreatePointLight(level.transform, "Light_Junction", new Vector3(21, 2.6f, 7), 6f, 1f),
                CreatePointLight(level.transform, "Light_FuseRoomA", new Vector3(21, 2.6f, 17.5f), 6f, 1.1f),
                CreatePointLight(level.transform, "Light_FuseRoomB", new Vector3(21, 2.6f, -3.5f), 6f, 1.1f),
                CreatePointLight(level.transform, "Light_ExitRoom", new Vector3(32, 2.6f, 7), 6f, 1.2f),
            };

            LightZone zoneRing = BuildLightZone(level.transform, "LightZone_Ring", lightsRing);
            LightZone zoneStart = BuildLightZone(level.transform, "LightZone_Start", lightsStart);
            LightZone zoneDeadEnd = BuildLightZone(level.transform, "LightZone_DeadEnd", lightsDeadEnd);
            LightZone zoneSafe = BuildLightZone(level.transform, "LightZone_Safe", lightsSafe);
            LightZone zoneEastWing = BuildLightZone(level.transform, "LightZone_EastWing", lightsEastWing);

            BuildSafeZone(level.transform, safeMat, safeSet);

            GameObject debugRoot = new GameObject("Debug");
            LightZoneTestToggler toggler = debugRoot.AddComponent<LightZoneTestToggler>();
            toggler.Bind(new[] { zoneRing, zoneStart, zoneDeadEnd, zoneSafe, zoneEastWing });

            // NavMesh must be baked before the agent (monster) is enabled and tries to path on it.
            surface.BuildNavMesh();

            // Player spawns in the Start room; the monster spawns in the far Dead-End room instead of
            // the central room (30m apart vs. the previous 15m) so Patrol has real breathing room
            // before Interested can trigger — at 15m the monster's own 10m patrol wander could already
            // land within its 12m detection radius of the player almost immediately (see chat).
            GameObject player = BuildPlayer(new Vector3(8f, 1f, -7f));
            TestPlayerController playerController = player.GetComponent<TestPlayerController>();
            Camera playerCamera = player.GetComponentInChildren<Camera>(true);
            PlayerGazeDetector gazeDetector = playerCamera.gameObject.AddComponent<PlayerGazeDetector>();
            gazeDetector.Bind(playerCamera, config);
            PlayerInteraction playerInteraction = playerCamera.gameObject.AddComponent<PlayerInteraction>();
            playerInteraction.Bind(playerCamera);

            // NavMesh lives at y=0.05. The monster root must start on that surface; its body
            // collider/mesh are already locally offset to stand on the floor.
            BuildStalkPoints(level.transform, safeSet, stalkSet, zoneStart, zoneDeadEnd, zoneRing, zoneEastWing);

            GameObject monster = BuildMonster(new Vector3(8f, 0.05f, 23f), monsterMat, config, safeSet,
                player.transform, gazeDetector, playerController, stalkSet);

            MonsterBrain brain = monster.GetComponent<MonsterBrain>();
            MonsterDebugGizmos gizmos = monster.AddComponent<MonsterDebugGizmos>();
            gizmos.Bind(brain);

            GameObject gameManagerGO = new GameObject("GameManager");
            gameManagerGO.SetActive(false); // Bind() before OnEnable — same pattern as the monster
            GameManager gameManager = gameManagerGO.AddComponent<GameManager>();
            gameManager.Bind(playerController); // TestPlayerController implements IPlayerControlGate
            gameManagerGO.SetActive(true);

            ObjectiveManager objectiveManager = debugRoot.AddComponent<ObjectiveManager>();
            objectiveManager.SetRequiredCount(3);

            // Fuse rooms now live in the East Wing (Faz 4 map extension — see chat), not bare in the
            // Start/Central rooms, matching TGD section 40's "fuse rooms" requirement.
            // TGD section 33 specifies 3 fuses (see chat: TGD comparison found we only had 2). Third
            // box shares Fuse Room A rather than new geometry — cheap to add, no new wall coordinates.
            BuildFuseBox(level.transform, wallMat, new Vector3(21f, 1f, 17.5f), gameManager, objectiveManager);
            BuildFuseBox(level.transform, wallMat, new Vector3(19.5f, 1f, 18.5f), gameManager, objectiveManager);
            BuildFuseBox(level.transform, wallMat, new Vector3(21f, 1f, -3.5f), gameManager, objectiveManager);

            ExitController exitController = BuildExit(level.transform, wallMat, new Vector3(32f, 1f, 7f));
            exitController.Bind(gameManager);

            // Real runtime wiring lives in SceneWiring (a scene component), not here — C# event
            // subscriptions are never serialized by Unity, so setting them up only in this Editor
            // script would silently vanish the moment the scene is reopened (see chat / SceneWiring.cs
            // doc comment for the full story). LightDirector shuts one zone per completed fuse
            // (Start, then DeadEnd); Ring and the East Wing stay lit until FinalChase kills everything.
            GameObject wiringGO = new GameObject("SceneWiring");
            SceneWiring sceneWiring = wiringGO.AddComponent<SceneWiring>();
            sceneWiring.Bind(brain, gameManager, objectiveManager, exitController,
                new[] { zoneStart, zoneDeadEnd, zoneEastWing, zoneRing }, config);

            MonsterDebugHUD hud = debugRoot.AddComponent<MonsterDebugHUD>();
            hud.Bind(brain, playerController, gameManager, objectiveManager);

            Directory_CreateIfMissing(ScenesFolder);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Monster AI test scene built at {ScenePath}. Enter Play Mode to test: WASD to move, mouse to look, " +
                      "Shift to sprint, F for flashlight, hold E on a fuse box, 1-5 to kill light zones.");

            // Sanity check: 0 here means the Bind()/OnEnable registration path is broken again.
            Debug.Log($"[Sanity] SafeZoneRuntimeSet.Items = {safeSet.Items.Count}. " +
                      $"ObjectiveManager.RegisteredCount = {objectiveManager.RegisteredCount} (expected 2).");
        }

        // ---------- folders & assets ----------

        private static void EnsureFolders()
        {
            Directory_CreateIfMissing(ProjectRoot);
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
            // Widened east to cover the East Wing (junction + fuse rooms + exit room), which extends
            // to x=34.5 (see chat: Faz 4 map extension).
            floor.transform.position = new Vector3(13f, -0.1f, 8f);
            floor.transform.localScale = new Vector3(46f, 0.2f, 40f);
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
                // Outer east (gap z5.4-8.6 -> East Wing spur, added for the T/cross junction — see chat)
                new Box(15f, y, 3.2f, t, h, 4.4f),
                new Box(15f, y, 11.8f, t, h, 6.4f),

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

                // East Wing spur (from ring, x15-18.5) — leads to a combined T/cross junction (TGD
                // section 40 requires both node types; a 4-way cross reads as a T from any one
                // approach, so one construct here satisfies both — see chat).
                new Box(16.75f, y, 8.6f, 3.5f, h, t),
                new Box(16.75f, y, 5.4f, 3.5f, h, t),

                // Cross junction room (18.5-23.5, 4.5-9.5), 4 openings: west (spur, open), north
                // (Fuse Room A), south (Fuse Room B), east (Exit Room). Each opening is framed by two
                // short stub walls rather than one long wall with a gap.
                new Box(18.95f, y, 9.5f, 0.9f, h, t),
                new Box(23.05f, y, 9.5f, 0.9f, h, t),
                new Box(18.95f, y, 4.5f, 0.9f, h, t),
                new Box(23.05f, y, 4.5f, 0.9f, h, t),
                new Box(23.5f, y, 4.95f, t, h, 0.9f),
                new Box(23.5f, y, 9.05f, t, h, 0.9f),

                // North corridor: junction -> Fuse Room A
                new Box(19.4f, y, 12.5f, t, h, 6f),
                new Box(22.6f, y, 12.5f, t, h, 6f),
                // Fuse Room A (18.5-23.5, 15.5-19.5)
                new Box(18.95f, y, 15.5f, 0.9f, h, t),
                new Box(23.05f, y, 15.5f, 0.9f, h, t),
                new Box(21f, y, 19.5f, 5f, h, t),
                new Box(18.5f, y, 17.5f, t, h, 4f),
                new Box(23.5f, y, 17.5f, t, h, 4f),

                // South corridor: junction -> Fuse Room B
                new Box(19.4f, y, 1.5f, t, h, 6f),
                new Box(22.6f, y, 1.5f, t, h, 6f),
                // Fuse Room B (18.5-23.5, -5.5 to -1.5)
                new Box(18.95f, y, -1.5f, 0.9f, h, t),
                new Box(23.05f, y, -1.5f, 0.9f, h, t),
                new Box(21f, y, -5.5f, 5f, h, t),
                new Box(18.5f, y, -3.5f, t, h, 4f),
                new Box(23.5f, y, -3.5f, t, h, 4f),

                // East corridor: junction -> Exit Room
                new Box(26.5f, y, 8.6f, 6f, h, t),
                new Box(26.5f, y, 5.4f, 6f, h, t),
                // Exit Room (29.5-34.5, 4.5-9.5)
                new Box(29.5f, y, 4.95f, t, h, 0.9f),
                new Box(29.5f, y, 9.05f, t, h, 0.9f),
                new Box(32f, y, 9.5f, 5f, h, t),
                new Box(32f, y, 4.5f, 5f, h, t),
                new Box(34.5f, y, 7f, t, h, 5f),
            };

            for (int i = 0; i < walls.Count; i++)
            {
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"Wall_{i:00}";
                wall.transform.SetParent(wallsRoot.transform, false);
                wall.transform.position = walls[i].Position;
                wall.transform.localScale = walls[i].Size;
                wall.GetComponent<MeshRenderer>().sharedMaterial = material;
                // NavMeshSurface collects its own children, so Unity 6's deprecated
                // NavigationStatic flag is neither needed nor correct here.
                GameObjectUtility.SetStaticEditorFlags(wall, StaticEditorFlags.ContributeGI);
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

        // TGD section 15-16: hand-placed candidate ambush/approach points. Without these,
        // Reposition/Flank/Ambush/CutOff were unreachable dead code — StalkPointScorer.PickBest()
        // always returned null and RepositionState.Enter() bounced straight back to Interested (see
        // chat). Positions are junctions/corners/dead-ends already present in the maze geometry above.
        private static void BuildStalkPoints(Transform parent, SafeZoneRuntimeSet safeSet, StalkPointRuntimeSet stalkSet,
            LightZone zoneStart, LightZone zoneDeadEnd, LightZone zoneRing, LightZone zoneEastWing)
        {
            BuildStalkPoint(parent, "Stalk_DeadEndRoom", new Vector3(8f, 0f, 22f), safeSet, stalkSet,
                zoneDeadEnd, isDeadEnd: true, isNearCorner: false, isJunction: false);

            BuildStalkPoint(parent, "Stalk_CentralCornerNW", new Vector3(5f, 0f, 11f), safeSet, stalkSet,
                zoneRing, isDeadEnd: false, isNearCorner: true, isJunction: false);
            BuildStalkPoint(parent, "Stalk_CentralCornerSE", new Vector3(11f, 0f, 5f), safeSet, stalkSet,
                zoneRing, isDeadEnd: false, isNearCorner: true, isJunction: false);

            BuildStalkPoint(parent, "Stalk_EastJunction", new Vector3(21f, 0f, 7f), safeSet, stalkSet,
                zoneEastWing, isDeadEnd: false, isNearCorner: false, isJunction: true, allowAmbush: false);

            BuildStalkPoint(parent, "Stalk_FuseRoomACorner", new Vector3(19.5f, 0f, 16.5f), safeSet, stalkSet,
                zoneEastWing, isDeadEnd: false, isNearCorner: true, isJunction: false);
            BuildStalkPoint(parent, "Stalk_FuseRoomBCorner", new Vector3(22.5f, 0f, -4.5f), safeSet, stalkSet,
                zoneEastWing, isDeadEnd: false, isNearCorner: true, isJunction: false);

            BuildStalkPoint(parent, "Stalk_StartAlcove", new Vector3(8f, 0f, -8f), safeSet, stalkSet,
                zoneStart, isDeadEnd: true, isNearCorner: false, isJunction: false);
        }

        private static void BuildStalkPoint(Transform parent, string name, Vector3 position,
            SafeZoneRuntimeSet safeSet, StalkPointRuntimeSet stalkSet, LightZone lightZone,
            bool isDeadEnd, bool isNearCorner, bool isJunction, bool allowAmbush = true)
        {
            GameObject go = new GameObject(name);
            go.SetActive(false); // Bind() before OnEnable, same pattern as SafeZone/FuseBox above
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            StalkPoint point = go.AddComponent<StalkPoint>();
            point.ConfigureTopology(isNearCorner, isJunction, isDeadEnd, allowAmbush);
            point.Bind(safeSet, lightZone, stalkSet);
            go.SetActive(true);
        }

        private static void BuildFuseBox(Transform parent, Material material, Vector3 position,
            GameManager gameManager, ObjectiveManager objectiveManager)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "FuseBox";
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.5f, 0.7f, 0.3f);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;

            FuseBox fuseBox = go.AddComponent<FuseBox>();
            fuseBox.Bind(gameManager, objectiveManager);
        }

        private static ExitController BuildExit(Transform parent, Material material, Vector3 position)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Exit";
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(1.2f, 2.2f, 0.2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.GetComponent<BoxCollider>().isTrigger = true;

            return go.AddComponent<ExitController>();
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
            SafeZoneRuntimeSet safeSet, Transform player, PlayerGazeDetector gazeDetector,
            IPlayerVelocitySource velocitySource, StalkPointRuntimeSet stalkSet)
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

            // Gaze hitbox is a large sphere centered on the body — big and roughly omnidirectional
            // so the player can "spot" the monster from any angle it happens to be facing. A small
            // forward-offset point (the original design) only worked when the monster's front was
            // toward the player; since it wanders and turns freely, that made the raycast hit/miss
            // flicker frame to frame and cascade into constant state thrashing (see chat).
            GameObject gazeVolumeGO = new GameObject("GazeVolume");
            gazeVolumeGO.transform.SetParent(monster.transform, false);
            gazeVolumeGO.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            SphereCollider gazeCollider = gazeVolumeGO.AddComponent<SphereCollider>();
            gazeCollider.radius = 0.7f;
            gazeCollider.isTrigger = true;

            // Small visible marker purely for flavor — no collider, plays no role in gaze detection.
            Material eyeVisualMat = CreateEmissiveMaterial("MonsterEyeMaterial_Debug", new Color(1f, 0.9f, 0.1f));
            GameObject eyeVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eyeVisual.name = "EyeVisual";
            eyeVisual.transform.SetParent(monster.transform, false);
            eyeVisual.transform.localPosition = new Vector3(0f, 1.9f, 0.5f);
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
            navigator.Bind(safeSet);

            MonsterPerception perception = monster.AddComponent<MonsterPerception>();
            perception.Bind(player, velocitySource);

            MonsterAngerController angerController = monster.AddComponent<MonsterAngerController>();
            angerController.Bind(config);

            MonsterGazeReceiver gazeReceiver = gazeVolumeGO.AddComponent<MonsterGazeReceiver>();

            MonsterBrain brain = monster.AddComponent<MonsterBrain>();
            brain.Bind(config, perception, angerController, navigator, gazeDetector, gazeReceiver, player);
            gazeReceiver.Bind(brain);

            // TGD section 13-17: wire the vision model + StalkPoint set that Reposition/Flank/Ambush/
            // CutOff need — previously left unbound, which silently made those states dead code (see
            // chat: they'd instantly bounce back to Interested every time). PlayerVisionModel lives on
            // the player (it models what the player can see), not the monster.
            PlayerVisionModel visionModel = player.gameObject.AddComponent<PlayerVisionModel>();
            visionModel.Bind(player, perception);
            brain.BindStalkSystems(stalkSet, visionModel);

            MonsterVisualDriver visualDriver = bodyGO.AddComponent<MonsterVisualDriver>();
            visualDriver.Bind(bodyRenderer, brain);

            monster.SetActive(true); // all Bind() calls are done — safe to let Awake/OnEnable run now
            return monster;
        }
    }
}
