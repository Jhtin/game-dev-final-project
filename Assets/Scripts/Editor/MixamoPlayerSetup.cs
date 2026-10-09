using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using HorrorEscape.Player;

namespace HorrorEscape.Editor
{
    /// <summary>
    /// Editor utility to import, configure Humanoid avatars, loop animations,
    /// build an Animator Controller, and instantiate the Mixamo 3D Player model
    /// onto the Player GameObject in the active Backrooms level.
    /// </summary>
    public static class MixamoPlayerSetup
    {
        private const string PlayerFbxPath = "Assets/Characters/Player/player.fbx";
        private const string AnimDir = "Assets/Characters/Player/Animations";
        private const string ControllerPath = "Assets/Characters/Player/PlayerAnimatorController.controller";
        private const string ScenePath = "Assets/Scenes/HorrorEscapeLevel.unity";

        [InitializeOnLoadMethod]
        private static void AutoSetupOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) return;
                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.isLoaded && (activeScene.name == "HorrorEscapeLevel" || string.IsNullOrEmpty(activeScene.name)))
                {
                    SetupMixamoPlayerBatch();
                }
            };
        }

        [MenuItem("Tools/Setup Mixamo Player Character & Animations")]
        public static void SetupMixamoPlayerMenu()
        {
            SetupMixamoPlayerBatch();

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Mixamo Player Setup",
                    "Mixamo 3D character model, Humanoid avatars, looping animations, and Animator Controller successfully configured and attached to the Player rig in the Backrooms level!",
                    "OK");
            }
        }

        public static void SetupMixamoPlayerBatch()
        {
            Debug.Log("[MixamoPlayerSetup] Starting Mixamo Player & Animation configuration...");

            // 1. Configure player.fbx as Humanoid Avatar
            ConfigurePlayerModelImporter();

            // 2. Load the generated Humanoid Avatar
            Avatar playerAvatar = LoadPlayerAvatar();
            if (playerAvatar == null)
            {
                Debug.LogError("[MixamoPlayerSetup] Failed to retrieve Humanoid Avatar from " + PlayerFbxPath);
                return;
            }
            Debug.Log("[MixamoPlayerSetup] Retrieved Humanoid Avatar: " + playerAvatar.name);

            // 3. Configure all animation FBX files in Animations directory
            ConfigureAnimations(playerAvatar);

            // 4. Build or update the Player Animator Controller
            RuntimeAnimatorController animController = BuildAnimatorController();

            // 5. Open scene and attach model to Player rig
            AttachCharacterToScenePlayer(playerAvatar, animController);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[MixamoPlayerSetup] Mixamo Player configuration finished successfully!");
        }

        private static void ConfigurePlayerModelImporter()
        {
            ModelImporter importer = AssetImporter.GetAtPath(PlayerFbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[MixamoPlayerSetup] ModelImporter not found at " + PlayerFbxPath);
                return;
            }

            bool needsReimport = false;

            if (importer.animationType != ModelImporterAnimationType.Human)
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
                AssetDatabase.ImportAsset(PlayerFbxPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();
                Debug.Log("[MixamoPlayerSetup] Reimported player.fbx as Humanoid Avatar.");
            }
        }

        private static Avatar LoadPlayerAvatar()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(PlayerFbxPath);
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
                bool shouldLoop = lower.Contains("idle") || lower.Contains("walk") || lower.Contains("run") || lower.Contains("sneak") || lower.Contains("crouch");

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
                    Debug.Log($"[MixamoPlayerSetup] Configured Humanoid animation: {Path.GetFileName(file)} (Loop: {shouldLoop})");
                }
            }
        }

        private static RuntimeAnimatorController BuildAnimatorController()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            // Ensure parameters exist
            EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "IsMoving", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "IsSprinting", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "IsCrouching", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "IsGrounded", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            // Load primary clips
            AnimationClip idleClip = FindClip("idle.fbx") ?? FindClip("idle (2).fbx");
            AnimationClip walkClip = FindClip("walking.fbx");
            AnimationClip runClip = FindClip("running.fbx");
            AnimationClip crouchClip = FindClip("crouched sneaking left.fbx") ?? FindClip("crouched sneaking right.fbx");

            // Clear old states to build fresh clean state machine
            var existingStates = sm.states;
            foreach (var st in existingStates)
            {
                sm.RemoveState(st.state);
            }

            // 1. Locomotion State with 1D Blend Tree (Idle -> Walk -> Run)
            AnimatorState locomotionState = sm.AddState("Locomotion", new Vector3(300, 100, 0));
            BlendTree blendTree = new BlendTree();
            blendTree.name = "LocomotionTree";
            blendTree.blendType = BlendTreeType.Simple1D;
            blendTree.blendParameter = "Speed";

            if (idleClip != null) blendTree.AddChild(idleClip, 0.0f);
            if (walkClip != null) blendTree.AddChild(walkClip, 1.0f);
            if (runClip != null) blendTree.AddChild(runClip, 2.0f);

            AssetDatabase.AddObjectToAsset(blendTree, controller);
            locomotionState.motion = blendTree;

            sm.defaultState = locomotionState;

            // 2. Crouch State
            if (crouchClip != null)
            {
                AnimatorState crouchState = sm.AddState("Crouch", new Vector3(300, 220, 0));
                crouchState.motion = crouchClip;

                // Transitions
                var toCrouch = locomotionState.AddTransition(crouchState);
                toCrouch.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
                toCrouch.hasExitTime = false;
                toCrouch.duration = 0.2f;

                var fromCrouch = crouchState.AddTransition(locomotionState);
                fromCrouch.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
                fromCrouch.hasExitTime = false;
                fromCrouch.duration = 0.2f;
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

        private static void AttachCharacterToScenePlayer(Avatar avatar, RuntimeAnimatorController controller)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject playerGO = GameObject.FindWithTag("Player");
            if (playerGO == null)
            {
                FirstPersonController fpc = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
                if (fpc != null) playerGO = fpc.gameObject;
            }

            if (playerGO == null)
            {
                Debug.LogError("[MixamoPlayerSetup] Could not find Player GameObject in scene " + ScenePath);
                return;
            }

            // Remove any old cylinder or temporary body placeholder
            Transform oldBody = playerGO.transform.Find("Body");
            if (oldBody != null)
            {
                UnityEngine.Object.DestroyImmediate(oldBody.gameObject);
            }

            // Check if MixamoCharacter already instantiated
            Transform existingModel = playerGO.transform.Find("MixamoCharacter");
            if (existingModel != null)
            {
                UnityEngine.Object.DestroyImmediate(existingModel.gameObject);
            }

            // Instantiate player.fbx
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerFbxPath);
            if (playerPrefab == null)
            {
                Debug.LogError("[MixamoPlayerSetup] Could not load player prefab at " + PlayerFbxPath);
                return;
            }

            GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, playerGO.transform);
            modelInstance.name = "MixamoCharacter";
            modelInstance.transform.localPosition = new Vector3(0f, 0f, 0f);
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one;

            // Ensure Materials are properly assigned to all renderers
            Material bodyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Characters/Player/Materials/Ch17_body.mat");
            Material hairMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Characters/Player/Materials/Ch17_hair.mat");

            SkinnedMeshRenderer[] renderers = modelInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in renderers)
            {
                Debug.Log($"[MixamoPlayerSetup] SMR: '{smr.gameObject.name}', bones: {smr.bones.Length}, rootBone: {smr.rootBone?.name}");
                string rName = smr.gameObject.name.ToLower();
                if (rName.Contains("hair") || rName.Contains("eyelash"))
                {
                    if (hairMat != null) smr.sharedMaterial = hairMat;
                }
                else
                {
                    if (bodyMat != null) smr.sharedMaterial = bodyMat;
                }
            }

            // Also check standard MeshRenderers if any
            MeshRenderer[] meshRenderers = modelInstance.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in meshRenderers)
            {
                string rName = mr.gameObject.name.ToLower();
                if (rName.Contains("hair") || rName.Contains("eyelash"))
                {
                    if (hairMat != null) mr.sharedMaterial = hairMat;
                }
                else
                {
                    if (bodyMat != null) mr.sharedMaterial = bodyMat;
                }
            }

            // Ensure Animator component
            Animator anim = modelInstance.GetComponent<Animator>();
            if (anim == null) anim = modelInstance.AddComponent<Animator>();

            anim.avatar = avatar;
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;

            // Hook Animator and Camera into FirstPersonController
            CharacterController cc = playerGO.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.height = 1.8f;
                cc.center = new Vector3(0f, 0.9f, 0f);
            }

            Transform camT = playerGO.transform.Find("Main Camera");
            if (camT != null)
            {
                camT.localPosition = new Vector3(0f, 1.60f, 0.12f);
                Camera camComp = camT.GetComponent<Camera>();
                if (camComp != null)
                {
                    camComp.nearClipPlane = 0.05f;
                }
            }

            FirstPersonController controllerScript = playerGO.GetComponent<FirstPersonController>();
            if (controllerScript != null)
            {
                SerializedObject so = new SerializedObject(controllerScript);
                SerializedProperty animProp = so.FindProperty("characterAnimator");
                if (animProp != null)
                {
                    animProp.objectReferenceValue = anim;
                    so.ApplyModifiedProperties();
                }
            }

            if (!EditorApplication.isPlayingOrWillChangePlaymode && !UnityEngine.Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log("[MixamoPlayerSetup] Successfully attached textured Mixamo character model to Player GameObject!");
        }
    }
}
