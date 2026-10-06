using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HorrorEscape.Interaction;
using HorrorEscape.Player;

namespace HorrorEscape.Editor
{
    /// <summary>
    /// Editor utility to upgrade all in-game pickup items and player handheld flashlight
    /// using authentic 3D models from the "Survival Tools" asset pack.
    /// </summary>
    public static class SurvivalToolsSetup
    {
        private const string BATTERY_PREFAB = "Assets/Survival Tools/Prefabs/battery.prefab";
        private const string WATER_PREFAB = "Assets/Survival Tools/Prefabs/waterbottle.prefab";
        private const string FLASHLIGHT_PREFAB = "Assets/Survival Tools/Prefabs/flashlight.prefab";
        private const string FIRSTAID_PREFAB = "Assets/Survival Tools/Prefabs/firstaid.prefab";
        private const string PILLS_PREFAB = "Assets/Survival Tools/Prefabs/pills.prefab";
        private const string WALKIE_PREFAB = "Assets/Survival Tools/Prefabs/walkie.prefab";
        private const string TAPE_PREFAB = "Assets/Survival Tools/Prefabs/tape.prefab";

        [InitializeOnLoadMethod]
        private static void AutoUpgradeOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.isLoaded && (activeScene.name == "HorrorEscapeLevel" || string.IsNullOrEmpty(activeScene.name)))
                {
                    UpgradeAllScenePickupsAndFlashlight();
                }
            };
        }

        [MenuItem("Tools/Upgrade Pickups & Handheld Flashlight (Survival Tools)")]
        public static void UpgradeAllScenePickupsAndFlashlight()
        {
            Debug.Log("[SurvivalToolsSetup] Starting Survival Tools upgrade...");

            // 1. Upgrade or create Handheld Flashlight on Player Rig
            UpgradePlayerFlashlight();

            // 2. Upgrade existing pickups in the scene
            UpgradeExistingBatteryPickups();

            // 3. Ensure Almond Water, First Aid, and Sanity Pills exist in the scene
            EnsureSurvivalPickupsInScene();

            // 4. Place immersive Backrooms lore props (Walkie-Talkie, VHS Tape)
            PlaceSurvivalLoreProps();

            // Mark active scene dirty & save
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[SurvivalToolsSetup] Successfully upgraded all pickups, handheld flashlight, and survival tools in active scene!");
        }

        public static void UpgradePlayerFlashlight()
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogWarning("[SurvivalToolsSetup] Player not found in scene. Flashlight model upgrade skipped.");
                return;
            }

            // Find Camera & Flashlight
            Camera cam = player.GetComponentInChildren<Camera>();
            if (cam == null) return;

            Transform flashTransform = cam.transform.Find("Flashlight");
            if (flashTransform == null)
            {
                GameObject fgo = new GameObject("Flashlight");
                fgo.transform.SetParent(cam.transform, false);
                flashTransform = fgo.transform;
            }

            // Ensure Light component
            Light spotLight = flashTransform.GetComponent<Light>();
            if (spotLight == null) spotLight = flashTransform.gameObject.AddComponent<Light>();
            spotLight.type = LightType.Spot;
            spotLight.spotAngle = 60f;
            spotLight.innerSpotAngle = 40f;
            spotLight.range = 22f;
            spotLight.intensity = 2.5f;
            spotLight.color = new Color(0.95f, 0.95f, 1.0f);
            spotLight.shadows = LightShadows.Hard;

            // Ensure FlashlightController
            FlashlightController fc = flashTransform.GetComponent<FlashlightController>();
            if (fc == null) fc = flashTransform.gameObject.AddComponent<FlashlightController>();

            // Configure Flashlight position & hold offset
            flashTransform.localPosition = new Vector3(0.24f, -0.2f, 0.35f);

            // Clean previous visual mesh children if any
            for (int i = flashTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = flashTransform.GetChild(i);
                if (child.name.StartsWith("Model_") || child.name.StartsWith("flashlight"))
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            // Instantiate 3D Flashlight Model
            GameObject flashPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FLASHLIGHT_PREFAB);
            if (flashPrefab != null)
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(flashPrefab, flashTransform);
                model.name = "Model_HandheldFlashlight";
                model.transform.localPosition = new Vector3(0f, -0.05f, 0f);
                // Rotate so front glass beam aligns with forward (+Z)
                model.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                model.transform.localScale = Vector3.one * 0.45f;

                // Strip animator/collider from held model
                var anim = model.GetComponent<Animator>();
                if (anim != null) Object.DestroyImmediate(anim);
                var col = model.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);

                Debug.Log("[SurvivalToolsSetup] 3D Handheld Flashlight model attached to Player rig!");
            }
        }

        public static void UpgradeExistingBatteryPickups()
        {
            var existingBatteries = Object.FindObjectsByType<BatteryPickup>(FindObjectsSortMode.None);
            GameObject batteryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BATTERY_PREFAB);

            foreach (var bp in existingBatteries)
            {
                GameObject go = bp.gameObject;

                // Remove primitive mesh renderer/filter if present directly on root
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null) Object.DestroyImmediate(mr);
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null) Object.DestroyImmediate(mf);

                // Ensure BoxCollider trigger on root
                var col = go.GetComponent<Collider>();
                if (col != null && !(col is BoxCollider))
                {
                    Object.DestroyImmediate(col);
                }
                BoxCollider box = go.GetComponent<BoxCollider>();
                if (box == null) box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = new Vector3(0f, 0.1f, 0f);
                box.size = new Vector3(0.5f, 0.5f, 0.5f);

                // Remove old model child if any
                Transform oldModel = go.transform.Find("Model_Battery");
                if (oldModel != null) Object.DestroyImmediate(oldModel.gameObject);

                // Instantiate authentic 3D Battery model
                if (batteryPrefab != null)
                {
                    GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(batteryPrefab, go.transform);
                    model.name = "Model_Battery";
                    model.transform.localPosition = Vector3.zero;
                    model.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
                    model.transform.localScale = Vector3.one * 0.75f;

                    var anim = model.GetComponent<Animator>();
                    if (anim != null) Object.DestroyImmediate(anim);
                    var mcol = model.GetComponent<Collider>();
                    if (mcol != null) Object.DestroyImmediate(mcol);
                }
            }

            Debug.Log($"[SurvivalToolsSetup] Upgraded {existingBatteries.Length} Battery pickups with 3D models.");
        }

        public static void EnsureSurvivalPickupsInScene()
        {
            GameObject propsRoot = GameObject.Find("Interactables");
            if (propsRoot == null) propsRoot = new GameObject("Interactables");

            // 1. Almond Water Pickups
            if (Object.FindFirstObjectByType<AlmondWaterPickup>() == null)
            {
                CreateAlmondWater(propsRoot, new Vector3(2f, 0.35f, 3.5f));
                CreateAlmondWater(propsRoot, new Vector3(9f, 0.35f, 15f));
                Debug.Log("[SurvivalToolsSetup] Almond Water 3D pickups spawned.");
            }

            // 2. First Aid Pickups
            if (Object.FindFirstObjectByType<FirstAidPickup>() == null)
            {
                CreateFirstAid(propsRoot, new Vector3(-10f, 0.35f, 15f)); // Medical Ward
                Debug.Log("[SurvivalToolsSetup] First Aid Kit 3D pickup spawned.");
            }

            // 3. Sanity Pills Pickups
            if (Object.FindFirstObjectByType<SanityPillsPickup>() == null)
            {
                CreateSanityPills(propsRoot, new Vector3(-2f, 0.35f, -2f)); // Start Room corner
                CreateSanityPills(propsRoot, new Vector3(12f, 0.35f, 12f)); // Storage Room
                Debug.Log("[SurvivalToolsSetup] Sanity Pills 3D pickups spawned.");
            }
        }

        public static GameObject CreateBattery(GameObject parent, Vector3 pos)
        {
            GameObject root = new GameObject("Pickup_Battery");
            root.transform.SetParent(parent != null ? parent.transform : null, false);
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
                if (anim != null) Object.DestroyImmediate(anim);
                var col = model.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
            }

            return root;
        }

        public static GameObject CreateAlmondWater(GameObject parent, Vector3 pos)
        {
            GameObject root = new GameObject("Pickup_AlmondWater");
            root.transform.SetParent(parent != null ? parent.transform : null, false);
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
                if (anim != null) Object.DestroyImmediate(anim);
                var col = model.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
            }

            return root;
        }

        public static GameObject CreateFirstAid(GameObject parent, Vector3 pos)
        {
            GameObject root = new GameObject("Pickup_FirstAid");
            root.transform.SetParent(parent != null ? parent.transform : null, false);
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
                if (anim != null) Object.DestroyImmediate(anim);
                var col = model.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
            }

            return root;
        }

        public static GameObject CreateSanityPills(GameObject parent, Vector3 pos)
        {
            GameObject root = new GameObject("Pickup_SanityPills");
            root.transform.SetParent(parent != null ? parent.transform : null, false);
            root.transform.position = pos;

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.1f, 0f);
            box.size = new Vector3(0.4f, 0.4f, 0.4f);

            root.AddComponent<SanityPillsPickup>();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PILLS_PREFAB);
            if (prefab != null)
            {
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                model.name = "Model_SanityPills";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(0f, 50f, 0f);
                model.transform.localScale = Vector3.one * 0.85f;

                var anim = model.GetComponent<Animator>();
                if (anim != null) Object.DestroyImmediate(anim);
                var col = model.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
            }

            return root;
        }

        public static void PlaceSurvivalLoreProps()
        {
            GameObject propsRoot = GameObject.Find("Interactables");
            if (propsRoot == null) return;

            // 1. Walkie-Talkie prop in Start Room next to note
            if (GameObject.Find("Prop_WalkieTalkie") == null)
            {
                GameObject walkiePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WALKIE_PREFAB);
                if (walkiePrefab != null)
                {
                    GameObject walkie = (GameObject)PrefabUtility.InstantiatePrefab(walkiePrefab, propsRoot.transform);
                    walkie.name = "Prop_WalkieTalkie";
                    walkie.transform.position = new Vector3(0.4f, 0.52f, 2.8f);
                    walkie.transform.localRotation = Quaternion.Euler(0f, -40f, 0f);
                    walkie.transform.localScale = Vector3.one * 0.7f;

                    var anim = walkie.GetComponent<Animator>();
                    if (anim != null) Object.DestroyImmediate(anim);
                }
            }

            // 2. VHS Tape prop in Exit Chamber
            if (GameObject.Find("Prop_VHSTape") == null)
            {
                GameObject tapePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TAPE_PREFAB);
                if (tapePrefab != null)
                {
                    GameObject tape = (GameObject)PrefabUtility.InstantiatePrefab(tapePrefab, propsRoot.transform);
                    tape.name = "Prop_VHSTape";
                    tape.transform.position = new Vector3(0.5f, 1.15f, 28f);
                    tape.transform.localRotation = Quaternion.Euler(0f, 65f, 0f);
                    tape.transform.localScale = Vector3.one * 0.75f;

                    var anim = tape.GetComponent<Animator>();
                    if (anim != null) Object.DestroyImmediate(anim);
                }
            }
        }
    }
}
