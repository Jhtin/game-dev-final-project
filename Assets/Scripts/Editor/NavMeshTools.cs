using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorEscape.Editor
{
    public static class NavMeshTools
    {
        private const string ScenePath = "Assets/Scenes/HorrorEscapeLevel.unity";

        [MenuItem("Tools/Bake Scene NavMesh")]
        public static void BakeNavMesh()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. Mark all environment objects as NavigationStatic if needed
            GameObject mapParent = GameObject.Find("BackroomsMap_Overhaul");
            if (mapParent == null) mapParent = GameObject.Find("ModularBackroomsMap");
            if (mapParent == null) mapParent = GameObject.Find("Level");

            int staticCount = 0;
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var r in renderers)
            {
                // If it's part of the environment, mark as NavigationStatic
                if (r.GetComponentInParent<HorrorEscape.Enemy.StalkerAI>() != null) continue;
                if (r.GetComponentInParent<HorrorEscape.Player.FirstPersonController>() != null) continue;

                GameObjectUtility.SetStaticEditorFlags(r.gameObject, 
                    GameObjectUtility.GetStaticEditorFlags(r.gameObject) | StaticEditorFlags.NavigationStatic);
                staticCount++;
            }

            Debug.Log($"[NavMeshTools] Marked {staticCount} renderers as NavigationStatic.");

            // 2. Call UnityEditor.NavMeshBuilder.BuildNavMesh()
            try
            {
                UnityEditor.NavMeshBuilder.BuildNavMesh();
                Debug.Log("[NavMeshTools] UnityEditor.NavMeshBuilder.BuildNavMesh() completed successfully!");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NavMeshTools] UnityEditor.NavMeshBuilder.BuildNavMesh() threw: " + ex.Message);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
    }
}
