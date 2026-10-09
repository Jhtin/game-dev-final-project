using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.Enemy;
using HorrorEscape.Interaction;
using HorrorEscape.Player;
using HorrorEscape.UI;

namespace HorrorEscape.Editor
{
    /// <summary>
    /// Configures authentic 3D weapons and survival props from the 'POLY - Lite Survival Collection'
    /// into the Backrooms Escape game:
    /// 1. Equips the player viewmodel with the authentic 3D Army Pistol (SM_Army_Pistol) and Tactical SMG (SM_Special_Submachine_Gun_Clean).
    /// 2. Replaces primitive ammo pickups with authentic 3D ammunition crates (SM_Chuck_Pistol_Box_9.19).
    /// 3. Replaces first aid pickups with authentic survival medical kits (Medical_Kit).
    /// 4. Places the unlockable Tactical SMG on a workbench (SM_Workbench) surrounded by survival props in the maintenance area.
    /// 5. Places canned rations (Small_Canned_Food_C_03), pallets, barrels, and canisters in key maze rooms.
    /// </summary>
    public static class PolygonSurvivalSetup
    {
        private const string ScenePath = "Assets/Scenes/HorrorEscapeLevel.unity";
        private const string RootDir = "Assets/Polygon-Lite Survival Collection/Prefabs";

        // Asset Paths
        private const string PistolPrefabPath = RootDir + "/SM_Army_Pistol.prefab";
        private const string SMGPrefabPath = RootDir + "/SM_Special_Submachine_Gun_Clean.prefab";
        private const string AmmoBoxPrefabPath = RootDir + "/SM_Chuck_Pistol_Box_9.19.prefab";
        private const string CartridgesPrefabPath = RootDir + "/SM_Cartridges_9.19 (3).prefab";
        private const string MedkitPrefabPath = RootDir + "/Medical_Kit.prefab";
        private const string CannedFoodPrefabPath = RootDir + "/Small_Canned_Food_C_03.prefab";
        private const string WorkbenchPrefabPath = RootDir + "/SM_Workbench.prefab";
        private const string PalletPrefabPath = RootDir + "/SM_Pallet_01.prefab";
        private const string BarrelPrefabPath = RootDir + "/SM_Barrel_Closed_01_A.prefab";
        private const string CanisterPrefabPath = RootDir + "/SM_Canister_01.prefab";
        private const string AxePrefabPath = RootDir + "/SM_Camping_Axe.prefab";

        [InitializeOnLoadMethod]
        private static void AutoSetupOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.isLoaded && (activeScene.name == "HorrorEscapeLevel" || string.IsNullOrEmpty(activeScene.name)))
                {
                    SetupPolygonSurvivalBatch();
                }
            };
        }

        [MenuItem("Tools/Setup Polygon Survival Weapons & Props")]
        public static void SetupPolygonSurvivalMenu()
        {
            SetupPolygonSurvivalBatch();

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Polygon Survival Setup",
                    "Polygon Lite Survival weapons (Army Pistol & Tactical SMG), 3D ammo boxes, medkits, workbenches, and environmental props configured successfully!",
                    "OK");
            }
        }

        public static void SetupPolygonSurvivalBatch()
        {
            Debug.Log("[PolygonSurvivalSetup] Starting Polygon Lite Survival setup...");

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.isLoaded || scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            // 1. Upgrade Player Weapons
            UpgradePlayerWeapons();

            // 2. Upgrade Ammo Pickups in Scene
            UpgradeAmmoPickups();

            // 3. Upgrade First Aid Pickups in Scene
            UpgradeFirstAidPickups();

            // 4. Place Tactical SMG Workbench & Maintenance Outpost
            SetupTacticalSMGOutpost();

            // 5. Place Survival Caches in Mazes
            SetupSurvivalAtmosphereProps();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PolygonSurvivalSetup] Successfully updated and saved scene with Polygon Survival assets!");
        }

        private static void UpgradePlayerWeapons()
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                var fpc = Object.FindFirstObjectByType<FirstPersonController>();
                if (fpc != null) player = fpc.gameObject;
            }

            if (player == null)
            {
                Debug.LogWarning("[PolygonSurvivalSetup] Player not found in scene!");
                return;
            }

            Camera cam = player.GetComponentInChildren<Camera>();
            if (cam == null) return;

            Transform gunRoot = cam.transform.Find("DefenseHandgun");
            if (gunRoot == null)
            {
                GameObject ggo = new GameObject("DefenseHandgun");
                ggo.transform.SetParent(cam.transform, false);
                gunRoot = ggo.transform;
            }

            gunRoot.localPosition = new Vector3(0.2f, -0.2f, 0.45f);
            gunRoot.localRotation = Quaternion.identity;

            // Remove crude primitive cube parts (Slide, Barrel, Grip, TriggerGuard, etc.)
            for (int i = gunRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = gunRoot.GetChild(i);
                if (child.name == "Slide" || child.name == "Barrel" || child.name == "Grip" ||
                    child.name == "TriggerGuard" || child.name.StartsWith("Model_"))
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            // Setup or find Muzzle Flash Light
            Transform muzzleT = gunRoot.Find("MuzzleFlashLight");
            Light muzzleLight = null;
            if (muzzleT == null)
            {
                GameObject mgo = new GameObject("MuzzleFlashLight");
                mgo.transform.SetParent(gunRoot, false);
                mgo.transform.localPosition = new Vector3(0f, 0.05f, 0.28f);
                muzzleLight = mgo.AddComponent<Light>();
                muzzleLight.type = LightType.Point;
                muzzleLight.color = new Color(1.0f, 0.85f, 0.4f);
                muzzleLight.intensity = 3.5f;
                muzzleLight.range = 6.0f;
                muzzleLight.enabled = false;
            }
            else
            {
                muzzleLight = muzzleT.GetComponent<Light>();
            }

            // 1. Instantiate Army Pistol Viewmodel
            Transform existingPistol = gunRoot.Find("Model_ArmyPistol");
            GameObject pistolGO = existingPistol != null ? existingPistol.gameObject : null;
            if (pistolGO == null)
            {
                GameObject pistolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PistolPrefabPath);
                if (pistolPrefab != null)
                {
                    pistolGO = (GameObject)PrefabUtility.InstantiatePrefab(pistolPrefab, gunRoot);
                    pistolGO.name = "Model_ArmyPistol";
                    // Position and orient pistol nicely in first-person view
                    pistolGO.transform.localPosition = new Vector3(0f, -0.04f, 0f);
                    pistolGO.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                    pistolGO.transform.localScale = Vector3.one * 0.9f;

                    pistolGO.SetActive(false);
                }
            }

            // 2. Instantiate Tactical SMG Viewmodel
            Transform existingSMG = gunRoot.Find("Model_TacticalSMG");
            GameObject smgGO = existingSMG != null ? existingSMG.gameObject : null;
            if (smgGO == null)
            {
                GameObject smgPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SMGPrefabPath);
                if (smgPrefab != null)
                {
                    smgGO = (GameObject)PrefabUtility.InstantiatePrefab(smgPrefab, gunRoot);
                    smgGO.name = "Model_TacticalSMG";
                    // Position and orient SMG nicely in first-person view
                    smgGO.transform.localPosition = new Vector3(0f, -0.05f, -0.05f);
                    smgGO.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                    smgGO.transform.localScale = Vector3.one * 0.8f;

                    // Strip colliders from viewmodel weapon
                    foreach (var c in smgGO.GetComponentsInChildren<Collider>())
                    {
                        Object.DestroyImmediate(c);
                    }
                    smgGO.SetActive(false);
                }
            }

            // Hook up PlayerCombat & ensure unarmed start
            PlayerCombat combat = player.GetComponent<PlayerCombat>();
            if (combat != null)
            {
                combat.SetWeaponTransform(gunRoot);
                combat.SetMuzzleFlashLight(muzzleLight);
                combat.SetWeaponObjects(pistolGO, smgGO);

                // Gun must NOT be active at start - player explores and discovers naturally
                SerializedObject combatSo = new SerializedObject(combat);
                combatSo.FindProperty("hasUnlockedPistol").boolValue = false;
                combatSo.FindProperty("hasUnlockedSMG").boolValue = false;
                combatSo.FindProperty("isCombatActive").boolValue = false;
                combatSo.ApplyModifiedProperties();
            }

            if (pistolGO != null) pistolGO.SetActive(false);
            if (smgGO != null) smgGO.SetActive(false);

            Debug.Log("[PolygonSurvivalSetup] Player weapons configured with authentic 3D models (Unarmed start).");
        }

        private static void UpgradeAmmoPickups()
        {
            GameObject ammoBoxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AmmoBoxPrefabPath);
            if (ammoBoxPrefab == null)
            {
                Debug.LogWarning("[PolygonSurvivalSetup] Ammo box prefab not found at " + AmmoBoxPrefabPath);
                return;
            }

            AmmunitionPickup[] pickups = Object.FindObjectsByType<AmmunitionPickup>(FindObjectsSortMode.None);
            int count = 0;
            foreach (var p in pickups)
            {
                // Disable existing primitive cube renderer if any
                MeshRenderer mr = p.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;

                // Ensure BoxCollider trigger exists
                BoxCollider col = p.GetComponent<BoxCollider>();
                if (col == null) col = p.gameObject.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(0.6f, 0.6f, 0.6f);

                // Check if 3D model child exists
                Transform model = p.transform.Find("Model_AmmoBox");
                if (model == null)
                {
                    GameObject modelGO = (GameObject)PrefabUtility.InstantiatePrefab(ammoBoxPrefab, p.transform);
                    modelGO.name = "Model_AmmoBox";
                    modelGO.transform.localPosition = Vector3.zero;
                    modelGO.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    modelGO.transform.localScale = Vector3.one * 1.5f;

                    foreach (var c in modelGO.GetComponentsInChildren<Collider>())
                    {
                        Object.DestroyImmediate(c);
                    }
                    count++;
                }
            }

            Debug.Log($"[PolygonSurvivalSetup] Upgraded {count} Ammunition Pickups with 3D Chuck Pistol Ammo Boxes.");
        }

        private static void UpgradeFirstAidPickups()
        {
            GameObject medkitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MedkitPrefabPath);
            if (medkitPrefab == null)
            {
                Debug.LogWarning("[PolygonSurvivalSetup] Medkit prefab not found at " + MedkitPrefabPath);
                return;
            }

            FirstAidPickup[] pickups = Object.FindObjectsByType<FirstAidPickup>(FindObjectsSortMode.None);
            int count = 0;
            foreach (var p in pickups)
            {
                MeshRenderer mr = p.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;

                BoxCollider col = p.GetComponent<BoxCollider>();
                if (col == null) col = p.gameObject.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(0.7f, 0.7f, 0.7f);

                Transform model = p.transform.Find("Model_Medkit");
                if (model == null)
                {
                    GameObject modelGO = (GameObject)PrefabUtility.InstantiatePrefab(medkitPrefab, p.transform);
                    modelGO.name = "Model_Medkit";
                    modelGO.transform.localPosition = Vector3.zero;
                    modelGO.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    modelGO.transform.localScale = Vector3.one * 1.4f;

                    foreach (var c in modelGO.GetComponentsInChildren<Collider>())
                    {
                        Object.DestroyImmediate(c);
                    }
                    count++;
                }
            }

            Debug.Log($"[PolygonSurvivalSetup] Upgraded {count} First Aid Pickups with 3D Medical Kits.");
        }

        private static void SetupTacticalSMGOutpost()
        {
            GameObject outpostRoot = GameObject.Find("Tactical_SMG_Outpost");
            if (outpostRoot != null) Object.DestroyImmediate(outpostRoot);

            outpostRoot = new GameObject("Tactical_SMG_Outpost");

            // Look for Maintenance Room waypoint to place outpost inside
            Vector3 basePos = new Vector3(16.5f, 0f, 34.5f);
            GameObject maintWP = GameObject.Find("WP_MaintRoom");
            if (maintWP != null)
            {
                basePos = maintWP.transform.position;
                basePos.y = 0f;
            }

            // 1. Workbench
            GameObject benchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorkbenchPrefabPath);
            if (benchPrefab != null)
            {
                GameObject bench = (GameObject)PrefabUtility.InstantiatePrefab(benchPrefab, outpostRoot.transform);
                bench.name = "Survival_Workbench";
                bench.transform.position = basePos + new Vector3(0.5f, 0f, 0.5f);
                bench.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            }

            // 2. SMG Pickup on the workbench
            GameObject smgPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SMGPrefabPath);
            if (smgPrefab != null)
            {
                GameObject pickupRoot = new GameObject("Pickup_TacticalSMG");
                pickupRoot.transform.SetParent(outpostRoot.transform, false);
                pickupRoot.transform.position = basePos + new Vector3(0.5f, 0.95f, 0.5f);

                BoxCollider col = pickupRoot.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(0.8f, 0.5f, 0.8f);

                pickupRoot.AddComponent<SMGPickup>();

                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(smgPrefab, pickupRoot.transform);
                visual.name = "Model_TacticalSMG";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.Euler(-90f, 25f, 0f);
                visual.transform.localScale = Vector3.one * 1.1f;

                foreach (var c in visual.GetComponentsInChildren<Collider>())
                {
                    Object.DestroyImmediate(c);
                }

                // Add a small spotlight illuminating the gun
                GameObject lightGO = new GameObject("SMG_Spotlight");
                lightGO.transform.SetParent(pickupRoot.transform, false);
                lightGO.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                lightGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Light l = lightGO.AddComponent<Light>();
                l.type = LightType.Spot;
                l.range = 3.5f;
                l.spotAngle = 45f;
                l.intensity = 2.0f;
                l.color = new Color(0.9f, 0.95f, 1.0f);
            }

            // 3. Extra Ammo Box on the table
            GameObject ammoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AmmoBoxPrefabPath);
            if (ammoPrefab != null)
            {
                GameObject extraAmmo = new GameObject("Pickup_WorkbenchAmmo");
                extraAmmo.transform.SetParent(outpostRoot.transform, false);
                extraAmmo.transform.position = basePos + new Vector3(0.9f, 0.95f, 0.2f);

                BoxCollider col = extraAmmo.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.size = new Vector3(0.6f, 0.4f, 0.6f);

                var ammoScript = extraAmmo.AddComponent<AmmunitionPickup>();
                SerializedObject ammoSo = new SerializedObject(ammoScript);
                ammoSo.FindProperty("ammoAmount").intValue = 12; // Extra cache of 12 rounds
                ammoSo.ApplyModifiedProperties();

                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(ammoPrefab, extraAmmo.transform);
                visual.name = "Model_AmmoBox";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.Euler(-90f, 10f, 0f);
                visual.transform.localScale = Vector3.one * 1.4f;

                foreach (var c in visual.GetComponentsInChildren<Collider>())
                {
                    Object.DestroyImmediate(c);
                }
            }

            // 4. Atmospheric props (Pallet, Barrels, Gas Canister, Camping Axe)
            GameObject palletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PalletPrefabPath);
            if (palletPrefab != null)
            {
                GameObject pallet = (GameObject)PrefabUtility.InstantiatePrefab(palletPrefab, outpostRoot.transform);
                pallet.transform.position = basePos + new Vector3(-1.2f, 0f, -0.4f);
                pallet.transform.rotation = Quaternion.Euler(0f, 15f, 0f);
            }

            GameObject barrelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BarrelPrefabPath);
            if (barrelPrefab != null)
            {
                GameObject barrel = (GameObject)PrefabUtility.InstantiatePrefab(barrelPrefab, outpostRoot.transform);
                barrel.transform.position = basePos + new Vector3(-1.1f, 0.15f, -0.3f);
                barrel.transform.rotation = Quaternion.Euler(0f, -20f, 0f);
            }

            GameObject canisterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanisterPrefabPath);
            if (canisterPrefab != null)
            {
                GameObject canister = (GameObject)PrefabUtility.InstantiatePrefab(canisterPrefab, outpostRoot.transform);
                canister.transform.position = basePos + new Vector3(1.3f, 0f, -0.6f);
                canister.transform.rotation = Quaternion.Euler(0f, 75f, 0f);
            }

            GameObject axePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AxePrefabPath);
            if (axePrefab != null)
            {
                GameObject axe = (GameObject)PrefabUtility.InstantiatePrefab(axePrefab, outpostRoot.transform);
                axe.transform.position = basePos + new Vector3(0.2f, 0.92f, 0.8f);
                axe.transform.rotation = Quaternion.Euler(90f, 40f, 0f);
                axe.transform.localScale = Vector3.one * 1.2f;
            }

            Debug.Log("[PolygonSurvivalSetup] Tactical SMG Maintenance Outpost placed successfully.");
        }

        private static void SetupSurvivalAtmosphereProps()
        {
            GameObject decorRoot = GameObject.Find("Survival_Decor_Props");
            if (decorRoot != null) Object.DestroyImmediate(decorRoot);

            decorRoot = new GameObject("Survival_Decor_Props");

            GameObject keyWP = GameObject.Find("WP_KeyRoom");
            if (keyWP != null)
            {
                Vector3 keyPos = keyWP.transform.position;
                keyPos.y = 0f;

                // Add pallet and canned food ration near keycard
                GameObject palletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PalletPrefabPath);
                if (palletPrefab != null)
                {
                    GameObject p = (GameObject)PrefabUtility.InstantiatePrefab(palletPrefab, decorRoot.transform);
                    p.transform.position = keyPos + new Vector3(1.2f, 0f, 0.8f);
                    p.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
                }

                GameObject foodPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CannedFoodPrefabPath);
                if (foodPrefab != null)
                {
                    GameObject f = (GameObject)PrefabUtility.InstantiatePrefab(foodPrefab, decorRoot.transform);
                    f.transform.position = keyPos + new Vector3(1.1f, 0.15f, 0.9f);
                    f.transform.rotation = Quaternion.Euler(0f, 15f, 0f);
                    f.transform.localScale = Vector3.one * 1.5f;
                }
            }

            GameObject farWP = GameObject.Find("WP_FarRoom");
            if (farWP != null)
            {
                Vector3 farPos = farWP.transform.position;
                farPos.y = 0f;

                // Abandoned survival cache in far room
                GameObject barrelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BarrelPrefabPath);
                if (barrelPrefab != null)
                {
                    GameObject b = (GameObject)PrefabUtility.InstantiatePrefab(barrelPrefab, decorRoot.transform);
                    b.transform.position = farPos + new Vector3(-1.4f, 0f, 0.5f);
                }

                GameObject canisterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanisterPrefabPath);
                if (canisterPrefab != null)
                {
                    GameObject c = (GameObject)PrefabUtility.InstantiatePrefab(canisterPrefab, decorRoot.transform);
                    c.transform.position = farPos + new Vector3(-1.0f, 0f, 0.9f);
                }
            }

            Debug.Log("[PolygonSurvivalSetup] Atmospheric survival props placed across maze.");
        }
    }
}
