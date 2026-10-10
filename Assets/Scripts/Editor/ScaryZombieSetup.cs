using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using HorrorEscape.Enemy;
using HorrorEscape.Inventory;

namespace HorrorEscape.Editor
{
    /// <summary>
    /// Editor utility to import, configure Humanoid avatars, loop animations,
    /// build an Animator Controller, and instantiate the authentic Mixamo Scary Zombie (Ch10)
    /// model with rotten flesh/decayed textures onto the Stalker Enemy in the Backrooms level.
    /// Also ensures duplicate enemy cleanup and scene stability.
    /// </summary>
    [InitializeOnLoad]
    public static class ScaryZombieSetup
    {
        static ScaryZombieSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!SessionState.GetBool("ScaryZombieSetup_Executed_v3", false))
                {
                    SessionState.SetBool("ScaryZombieSetup_Executed_v3", true);
                    SetupScaryZombieBatch();
                }
            };
        }

        private const string ZombieFbxPath = "Assets/Characters/Zombie/zombie.fbx";
        private const string AnimDir = "Assets/Characters/Zombie/Animations";
        private const string ControllerPath = "Assets/Characters/Zombie/ZombieAnimatorController.controller";
        private const string ScenePath = "Assets/Scenes/HorrorEscapeLevel.unity";
        private const string BodyMatPath = "Assets/Characters/Zombie/Materials/Ch10_body.mat";
        private const string HeadMatPath = "Assets/Characters/Zombie/Materials/Ch10_head.mat";

        [MenuItem("Tools/Setup Scary Zombie Enemy & Animations")]
        public static void SetupScaryZombieMenu()
        {
            SetupScaryZombieBatch();

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Scary Zombie Setup",
                    "Authentic Mixamo Zombie Ch10 model, materials, Humanoid avatar, looping animations, and Animator Controller successfully configured and attached to the Stalker Enemy in the Backrooms level!",
                    "OK");
            }
        }

        public static void SetupScaryZombieBatch()
        {
            Debug.Log("[ScaryZombieSetup] Starting Scary Zombie Ch10 configuration...");

            // 0. Ensure Textures & Materials
            ConfigureTexturesAndMaterials();

            // 1. Configure zombie.fbx as Humanoid Avatar
            ConfigureZombieModelImporter();

            // 2. Load the generated Humanoid Avatar
            Avatar zombieAvatar = LoadZombieAvatar();
            if (zombieAvatar == null)
            {
                Debug.LogError("[ScaryZombieSetup] Failed to retrieve Humanoid Avatar from " + ZombieFbxPath);
                return;
            }
            Debug.Log("[ScaryZombieSetup] Retrieved Humanoid Avatar: " + zombieAvatar.name);

            // 3. Configure all animation FBX files in Animations directory
            ConfigureAnimations(zombieAvatar);

            // 4. Build or update the Zombie Animator Controller
            RuntimeAnimatorController animController = BuildAnimatorController();

            // 5. Open scene, deduplicate enemies, and attach model to Stalker Enemy rig
            AttachZombieToSceneEnemy(zombieAvatar, animController);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ScaryZombieSetup] Scary Zombie Ch10 configuration finished successfully!");
        }

        private static void ConfigureTexturesAndMaterials()
        {
            // Configure Normal Maps
            string[] normalPaths = {
                "Assets/Characters/Zombie/Textures/Ch10_1001_Normal.png",
                "Assets/Characters/Zombie/Textures/Ch10_1002_Normal.png"
            };

            foreach (var path in normalPaths)
            {
                if (File.Exists(path))
                {
                    TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (ti != null && ti.textureType != TextureImporterType.NormalMap)
                    {
                        ti.textureType = TextureImporterType.NormalMap;
                        ti.sRGBTexture = false;
                        ti.SaveAndReimport();
                        Debug.Log("[ScaryZombieSetup] Configured normal map at " + path);
                    }
                }
            }

            Directory.CreateDirectory("Assets/Characters/Zombie/Materials");

            // Ensure Body Material
            Material bodyMat = AssetDatabase.LoadAssetAtPath<Material>(BodyMatPath);
            if (bodyMat == null)
            {
                bodyMat = new Material(Shader.Find("Standard"));
                bodyMat.name = "Ch10_body";
                AssetDatabase.CreateAsset(bodyMat, BodyMatPath);
            }
            Texture2D bodyTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Characters/Zombie/Textures/Ch10_1001_Diffuse.png");
            Texture2D bodyNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Characters/Zombie/Textures/Ch10_1001_Normal.png");
            if (bodyTex != null) bodyMat.mainTexture = bodyTex;
            if (bodyNorm != null)
            {
                bodyMat.EnableKeyword("_NORMALMAP");
                bodyMat.SetTexture("_BumpMap", bodyNorm);
            }
            bodyMat.SetFloat("_Glossiness", 0.35f);
            bodyMat.SetFloat("_Metallic", 0.1f);
            EditorUtility.SetDirty(bodyMat);

            // Ensure Head Material
            Material headMat = AssetDatabase.LoadAssetAtPath<Material>(HeadMatPath);
            if (headMat == null)
            {
                headMat = new Material(Shader.Find("Standard"));
                headMat.name = "Ch10_head";
                AssetDatabase.CreateAsset(headMat, HeadMatPath);
            }
            Texture2D headTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Characters/Zombie/Textures/Ch10_1002_Diffuse.png");
            Texture2D headNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Characters/Zombie/Textures/Ch10_1002_Normal.png");
            if (headTex != null) headMat.mainTexture = headTex;
            if (headNorm != null)
            {
                headMat.EnableKeyword("_NORMALMAP");
                headMat.SetTexture("_BumpMap", headNorm);
            }
            headMat.SetFloat("_Glossiness", 0.4f);
            headMat.SetFloat("_Metallic", 0.05f);
            EditorUtility.SetDirty(headMat);

            AssetDatabase.SaveAssets();
        }

        private static void ConfigureZombieModelImporter()
        {
            ModelImporter importer = AssetImporter.GetAtPath(ZombieFbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[ScaryZombieSetup] ModelImporter not found at " + ZombieFbxPath);
                return;
            }

            bool needsReimport = false;

            if (importer.animationType != ModelImporterAnimationType.Human ||
                importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                needsReimport = true;
            }

            importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

            if (needsReimport)
            {
                importer.SaveAndReimport();
            }
            AssetDatabase.ImportAsset(ZombieFbxPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            Debug.Log("[ScaryZombieSetup] Reimported zombie.fbx as Humanoid Avatar.");
        }

        private static Avatar LoadZombieAvatar()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(ZombieFbxPath);
            foreach (var a in assets)
            {
                if (a is Avatar av && av.isValid)
                {
                    return av;
                }
            }
            return null;
        }

        private static void ConfigureAnimations(Avatar sourceAvatar)
        {
            if (!Directory.Exists(AnimDir)) return;

            string[] animFiles = Directory.GetFiles(AnimDir, "*.fbx", SearchOption.TopDirectoryOnly);

            foreach (string file in animFiles)
            {
                string assetPath = file.Replace("\\", "/");
                ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer == null) continue;

                bool modified = false;

                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    modified = true;
                }

                if (importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther || importer.sourceAvatar != sourceAvatar)
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    importer.sourceAvatar = sourceAvatar;
                    modified = true;
                }

                string lower = Path.GetFileNameWithoutExtension(file).ToLower();
                bool shouldLoop = lower.Contains("idle") || lower.Contains("walk") || lower.Contains("run") || lower.Contains("crawl");

                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                if (clips == null || clips.Length == 0) clips = importer.clipAnimations;

                if (clips != null && clips.Length > 0)
                {
                    for (int i = 0; i < clips.Length; i++)
                    {
                        clips[i].loopTime = shouldLoop;
                        clips[i].loopPose = shouldLoop;
                        clips[i].lockRootRotation = true;
                        clips[i].lockRootHeightY = true;
                        clips[i].lockRootPositionXZ = true;
                    }
                    importer.clipAnimations = clips;
                    modified = true;
                }

                if (modified)
                {
                    importer.SaveAndReimport();
                    Debug.Log($"[ScaryZombieSetup] Configured animation: {Path.GetFileName(file)} (Loop: {shouldLoop})");
                }
            }
        }

        private static RuntimeAnimatorController BuildAnimatorController()
        {
            if (File.Exists(ControllerPath))
            {
                AssetDatabase.DeleteAsset(ControllerPath);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            Debug.Log("[ScaryZombieSetup] Created fresh ZombieAnimatorController at " + ControllerPath);

            // Ensure Parameters
            EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Stun", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Die", AnimatorControllerParameterType.Trigger);

            var rootSm = controller.layers[0].stateMachine;

            // Find Clips
            AnimationClip idleClip = FindClip("zombie idle.fbx");
            AnimationClip walkClip = FindClip("zombie walk.fbx");
            AnimationClip runClip = FindClip("zombie run.fbx");
            AnimationClip attackClip = FindClip("zombie attack.fbx");
            AnimationClip screamClip = FindClip("zombie scream.fbx");
            AnimationClip deathClip = FindClip("zombie death.fbx") ?? FindClip("zombie dying.fbx");

            // 1. Locomotion Blend Tree State (Default State)
            AnimatorState locomotionState = rootSm.AddState("Locomotion", new Vector3(300, 100, 0));
            BlendTree blendTree = new BlendTree
            {
                name = "LocomotionBlend",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed"
            };

            if (idleClip != null) blendTree.AddChild(idleClip, 0.0f);
            if (walkClip != null) blendTree.AddChild(walkClip, 1.0f);
            if (runClip != null) blendTree.AddChild(runClip, 2.0f);

            AssetDatabase.AddObjectToAsset(blendTree, controller);
            locomotionState.motion = blendTree;
            rootSm.defaultState = locomotionState;

            // 2. Attack State
            if (attackClip != null)
            {
                AnimatorState attackState = rootSm.AddState("Attack", new Vector3(560, 100, 0));
                attackState.motion = attackClip;

                var toAttack = locomotionState.AddTransition(attackState);
                toAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
                toAttack.hasExitTime = false;
                toAttack.duration = 0.15f;

                var fromAttack = attackState.AddTransition(locomotionState);
                fromAttack.hasExitTime = true;
                fromAttack.exitTime = 0.85f;
                fromAttack.duration = 0.2f;
            }

            // 3. Stun / Scream State
            if (screamClip != null)
            {
                AnimatorState stunState = rootSm.AddState("Stunned", new Vector3(300, 240, 0));
                stunState.motion = screamClip;

                var anyToStun = rootSm.AddAnyStateTransition(stunState);
                anyToStun.AddCondition(AnimatorConditionMode.If, 0, "Stun");
                anyToStun.hasExitTime = false;
                anyToStun.duration = 0.1f;

                var fromStun = stunState.AddTransition(locomotionState);
                fromStun.hasExitTime = true;
                fromStun.exitTime = 0.85f;
                fromStun.duration = 0.2f;
            }

            // 4. Death State
            if (deathClip != null)
            {
                AnimatorState deathState = rootSm.AddState("Death", new Vector3(560, 240, 0));
                deathState.motion = deathClip;

                var anyToDeath = rootSm.AddAnyStateTransition(deathState);
                anyToDeath.AddCondition(AnimatorConditionMode.If, 0, "Die");
                anyToDeath.hasExitTime = false;
                anyToDeath.duration = 0.1f;
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            if (!controller.parameters.Any(p => p.name == name))
            {
                controller.AddParameter(name, type);
            }
        }

        private static AnimationClip FindClip(string fbxName)
        {
            string path = $"{AnimDir}/{fbxName}";
            if (!File.Exists(path)) return null;

            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var a in assets)
            {
                if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }
            return null;
        }

        private static void AttachZombieToSceneEnemy(Avatar avatar, RuntimeAnimatorController controller)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. Clean up duplicate StalkerEnemy instances in the scene
            StalkerAI[] allStalkers = UnityEngine.Object.FindObjectsByType<StalkerAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject enemyGO = null;

            if (allStalkers != null && allStalkers.Length > 0)
            {
                enemyGO = allStalkers[0].gameObject;
                for (int i = 1; i < allStalkers.Length; i++)
                {
                    Debug.Log($"[ScaryZombieSetup] Destroying duplicate StalkerEnemy instance {allStalkers[i].gameObject.name}");
                    UnityEngine.Object.DestroyImmediate(allStalkers[i].gameObject);
                }
            }

            if (enemyGO == null)
            {
                enemyGO = GameObject.Find("StalkerEnemy");
            }

            if (enemyGO == null)
            {
                enemyGO = new GameObject("StalkerEnemy");
                enemyGO.transform.position = new Vector3(10.5f, 0.1f, 16.5f);
            }

            // Clean up empty or duplicate "Enemies" root parents
            GameObject[] rootObjects = scene.GetRootGameObjects();
            GameObject mainEnemiesParent = null;
            foreach (var r in rootObjects)
            {
                if (r.name == "Enemies")
                {
                    if (mainEnemiesParent == null)
                    {
                        mainEnemiesParent = r;
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(r);
                    }
                }
            }
            if (mainEnemiesParent == null) mainEnemiesParent = new GameObject("Enemies");
            enemyGO.transform.SetParent(mainEnemiesParent.transform, true);

            // Remove previous model representations
            for (int i = enemyGO.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = enemyGO.transform.GetChild(i);
                if (child.name.Contains("Zombie") || child.name.Contains("Body") || child.name.Contains("Eye"))
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }

            // 2. Instantiate authentic Mixamo Zombie Ch10 model
            GameObject zombiePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZombieFbxPath);
            if (zombiePrefab == null)
            {
                Debug.LogError("[ScaryZombieSetup] Could not load zombie prefab at " + ZombieFbxPath);
                return;
            }

            GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(zombiePrefab, enemyGO.transform);
            modelInstance.name = "ZombieCharacter";
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one * 1.0f;

            // 3. Assign authentic zombie materials
            Material bodyMat = AssetDatabase.LoadAssetAtPath<Material>(BodyMatPath);
            Material headMat = AssetDatabase.LoadAssetAtPath<Material>(HeadMatPath);

            var smrs = modelInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                Material[] currentMats = smr.sharedMaterials;
                for (int m = 0; m < currentMats.Length; m++)
                {
                    string mName = (currentMats[m] != null ? currentMats[m].name : smr.name).ToLower();
                    if (mName.Contains("hair") || mName.Contains("head") || mName.Contains("1002"))
                    {
                        currentMats[m] = headMat;
                    }
                    else
                    {
                        currentMats[m] = bodyMat;
                    }
                }
                smr.sharedMaterials = currentMats;
            }

            // 4. Configure Animator
            Animator anim = modelInstance.GetComponent<Animator>();
            if (anim == null) anim = modelInstance.AddComponent<Animator>();
            anim.avatar = avatar;
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;

            // 5. Configure NavMeshAgent
            NavMeshAgent agent = enemyGO.GetComponent<NavMeshAgent>();
            if (agent == null) agent = enemyGO.AddComponent<NavMeshAgent>();
            agent.height = 1.9f;
            agent.radius = 0.45f;
            agent.baseOffset = 0f;
            agent.speed = 2.0f;
            agent.stoppingDistance = 0.5f;

            // 6. Configure CapsuleCollider
            CapsuleCollider col = enemyGO.GetComponent<CapsuleCollider>();
            if (col == null) col = enemyGO.AddComponent<CapsuleCollider>();
            col.height = 1.9f;
            col.radius = 0.45f;
            col.center = new Vector3(0f, 0.95f, 0f);

            // 7. Configure StalkerAI & Blackout Zone
            StalkerAI stalkerAI = enemyGO.GetComponent<StalkerAI>();
            if (stalkerAI == null) stalkerAI = enemyGO.AddComponent<StalkerAI>();
            stalkerAI.SetAnimator(anim);

            if (enemyGO.GetComponent<EntityBlackoutZone>() == null)
            {
                enemyGO.AddComponent<EntityBlackoutZone>();
            }

            // Ensure waypoints are assigned
            Transform wpFar = GameObject.Find("WP_FarRoom")?.transform;
            Transform wpMaint = GameObject.Find("WP_MaintRoom")?.transform;
            Transform wpKey = GameObject.Find("WP_KeyRoom")?.transform;
            if (wpFar != null) stalkerAI.AddWaypoint(wpFar);
            if (wpMaint != null) stalkerAI.AddWaypoint(wpMaint);
            if (wpKey != null) stalkerAI.AddWaypoint(wpKey);

            // 8. Ensure RuntimeNavMeshBaker is in the scene
            if (UnityEngine.Object.FindFirstObjectByType<RuntimeNavMeshBaker>() == null)
            {
                GameObject navBakerGO = new GameObject("RuntimeNavMeshBaker");
                navBakerGO.AddComponent<RuntimeNavMeshBaker>();
                Debug.Log("[ScaryZombieSetup] Added RuntimeNavMeshBaker to scene.");
            }

            // 9. Ensure InventoryManager is in the scene
            if (UnityEngine.Object.FindFirstObjectByType<InventoryManager>() == null)
            {
                GameObject invGO = new GameObject("InventoryManager");
                invGO.AddComponent<InventoryManager>();
                Debug.Log("[ScaryZombieSetup] Created InventoryManager in scene.");
            }

            // 10. Ensure dynamic NavMesh is initialized
            try
            {
                RuntimeNavMeshBaker.EnsureNavMesh();
                Debug.Log("[ScaryZombieSetup] Dynamic NavMesh verified/generated!");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ScaryZombieSetup] Dynamic NavMesh setup caught exception: " + ex.Message);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[ScaryZombieSetup] Attached Authentic Mixamo Zombie Ch10 character, Animator, and EntityBlackoutZone to StalkerEnemy!");
        }
    }
}
