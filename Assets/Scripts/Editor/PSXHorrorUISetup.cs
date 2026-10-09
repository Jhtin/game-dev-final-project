using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using HorrorEscape.UI;

namespace HorrorEscape.Editor
{
    /// <summary>
    /// Editor utility to link PSX Horror UI Free assets (Fog Theme)
    /// into HUDManager and MainMenuManager across scenes.
    /// </summary>
    public static class PSXHorrorUISetup
    {
        private const string ScenePath = "Assets/Scenes/HorrorEscapeLevel.unity";
        private const string PSXRootDir = "Assets/PSXHorrorUIFree";

        [InitializeOnLoadMethod]
        private static void AutoSetupOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.isLoaded && (activeScene.name == "HorrorEscapeLevel" || string.IsNullOrEmpty(activeScene.name)))
                {
                    SetupPSXUIBatch();
                }
            };
        }

        [MenuItem("Tools/Setup PSX Horror UI (Fog Theme & Effects)")]
        public static void SetupPSXUIMenu()
        {
            SetupPSXUIBatch();

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("PSX Horror UI Setup",
                    "PSX Horror UI (Fog Theme, ECG Waveforms, Status Bars, Icons & Cursors) successfully configured and assigned to HUDManager!",
                    "OK");
            }
        }

        public static void SetupPSXUIBatch()
        {
            Debug.Log("[PSXHorrorUISetup] Starting PSX Horror UI configuration...");

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.isLoaded || scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            HUDManager hudMgr = Object.FindFirstObjectByType<HUDManager>();
            if (hudMgr == null)
            {
                GameObject go = GameObject.Find("HUDManager");
                if (go == null) go = new GameObject("HUDManager");
                hudMgr = go.AddComponent<HUDManager>();
            }

            SerializedObject hudSo = new SerializedObject(hudMgr);

            // 1. Theme Frames & Panels
            SetSprite(hudSo, "psxWindowSprite", $"{PSXRootDir}/Sprites/themes/fog/window.png");
            SetSprite(hudSo, "psxPanelSprite", $"{PSXRootDir}/Sprites/themes/fog/panel.png");
            SetSprite(hudSo, "psxDocumentSprite", $"{PSXRootDir}/Sprites/themes/fog/document.png");

            // 2. Button States
            SetSprite(hudSo, "psxButtonNormalSprite", $"{PSXRootDir}/Sprites/themes/fog/button_normal.png");
            SetSprite(hudSo, "psxButtonHoverSprite", $"{PSXRootDir}/Sprites/themes/fog/button_hover.png");
            SetSprite(hudSo, "psxButtonPressedSprite", $"{PSXRootDir}/Sprites/themes/fog/button_pressed.png");

            // 3. Progress Bars
            SetSprite(hudSo, "psxProgressBgSprite", $"{PSXRootDir}/Sprites/themes/fog/progress_bg.png");
            SetSprite(hudSo, "psxProgressFillSprite", $"{PSXRootDir}/Sprites/themes/fog/progress_fill.png");

            // 4. Large Icons
            SetSprite(hudSo, "psxPistolIcon", $"{PSXRootDir}/Sprites/icons/large/pistol.png");
            SetSprite(hudSo, "psxNoteIcon", $"{PSXRootDir}/Sprites/icons/large/note.png");
            SetSprite(hudSo, "psxKeyIcon", $"{PSXRootDir}/Sprites/icons/large/key_skeleton.png");

            // 5. Markers & Pointers
            SetSprite(hudSo, "psxPointerIcon", $"{PSXRootDir}/Sprites/themes/fog/pointer.png");
            SetSprite(hudSo, "psxMarkItemIcon", $"{PSXRootDir}/Sprites/themes/fog/mark_item.png");
            SetSprite(hudSo, "psxMarkDoorIcon", $"{PSXRootDir}/Sprites/themes/fog/mark_door.png");

            // 6. ECG Status Monitors
            SetSprite(hudSo, "psxEcgFineSprite", $"{PSXRootDir}/Sprites/status/ecg_fine.png");
            SetSprite(hudSo, "psxEcgCautionSprite", $"{PSXRootDir}/Sprites/status/ecg_caution.png");
            SetSprite(hudSo, "psxEcgDangerSprite", $"{PSXRootDir}/Sprites/status/ecg_danger.png");

            // 7. Status Bars
            SetSprite(hudSo, "psxBarFineSprite", $"{PSXRootDir}/Sprites/status/bar_fine.png");
            SetSprite(hudSo, "psxBarCautionSprite", $"{PSXRootDir}/Sprites/status/bar_caution.png");
            SetSprite(hudSo, "psxBarDangerSprite", $"{PSXRootDir}/Sprites/status/bar_danger.png");

            // 8. Hardware Mouse Cursor
            Texture2D cursorTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PSXRootDir}/Cursors/cursor_2x.png")
                               ?? AssetDatabase.LoadAssetAtPath<Texture2D>($"{PSXRootDir}/Cursors/cursor.png");
            SerializedProperty cursorProp = hudSo.FindProperty("psxCursorTexture");
            if (cursorProp != null) cursorProp.objectReferenceValue = cursorTex;

            hudSo.ApplyModifiedProperties();

            // MainMenuManager if present in scene
            MainMenuManager menuMgr = Object.FindFirstObjectByType<MainMenuManager>();
            if (menuMgr != null)
            {
                SerializedObject menuSo = new SerializedObject(menuMgr);
                SetSprite(menuSo, "psxWindowSprite", $"{PSXRootDir}/Sprites/themes/fog/window.png");
                SetSprite(menuSo, "psxButtonNormalSprite", $"{PSXRootDir}/Sprites/themes/fog/button_normal.png");
                SetSprite(menuSo, "psxButtonHoverSprite", $"{PSXRootDir}/Sprites/themes/fog/button_hover.png");
                SetSprite(menuSo, "psxButtonPressedSprite", $"{PSXRootDir}/Sprites/themes/fog/button_pressed.png");
                SerializedProperty menuCursorProp = menuSo.FindProperty("psxCursorTexture");
                if (menuCursorProp != null) menuCursorProp.objectReferenceValue = cursorTex;
                menuSo.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[PSXHorrorUISetup] Successfully configured PSX Horror UI in " + ScenePath);

            // Also invoke Polygon Survival setup
            PolygonSurvivalSetup.SetupPolygonSurvivalBatch();
        }

        private static void SetSprite(SerializedObject so, string propertyName, string assetPath)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                prop.objectReferenceValue = sprite;
            }
        }
    }
}
