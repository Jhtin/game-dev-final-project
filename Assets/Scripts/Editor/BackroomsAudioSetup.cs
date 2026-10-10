using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.Enemy;

namespace HorrorEscape.Editor
{
    /// <summary>
    /// Editor utility to find and assign all imported Backrooms Ambience music tracks
    /// and Backrooms Entity SFX clips to the scene's AudioManager and Stalker enemy.
    /// </summary>
    public static class BackroomsAudioSetup
    {
        private const string ScenePath = "Assets/Scenes/HorrorEscapeLevel.unity";
        private const string AmbienceDir = "Assets/Backrooms Ambience";
        private const string EntitySFXDir = "Assets/Backrooms Entity SFX";

        [InitializeOnLoadMethod]
        private static void AutoSetupOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.isLoaded && (activeScene.name == "HorrorEscapeLevel" || string.IsNullOrEmpty(activeScene.name)))
                {
                    SetupAudioBatch();
                }
            };
        }

        [MenuItem("Tools/Setup Backrooms Audio & Entity SFX")]
        public static void SetupAudioMenu()
        {
            SetupAudioBatch();

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Backrooms Audio Setup",
                    "Backrooms Ambience music and Entity Sound Effects successfully configured and assigned to AudioManager and Stalker Enemy!",
                    "OK");
            }
        }

        public static void SetupAudioBatch()
        {
            Debug.Log("[BackroomsAudioSetup] Starting Backrooms Audio & Entity SFX configuration...");

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.isLoaded || scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            // 1. Locate AudioManager
            AudioManager audioMgr = UnityEngine.Object.FindFirstObjectByType<AudioManager>();
            if (audioMgr == null)
            {
                GameObject go = GameObject.Find("AudioManager");
                if (go == null) go = new GameObject("AudioManager");
                audioMgr = go.AddComponent<AudioManager>();
            }

            SerializedObject audioSo = new SerializedObject(audioMgr);

            // 2. Load and Assign Backrooms Ambience Tracks
            // Tracks:
            // Phase 1 Explore: "3_LOOP_Backrooms Friend" or "1_LOOP_Backrooms Tea Party" (quiet, lonely, atmospheric)
            // Phase 2-4 Tension: "4_LOOP_Backrooms Stalker" (heavy suspense, unnerving drone)
            // Phase 5 Escape: "6_LOOP_Backrooms Anomaly" or "8_LOOP_Broken Backrooms Portal" (intense, frantic)
            AudioClip exploreClip = LoadAudioClip($"{AmbienceDir}/3_LOOP_Backrooms Friend (by juanjo_sound).wav")
                                 ?? LoadAudioClip($"{AmbienceDir}/1_LOOP_Backrooms Tea Party (by juanjo_sound).wav");

            AudioClip tensionClip = LoadAudioClip($"{AmbienceDir}/4_LOOP_Backrooms Stalker (by juanjo_sound).wav")
                                 ?? LoadAudioClip($"{AmbienceDir}/2_LOOP_Backrooms Portal (by juanjo_sound).wav");

            AudioClip escapeClip = LoadAudioClip($"{AmbienceDir}/6_LOOP_Backrooms Anomaly (by juanjo_sound).wav")
                                ?? LoadAudioClip($"{AmbienceDir}/8_LOOP_Broken Backrooms Portal (by juanjo_sound).wav");

            audioSo.FindProperty("ambienceExploreClip").objectReferenceValue = exploreClip;
            audioSo.FindProperty("ambienceTensionClip").objectReferenceValue = tensionClip;
            audioSo.FindProperty("ambienceEscapeClip").objectReferenceValue = escapeClip;
            audioSo.FindProperty("ambienceVolume").floatValue = 0.45f;

            Debug.Log($"[BackroomsAudioSetup] Ambience tracks assigned: Explore='{exploreClip?.name}', Tension='{tensionClip?.name}', Escape='{escapeClip?.name}'");

            // 3. Load and Assign Backrooms Entity SFX (all 30 entity sound clips)
            if (Directory.Exists(EntitySFXDir))
            {
                string[] files = Directory.GetFiles(EntitySFXDir, "*.wav")
                    .OrderBy(f =>
                    {
                        // Natural numerical sort: 1, 2, ... 10, ... 30
                        string name = Path.GetFileNameWithoutExtension(f);
                        int num = 0;
                        var digits = new string(name.Where(char.IsDigit).ToArray());
                        int.TryParse(digits, out num);
                        return num;
                    })
                    .ToArray();

                SerializedProperty clipsProp = audioSo.FindProperty("entityClips");
                clipsProp.ClearArray();
                clipsProp.arraySize = files.Length;

                for (int i = 0; i < files.Length; i++)
                {
                    string assetPath = files[i].Replace("\\", "/");
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                    clipsProp.GetArrayElementAtIndex(i).objectReferenceValue = clip;
                }

                Debug.Log($"[BackroomsAudioSetup] Loaded {files.Length} Backrooms Entity SFX clips into AudioManager.");

                // Also populate fallback slots if empty
                AudioClip screamClip = LoadAudioClip($"{EntitySFXDir}/juanjo_sound - Backrooms Entity 8.wav")
                                    ?? LoadAudioClip($"{EntitySFXDir}/juanjo_sound - Backrooms Entity 9.wav");
                AudioClip growlClip = LoadAudioClip($"{EntitySFXDir}/juanjo_sound - Backrooms Entity 1.wav");

                if (audioSo.FindProperty("monsterSpottedClip").objectReferenceValue == null && screamClip != null)
                {
                    audioSo.FindProperty("monsterSpottedClip").objectReferenceValue = screamClip;
                }
                if (audioSo.FindProperty("jumpscareClip").objectReferenceValue == null && screamClip != null)
                {
                    audioSo.FindProperty("jumpscareClip").objectReferenceValue = screamClip;
                }
                if (audioSo.FindProperty("monsterGrowlClip").objectReferenceValue == null && growlClip != null)
                {
                    audioSo.FindProperty("monsterGrowlClip").objectReferenceValue = growlClip;
                }

                // Also assign Gunshot sound from imported Free Pack if empty
                if (audioSo.FindProperty("gunshotClip").objectReferenceValue == null)
                {
                    AudioClip gunClip = LoadAudioClip("Assets/Free Pack/Hand Gun 1.wav");
                    if (gunClip != null)
                    {
                        audioSo.FindProperty("gunshotClip").objectReferenceValue = gunClip;
                        Debug.Log("[BackroomsAudioSetup] Assigned Hand Gun 1.wav to AudioManager.gunshotClip.");
                    }
                }
            }

            // 4. Load and Assign Door, Cabinet and Locker Sound Pack (Free)
            const string DoorPackDir = "Assets/Door, Cabinet and Locker Sound Pack (Free)/FREE VERSION";
            if (Directory.Exists(DoorPackDir))
            {
                AudioClip doorOpen = LoadAudioClip($"{DoorPackDir}/Open Door 13.wav");
                AudioClip doorClose = LoadAudioClip($"{DoorPackDir}/Close Door 16.wav");
                AudioClip doorUnlock = LoadAudioClip($"{DoorPackDir}/Unlock 1.wav");
                AudioClip doorLocked = LoadAudioClip($"{DoorPackDir}/Locked Door Turn Doorknob 3.wav");
                AudioClip doorCreak = LoadAudioClip($"{DoorPackDir}/Open Push Door With Long Creak 1.wav");
                AudioClip doorLatch = LoadAudioClip($"{DoorPackDir}/Close Latch 1.wav");
                AudioClip cabOpen = LoadAudioClip($"{DoorPackDir}/Open Push Door With Long Creak 1.wav");
                AudioClip cabClose = LoadAudioClip($"{DoorPackDir}/Close Cabinet Cupboard 1.wav");
                AudioClip lockerOpen = LoadAudioClip($"{DoorPackDir}/Open Close Metal Door Locker Cabinet Box 3.wav");
                AudioClip lockerClose = LoadAudioClip($"{DoorPackDir}/Close Metal Door Locker Cabinet Box 1.wav");
                AudioClip gateClang = LoadAudioClip($"{DoorPackDir}/Swinging Metal Door Clang Shut 1.wav");
                AudioClip gateOpen = LoadAudioClip($"{DoorPackDir}/Open Push Door With Long Creak 1.wav");
                AudioClip powerRestore = LoadAudioClip($"{DoorPackDir}/Call Elevator Button Lift Big Large Mechanical Noise 1.wav");

                audioSo.FindProperty("doorOpenClip").objectReferenceValue = doorOpen;
                audioSo.FindProperty("doorCloseClip").objectReferenceValue = doorClose;
                audioSo.FindProperty("doorUnlockClip").objectReferenceValue = doorUnlock;
                audioSo.FindProperty("doorLockedClip").objectReferenceValue = doorLocked;
                audioSo.FindProperty("doorCreakOpenClip").objectReferenceValue = doorCreak;
                audioSo.FindProperty("doorLatchClip").objectReferenceValue = doorLatch;
                audioSo.FindProperty("cabinetOpenClip").objectReferenceValue = cabOpen;
                audioSo.FindProperty("cabinetCloseClip").objectReferenceValue = cabClose;
                audioSo.FindProperty("lockerOpenClip").objectReferenceValue = lockerOpen;
                audioSo.FindProperty("lockerCloseClip").objectReferenceValue = lockerClose;
                audioSo.FindProperty("gateOpenClip").objectReferenceValue = gateOpen;
                audioSo.FindProperty("gateClangClip").objectReferenceValue = gateClang;

                if (powerRestore != null)
                {
                    audioSo.FindProperty("powerRestoreClip").objectReferenceValue = powerRestore;
                }

                Debug.Log("[BackroomsAudioSetup] Assigned Door, Cabinet & Locker Sound Pack clips to AudioManager.");
            }

            audioSo.ApplyModifiedProperties();

            // 4. Attach EntityProximityAudio component to StalkerEnemy in scene
            StalkerAI stalker = UnityEngine.Object.FindFirstObjectByType<StalkerAI>();
            if (stalker != null)
            {
                EntityProximityAudio proximityAudio = stalker.GetComponent<EntityProximityAudio>();
                if (proximityAudio == null)
                {
                    proximityAudio = stalker.gameObject.AddComponent<EntityProximityAudio>();
                    Debug.Log("[BackroomsAudioSetup] Attached EntityProximityAudio to Stalker Enemy.");
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[BackroomsAudioSetup] Successfully configured and saved Backrooms Audio in " + ScenePath);
        }

        private static AudioClip LoadAudioClip(string path)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
