using UnityEngine;
using HorrorEscape.Interaction;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorEscape.Environment
{
    /// <summary>
    /// Builder utility to construct interactive 3D Backrooms Hiding Cabinets:
    /// - Outer wardrobe frame with authentic wood textures.
    /// - Hinged front door that swings open and closed with latch/creak audio.
    /// - Eye-level horizontal ventilation peek slits so the player can peer out from inside.
    /// - NavMeshObstacle with carving enabled so entities path around the cabinet.
    /// - Interior and exit anchors for smooth player transitions.
    /// </summary>
    public static class HidingCabinetBuilder
    {
        public static GameObject BuildCabinet(Vector3 position, Quaternion rotation, Transform parent, Material woodMat = null, Material metalMat = null)
        {
            GameObject cabinet = new GameObject("Hiding_Cabinet");
            cabinet.transform.SetParent(parent, false);
            cabinet.transform.position = position;
            cabinet.transform.rotation = rotation;

            if (woodMat == null)
            {
#if UNITY_EDITOR
                string[] woodMatPaths = new string[]
                {
                    "Assets/Hand Painted Seamless Wood Texture/Materials/M_Wood1.mat",
                    "Assets/Hand Painted Seamless Wood Texture/Materials/M_Wood2.mat",
                    "Assets/Hand Painted Seamless Wood Texture/Materials/M_Wood3.mat"
                };
                string chosenPath = woodMatPaths[Random.Range(0, woodMatPaths.Length)];
                woodMat = AssetDatabase.LoadAssetAtPath<Material>(chosenPath);
                if (woodMat == null)
                {
                    woodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Hand Painted Seamless Wood Texture/Materials/M_Wood1.mat");
                }
#endif
                if (woodMat == null)
                {
                    woodMat = new Material(Shader.Find("Standard"))
                    {
                        name = "M_HandPaintedWood_Fallback",
                        color = new Color(0.42f, 0.28f, 0.16f)
                    };
                }
            }

            if (metalMat == null)
            {
#if UNITY_EDITOR
                metalMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Monster.mat");
#endif
                if (metalMat == null)
                {
                    metalMat = new Material(Shader.Find("Standard"))
                    {
                        color = new Color(0.2f, 0.2f, 0.2f)
                    };
                }
            }

            // 1. Cabinet Shell Frame (Outer walls, roof, base)
            GameObject frame = new GameObject("Cabinet_Frame");
            frame.transform.SetParent(cabinet.transform, false);

            // Left Wall
            CreatePanel(frame.transform, "Left_Wall", new Vector3(-0.54f, 1.15f, 0f), new Vector3(0.08f, 2.3f, 0.95f), woodMat);
            // Right Wall
            CreatePanel(frame.transform, "Right_Wall", new Vector3(0.54f, 1.15f, 0f), new Vector3(0.08f, 2.3f, 0.95f), woodMat);
            // Back Wall
            CreatePanel(frame.transform, "Back_Wall", new Vector3(0f, 1.15f, -0.44f), new Vector3(1.16f, 2.3f, 0.08f), woodMat);
            // Roof
            CreatePanel(frame.transform, "Roof", new Vector3(0f, 2.31f, 0f), new Vector3(1.16f, 0.08f, 0.96f), woodMat);
            // Floor Plate
            CreatePanel(frame.transform, "Floor_Plate", new Vector3(0f, 0.02f, 0f), new Vector3(1.16f, 0.04f, 0.96f), woodMat);

            // 2. Hinged Door on front-left edge
            GameObject hingeGO = new GameObject("DoorHinge");
            hingeGO.transform.SetParent(cabinet.transform, false);
            hingeGO.transform.localPosition = new Vector3(-0.50f, 1.15f, 0.44f);

            // Lower Door Panel (From floor up to eye level: ~1.45m high)
            CreatePanel(hingeGO.transform, "Door_Lower", new Vector3(0.50f, -0.42f, 0f), new Vector3(1.00f, 1.44f, 0.05f), woodMat);

            // Upper Door Panel (From above eye level to roof: ~0.55m high)
            CreatePanel(hingeGO.transform, "Door_Upper", new Vector3(0.50f, 0.84f, 0f), new Vector3(1.00f, 0.58f, 0.05f), woodMat);

            // Eye-Level Peek Slits (horizontal slats creating narrow peephole gaps at eye height ~1.55m - 1.65m)
            CreatePanel(hingeGO.transform, "Door_Slat_1", new Vector3(0.50f, 0.38f, 0f), new Vector3(1.00f, 0.04f, 0.05f), woodMat);
            CreatePanel(hingeGO.transform, "Door_Slat_2", new Vector3(0.50f, 0.48f, 0f), new Vector3(1.00f, 0.04f, 0.05f), woodMat);

            // Metal Door Handle
            CreatePanel(hingeGO.transform, "Door_Handle", new Vector3(0.92f, 0.0f, 0.04f), new Vector3(0.04f, 0.22f, 0.06f), metalMat);

            // 3. Anchors for player entry and exit
            GameObject interiorGO = new GameObject("InteriorAnchor");
            interiorGO.transform.SetParent(cabinet.transform, false);
            interiorGO.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            interiorGO.transform.localRotation = Quaternion.identity;

            GameObject exitGO = new GameObject("ExitAnchor");
            exitGO.transform.SetParent(cabinet.transform, false);
            exitGO.transform.localPosition = new Vector3(0f, 0.05f, 1.25f);
            exitGO.transform.localRotation = Quaternion.identity;

            // 4. NavMeshObstacle with carving (AI navigates around it)
            var navObs = cabinet.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            navObs.size = new Vector3(1.25f, 2.35f, 1.05f);
            navObs.center = new Vector3(0f, 1.15f, 0f);
            navObs.carving = true;

            // 5. HidingSpot Component
            HidingSpot spot = cabinet.AddComponent<HidingSpot>();
            spot.Configure(hingeGO.transform, interiorGO.transform, exitGO.transform, -95.0f);

#if UNITY_EDITOR
            // Mark static frame geometry for lightmapping
            GameObjectUtility.SetStaticEditorFlags(frame,
                StaticEditorFlags.ContributeGI |
                StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.BatchingStatic);
#endif

            return cabinet;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = name;
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = localPos;
            panel.transform.localScale = localScale;

            if (mat != null)
            {
                var renderer = panel.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterial = mat;
            }

            return panel;
        }
    }
}
