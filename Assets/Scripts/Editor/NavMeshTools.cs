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

            Debug.Log("[NavMeshTools] Generating dynamic NavMesh for active scene...");

            // 2. Build NavMesh via RuntimeNavMeshBaker
            try
            {
                HorrorEscape.Enemy.RuntimeNavMeshBaker.EnsureNavMesh();
                Debug.Log("[NavMeshTools] Dynamic NavMesh generated successfully!");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NavMeshTools] NavMesh generation threw: " + ex.Message);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
    }
}
