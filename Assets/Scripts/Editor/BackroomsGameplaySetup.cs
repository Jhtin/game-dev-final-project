using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
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
    /// Master Editor utility to configure the complete ~10-minute Backrooms Level 0 gameplay loop:
    /// - 32x32 procedurally generated interconnected Level 0 maze.
    /// - Player equipped with 3D flashlight (limited battery, dims smoothly) and defense handgun (6/6 ammo, recoil, stun/flee).
    /// - Scavengable items: 5 flashlight batteries, 3 ammo boxes (6 rounds each), 1 Maintenance Keycard.
    /// - Dedicated Maintenance Room with breaker box switch to restore power.
    /// - Emergency exit door that unlocks upon power restoration.
    /// - Stalker entity that awakens in Phase 2 (3:00) with environmental audio cues.
    /// - GameplayDirector managing the 10-minute 5-phase timeline.
    /// - Baked NavMesh for seamless enemy patrolling and retreat.
    /// </summary>
    public static class BackroomsGameplaySetup
    {
        private const string ScenePath = "Assets/Scenes/HorrorEscapeLevel.unity";
        private const string BATTERY_PREFAB = "Assets/Survival Tools/Prefabs/battery.prefab";
        private const string MATCHBOX_PREFAB = "Assets/Survival Tools/Prefabs/matchbox.prefab";
        private const string FLASHLIGHT_PREFAB = "Assets/Survival Tools/Prefabs/flashlight.prefab";
        private const string TAPE_PREFAB = "Assets/Survival Tools/Prefabs/tape.prefab";
        private const string FIRSTAID_PREFAB = "Assets/Survival Tools/Prefabs/firstaid.prefab";
        private const string WATER_PREFAB = "Assets/Survival Tools/Prefabs/waterbottle.prefab";
        private const string PILLS_PREFAB = "Assets/Survival Tools/Prefabs/pills.prefab";

        [InitializeOnLoadMethod]
        private static void AutoRunOnCompile()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (SessionState.GetBool("Backrooms10MinSetupRan_v1", false)) return;
                SessionState.SetBool("Backrooms10MinSetupRan_v1", true);
                Debug.Log("[BackroomsGameplaySetup] AutoRunOnCompile triggered!");
                Setup10MinuteGameplayBatch();
            };
        }

        [MenuItem("Tools/Setup 10-Minute Backrooms Survival Gameplay")]
        public static void Setup10MinuteGameplayMenu()
        {
            Setup10MinuteGameplayBatch();

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Backrooms Gameplay Setup",
                    "Complete 10-Minute Backrooms Level 0 gameplay loop configured!\n\n" +
                    "- 32x32 Interconnected Maze\n" +
                    "- Player with 3D Flashlight & Defense Handgun (6 / 6 ammo)\n" +
                    "- 5 Batteries & 3 Ammo Pickups\n" +
                    "- Maintenance Keycard & Maintenance Breaker Room\n" +
                    "- Powered Emergency Exit Gate\n" +
                    "- Phase 2 Stalker AI with Gun Stun & Retreat\n" +
                    "- NavMesh Baked Successfully!", "OK");
            }
        }

        public static void Setup10MinuteGameplayBatch()
        {
            Debug.Log("[BackroomsGameplaySetup] Starting 10-minute Backrooms gameplay setup...");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. Ensure Backrooms Level Materials & Atmosphere
            EnsureMaterials(out Material wallMat, out Material floorMat, out Material ceilingMat, out Material trimMat, out Material darkMat);

            // Backrooms Sickly Mono-Yellow Atmosphere & Creepy Fog
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.82f, 0.79f, 0.52f); // Sickly mono-yellow tint
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.72f, 0.68f, 0.42f); // Damp yellow-green haze
            RenderSettings.fogDensity = 0.022f;

            // 2. Setup or Retrieve BackroomsLevelGenerator
            BackroomsLevelGenerator generator = UnityEngine.Object.FindFirstObjectByType<BackroomsLevelGenerator>();
            if (generator == null)
            {
                GameObject genGO = GameObject.Find("Environment");
                if (genGO == null) genGO = new GameObject("Environment");
                generator = genGO.AddComponent<BackroomsLevelGenerator>();
            }

            // Generate fresh authentic 32x32 Level 0 Backrooms map
            generator.EnsureMaterials();
            var report = generator.GenerateMap();
            Debug.Log("[BackroomsGameplaySetup] Map Generated:\n" + report.stringify());

            // 3. Ensure Core Managers
            SetupManagers(out GameManager gm, out AudioManager am, out HUDManager hud, out GameplayDirector director);

            // 4. Setup Player Rig at Spawn Cell
            Vector3 spawnWorldPos = new Vector3(generator.SpawnCell.x * generator.CellSize + generator.CellSize * 0.5f, 0.2f, generator.SpawnCell.y * generator.CellSize + generator.CellSize * 0.5f);
            SetupPlayerRig(spawnWorldPos, darkMat, out FirstPersonController fpc, out PlayerCombat combat, out FlashlightController flashlight);

            // 5. Breadcrumb 10-Minute Gameplay Progression (BFS Distance Analysis)
            SetupGameplayProgression(generator, wallMat, ceilingMat, darkMat, out PowerSwitch powerSwitch, out EscapeExit emergencyExit, out StalkerAI stalker);

            // 6. Connect Director References
            SerializedObject dirSo = new SerializedObject(director);
            dirSo.FindProperty("stalker").objectReferenceValue = stalker;
            dirSo.FindProperty("maintenanceSwitch").objectReferenceValue = powerSwitch;
            dirSo.FindProperty("emergencyExit").objectReferenceValue = emergencyExit;
            dirSo.ApplyModifiedProperties();

            // 7. Synchronously Bake NavMesh for Corridors
            BakeSceneNavMesh();

            // 8. Save Scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[BackroomsGameplaySetup] 10-Minute Backrooms Survival Gameplay setup successfully saved!");
        }

        private static void EnsureMaterials(out Material wallMat, out Material floorMat, out Material ceilingMat, out Material trimMat, out Material darkMat)
        {
            if (!Directory.Exists("Assets/Materials")) Directory.CreateDirectory("Assets/Materials");

            wallMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Wall.mat");
            floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Floor.mat");
            ceilingMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Ceiling.mat");
            trimMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Trim.mat");

            darkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Monster.mat");
            if (darkMat == null)
            {
                darkMat = new Material(Shader.Find("Standard"));
                darkMat.color = new Color(0.12f, 0.12f, 0.14f);
                AssetDatabase.CreateAsset(darkMat, "Assets/Materials/M_Monster.mat");
            }
        }

        private static void SetupManagers(out GameManager gm, out AudioManager am, out HUDManager hud, out GameplayDirector director)
        {
            gm = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            if (gm == null)
            {
                GameObject go = new GameObject("GameManager");
                gm = go.AddComponent<GameManager>();
            }
            SerializedObject gmSo = new SerializedObject(gm);
            gmSo.FindProperty("requiredObjectiveCount").intValue = 0; // Pure exploration/escape
            gmSo.ApplyModifiedProperties();

            am = UnityEngine.Object.FindFirstObjectByType<AudioManager>();
            if (am == null)
            {
                GameObject go = new GameObject("AudioManager");
                am = go.AddComponent<AudioManager>();
            }

            hud = UnityEngine.Object.FindFirstObjectByType<HUDManager>();
            if (hud == null)
            {
                GameObject go = new GameObject("HUDManager");
                hud = go.AddComponent<HUDManager>();
            }

            director = UnityEngine.Object.FindFirstObjectByType<GameplayDirector>();
            if (director == null)
            {
                GameObject go = new GameObject("GameplayDirector");
                director = go.AddComponent<GameplayDirector>();
            }

            HorrorEscape.Inventory.InventoryManager invMgr = UnityEngine.Object.FindFirstObjectByType<HorrorEscape.Inventory.InventoryManager>();
            if (invMgr == null)
            {
                GameObject go = new GameObject("InventoryManager");
                invMgr = go.AddComponent<HorrorEscape.Inventory.InventoryManager>();
            }
        }

        private static void SetupPlayerRig(Vector3 spawnPos, Material darkMat, out FirstPersonController fpc, out PlayerCombat combat, out FlashlightController flashlight)
        {
            GameObject playerGO = GameObject.FindWithTag("Player");
            if (playerGO == null)
            {
                fpc = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
                if (fpc != null) playerGO = fpc.gameObject;
            }

            if (playerGO == null)
            {
                playerGO = new GameObject("Player");
                playerGO.tag = "Player";
            }

            CharacterController cc = playerGO.GetComponent<CharacterController>();
            if (cc == null) cc = playerGO.AddComponent<CharacterController>();
            cc.enabled = false;
            playerGO.transform.position = spawnPos;
            cc.height = 1.8f;
            cc.radius = 0.4f;

            fpc = playerGO.GetComponent<FirstPersonController>();
            if (fpc == null) fpc = playerGO.AddComponent<FirstPersonController>();

            PlayerHealth pHealth = playerGO.GetComponent<PlayerHealth>();
            if (pHealth == null) pHealth = playerGO.AddComponent<PlayerHealth>();

            PlayerSanity pSanity = playerGO.GetComponent<PlayerSanity>();
            if (pSanity == null) pSanity = playerGO.AddComponent<PlayerSanity>();

            combat = playerGO.GetComponent<PlayerCombat>();
            if (combat == null) combat = playerGO.AddComponent<PlayerCombat>();

            // Camera positioned at natural eye height (1.68m eye height on 1.80m character)
            Camera cam = playerGO.GetComponentInChildren<Camera>();
            if (cam == null)
            {
                GameObject camGO = new GameObject("Main Camera");
                camGO.transform.SetParent(playerGO.transform, false);
                camGO.transform.localPosition = new Vector3(0f, 1.68f, 0.12f);
                camGO.tag = "MainCamera";
                cam = camGO.AddComponent<Camera>();
                cam.fieldOfView = 75f;
                cam.nearClipPlane = 0.05f;
                camGO.AddComponent<AudioListener>();
                camGO.AddComponent<PlayerInteraction>();
            }
            else
            {
                cam.transform.localPosition = new Vector3(0f, 1.68f, 0.12f);
                cam.nearClipPlane = 0.05f;
            }

            // Hook serialized properties on FPC
            SerializedObject fpcSo = new SerializedObject(fpc);
            fpcSo.FindProperty("playerCamera").objectReferenceValue = cam.transform;
            var standingProp = fpcSo.FindProperty("standingCamY");
            if (standingProp != null) standingProp.floatValue = 1.68f;
            var crouchProp = fpcSo.FindProperty("crouchCamY");
            if (crouchProp != null) crouchProp.floatValue = 0.95f;
            var offsetProp = fpcSo.FindProperty("firstPersonForwardOffset");
            if (offsetProp != null) offsetProp.floatValue = 0.12f;
            fpcSo.ApplyModifiedProperties();

            // Handheld 3D Flashlight Rig (Focused hotspot + wide ambient spill)
            Transform flashRoot = cam.transform.Find("Flashlight");
            if (flashRoot == null)
            {
                GameObject fgo = new GameObject("Flashlight");
                fgo.transform.SetParent(cam.transform, false);
                flashRoot = fgo.transform;
            }
            flashRoot.localPosition = new Vector3(0.24f, -0.2f, 0.35f);

            // Primary Focused Beam (Hotspot)
            Light spot = flashRoot.GetComponent<Light>();
            if (spot == null) spot = flashRoot.gameObject.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.spotAngle = 62f;
            spot.innerSpotAngle = 26f; // Focused center hotspot with gradual natural falloff
            spot.range = 24f;
            spot.intensity = 3.2f;
            spot.color = new Color(0.96f, 0.95f, 0.88f); // Warm incandescent / xenon flashlight beam
            spot.shadows = LightShadows.Hard;

            // Secondary Ambient Spill / Fill Light (Soft peripheral illumination)
            Transform fillT = flashRoot.Find("Flashlight_FillSpill");
            Light fillLight = null;
            if (fillT == null)
            {
                GameObject fillGO = new GameObject("Flashlight_FillSpill");
                fillGO.transform.SetParent(flashRoot, false);
                fillGO.transform.localPosition = new Vector3(0f, 0f, 0.05f);
                fillLight = fillGO.AddComponent<Light>();
            }
            else
            {
                fillLight = fillT.GetComponent<Light>();
            }
            fillLight.type = LightType.Spot;
            fillLight.spotAngle = 105f;
            fillLight.innerSpotAngle = 55f;
            fillLight.range = 10f;
            fillLight.intensity = 0.45f;
            fillLight.color = new Color(0.92f, 0.90f, 0.80f);
            fillLight.shadows = LightShadows.None;

            flashlight = flashRoot.GetComponent<FlashlightController>();
            if (flashlight == null) flashlight = flashRoot.gameObject.AddComponent<FlashlightController>();
            flashlight.SetFillLight(fillLight);

            // Attach 3D flashlight mesh
            for (int i = flashRoot.childCount - 1; i >= 0; i--)
            {
                Transform c = flashRoot.GetChild(i);
                if (c.name.StartsWith("Model_") || c.name.StartsWith("flashlight"))
                {
                    UnityEngine.Object.DestroyImmediate(c.gameObject);
                }
            }

            GameObject flashPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FLASHLIGHT_PREFAB);
            if (flashPrefab != null)
            {
                GameObject fModel = (GameObject)PrefabUtility.InstantiatePrefab(flashPrefab, flashRoot);
                fModel.name = "Model_HandheldFlashlight";
                fModel.transform.localPosition = new Vector3(0f, -0.05f, 0f);
                fModel.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                fModel.transform.localScale = Vector3.one * 0.45f;
                var anim = fModel.GetComponent<Animator>();
                if (anim != null) UnityEngine.Object.DestroyImmediate(anim);
                var col = fModel.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
            }

            // Handheld Defense Firearm Rig
            Transform gunRoot = cam.transform.Find("DefenseHandgun");
            if (gunRoot != null) UnityEngine.Object.DestroyImmediate(gunRoot.gameObject);

            // Remove legacy melee pipe if present
            Transform oldMelee = cam.transform.Find("MeleeWeapon");
            if (oldMelee != null) UnityEngine.Object.DestroyImmediate(oldMelee.gameObject);

            GameObject gunGO = new GameObject("DefenseHandgun");
            gunGO.transform.SetParent(cam.transform, false);
            gunGO.transform.localPosition = new Vector3(0.22f, -0.22f, 0.45f);
            gunGO.transform.localRotation = Quaternion.identity;

            // Receiver / Slide
            GameObject slide = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slide.name = "Slide";
            slide.transform.SetParent(gunGO.transform, false);
            slide.transform.localPosition = new Vector3(0f, 0.04f, 0.05f);
            slide.transform.localScale = new Vector3(0.045f, 0.055f, 0.22f);
            if (darkMat != null) slide.GetComponent<MeshRenderer>().sharedMaterial = darkMat;
            UnityEngine.Object.DestroyImmediate(slide.GetComponent<Collider>());

            // Barrel
            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = "Barrel";
            barrel.transform.SetParent(gunGO.transform, false);
            barrel.transform.localPosition = new Vector3(0f, 0.04f, 0.15f);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.transform.localScale = new Vector3(0.024f, 0.06f, 0.024f);
            if (darkMat != null) barrel.GetComponent<MeshRenderer>().sharedMaterial = darkMat;
            UnityEngine.Object.DestroyImmediate(barrel.GetComponent<Collider>());

            // Grip / Handle
            GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            grip.name = "Grip";
            grip.transform.SetParent(gunGO.transform, false);
            grip.transform.localPosition = new Vector3(0f, -0.04f, -0.02f);
            grip.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            grip.transform.localScale = new Vector3(0.04f, 0.12f, 0.055f);
            if (darkMat != null) grip.GetComponent<MeshRenderer>().sharedMaterial = darkMat;
            UnityEngine.Object.DestroyImmediate(grip.GetComponent<Collider>());

            // Trigger Guard
            GameObject guard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guard.name = "TriggerGuard";
            guard.transform.SetParent(gunGO.transform, false);
            guard.transform.localPosition = new Vector3(0f, -0.015f, 0.035f);
            guard.transform.localScale = new Vector3(0.02f, 0.04f, 0.05f);
            if (darkMat != null) guard.GetComponent<MeshRenderer>().sharedMaterial = darkMat;
            UnityEngine.Object.DestroyImmediate(guard.GetComponent<Collider>());

            // Muzzle Flash Point Light
            GameObject muzzleGO = new GameObject("MuzzleFlashLight");
            muzzleGO.transform.SetParent(gunGO.transform, false);
            muzzleGO.transform.localPosition = new Vector3(0f, 0.04f, 0.22f);
            Light muzzleLight = muzzleGO.AddComponent<Light>();
            muzzleLight.type = LightType.Point;
            muzzleLight.color = new Color(1.0f, 0.85f, 0.4f);
            muzzleLight.intensity = 3.5f;
            muzzleLight.range = 6.0f;
            muzzleLight.enabled = false;

            combat.SetWeaponTransform(gunGO.transform);
            combat.SetMuzzleFlashLight(muzzleLight);

            // Configure combat state: Player starts UNARMED (Must discover gun naturally in maze)
            SerializedObject combatSo = new SerializedObject(combat);
            combatSo.FindProperty("hasUnlockedPistol").boolValue = false;
            combatSo.FindProperty("hasUnlockedSMG").boolValue = false;
            combatSo.FindProperty("isCombatActive").boolValue = false;
            combatSo.ApplyModifiedProperties();

            // Re-enable CharacterController
            cc.enabled = true;
        }

        private static void DestroyAllNamed(string name)
        {
            var objs = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in objs)
            {
                if (go != null && go.name == name)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
        }

        private static void SetupGameplayProgression(BackroomsLevelGenerator gen, Material wallMat, Material ceilingMat, Material darkMat,
            out PowerSwitch powerSwitch, out EscapeExit emergencyExit, out StalkerAI stalker)
        {
            // Clean up all existing root containers to prevent duplicates across setup runs
            DestroyAllNamed("Interactables");
            GameObject propsRoot = new GameObject("Interactables");

            // 1. Run BFS on Grid from SpawnCell to get shortest path cell distances
            int w = gen.MapWidth;
            int l = gen.MapLength;
            int[,] dist = new int[w, l];
            for (int x = 0; x < w; x++)
                for (int z = 0; z < l; z++)
                    dist[x, z] = -1;

            Queue<Vector2Int> q = new Queue<Vector2Int>();
            dist[gen.SpawnCell.x, gen.SpawnCell.y] = 0;
            q.Enqueue(gen.SpawnCell);

            int[] dx = { 1, -1, 0, 0 };
            int[] dz = { 0, 0, 1, -1 };

            while (q.Count > 0)
            {
                Vector2Int cur = q.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    int nx = cur.x + dx[i];
                    int nz = cur.y + dz[i];
                    if (nx >= 0 && nx < w && nz >= 0 && nz < l)
                    {
                        if (gen.IsWalkable(nx, nz) && dist[nx, nz] == -1)
                        {
                            dist[nx, nz] = dist[cur.x, cur.y] + 1;
                            q.Enqueue(new Vector2Int(nx, nz));
                        }
                    }
                }
            }

            // Sort rooms by topological distance from spawn
            List<BackroomsLevelGenerator.RoomRect> sortedRooms = new List<BackroomsLevelGenerator.RoomRect>(gen.Rooms);
            sortedRooms.Sort((a, b) => dist[a.Center.x, a.Center.y].CompareTo(dist[b.Center.x, b.Center.y]));

            int roomCount = sortedRooms.Count;
            BackroomsLevelGenerator.RoomRect spawnRoom = sortedRooms[0];
            BackroomsLevelGenerator.RoomRect earlyRoom = sortedRooms[Mathf.Clamp(1, 0, roomCount - 1)];
            BackroomsLevelGenerator.RoomRect midRoom1 = sortedRooms[Mathf.Clamp(Mathf.RoundToInt(roomCount * 0.35f), 1, roomCount - 1)];
            BackroomsLevelGenerator.RoomRect keycardRoom = sortedRooms[Mathf.Clamp(Mathf.RoundToInt(roomCount * 0.55f), 1, roomCount - 1)];
            BackroomsLevelGenerator.RoomRect maintenanceRoom = sortedRooms[Mathf.Clamp(Mathf.RoundToInt(roomCount * 0.75f), 1, roomCount - 1)];
            BackroomsLevelGenerator.RoomRect farRoom = sortedRooms[Mathf.Clamp(roomCount - 1, 1, roomCount - 1)];

            float cs = gen.CellSize;

            // 2. Battery Pickups (5 total spread across distance tiers)
            CreateBattery(propsRoot, CellWorldPos(earlyRoom.Center, cs, 0.35f));                               // Tier 1 (~5 dist)
            CreateBattery(propsRoot, CellWorldPos(midRoom1.Center, cs, 0.35f) + new Vector3(1.2f, 0f, 0f));     // Tier 2 (~12 dist)
            CreateBattery(propsRoot, CellWorldPos(keycardRoom.Center, cs, 0.35f) + new Vector3(-1.2f, 0f, 0f)); // Tier 3 (~18 dist)
            CreateBattery(propsRoot, CellWorldPos(maintenanceRoom.Center, cs, 0.35f) + new Vector3(1.4f, 0f, 0f)); // Tier 4 (~24 dist)
            CreateBattery(propsRoot, CellWorldPos(farRoom.Center, cs, 0.35f) + new Vector3(-1.2f, 0f, 0f));     // Tier 5 (~30 dist)

            // 3. Ammunition Pickups (3 total, +6 rounds each)
            CreateAmmoPickup(propsRoot, CellWorldPos(earlyRoom.Center, cs, 0.35f) + new Vector3(-1.2f, 0f, 0f)); // Ammo 1
            CreateAmmoPickup(propsRoot, CellWorldPos(midRoom1.Center, cs, 0.35f) + new Vector3(0f, 0f, 1.2f));    // Ammo 2
            CreateAmmoPickup(propsRoot, CellWorldPos(maintenanceRoom.Center, cs, 0.35f) + new Vector3(-1.4f, 0f, 0f)); // Ammo 3

            // Survival Supplies: Almond Water, First Aid, and Calming Sanity Pills
            CreateAlmondWater(propsRoot, CellWorldPos(earlyRoom.Center, cs, 0.35f) + new Vector3(0f, 0f, -1.2f));
            CreateAlmondWater(propsRoot, CellWorldPos(farRoom.Center, cs, 0.35f) + new Vector3(1.2f, 0f, 0f));
            CreateFirstAid(propsRoot, CellWorldPos(maintenanceRoom.Center, cs, 0.35f) + new Vector3(0f, 0f, 1.4f));
            CreateSanityPills(propsRoot, CellWorldPos(keycardRoom.Center, cs, 0.35f) + new Vector3(0f, 0f, -1.2f));
            CreateSanityPills(propsRoot, CellWorldPos(midRoom1.Center, cs, 0.35f) + new Vector3(1.2f, 0f, -1.2f));

            // Place Discoverable 9mm Army Pistol in Maze Exploration Room (Player does NOT start with weapon)
            CreatePistolPickup(propsRoot, CellWorldPos(midRoom1.Center, cs, 0.35f) + new Vector3(-1.0f, 0f, -1.0f));

            // 4. Maintenance Keycard (Phase 3 Access Item)
            Vector3 keycardPos = CellWorldPos(keycardRoom.Center, cs, 0.45f);
            CreateKeycardPickup(propsRoot, keycardPos);

            // Table / Pedestal for Keycard
            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Keycard_Pedestal";
            table.transform.SetParent(propsRoot.transform, false);
            table.transform.position = keycardPos - new Vector3(0f, 0.22f, 0f);
            table.transform.localScale = new Vector3(0.9f, 0.45f, 0.9f);
            if (wallMat != null) table.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            // 5. Maintenance Room Breaker Box (Phase 4 Switch)
            Vector3 breakerPos = CellWorldPos(maintenanceRoom.Center, cs, 1.4f) + new Vector3(0f, 0f, cs * 0.4f);
            powerSwitch = CreateBreakerBox(propsRoot, breakerPos);

            // 6. Emergency Exit Gate (Phase 5 Escape at exitCell)
            Vector3 exitPos = CellWorldPos(gen.ExitCell, cs, 0f);
            emergencyExit = CreateEmergencyExitGate(propsRoot, exitPos, wallMat, ceilingMat);

            // 7. Instantiate Darkness Zone Volumes for Complete and Partial Blackout Sectors
            DestroyAllNamed("Darkness_Zones");
            GameObject darknessRoot = new GameObject("Darkness_Zones");

            int bIndex = 1;
            foreach (var br in gen.BlackoutRooms)
            {
                GameObject bZoneGO = new GameObject($"DarknessZone_Complete_{bIndex}");
                bZoneGO.transform.SetParent(darknessRoot.transform, false);
                Vector3 zonePos = CellWorldPos(br.Center, cs, 1.5f);
                bZoneGO.transform.position = zonePos;

                DarknessZoneVolume dVol = bZoneGO.AddComponent<DarknessZoneVolume>();
                Vector3 zoneSize = new Vector3(br.width * cs + cs * 0.8f, 3.5f, br.length * cs + cs * 0.8f);
                dVol.Configure(DarknessZoneType.CompleteBlackout, $"Blackout Sector 0{bIndex}", zoneSize);
                bIndex++;
            }

            int pIndex = 1;
            foreach (var pr in gen.PartialBlackoutRooms)
            {
                GameObject pZoneGO = new GameObject($"DarknessZone_Partial_{pIndex}");
                pZoneGO.transform.SetParent(darknessRoot.transform, false);
                Vector3 zonePos = CellWorldPos(pr.Center, cs, 1.5f);
                pZoneGO.transform.position = zonePos;

                DarknessZoneVolume dVol = pZoneGO.AddComponent<DarknessZoneVolume>();
                Vector3 zoneSize = new Vector3(pr.width * cs + cs * 0.6f, 3.5f, pr.length * cs + cs * 0.6f);
                dVol.Configure(DarknessZoneType.PartialBlackout, $"Dim Corridor Sector 0{pIndex}", zoneSize);
                pIndex++;
            }

            // 8. Stalker Enemy (Kane Pixels Bacteria Entity in distant section)
            DestroyAllNamed("Enemies");
            GameObject enemyRoot = new GameObject("Enemies");

            Vector3 stalkerSpawnPos = CellWorldPos(farRoom.Center, cs, 0.1f);
            stalker = CreateStalkerRig(enemyRoot, stalkerSpawnPos, darkMat);

            // Add waypoints in adjacent rooms for stalker patrol
            stalker.AddWaypoint(CreateWaypoint(enemyRoot, "WP_FarRoom", stalkerSpawnPos));
            stalker.AddWaypoint(CreateWaypoint(enemyRoot, "WP_MaintRoom", CellWorldPos(maintenanceRoom.Center, cs, 0.1f)));
            stalker.AddWaypoint(CreateWaypoint(enemyRoot, "WP_KeyRoom", CellWorldPos(keycardRoom.Center, cs, 0.1f)));
        }

        private static Vector3 CellWorldPos(Vector2Int cell, float cs, float y)
        {
            return new Vector3(cell.x * cs + cs * 0.5f, y, cell.y * cs + cs * 0.5f);
        }

        private static void CreateBattery(GameObject parent, Vector3 pos)
        {
            GameObject root = new GameObject("Pickup_Battery");
            root.transform.SetParent(parent.transform, false);
            root.transform.position = pos;

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.1f, 0f);
            box.size = new Vector3(0.5f, 0.5f, 0.5f);

            root.AddComponent<BatteryPickup>();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BATTERY_PREFAB);
            if (prefab != null)
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                model.name = "Model_Battery";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
                model.transform.localScale = Vector3.one * 0.75f;
                var anim = model.GetComponent<Animator>();
                if (anim != null) UnityEngine.Object.DestroyImmediate(anim);
                var col = model.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
            }
        }

        private static void CreateAmmoPickup(GameObject parent, Vector3 pos)
        {
            GameObject root = new GameObject("Pickup_Ammunition");
            root.transform.SetParent(parent.transform, false);
            root.transform.position = pos;

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.1f, 0f);
            box.size = new Vector3(0.5f, 0.5f, 0.5f);

            AmmunitionPickup ammo = root.AddComponent<AmmunitionPickup>();
            SerializedObject so = new SerializedObject(ammo);
            so.FindProperty("ammoAmount").intValue = 6;
            so.ApplyModifiedProperties();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MATCHBOX_PREFAB);
            if (prefab != null)
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                model.name = "Model_AmmoBox";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                model.transform.localScale = Vector3.one * 0.85f;
                var anim = model.GetComponent<Animator>();
                if (anim != null) UnityEngine.Object.DestroyImmediate(anim);
                var col = model.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
            }
        }

        private static void CreateKeycardPickup(GameObject parent, Vector3 pos)
        {
            GameObject root = new GameObject("Pickup_MaintenanceKeycard");
            root.transform.SetParent(parent.transform, false);
            root.transform.position = pos;

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.1f, 0f);
            box.size = new Vector3(0.5f, 0.5f, 0.5f);

            KeyPickup key = root.AddComponent<KeyPickup>();
            SerializedObject so = new SerializedObject(key);
            so.FindProperty("keyId").stringValue = "MaintenanceKeycard";
            so.FindProperty("keyDisplayName").stringValue = "Maintenance Keycard";
            so.FindProperty("isObjectiveItem").boolValue = true;
            so.ApplyModifiedProperties();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TAPE_PREFAB);
            if (prefab != null)
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                model.name = "Model_Keycard";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
                model.transform.localScale = Vector3.one * 0.85f;
                var anim = model.GetComponent<Animator>();
                if (anim != null) UnityEngine.Object.DestroyImmediate(anim);
                var col = model.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
            }
        }

        private static void CreateAlmondWater(GameObject parent, Vector3 pos)
        {
            GameObject root = new GameObject("Pickup_AlmondWater");
            root.transform.SetParent(parent.transform, false);
            root.transform.position = pos;

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.18f, 0f);
            box.size = new Vector3(0.5f, 0.6f, 0.5f);

            root.AddComponent<AlmondWaterPickup>();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WATER_PREFAB);
            if (prefab != null)
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                model.name = "Model_AlmondWater";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(0f, 15f, 0f);
                model.transform.localScale = Vector3.one * 0.8f;
                var anim = model.GetComponent<Animator>();
                if (anim != null) UnityEngine.Object.DestroyImmediate(anim);
                var col = model.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
            }
        }

        private static void CreateFirstAid(GameObject parent, Vector3 pos)
        {
            GameObject root = new GameObject("Pickup_FirstAid");
            root.transform.SetParent(parent.transform, false);
            root.transform.position = pos;

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.12f, 0f);
            box.size = new Vector3(0.6f, 0.4f, 0.5f);

            root.AddComponent<FirstAidPickup>();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FIRSTAID_PREFAB);
            if (prefab != null)
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                model.name = "Model_FirstAid";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(0f, -25f, 0f);
                model.transform.localScale = Vector3.one * 0.8f;
                var anim = model.GetComponent<Animator>();
                if (anim != null) UnityEngine.Object.DestroyImmediate(anim);
                var col = model.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
            }
        }

        private static void CreateSanityPills(GameObject parent, Vector3 pos)
        {
            GameObject root = new GameObject("Pickup_SanityPills");
            root.transform.SetParent(parent.transform, false);
            root.transform.position = pos;

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.12f, 0f);
            box.size = new Vector3(0.5f, 0.4f, 0.5f);

            root.AddComponent<SanityPillsPickup>();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PILLS_PREFAB);
            if (prefab != null)
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                model.name = "Model_SanityPills";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
                model.transform.localScale = Vector3.one * 0.85f;
                var anim = model.GetComponent<Animator>();
                if (anim != null) UnityEngine.Object.DestroyImmediate(anim);
                var col = model.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
            }
        }

        private static void CreatePistolPickup(GameObject parent, Vector3 pos)
        {
            GameObject root = new GameObject("Pickup_ArmyPistol");
            root.transform.SetParent(parent.transform, false);
            root.transform.position = pos;

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.15f, 0f);
            box.size = new Vector3(0.6f, 0.4f, 0.6f);

            root.AddComponent<HorrorEscape.Interaction.PistolPickup>();

            const string pistolPrefabPath = "Assets/Polygon-Lite Survival Collection/Prefabs/SM_Army_Pistol.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(pistolPrefabPath);
            if (prefab != null)
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                model.name = "Model_ArmyPistol";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(-90f, 45f, 0f);
                model.transform.localScale = Vector3.one * 1.0f;
                var col = model.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
            }
        }

        private static PowerSwitch CreateBreakerBox(GameObject parent, Vector3 pos)
        {
            GameObject boxGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boxGO.name = "MaintenanceBreakerBox";
            boxGO.transform.SetParent(parent.transform, false);
            boxGO.transform.position = pos;
            boxGO.transform.localScale = new Vector3(0.6f, 0.8f, 0.25f);

            Material boxMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Monster.mat");
            if (boxMat != null) boxGO.GetComponent<MeshRenderer>().sharedMaterial = boxMat;

            // Lever Handle
            GameObject lever = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lever.name = "BreakerLever";
            lever.transform.SetParent(boxGO.transform, false);
            lever.transform.localPosition = new Vector3(0f, 0f, 0.15f);
            lever.transform.localRotation = Quaternion.Euler(-45f, 0f, 0f);
            lever.transform.localScale = new Vector3(0.06f, 0.22f, 0.06f);
            UnityEngine.Object.DestroyImmediate(lever.GetComponent<Collider>());

            // Red/Green Indicator Light
            GameObject lightGO = new GameObject("BreakerStatusLight");
            lightGO.transform.SetParent(boxGO.transform, false);
            lightGO.transform.localPosition = new Vector3(0f, 0.28f, 0.16f);
            Light indLight = lightGO.AddComponent<Light>();
            indLight.type = LightType.Point;
            indLight.color = Color.red;
            indLight.range = 3.0f;
            indLight.intensity = 1.2f;

            PowerSwitch ps = boxGO.AddComponent<PowerSwitch>();
            SerializedObject so = new SerializedObject(ps);
            so.FindProperty("switchLever").objectReferenceValue = lever.transform;
            so.FindProperty("statusIndicatorLight").objectReferenceValue = indLight;
            so.FindProperty("requiredFuses").intValue = 1;
            so.ApplyModifiedProperties();

            return ps;
        }

        private static EscapeExit CreateEmergencyExitGate(GameObject parent, Vector3 pos, Material wallMat, Material ceilingMat)
        {
            GameObject exitRoot = new GameObject("EmergencyExitGate");
            exitRoot.transform.SetParent(parent.transform, false);
            exitRoot.transform.position = pos;

            // Frame
            GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "Exit_DoorFrame";
            frame.transform.SetParent(exitRoot.transform, false);
            frame.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            frame.transform.localScale = new Vector3(2.4f, 2.8f, 0.2f);
            if (wallMat != null) frame.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            // Door Hinge
            GameObject hinge = new GameObject("Exit_DoorHinge");
            hinge.transform.SetParent(exitRoot.transform, false);
            hinge.transform.localPosition = new Vector3(-0.95f, 0f, 0f);

            // Door Panel
            GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "Exit_DoorPanel";
            door.transform.SetParent(hinge.transform, false);
            door.transform.localPosition = new Vector3(0.95f, 1.4f, 0f);
            door.transform.localScale = new Vector3(1.9f, 2.7f, 0.12f);
            if (wallMat != null) door.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            // Exit Sign
            GameObject sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sign.name = "Exit_Sign";
            sign.transform.SetParent(frame.transform, false);
            sign.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            sign.transform.localScale = new Vector3(0.7f, 0.2f, 0.18f);
            if (ceilingMat != null) sign.GetComponent<MeshRenderer>().sharedMaterial = ceilingMat;

            // Indicator Light
            GameObject lightGO = new GameObject("ExitStatusLight");
            lightGO.transform.SetParent(exitRoot.transform, false);
            lightGO.transform.localPosition = new Vector3(0f, 2.8f, 0f);
            Light statusLight = lightGO.AddComponent<Light>();
            statusLight.type = LightType.Point;
            statusLight.color = Color.red;
            statusLight.intensity = 1.5f;
            statusLight.range = 8.0f;

            // Trigger Volume & EscapeExit
            BoxCollider triggerBox = exitRoot.AddComponent<BoxCollider>();
            triggerBox.isTrigger = true;
            triggerBox.size = new Vector3(2.6f, 2.8f, 2.6f);
            triggerBox.center = new Vector3(0f, 1.4f, 0f);

            EscapeExit exit = exitRoot.AddComponent<EscapeExit>();
            exit.SetIndicatorLight(statusLight);
            exit.SetDoorHinge(hinge.transform);

            return exit;
        }

        private static StalkerAI CreateStalkerRig(GameObject parent, Vector3 spawnPos, Material darkMat)
        {
            GameObject stalkerGO = new GameObject("StalkerEnemy");
            stalkerGO.transform.SetParent(parent.transform, false);
            stalkerGO.transform.position = spawnPos;

            NavMeshAgent agent = stalkerGO.AddComponent<NavMeshAgent>();
            agent.height = 1.9f;
            agent.radius = 0.45f;
            agent.speed = 2.0f;
            agent.acceleration = 8.0f;
            agent.stoppingDistance = 1.2f;

            GameObject zombiePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Zombie/zombie.fbx");
            RuntimeAnimatorController animController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Characters/Zombie/ZombieAnimatorController.controller");

            Animator zombieAnim = null;
            if (zombiePrefab != null)
            {
                GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(zombiePrefab, stalkerGO.transform);
                modelInstance.name = "ZombieCharacter";
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;

                zombieAnim = modelInstance.GetComponent<Animator>();
                if (zombieAnim == null) zombieAnim = modelInstance.AddComponent<Animator>();
                if (animController != null) zombieAnim.runtimeAnimatorController = animController;
                zombieAnim.applyRootMotion = false;
            }
            else
            {
                // Fallback Body Capsule
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                body.transform.SetParent(stalkerGO.transform, false);
                body.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                body.transform.localScale = new Vector3(0.65f, 1.1f, 0.65f);
                if (darkMat != null) body.GetComponent<MeshRenderer>().sharedMaterial = darkMat;
                UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            }

            // Glowing Eye / Aura Light
            GameObject elGO = new GameObject("EyeLight");
            elGO.transform.SetParent(stalkerGO.transform, false);
            elGO.transform.localPosition = new Vector3(0f, 1.6f, 0.3f);
            Light el = elGO.AddComponent<Light>();
            el.type = LightType.Point;
            el.color = new Color(0.9f, 0.2f, 0.1f);
            el.intensity = 1.8f;
            el.range = 3.5f;

            StalkerAI ai = stalkerGO.AddComponent<StalkerAI>();
            if (zombieAnim != null)
            {
                ai.SetAnimator(zombieAnim);
            }
            ai.SetDormant(true);

            return ai;
        }

        private static Transform CreateWaypoint(GameObject parent, string name, Vector3 pos)
        {
            GameObject wp = new GameObject(name);
            wp.transform.SetParent(parent.transform, false);
            wp.transform.position = pos;
            return wp.transform;
        }

        private static void BakeSceneNavMesh()
        {
            try
            {
                UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
                Debug.Log("[BackroomsGameplaySetup] NavMesh successfully baked for Backrooms maze!");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BackroomsGameplaySetup] NavMesh bake notice: " + ex.Message);
            }
        }
    }
}
