using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using HorrorEscape.Audio;
using HorrorEscape.Enemy;
using HorrorEscape.Environment;
using HorrorEscape.Interaction;
using HorrorEscape.Managers;
using HorrorEscape.Player;
using HorrorEscape.UI;

namespace HorrorEscape.Editor
{
    /// <summary>
    /// Editor utility to automatically construct a fully playable 3D Horror Escape prototype level,
    /// complete with modular rooms, locked doors, keys, battery pickups, lore notes,
    /// dynamic lighting, player rig, HUD, and Stalker AI with waypoints.
    /// </summary>
    public static class HorrorSceneBuilder
    {
        [MenuItem("Tools/Apply Authentic Backrooms Textures & Materials")]
        public static void ApplyAuthenticBackroomsMaterialsMenu()
        {
            ApplyAuthenticBackroomsMaterialsInternal(out Material _, out Material _, out Material _, out Material _);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[HorrorSceneBuilder] Authentic Backrooms wallpaper, carpet, ceiling tiles, and lighting successfully applied!");
        }

        [MenuItem("Tools/Setup Horror Gameplay in Active Scene (Backrooms)")]
        public static void SetupActiveScene()
        {
            if (!Directory.Exists("Assets/Materials")) Directory.CreateDirectory("Assets/Materials");

            ApplyAuthenticBackroomsMaterialsInternal(out Material wallMat, out Material floorMat, out Material ceilingMat, out Material trimMat);

            Material monsterMat = CreateSimpleMaterial("Assets/Materials/M_Monster.mat", new Color(0.04f, 0.04f, 0.04f));
            Material eyeMat = CreateSimpleMaterial("Assets/Materials/M_MonsterEye.mat", new Color(1.0f, 0.1f, 0.1f), true);
            Material keyMat = CreateSimpleMaterial("Assets/Materials/M_Key.mat", new Color(1.0f, 0.85f, 0.2f), true);
            Material batteryMat = CreateSimpleMaterial("Assets/Materials/M_Battery.mat", new Color(0.2f, 0.9f, 0.3f), true);
            Material almondWaterMat = CreateSimpleMaterial("Assets/Materials/M_AlmondWater.mat", new Color(0.7f, 0.9f, 0.8f), true);
            Material doorMat = CreateSimpleMaterial("Assets/Materials/M_Door.mat", new Color(0.35f, 0.22f, 0.12f));

            // Backrooms Sickly Mono-Yellow Atmosphere
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.82f, 0.79f, 0.52f); // Sickly mono-yellow tint
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.72f, 0.68f, 0.42f); // Damp yellow haze
            RenderSettings.fogDensity = 0.018f;

            // Disable any existing static cameras in the scene to avoid conflict
            foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (cam.gameObject.name != "Main Camera" || cam.transform.parent == null)
                {
                    cam.gameObject.SetActive(false);
                }
            }

            // Ensure Managers
            if (Object.FindFirstObjectByType<GameManager>() == null)
            {
                GameObject gm = new GameObject("GameManager");
                gm.AddComponent<GameManager>();
                Undo.RegisterCreatedObjectUndo(gm, "Create GameManager");
            }

            if (Object.FindFirstObjectByType<AudioManager>() == null)
            {
                GameObject am = new GameObject("AudioManager");
                am.AddComponent<AudioManager>();
                Undo.RegisterCreatedObjectUndo(am, "Create AudioManager");
            }

            // Ensure Player
            if (Object.FindFirstObjectByType<FirstPersonController>() == null)
            {
                BuildPlayerRig(new Vector3(0f, 0.2f, 0f));
            }

            // Root for interactables
            GameObject propsRoot = GameObject.Find("Interactables");
            if (propsRoot == null)
            {
                propsRoot = new GameObject("Interactables");
                Undo.RegisterCreatedObjectUndo(propsRoot, "Create Interactables Root");
            }

            // Spawn Fuses & Pickups if not present
            if (Object.FindFirstObjectByType<KeyPickup>() == null)
            {
                CreateKeyPickup(propsRoot, new Vector3(3f, 0.5f, 5f), "Fuse1", "Elevator Fuse (1/3)", keyMat, true);
                CreateKeyPickup(propsRoot, new Vector3(-8f, 0.5f, 12f), "Fuse2", "Elevator Fuse (2/3)", keyMat, true);
                CreateKeyPickup(propsRoot, new Vector3(10f, 0.5f, 18f), "Fuse3", "Elevator Fuse (3/3)", keyMat, true);
                CreateBatteryPickup(propsRoot, new Vector3(1f, 0.5f, 2f), batteryMat);
                CreateBatteryPickup(propsRoot, new Vector3(-5f, 0.5f, 15f), batteryMat);

                // Iconic Backrooms Almond Water Pickups
                CreateAlmondWaterPickup(propsRoot, new Vector3(2f, 0.5f, 3.5f), almondWaterMat);
                CreateAlmondWaterPickup(propsRoot, new Vector3(-4f, 0.5f, 11f), almondWaterMat);

                // Authentic M.E.G. & Wanderer Notes
                CreateNotePickup(propsRoot, new Vector3(0f, 0.5f, 3f),
                    "M.E.G. Field Log #04 - Level 0",
                    "Level 0: The Lobby. Non-finite mono-yellow rooms and damp rotting carpet. The fluorescent hum-buzz will drive you mad if you linger. Entities (Bacteria) react to sprinting footsteps and flashlight beams. Conserve your Almond Water and locate 3 elevator fuses to reach Level 1.");

                CreateNotePickup(propsRoot, new Vector3(8f, 0.5f, 13f),
                    "Scrawled Note on Wallpaper",
                    "DON'T RUN IN THE HALLS. It hears your steps across the partitions. Stay crouched in the dark... Almond Water keeps the headache away.");
            }

            // Spawn Master Power Switch if not present
            if (Object.FindFirstObjectByType<PowerSwitch>() == null)
            {
                CreatePowerSwitch(propsRoot, new Vector3(0f, 1.4f, 22f), Quaternion.identity);
            }

            // Spawn Exit Gate if not present
            if (Object.FindFirstObjectByType<EscapeExit>() == null)
            {
                CreateExitGate(propsRoot, new Vector3(0f, 1.5f, 25f), doorMat);
            }

            // Ensure Stalker AI (Kane Pixels Bacteria Entity)
            if (Object.FindFirstObjectByType<StalkerAI>() == null)
            {
                GameObject enemyRoot = GameObject.Find("Enemies");
                if (enemyRoot == null)
                {
                    enemyRoot = new GameObject("Enemies");
                    Undo.RegisterCreatedObjectUndo(enemyRoot, "Create Enemies Root");
                }
                BuildStalkerRig(enemyRoot, new Vector3(0f, 0.1f, 16f), monsterMat, eyeMat);
            }

            // Upgrade pickups, handheld flashlight, and survival tools (Survival Tools asset pack)
            SurvivalToolsSetup.UpgradePlayerFlashlight();
            SurvivalToolsSetup.UpgradeExistingBatteryPickups();
            SurvivalToolsSetup.EnsureSurvivalPickupsInScene();
            SurvivalToolsSetup.PlaceSurvivalLoreProps();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[HorrorSceneBuilder] Active Backrooms scene successfully configured with full horror gameplay!");
        }

        [MenuItem("Tools/Generate Main Menu Scene")]
        public static void GenerateMainMenuScene()
        {
            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");

            var menuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ambiance
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.11f, 0.08f);

            // Camera
            GameObject camGO = new GameObject("Main Camera");
            Camera cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.05f);
            camGO.AddComponent<AudioListener>();

            // Audio Manager with fluorescent hum
            GameObject amGO = new GameObject("AudioManager");
            amGO.AddComponent<AudioManager>();

            // Canvas
            GameObject canvasGO = new GameObject("MenuCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Main Menu Panel
            GameObject mainPanel = new GameObject("MainPanel");
            mainPanel.transform.SetParent(canvas.transform, false);
            RectTransform mpRt = mainPanel.AddComponent<RectTransform>();
            mpRt.anchorMin = Vector2.zero;
            mpRt.anchorMax = Vector2.one;
            mpRt.sizeDelta = Vector2.zero;

            // Title
            GameObject titleGO = new GameObject("TitleText");
            titleGO.transform.SetParent(mainPanel.transform, false);
            Text title = titleGO.AddComponent<Text>();
            title.font = font;
            title.fontSize = 44;
            title.fontStyle = FontStyle.Bold;
            title.text = "THE BACKROOMS:\nFACILITY BREACH";
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(1.0f, 0.88f, 0.45f);
            RectTransform trt = titleGO.GetComponent<RectTransform>();
            trt.anchoredPosition = new Vector2(0f, 130f);
            trt.sizeDelta = new Vector2(700f, 120f);

            // Subtitle
            GameObject subGO = new GameObject("SubtitleText");
            subGO.transform.SetParent(mainPanel.transform, false);
            Text sub = subGO.AddComponent<Text>();
            sub.font = font;
            sub.fontSize = 17;
            sub.text = "IT 402W - FINAL PROJECT (BulSU CICT)";
            sub.alignment = TextAnchor.MiddleCenter;
            sub.color = new Color(0.7f, 0.7f, 0.7f);
            RectTransform srt = subGO.GetComponent<RectTransform>();
            srt.anchoredPosition = new Vector2(0f, 50f);
            srt.sizeDelta = new Vector2(500f, 35f);

            MainMenuManager mmManager = canvasGO.AddComponent<MainMenuManager>();

            // Controls Panel
            GameObject controlsPanel = new GameObject("ControlsPanel");
            controlsPanel.transform.SetParent(canvas.transform, false);
            Image cpBg = controlsPanel.AddComponent<Image>();
            cpBg.color = new Color(0.06f, 0.06f, 0.06f, 0.96f);
            RectTransform cpRt = controlsPanel.GetComponent<RectTransform>();
            cpRt.anchorMin = Vector2.zero;
            cpRt.anchorMax = Vector2.one;
            cpRt.sizeDelta = Vector2.zero;

            GameObject cpTitleGO = new GameObject("ControlsTitle");
            cpTitleGO.transform.SetParent(controlsPanel.transform, false);
            Text cpTitle = cpTitleGO.AddComponent<Text>();
            cpTitle.font = font;
            cpTitle.fontSize = 36;
            cpTitle.fontStyle = FontStyle.Bold;
            cpTitle.text = "HOW TO PLAY & CONTROLS";
            cpTitle.alignment = TextAnchor.MiddleCenter;
            cpTitle.color = new Color(1f, 0.9f, 0.5f);
            RectTransform ctrt = cpTitleGO.GetComponent<RectTransform>();
            ctrt.anchoredPosition = new Vector2(0f, 140f);
            ctrt.sizeDelta = new Vector2(600f, 50f);

            GameObject cpBodyGO = new GameObject("ControlsBody");
            cpBodyGO.transform.SetParent(controlsPanel.transform, false);
            Text cpBody = cpBodyGO.AddComponent<Text>();
            cpBody.font = font;
            cpBody.fontSize = 17;
            cpBody.text = "WASD - Movement\nLeft Shift - Sprint (Produces Noise - Entity tracks steps!)\nLeft Ctrl / C - Crouch (Stealth - Halves Detection Distance)\nLeft Mouse Click - Melee Strike (Damages, Staggers & Stuns Entity!)\nF / Right Click - Flashlight (Conserves Battery, Flickers near Danger)\nE - Interact (Open Doors, Collect Fuses, Drink Almond Water, Pull Master Switch)\nEsc / P - Pause Menu\n\nOBJECTIVE: Find 3 Power Fuses, pull the Master Switch, and Escape Level 0!";
            cpBody.alignment = TextAnchor.MiddleCenter;
            cpBody.color = Color.white;
            RectTransform cbrt = cpBodyGO.GetComponent<RectTransform>();
            cbrt.anchoredPosition = new Vector2(0f, 0f);
            cbrt.sizeDelta = new Vector2(750f, 220f);

            CreateMenuButton(controlsPanel, font, "BACK", new Vector2(0f, -150f), () => mmManager.CloseControls());

            CreateMenuButton(mainPanel, font, "START GAME", new Vector2(0f, -25f), () => mmManager.PlayGame());
            CreateMenuButton(mainPanel, font, "HOW TO PLAY", new Vector2(0f, -85f), () => mmManager.OpenControls());
            CreateMenuButton(mainPanel, font, "QUIT", new Vector2(0f, -145f), () => mmManager.QuitGame());

            SerializedObject mmSo = new SerializedObject(mmManager);
            mmSo.FindProperty("mainPanel").objectReferenceValue = mainPanel;
            mmSo.FindProperty("controlsPanel").objectReferenceValue = controlsPanel;
            mmSo.ApplyModifiedProperties();

            controlsPanel.SetActive(false);

            string scenePath = "Assets/Scenes/MainMenu.unity";
            EditorSceneManager.SaveScene(menuScene, scenePath);

            string levelPath = File.Exists("Assets/LoafbrrAssets/BackroomsLikeAssetRe/scenes/LevelTst.unity")
                ? "Assets/LoafbrrAssets/BackroomsLikeAssetRe/scenes/LevelTst.unity"
                : "Assets/Scenes/HorrorEscapeLevel.unity";

            EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(scenePath, true),
                new EditorBuildSettingsScene(levelPath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HorrorSceneBuilder] Main Menu scene generated at " + scenePath);
        }

        private static GameObject CreateMenuButton(GameObject parent, Font font, string label, Vector2 pos, UnityEngine.Events.UnityAction action)
        {
            GameObject btnGO = new GameObject("Button_" + label);
            btnGO.transform.SetParent(parent.transform, false);
            Image img = btnGO.AddComponent<Image>();
            img.color = new Color(0.2f, 0.18f, 0.14f);
            Button btn = btnGO.AddComponent<Button>();
            btn.onClick.AddListener(action);

            RectTransform rt = btnGO.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(250f, 48f);

            GameObject txtGO = new GameObject("Text");
            txtGO.transform.SetParent(btnGO.transform, false);
            Text txt = txtGO.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = 18;
            txt.text = label;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            RectTransform trt = txtGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero;

            return btnGO;
        }

        [MenuItem("Tools/Generate Horror Escape Level (From Scratch)")]
        public static void BuildDefaultHorrorScene()
        {
            // Ensure directories exist
            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
            if (!Directory.Exists("Assets/Materials")) Directory.CreateDirectory("Assets/Materials");

            // Create new scene
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Setup Authentic Backrooms Materials
            ApplyAuthenticBackroomsMaterialsInternal(out Material wallMat, out Material floorMat, out Material ceilingMat, out Material trimMat);

            Material doorMat = CreateSimpleMaterial("Assets/Materials/M_Door.mat", new Color(0.35f, 0.22f, 0.12f));
            Material monsterMat = CreateSimpleMaterial("Assets/Materials/M_Monster.mat", new Color(0.04f, 0.04f, 0.04f));
            Material eyeMat = CreateSimpleMaterial("Assets/Materials/M_MonsterEye.mat", new Color(1.0f, 0.1f, 0.1f), true);
            Material keyMat = CreateSimpleMaterial("Assets/Materials/M_Key.mat", new Color(1.0f, 0.85f, 0.2f), true);
            Material batteryMat = CreateSimpleMaterial("Assets/Materials/M_Battery.mat", new Color(0.2f, 0.9f, 0.3f), true);

            // Configure Lighting & Atmosphere - Sickly Fluorescent Yellow Backrooms Wash
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.82f, 0.79f, 0.52f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.72f, 0.68f, 0.42f);
            RenderSettings.fogDensity = 0.018f;

            // Root Containers
            GameObject environmentRoot = new GameObject("Environment");
            GameObject propsRoot = new GameObject("Interactables");
            GameObject enemyRoot = new GameObject("Enemies");

            // 1. Build Rooms & Corridors with Wallpaper, Carpet, and Drop Ceiling
            BuildRoom(environmentRoot, new Vector3(0, 0, 0), new Vector3(10, 4, 10), floorMat, wallMat, ceilingMat, "StartRoom");
            BuildRoom(environmentRoot, new Vector3(0, 0, 13), new Vector3(4, 4, 16), floorMat, wallMat, ceilingMat, "CentralCorridor");
            BuildRoom(environmentRoot, new Vector3(10, 0, 13), new Vector3(8, 4, 8), floorMat, wallMat, ceilingMat, "StorageRoom");
            BuildRoom(environmentRoot, new Vector3(-10, 0, 13), new Vector3(8, 4, 8), floorMat, wallMat, ceilingMat, "MedicalWard");
            BuildRoom(environmentRoot, new Vector3(0, 0, 26), new Vector3(10, 4, 10), floorMat, wallMat, ceilingMat, "ExitChamber");

            // Structural Backrooms Columns / Pillars with skirting baseboards
            CreatePillar(environmentRoot, new Vector3(-3f, 2f, 2.5f), wallMat, trimMat);
            CreatePillar(environmentRoot, new Vector3(3f, 2f, 2.5f), wallMat, trimMat);
            CreatePillar(environmentRoot, new Vector3(10f, 2f, 13f), wallMat, trimMat);
            CreatePillar(environmentRoot, new Vector3(-10f, 2f, 13f), wallMat, trimMat);
            CreatePillar(environmentRoot, new Vector3(-2.5f, 2f, 26f), wallMat, trimMat);
            CreatePillar(environmentRoot, new Vector3(2.5f, 2f, 26f), wallMat, trimMat);

            // Overhead Fluorescent Lights (Warm White Tube Glow)
            CreateCeilingLight(environmentRoot, new Vector3(0, 3.5f, 0), new Color(1f, 0.98f, 0.82f), 1.2f, 12f);
            CreateCeilingLight(environmentRoot, new Vector3(0, 3.5f, 13), new Color(1f, 0.98f, 0.82f), 1.1f, 14f, true); // Flickering Hallway Light
            CreateCeilingLight(environmentRoot, new Vector3(10, 3.5f, 13), new Color(1f, 0.98f, 0.82f), 1.1f, 11f);
            CreateCeilingLight(environmentRoot, new Vector3(-10, 3.5f, 13), new Color(1f, 0.98f, 0.82f), 1.1f, 11f, true);
            CreateCeilingLight(environmentRoot, new Vector3(0, 3.5f, 26), new Color(0.9f, 0.2f, 0.2f), 1.0f, 10f); // Red Emergency Light

            // 2. Doors
            // Normal door to Storage Room
            CreateDoor(propsRoot, new Vector3(5f, 0f, 13f), Quaternion.identity, doorMat, "", "");

            // Locked door to Medical Ward (requires StorageKey)
            CreateDoor(propsRoot, new Vector3(-5f, 0f, 13f), Quaternion.identity, doorMat, "StorageKey", "Medical Keycard");

            // 3. Pickups & Objectives
            // Key to Medical Ward (hidden in Storage Room)
            CreateKeyPickup(propsRoot, new Vector3(12f, 0.6f, 15f), "StorageKey", "Medical Keycard", keyMat, false);

            // Fuse 1 (in Start Room corner)
            CreateKeyPickup(propsRoot, new Vector3(3.5f, 0.6f, -3.5f), "Fuse1", "Power Fuse (1/3)", keyMat, true);

            // Fuse 2 (in Storage Room)
            CreateKeyPickup(propsRoot, new Vector3(11f, 0.6f, 11f), "Fuse2", "Power Fuse (2/3)", keyMat, true);

            // Fuse 3 (inside locked Medical Ward)
            CreateKeyPickup(propsRoot, new Vector3(-12f, 0.6f, 15f), "Fuse3", "Power Fuse (3/3)", keyMat, true);

            // Batteries, Almond Water, First Aid & Sanity Pills (Survival Tools)
            CreateBatteryPickup(propsRoot, new Vector3(-2f, 0.4f, 2f), batteryMat);
            CreateBatteryPickup(propsRoot, new Vector3(-12f, 0.4f, 12f), batteryMat);
            CreateAlmondWaterPickup(propsRoot, new Vector3(1f, 0.4f, -1.5f), null);
            CreateAlmondWaterPickup(propsRoot, new Vector3(9f, 0.4f, 15f), null);
            CreateFirstAidPickup(propsRoot, new Vector3(-10f, 0.4f, 15f));
            CreateSanityPillsPickup(propsRoot, new Vector3(-3f, 0.4f, -2f));
            CreateSanityPillsPickup(propsRoot, new Vector3(12f, 0.4f, 12f));

            SurvivalToolsSetup.PlaceSurvivalLoreProps();

            // Lore Notes
            CreateNotePickup(propsRoot, new Vector3(0f, 0.6f, -2.5f),
                "Survivor's Note",
                "The facility went into lockdown after containment breach. The North Exit is sealed until all 3 main power fuses are restored. Don't run through the main hallway... IT lurks there and tracks footsteps!");

            CreateNotePickup(propsRoot, new Vector3(8f, 0.6f, 13f),
                "Maintenance Memo",
                "Stored the medical wing keycard in the back crate. The medical ward contains the final auxiliary fuse.");

            // 4. Power Switch & Escape Exit Gate
            CreatePowerSwitch(propsRoot, new Vector3(0f, 1.4f, 28f), Quaternion.identity);
            CreateExitGate(propsRoot, new Vector3(0f, 0f, 30.8f), doorMat);

            // 5. Build Managers
            GameObject gmGO = new GameObject("GameManager");
            gmGO.AddComponent<GameManager>();

            GameObject amGO = new GameObject("AudioManager");
            amGO.AddComponent<AudioManager>();

            // 6. Build Player
            BuildPlayerRig(new Vector3(0f, 0.2f, 0f));

            // 7. Build Stalker AI & Patrol Waypoints
            BuildStalkerRig(enemyRoot, new Vector3(0f, 0.1f, 20f), monsterMat, eyeMat);

            // Mark Environment Static for Navigation
            MarkNavigationStatic(environmentRoot);

            // Save Scene
            string scenePath = "Assets/Scenes/HorrorEscapeLevel.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);

            // Register in Build Settings
            var sceneList = new EditorBuildSettingsScene[] { new EditorBuildSettingsScene(scenePath, true) };
            EditorBuildSettings.scenes = sceneList;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[HorrorSceneBuilder] Complete Horror Escape level generated successfully at " + scenePath);
        }

        private static Material CreateSimpleMaterial(string path, Color color, bool emission = false)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader standardShader = Shader.Find("Standard");
            if (standardShader == null) standardShader = Shader.Find("Universal Render Pipeline/Lit");
            if (standardShader == null) standardShader = Shader.Find("Diffuse");

            Material mat = new Material(standardShader);
            mat.color = color;
            if (emission)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 1.5f);
            }

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void BuildRoom(GameObject root, Vector3 center, Vector3 size, Material floorMat, Material wallMat, Material ceilingMat, string name)
        {
            GameObject room = new GameObject(name);
            room.transform.SetParent(root.transform, false);
            room.transform.position = center;

            float halfX = size.x * 0.5f;
            float halfZ = size.z * 0.5f;
            float height = size.y;

            // Floor
            CreateBox(room, "Floor", Vector3.zero, new Vector3(size.x, 0.2f, size.z), floorMat);

            // Ceiling (Acoustic Drop Tiles with Fluorescent Light Panels)
            CreateBox(room, "Ceiling", new Vector3(0, height, 0), new Vector3(size.x, 0.2f, size.z), ceilingMat != null ? ceilingMat : wallMat);

            // North Wall
            CreateBox(room, "Wall_North", new Vector3(0, height * 0.5f, halfZ), new Vector3(size.x, height, 0.3f), wallMat);

            // South Wall
            CreateBox(room, "Wall_South", new Vector3(0, height * 0.5f, -halfZ), new Vector3(size.x, height, 0.3f), wallMat);

            // East Wall
            CreateBox(room, "Wall_East", new Vector3(halfX, height * 0.5f, 0), new Vector3(0.3f, height, size.z), wallMat);

            // West Wall
            CreateBox(room, "Wall_West", new Vector3(-halfX, height * 0.5f, 0), new Vector3(0.3f, height, size.z), wallMat);
        }

        public static void ApplyAuthenticBackroomsMaterialsInternal(out Material wallMat, out Material floorMat, out Material ceilingMat, out Material trimMat)
        {
            if (!Directory.Exists("Assets/Materials")) Directory.CreateDirectory("Assets/Materials");

            Texture2D wallDiff = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LoafbrrAssets/BackroomsLikeAssetRe/Textures/BRW_A/BRW_A_Diffuse_2K.png");
            Texture2D wallNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LoafbrrAssets/BackroomsLikeAssetRe/Textures/BRW_A/BRW_A_Normal_2K.png");

            Texture2D floorDiff = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LoafbrrAssets/BackroomsLikeAssetRe/Textures/BRF_A/BRF_A_Diffuse_2K.png");
            Texture2D floorNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LoafbrrAssets/BackroomsLikeAssetRe/Textures/BRF_A/BRF_A_Normal_2K.png");

            Texture2D ceilingDiff = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LoafbrrAssets/BackroomsLikeAssetRe/Textures/BRC_A/BRC_A_Diffuse_2K.png");
            Texture2D ceilingNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LoafbrrAssets/BackroomsLikeAssetRe/Textures/BRC_A/BRC_A_Normal_2K.png");
            Texture2D ceilingEmis = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LoafbrrAssets/BackroomsLikeAssetRe/Textures/BRC_A/BRC_A_Emission_2K.png");

            Texture2D trimDiff = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LoafbrrAssets/BackroomsLikeAssetRe/Textures/DWTrim/DWTrim_Diffuse_2K_A.png");
            Texture2D trimNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/LoafbrrAssets/BackroomsLikeAssetRe/Textures/DWTrim/DWTrim_Normal_2K.png");

            Shader worldTiledShader = Shader.Find("Backrooms/WorldTiled");
            Shader standardShader = Shader.Find("Standard");
            if (standardShader == null) standardShader = Shader.Find("Universal Render Pipeline/Lit");
            if (standardShader == null) standardShader = Shader.Find("Diffuse");
            Shader activeShader = worldTiledShader != null ? worldTiledShader : standardShader;

            // 1. Wall Material (Yellow chevron wallpaper)
            wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Wall.mat");
            if (wallMat == null)
            {
                wallMat = new Material(activeShader);
                AssetDatabase.CreateAsset(wallMat, "Assets/Materials/M_Wall.mat");
            }
            wallMat.shader = activeShader;
            wallMat.color = Color.white;
            if (wallDiff != null) wallMat.mainTexture = wallDiff;
            if (wallNorm != null)
            {
                wallMat.SetTexture("_BumpMap", wallNorm);
                wallMat.EnableKeyword("_NORMALMAP");
            }
            wallMat.SetFloat("_Glossiness", 0.12f);
            wallMat.SetFloat("_TileMetersX", 2.5f);
            wallMat.SetFloat("_TileMetersY", 2.5f);
            EditorUtility.SetDirty(wallMat);

            // 2. Floor Material (Damp rotting carpet)
            floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Floor.mat");
            if (floorMat == null)
            {
                floorMat = new Material(activeShader);
                AssetDatabase.CreateAsset(floorMat, "Assets/Materials/M_Floor.mat");
            }
            floorMat.shader = activeShader;
            floorMat.color = Color.white;
            if (floorDiff != null) floorMat.mainTexture = floorDiff;
            if (floorNorm != null)
            {
                floorMat.SetTexture("_BumpMap", floorNorm);
                floorMat.EnableKeyword("_NORMALMAP");
            }
            floorMat.SetFloat("_Glossiness", 0.08f);
            floorMat.SetFloat("_TileMetersX", 2.5f);
            floorMat.SetFloat("_TileMetersY", 2.5f);
            EditorUtility.SetDirty(floorMat);

            // 3. Ceiling Material (Drop ceiling tiles with glowing fluorescent panels)
            ceilingMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Ceiling.mat");
            if (ceilingMat == null)
            {
                ceilingMat = new Material(activeShader);
                AssetDatabase.CreateAsset(ceilingMat, "Assets/Materials/M_Ceiling.mat");
            }
            ceilingMat.shader = activeShader;
            ceilingMat.color = Color.white;
            if (ceilingDiff != null) ceilingMat.mainTexture = ceilingDiff;
            if (ceilingNorm != null)
            {
                ceilingMat.SetTexture("_BumpMap", ceilingNorm);
                ceilingMat.EnableKeyword("_NORMALMAP");
            }
            if (ceilingEmis != null)
            {
                ceilingMat.SetTexture("_EmissionMap", ceilingEmis);
                ceilingMat.SetColor("_EmissionColor", new Color(1.4f, 1.35f, 1.05f));
                ceilingMat.EnableKeyword("_EMISSION");
                ceilingMat.SetFloat("_UseEmission", 1.0f);
            }
            ceilingMat.SetFloat("_Glossiness", 0.15f);
            ceilingMat.SetFloat("_TileMetersX", 2.5f);
            ceilingMat.SetFloat("_TileMetersY", 2.5f);
            EditorUtility.SetDirty(ceilingMat);

            // 4. Baseboard Trim Material
            trimMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Trim.mat");
            if (trimMat == null)
            {
                trimMat = new Material(standardShader);
                AssetDatabase.CreateAsset(trimMat, "Assets/Materials/M_Trim.mat");
            }
            trimMat.shader = standardShader;
            trimMat.color = Color.white;
            if (trimDiff != null) trimMat.mainTexture = trimDiff;
            if (trimNorm != null)
            {
                trimMat.SetTexture("_BumpMap", trimNorm);
                trimMat.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(trimMat);

            // 5. Convert Asset Pack Materials to Standard shader so modular prefabs render cleanly
            FixAssetPackMaterials(standardShader);

            // 6. Assign Materials to Scene Renderers
            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            foreach (var mr in renderers)
            {
                string n = mr.gameObject.name.ToLower();
                if (n.Contains("ceiling"))
                {
                    mr.sharedMaterial = ceilingMat;
                }
                else if (n.Contains("floor"))
                {
                    mr.sharedMaterial = floorMat;
                }
                else if (n.Contains("wall"))
                {
                    mr.sharedMaterial = wallMat;
                }
            }

            // 7. Ambiance & Lighting
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.82f, 0.79f, 0.52f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.72f, 0.68f, 0.42f);
            RenderSettings.fogDensity = 0.018f;

            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var lt in lights)
            {
                if (lt.type == LightType.Point && lt.color.r > 0.3f && lt.color.g > 0.3f)
                {
                    if (!(lt.color.r > 0.8f && lt.color.g < 0.3f))
                    {
                        lt.color = new Color(1.0f, 0.98f, 0.82f);
                        lt.intensity = Mathf.Max(lt.intensity, 1.15f);
                        lt.range = Mathf.Max(lt.range, 12f);
                    }
                }
            }

            // 8. Add Structural Pillars if missing
            CreateRoomPillarsIfMissing(wallMat, trimMat);

            AssetDatabase.SaveAssets();
        }

        private static void CreateRoomPillarsIfMissing(Material wallMat, Material trimMat)
        {
            GameObject env = GameObject.Find("Environment");
            if (env == null || GameObject.Find("Pillar") != null) return;

            Transform startRoom = env.transform.Find("StartRoom");
            if (startRoom != null)
            {
                CreatePillar(startRoom.gameObject, new Vector3(-3f, 2f, 2.5f), wallMat, trimMat);
                CreatePillar(startRoom.gameObject, new Vector3(3f, 2f, 2.5f), wallMat, trimMat);
            }
            Transform storage = env.transform.Find("StorageRoom");
            if (storage != null)
            {
                CreatePillar(storage.gameObject, new Vector3(10f, 2f, 13f), wallMat, trimMat);
            }
            Transform med = env.transform.Find("MedicalWard");
            if (med != null)
            {
                CreatePillar(med.gameObject, new Vector3(-10f, 2f, 13f), wallMat, trimMat);
            }
            Transform exitChamber = env.transform.Find("ExitChamber");
            if (exitChamber != null)
            {
                CreatePillar(exitChamber.gameObject, new Vector3(-2.5f, 2f, 26f), wallMat, trimMat);
                CreatePillar(exitChamber.gameObject, new Vector3(2.5f, 2f, 26f), wallMat, trimMat);
            }
        }

        private static void CreatePillar(GameObject parent, Vector3 pos, Material wallMat, Material trimMat)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "Pillar";
            pillar.transform.SetParent(parent.transform, false);
            pillar.transform.position = pos;
            pillar.transform.localScale = new Vector3(1.3f, 4f, 1.3f);
            if (wallMat != null) pillar.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            GameObject baseboard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseboard.name = "Pillar_Baseboard";
            baseboard.transform.SetParent(pillar.transform, false);
            baseboard.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            baseboard.transform.localScale = new Vector3(1.06f, 0.1f, 1.06f);
            if (trimMat != null) baseboard.GetComponent<MeshRenderer>().sharedMaterial = trimMat;
            Object.DestroyImmediate(baseboard.GetComponent<Collider>());
        }

        private static void FixAssetPackMaterials(Shader standardShader)
        {
            string matDir = "Assets/LoafbrrAssets/BackroomsLikeAssetRe/material";
            if (!Directory.Exists(matDir)) return;

            string[] matFiles = Directory.GetFiles(matDir, "*.mat", SearchOption.TopDirectoryOnly);
            foreach (string file in matFiles)
            {
                Material m = AssetDatabase.LoadAssetAtPath<Material>(file);
                if (m == null) continue;
                if (m.shader != null && (m.shader.name.Contains("Shader Graphs") || m.shader.name.Contains("Universal") || m.shader.name == "Hidden/InternalErrorShader"))
                {
                    m.shader = standardShader;
                    EditorUtility.SetDirty(m);
                }
            }
        }

        private static GameObject CreateBox(GameObject parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent.transform, false);
            box.transform.localPosition = localPos;
            box.transform.localScale = scale;
            if (mat != null)
            {
                box.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            return box;
        }

        private static void CreateCeilingLight(GameObject parent, Vector3 pos, Color color, float intensity, float range, bool flickering = false)
        {
            GameObject lightGO = new GameObject("Light_" + (flickering ? "Flicker" : "Static"));
            lightGO.transform.SetParent(parent.transform, false);
            lightGO.transform.position = pos;

            Light lt = lightGO.AddComponent<Light>();
            lt.type = LightType.Point;
            lt.color = color;
            lt.intensity = intensity;
            lt.range = range;
            lt.shadows = LightShadows.Soft;

            if (flickering)
            {
                lightGO.AddComponent<FlickeringLight>();
            }
        }

        private static void CreateDoor(GameObject parent, Vector3 pos, Quaternion rot, Material mat, string reqKey, string keyName)
        {
            GameObject doorRoot = new GameObject(string.IsNullOrEmpty(reqKey) ? "Door_Openable" : "Door_Locked");
            doorRoot.transform.SetParent(parent.transform, false);
            doorRoot.transform.position = pos;
            doorRoot.transform.rotation = rot;

            // Hinge & Mesh
            GameObject hinge = new GameObject("Hinge");
            hinge.transform.SetParent(doorRoot.transform, false);
            hinge.transform.localPosition = new Vector3(-0.9f, 0f, 0f); // Pivot at side

            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "DoorPanel";
            panel.transform.SetParent(hinge.transform, false);
            panel.transform.localPosition = new Vector3(0.9f, 1.4f, 0f);
            panel.transform.localScale = new Vector3(1.8f, 2.8f, 0.15f);
            if (mat != null) panel.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // Door Script
            Door doorScript = doorRoot.AddComponent<Door>();
            doorRoot.AddComponent<NavMeshObstacle>().size = new Vector3(2f, 3f, 0.5f);

            // Set private serialized fields via SerializedObject
            SerializedObject so = new SerializedObject(doorScript);
            so.FindProperty("doorHinge").objectReferenceValue = hinge.transform;
            so.FindProperty("isLocked").boolValue = !string.IsNullOrEmpty(reqKey);
            so.FindProperty("requiredKeyId").stringValue = reqKey;
            so.FindProperty("keyDisplayName").stringValue = keyName;
            so.ApplyModifiedProperties();
        }

        private static void CreateKeyPickup(GameObject parent, Vector3 pos, string id, string name, Material mat, bool isObj)
        {
            GameObject keyGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            keyGO.name = "Pickup_" + id;
            keyGO.transform.SetParent(parent.transform, false);
            keyGO.transform.position = pos;
            keyGO.transform.localScale = new Vector3(0.25f, 0.1f, 0.25f);
            if (mat != null) keyGO.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var col = keyGO.GetComponent<Collider>();
            col.isTrigger = true;

            KeyPickup pickup = keyGO.AddComponent<KeyPickup>();
            SerializedObject so = new SerializedObject(pickup);
            so.FindProperty("keyId").stringValue = id;
            so.FindProperty("keyDisplayName").stringValue = name;
            so.FindProperty("isObjectiveItem").boolValue = isObj;
            so.ApplyModifiedProperties();
        }

        private static void CreateBatteryPickup(GameObject parent, Vector3 pos, Material mat)
        {
            SurvivalToolsSetup.CreateBattery(parent, pos);
        }

        private static void CreateAlmondWaterPickup(GameObject parent, Vector3 pos, Material mat)
        {
            SurvivalToolsSetup.CreateAlmondWater(parent, pos);
        }

        private static void CreateFirstAidPickup(GameObject parent, Vector3 pos)
        {
            SurvivalToolsSetup.CreateFirstAid(parent, pos);
        }

        private static void CreateSanityPillsPickup(GameObject parent, Vector3 pos)
        {
            SurvivalToolsSetup.CreateSanityPills(parent, pos);
        }

        private static void CreateNotePickup(GameObject parent, Vector3 pos, string title, string body)
        {
            GameObject noteGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            noteGO.name = "Note_" + title;
            noteGO.transform.SetParent(parent.transform, false);
            noteGO.transform.position = pos;
            noteGO.transform.localScale = new Vector3(0.4f, 0.02f, 0.5f);

            noteGO.GetComponent<Collider>().isTrigger = true;
            NotePickup note = noteGO.AddComponent<NotePickup>();

            SerializedObject so = new SerializedObject(note);
            so.FindProperty("noteTitle").stringValue = title;
            so.FindProperty("noteBody").stringValue = body;
            so.ApplyModifiedProperties();
        }

        private static void CreateExitGate(GameObject parent, Vector3 pos, Material mat)
        {
            GameObject gateGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gateGO.name = "EscapeExitGate";
            gateGO.transform.SetParent(parent.transform, false);
            gateGO.transform.position = pos;
            gateGO.transform.localScale = new Vector3(3.5f, 3.5f, 0.3f);
            if (mat != null) gateGO.GetComponent<MeshRenderer>().sharedMaterial = mat;

            gateGO.AddComponent<EscapeExit>();
        }

        private static void BuildPlayerRig(Vector3 spawnPos)
        {
            GameObject playerGO = new GameObject("Player");
            playerGO.transform.position = spawnPos;
            playerGO.tag = "Player";

            CharacterController cc = playerGO.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.4f;

            FirstPersonController fpc = playerGO.AddComponent<FirstPersonController>();
            playerGO.AddComponent<PlayerSanity>();

            // Camera
            GameObject camGO = new GameObject("Main Camera");
            camGO.transform.SetParent(playerGO.transform, false);
            camGO.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            camGO.tag = "MainCamera";

            Camera cam = camGO.AddComponent<Camera>();
            cam.fieldOfView = 75f;
            cam.nearClipPlane = 0.1f;
            camGO.AddComponent<AudioListener>();
            camGO.AddComponent<PlayerInteraction>();

            // Flashlight
            GameObject flashGO = new GameObject("Flashlight");
            flashGO.transform.SetParent(camGO.transform, false);
            flashGO.transform.localPosition = new Vector3(0.24f, -0.2f, 0.35f);

            Light flashLight = flashGO.AddComponent<Light>();
            flashLight.type = LightType.Spot;
            flashLight.spotAngle = 60f;
            flashLight.innerSpotAngle = 40f;
            flashLight.range = 22f;
            flashLight.intensity = 2.5f;
            flashLight.color = new Color(0.95f, 0.95f, 1.0f);
            flashLight.shadows = LightShadows.Hard;

            flashGO.AddComponent<FlashlightController>();

            // 3D Handheld Flashlight model (Survival Tools)
            GameObject flashPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Survival Tools/Prefabs/flashlight.prefab");
            if (flashPrefab != null)
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(flashPrefab, flashGO.transform);
                model.name = "Model_HandheldFlashlight";
                model.transform.localPosition = new Vector3(0f, -0.05f, 0f);
                model.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                model.transform.localScale = Vector3.one * 0.45f;
                var anim = model.GetComponent<Animator>();
                if (anim != null) Object.DestroyImmediate(anim);
                var col = model.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
            }

            playerGO.AddComponent<PlayerHealth>();
            PlayerCombat combat = playerGO.AddComponent<PlayerCombat>();

            // 3D Melee Weapon Model (Steel Pipe / Crowbar)
            GameObject weaponHolder = new GameObject("MeleeWeapon");
            weaponHolder.transform.SetParent(camGO.transform, false);
            weaponHolder.transform.localPosition = new Vector3(0.32f, -0.28f, 0.5f);
            weaponHolder.transform.localRotation = Quaternion.Euler(-15f, -25f, 15f);

            GameObject pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pipe.name = "SteelPipeMesh";
            pipe.transform.SetParent(weaponHolder.transform, false);
            pipe.transform.localPosition = Vector3.zero;
            pipe.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            pipe.transform.localScale = new Vector3(0.04f, 0.35f, 0.04f);
            Object.DestroyImmediate(pipe.GetComponent<Collider>()); // No collision on held weapon

            Material pipeMat = CreateSimpleMaterial("Assets/Materials/M_Pipe.mat", new Color(0.48f, 0.5f, 0.52f));
            pipe.GetComponent<MeshRenderer>().sharedMaterial = pipeMat;

            combat.SetWeaponTransform(weaponHolder.transform);

            // Hook serialized properties on FPC
            SerializedObject fpcSo = new SerializedObject(fpc);
            fpcSo.FindProperty("playerCamera").objectReferenceValue = camGO.transform;
            fpcSo.ApplyModifiedProperties();

            // HUD
            GameObject hudGO = new GameObject("HUDManager");
            hudGO.AddComponent<HUDManager>();
            hudGO.AddComponent<VHSOverlay>();
        }

        private static void CreatePowerSwitch(GameObject parent, Vector3 pos, Quaternion rot)
        {
            GameObject switchBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            switchBox.name = "GeneratorMasterSwitch";
            switchBox.transform.SetParent(parent.transform, false);
            switchBox.transform.position = pos;
            switchBox.transform.rotation = rot;
            switchBox.transform.localScale = new Vector3(0.5f, 0.7f, 0.2f);

            Material boxMat = CreateSimpleMaterial("Assets/Materials/M_SwitchBox.mat", new Color(0.2f, 0.2f, 0.25f));
            switchBox.GetComponent<MeshRenderer>().sharedMaterial = boxMat;

            GameObject lever = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lever.name = "Lever";
            lever.transform.SetParent(switchBox.transform, false);
            lever.transform.localPosition = new Vector3(0f, 0f, 0.15f);
            lever.transform.localRotation = Quaternion.Euler(-40f, 0f, 0f);
            lever.transform.localScale = new Vector3(0.06f, 0.22f, 0.06f);
            Object.DestroyImmediate(lever.GetComponent<Collider>());

            Material leverMat = CreateSimpleMaterial("Assets/Materials/M_Lever.mat", new Color(0.85f, 0.15f, 0.15f));
            lever.GetComponent<MeshRenderer>().sharedMaterial = leverMat;

            GameObject lightGO = new GameObject("IndicatorLight");
            lightGO.transform.SetParent(switchBox.transform, false);
            lightGO.transform.localPosition = new Vector3(0f, 0.25f, 0.15f);
            Light indLight = lightGO.AddComponent<Light>();
            indLight.type = LightType.Point;
            indLight.color = Color.red;
            indLight.range = 2.0f;
            indLight.intensity = 0.8f;

            PowerSwitch ps = switchBox.AddComponent<PowerSwitch>();
            SerializedObject so = new SerializedObject(ps);
            so.FindProperty("switchLever").objectReferenceValue = lever.transform;
            so.FindProperty("statusIndicatorLight").objectReferenceValue = indLight;
            so.ApplyModifiedProperties();
        }

        private static void BuildStalkerRig(GameObject root, Vector3 spawnPos, Material monsterMat, Material eyeMat)
        {
            GameObject stalkerGO = new GameObject("StalkerEnemy");
            stalkerGO.transform.SetParent(root.transform, false);
            stalkerGO.transform.position = spawnPos;

            // NavMeshAgent
            NavMeshAgent agent = stalkerGO.AddComponent<NavMeshAgent>();
            agent.height = 2.2f;
            agent.radius = 0.5f;
            agent.speed = 2.2f;
            agent.acceleration = 8.0f;
            agent.stoppingDistance = 1.2f;

            // Visual Body (Slender tall silhouette)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(stalkerGO.transform, false);
            body.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            body.transform.localScale = new Vector3(0.7f, 1.1f, 0.7f);
            if (monsterMat != null) body.GetComponent<MeshRenderer>().sharedMaterial = monsterMat;
            Object.DestroyImmediate(body.GetComponent<Collider>()); // Let agent/root handle triggers

            // Glowing Eyes
            GameObject eyeL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eyeL.name = "EyeLeft";
            eyeL.transform.SetParent(stalkerGO.transform, false);
            eyeL.transform.localPosition = new Vector3(-0.15f, 1.75f, 0.32f);
            eyeL.transform.localScale = Vector3.one * 0.1f;
            if (eyeMat != null) eyeL.GetComponent<MeshRenderer>().sharedMaterial = eyeMat;
            Object.DestroyImmediate(eyeL.GetComponent<Collider>());

            GameObject eyeR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eyeR.name = "EyeRight";
            eyeR.transform.SetParent(stalkerGO.transform, false);
            eyeR.transform.localPosition = new Vector3(0.15f, 1.75f, 0.32f);
            eyeR.transform.localScale = Vector3.one * 0.1f;
            if (eyeMat != null) eyeR.GetComponent<MeshRenderer>().sharedMaterial = eyeMat;
            Object.DestroyImmediate(eyeR.GetComponent<Collider>());

            // Red eye point light
            GameObject eyeLightGO = new GameObject("EyeGlowLight");
            eyeLightGO.transform.SetParent(stalkerGO.transform, false);
            eyeLightGO.transform.localPosition = new Vector3(0f, 1.75f, 0.4f);
            Light el = eyeLightGO.AddComponent<Light>();
            el.type = LightType.Point;
            el.color = Color.red;
            el.intensity = 1.2f;
            el.range = 3.5f;

            // StalkerAI component
            StalkerAI ai = stalkerGO.AddComponent<StalkerAI>();

            // Waypoints
            GameObject wpRoot = new GameObject("Waypoints");
            wpRoot.transform.SetParent(root.transform, false);

            Transform wp1 = CreateWaypoint(wpRoot, "WP_CentralHall", new Vector3(0f, 0f, 13f));
            Transform wp2 = CreateWaypoint(wpRoot, "WP_StorageEntrance", new Vector3(6f, 0f, 13f));
            Transform wp3 = CreateWaypoint(wpRoot, "WP_MedicalEntrance", new Vector3(-6f, 0f, 13f));
            Transform wp4 = CreateWaypoint(wpRoot, "WP_ExitHall", new Vector3(0f, 0f, 24f));

            ai.AddWaypoint(wp1);
            ai.AddWaypoint(wp2);
            ai.AddWaypoint(wp4);
            ai.AddWaypoint(wp3);
        }

        private static Transform CreateWaypoint(GameObject parent, string name, Vector3 pos)
        {
            GameObject wp = new GameObject(name);
            wp.transform.SetParent(parent.transform, false);
            wp.transform.position = pos;
            return wp.transform;
        }

        private static void MarkNavigationStatic(GameObject root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.SetStaticEditorFlags(child.gameObject,
                    StaticEditorFlags.ContributeGI |
                    StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.BatchingStatic);
            }
        }
    }
}
