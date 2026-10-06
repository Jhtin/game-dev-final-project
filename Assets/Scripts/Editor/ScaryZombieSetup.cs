using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using HorrorEscape.Enemy;

namespace HorrorEscape.Editor
{
    /// <summary>
    /// Editor utility to import, configure Humanoid avatars, loop animations,
    /// build an Animator Controller, and instantiate the authentic Scary Zombie model
    /// (Ch17_nonPBR / zombie.fbx) onto the Stalker Enemy in the Backrooms level.
    /// </summary>
    public static class ScaryZombieSetup
    {
        private const string ZombieFbxPath = "Assets/Characters/Zombie/zombie.fbx";
        private const string AnimDir = "Assets/Characters/Zombie/Animations";
        private const string ControllerPath = "Assets/Characters/Zombie/ZombieAnimatorController.controller";
        private const string ScenePath = "Assets/Scenes/HorrorEscapeLevel.unity";

        [MenuItem("Tools/Setup Scary Zombie Enemy & Animations")]
        public static void SetupScaryZombieMenu()
        {
            SetupScaryZombieBatch();

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Scary Zombie Setup",
                    "Scary Zombie 3D model, Humanoid avatar, looping animations, and Animator Controller successfully configured and attached to the Stalker Enemy in the Backrooms level!",
                    "OK");
            }
        }

        public static void SetupScaryZombieBatch()
        {
            Debug.Log("[ScaryZombieSetup] Starting Scary Zombie configuration...");

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

            // 5. Open scene and attach model to Stalker Enemy rig
            AttachZombieToSceneEnemy(zombieAvatar, animController);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ScaryZombieSetup] Scary Zombie configuration finished successfully!");
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

            GameObject enemyGO = GameObject.Find("StalkerEnemy");
            if (enemyGO == null)
            {
                StalkerAI ai = UnityEngine.Object.FindFirstObjectByType<StalkerAI>();
                if (ai != null) enemyGO = ai.gameObject;
            }

            if (enemyGO == null)
            {
                Debug.LogError("[ScaryZombieSetup] Could not find StalkerEnemy in scene " + ScenePath);
                return;
            }

            // Remove old placeholder body and eye objects
            Transform oldBody = enemyGO.transform.Find("Body");
            if (oldBody != null) UnityEngine.Object.DestroyImmediate(oldBody.gameObject);

            Transform oldEyeL = enemyGO.transform.Find("EyeLeft");
            if (oldEyeL != null) UnityEngine.Object.DestroyImmediate(oldEyeL.gameObject);

            Transform oldEyeR = enemyGO.transform.Find("EyeRight");
            if (oldEyeR != null) UnityEngine.Object.DestroyImmediate(oldEyeR.gameObject);

            Transform existingZombie = enemyGO.transform.Find("ZombieCharacter");
            if (existingZombie != null) UnityEngine.Object.DestroyImmediate(existingZombie.gameObject);

            // Instantiate zombie.fbx model as child
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

            // Configure Animator
            Animator anim = modelInstance.GetComponent<Animator>();
            if (anim == null) anim = modelInstance.AddComponent<Animator>();
            anim.avatar = avatar;
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;

            // Configure NavMeshAgent
            NavMeshAgent agent = enemyGO.GetComponent<NavMeshAgent>();
            if (agent == null) agent = enemyGO.AddComponent<NavMeshAgent>();
            agent.height = 1.9f;
            agent.radius = 0.45f;
            agent.baseOffset = 0f;

            // Configure StalkerAI
            StalkerAI stalkerAI = enemyGO.GetComponent<StalkerAI>();
            if (stalkerAI != null)
            {
                stalkerAI.SetAnimator(anim);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[ScaryZombieSetup] Attached Scary Zombie character and Animator to StalkerEnemy!");
        }
    }
}
