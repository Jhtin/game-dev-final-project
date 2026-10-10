using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using HorrorEscape.Environment;

namespace HorrorEscape.Editor
{
    [CustomEditor(typeof(BackroomsLevelGenerator))]
    public class BackroomsLevelGeneratorEditor : UnityEditor.Editor
    {
        private BackroomsLevelGenerator.ValidationReport lastReport;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            BackroomsLevelGenerator gen = (BackroomsLevelGenerator)target;

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Procedural Backrooms Controls", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(0.95f, 0.85f, 0.35f);
            if (GUILayout.Button("GENERATE LEVEL 0 BACKROOMS (10-MIN MAZE)", GUILayout.Height(36)))
            {
                lastReport = gen.GenerateMap();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            GUI.backgroundColor = new Color(0.4f, 0.9f, 0.5f);
            if (GUILayout.Button("SETUP COMPLETE 10-MIN SURVIVAL GAMEPLAY", GUILayout.Height(32)))
            {
                BackroomsGameplaySetup.Setup10MinuteGameplayBatch();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            GUI.backgroundColor = new Color(0.35f, 0.75f, 0.95f);
            if (GUILayout.Button("POPULATE BACKROOMS FURNITURE", GUILayout.Height(30)))
            {
                FurniturePlacer.PopulateFurnitureMenu();
            }

            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("Validate Map & Playtime QC", GUILayout.Height(28)))
            {
                lastReport = gen.ValidateMap();
                Debug.Log(lastReport.stringify());
            }

            GUI.backgroundColor = new Color(1.0f, 0.4f, 0.4f);
            if (GUILayout.Button("Clear Generated Map", GUILayout.Height(24)))
            {
                if (EditorUtility.DisplayDialog("Clear Map", "Are you sure you want to delete the generated map geometry?", "Yes", "No"))
                {
                    gen.ClearExistingMap();
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                }
            }

            GUI.backgroundColor = Color.white;

            if (lastReport != null)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox(lastReport.stringify(), lastReport.allChecksPassed ? MessageType.Info : MessageType.Warning);
            }
        }

        [MenuItem("Tools/Generate Level 0 Backrooms Map (Procedural)")]
        public static void GenerateLevel0FromMenu()
        {
            BackroomsLevelGenerator gen = Object.FindFirstObjectByType<BackroomsLevelGenerator>();
            if (gen == null)
            {
                GameObject genGO = new GameObject("BackroomsLevelGenerator");
                gen = genGO.AddComponent<BackroomsLevelGenerator>();
                Undo.RegisterCreatedObjectUndo(genGO, "Create BackroomsLevelGenerator");
            }

            gen.EnsureMaterials();
            var report = gen.GenerateMap();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Backrooms Level 0 Generated",
                    $"Map Generation & QC Validation Complete!\n\n" +
                    $"Estimated Playtime: {report.estimatedPlaytimeMinutes:F1} minutes\n" +
                    $"Walkable Cells: {report.totalWalkableCells}\n" +
                    $"Shortest Path to Exit: {report.shortestPathSteps} steps ({report.shortestPathDistanceMeters:F0}m)\n" +
                    $"All 10 QC Checks Passed: {report.allChecksPassed}",
                    "OK");
            }
        }

        public static void GenerateLevel0Batch()
        {
            string scenePath = "Assets/Scenes/HorrorEscapeLevel.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            BackroomsLevelGenerator gen = Object.FindFirstObjectByType<BackroomsLevelGenerator>();
            if (gen == null)
            {
                GameObject genGO = new GameObject("BackroomsLevelGenerator");
                gen = genGO.AddComponent<BackroomsLevelGenerator>();
            }

            gen.EnsureMaterials();
            var report = gen.GenerateMap();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[BackroomsLevelGenerator] Batch generation finished successfully!\n" + report.ToString());
        }
    }
}
