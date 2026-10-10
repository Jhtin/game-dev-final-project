using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorEscape.Enemy
{
    /// <summary>
    /// Ensures a valid NavMesh is always present in the Backrooms level.
    /// If static NavMesh was not baked or is missing in the scene, this dynamically
    /// collects colliders and builds the NavMesh at startup, preventing
    /// "Failed to create agent because there is no valid NavMesh".
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class RuntimeNavMeshBaker : MonoBehaviour
    {
        private static NavMeshDataInstance navMeshDataInstance;

        private void Awake()
        {
            EnsureNavMesh();
        }

        public static void EnsureNavMesh()
        {
            // Check if there is already an active NavMesh in the scene
            try
            {
                var triangulation = NavMesh.CalculateTriangulation();
                if (triangulation.vertices != null && triangulation.vertices.Length > 0)
                {
                    Debug.Log($"[RuntimeNavMeshBaker] Valid NavMesh already active ({triangulation.vertices.Length} vertices).");
                    return;
                }
            }
            catch
            {
                // If checking fails, proceed to generate
            }

            Debug.Log("[RuntimeNavMeshBaker] No NavMesh found! Generating dynamic NavMesh for Backrooms level...");

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.45f;
            settings.agentHeight = 1.9f;
            settings.agentSlope = 45f;
            settings.agentClimb = 0.4f;

            // Compute actual bounding box of all scene geometry
            Bounds bounds = new Bounds(Vector3.zero, new Vector3(100f, 20f, 100f));
            Renderer[] allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            if (allRenderers.Length > 0)
            {
                bounds = allRenderers[0].bounds;
                for (int i = 1; i < allRenderers.Length; i++)
                {
                    bounds.Encapsulate(allRenderers[i].bounds);
                }
                bounds.Expand(15.0f);
            }

            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
            List<NavMeshBuildMarkup> markups = new List<NavMeshBuildMarkup>();

            int layerMask = ~0; // Include all layers
            NavMeshBuilder.CollectSources(bounds, layerMask, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);

            if (sources.Count == 0)
            {
                // Fallback to render meshes if physics colliders weren't picked up
                NavMeshBuilder.CollectSources(bounds, layerMask, NavMeshCollectGeometry.RenderMeshes, 0, markups, sources);
            }

            Debug.Log($"[RuntimeNavMeshBaker] Collected {sources.Count} geometry sources for NavMesh generation.");

            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data != null)
            {
                if (navMeshDataInstance.valid)
                {
                    NavMesh.RemoveNavMeshData(navMeshDataInstance);
                }
                navMeshDataInstance = NavMesh.AddNavMeshData(data);
                Debug.Log($"[RuntimeNavMeshBaker] Successfully built and activated dynamic NavMeshData! (Instance valid: {navMeshDataInstance.valid})");
            }
            else
            {
                Debug.LogError("[RuntimeNavMeshBaker] NavMeshBuilder.BuildNavMeshData returned null!");
            }
        }

        private void OnDestroy()
        {
            if (navMeshDataInstance.valid)
            {
                NavMesh.RemoveNavMeshData(navMeshDataInstance);
            }
        }
    }
}
