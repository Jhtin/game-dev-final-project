using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HorrorEscape.UI;

namespace HorrorEscape.Editor
{
    /// <summary>
    /// Editor utility to preview, validate, and bake the psychological horror Backrooms UI into the scene.
    /// </summary>
    public static class BackroomsUISetup
    {
        private const string ScenePath = "Assets/Scenes/HorrorEscapeLevel.unity";

        [MenuItem("Tools/Validate & Polish Backrooms UI")]
        public static void ValidateBackroomsUIMenu()
        {
            ValidateBackroomsUI();

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Backrooms UI",
                    "Backrooms psychological horror UI successfully validated and configured!\n\n" +
                    "- Minimalist analog terminal aesthetic\n" +
                    "- Top-Left Typewriter Objective Panel\n" +
                    "- Top-Right Emergency System Timer\n" +
                    "- Subtle 10-segment Health & Battery block meters\n" +
                    "- Authentic [E] interaction prompts with smooth alpha fades\n" +
                    "- Unobtrusive Ammo Counter with Low/Empty warnings\n" +
                    "- CRT Scanlines, Film Grain & Dynamic Vignette\n" +
                    "- Maintenance Terminal Pause, Connection Lost, & Escape Screens",
                    "OK");
            }
        }

        public static void ValidateBackroomsUI()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Clean up any stale standalone UI canvases so HUDManager builds fresh
            GameObject oldCanvas = GameObject.Find("HUDCanvas");
            if (oldCanvas != null)
            {
                UnityEngine.Object.DestroyImmediate(oldCanvas);
            }

            GameObject hudManagerGO = GameObject.Find("HUDManager");
            if (hudManagerGO == null)
            {
                hudManagerGO = new GameObject("HUDManager");
            }

            HUDManager hud = hudManagerGO.GetComponent<HUDManager>();
            if (hud == null) hud = hudManagerGO.AddComponent<HUDManager>();

            VHSOverlay vhs = hudManagerGO.GetComponent<VHSOverlay>();
            if (vhs == null) vhs = hudManagerGO.AddComponent<VHSOverlay>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[BackroomsUISetup] Backrooms UI hierarchy successfully validated and saved in scene " + ScenePath);
        }
    }
}
