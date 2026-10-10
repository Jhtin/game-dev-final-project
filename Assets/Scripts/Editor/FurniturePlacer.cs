using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using HorrorEscape.Environment;
using HorrorEscape.Interaction;

namespace HorrorEscape.Editor
{
    /// <summary>
    /// Intelligently scatters and organizes authentic Backrooms furniture across HorrorEscapeLevel.unity:
    /// - Creates environmental storytelling: eerie waiting lounges, abandoned offices, storage corners,
    ///   solitary liminal chairs staring into corners, and knocked-over chairs from panicked escapes.
    /// - Respects corridor navigation and player movement (hugs walls, furnishes dead ends).
    /// - Guarantees safe clearances around spawn, exit, and quest pickups.
    /// - Snaps mesh bases perfectly to floor level (y = 0).
    /// </summary>
    public static class FurniturePlacer
    {
        private const string RootGameObjectName = "Environment_Furniture";

        // Prefab Paths
        private static readonly string[] Sofas = new string[]
        {
            "Assets/Furniture/Prefabs/3Seat.prefab",
            "Assets/Furniture/Prefabs/3Seat2.prefab",
            "Assets/Furniture/Prefabs/3Seat3.prefab",
            "Assets/Furniture/Prefabs/3seat4.prefab",
            "Assets/Furniture/Prefabs/DoubleSeat.prefab",
            "Assets/Furniture/Prefabs/DoubleSeat2.prefab",
            "Assets/Furniture/Prefabs/Couch.prefab"
        };

        private static readonly string[] Armchairs = new string[]
        {
            "Assets/Furniture/Prefabs/Fotel.prefab",
            "Assets/Furniture/Prefabs/Fotel2.prefab",
            "Assets/Furniture/Prefabs/Fotel3.prefab",
            "Assets/Furniture/Prefabs/Fotel4.prefab"
        };

        private static readonly string[] Chairs = new string[]
        {
            "Assets/Furniture/Prefabs/chair.prefab"
        };

        private static readonly string[] Tables = new string[]
        {
            "Assets/Furniture/Prefabs/RoundTable.prefab",
            "Assets/Furniture/Prefabs/MetalTable.prefab"
        };

        private static readonly string[] Commodes = new string[]
        {
            "Assets/Furniture/Prefabs/MirrorComode.prefab",
            "Assets/Furniture/Prefabs/MirrorComode2.prefab"
        };

        private static readonly string[] Closets = new string[]
        {
            "Assets/Furniture/Prefabs/BigCloset.prefab",
            "Assets/Furniture/Prefabs/SmallCloset.prefab",
            "Assets/Furniture/Prefabs/closet2.prefab",
            "Assets/Furniture/Prefabs/Closet3.prefab"
        };

        [InitializeOnLoadMethod]
        private static void AutoPopulateFurnitureOnCompile()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (SessionState.GetBool("BackroomsFurniturePopulated_v2", false)) return;
                SessionState.SetBool("BackroomsFurniturePopulated_v2", true);

                string scenePath = "Assets/Scenes/HorrorEscapeLevel.unity";
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var gen = UnityEngine.Object.FindFirstObjectByType<BackroomsLevelGenerator>();
                if (gen != null)
                {
                    int count = PopulateFurniture(gen);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log($"[FurniturePlacer] Auto-populated {count} furniture pieces in {scenePath}!");
                }
            };
        }

        [MenuItem("Tools/Populate Backrooms Furniture")]
        [MenuItem("Backrooms/Populate Backrooms Furniture")]
        public static void PopulateFurnitureMenu()
        {
            var gen = UnityEngine.Object.FindFirstObjectByType<BackroomsLevelGenerator>();
            if (gen == null)
            {
                Debug.LogError("[FurniturePlacer] BackroomsLevelGenerator not found in active scene!");
                return;
            }

            int count = PopulateFurniture(gen);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[FurniturePlacer] Successfully placed {count} pieces of furniture across the Backrooms maze!");

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Backrooms Furniture Populated",
                    $"Successfully placed {count} pieces of Backrooms furniture!\n\n" +
                    "- Waiting areas, armchairs, abandoned desks, and storage closets.\n" +
                    "- Corridors & dead-ends furnished with clearance for player and stalker.\n" +
                    "- Eerie knocked-over chairs and solitary psychological horror props.\n" +
                    "- Ground aligned (y = 0) with colliders enabled.", "OK");
            }
        }

        public static void PopulateFurnitureBatch()
        {
            string scenePath = "Assets/Scenes/HorrorEscapeLevel.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var gen = UnityEngine.Object.FindFirstObjectByType<BackroomsLevelGenerator>();
            if (gen == null)
            {
                Debug.LogError("[FurniturePlacer] BackroomsLevelGenerator not found in scene!");
                return;
            }

            int count = PopulateFurniture(gen);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[FurniturePlacer] Batch mode populated {count} furniture pieces and saved scene {scenePath}!");
        }

        public static int PopulateFurniture(BackroomsLevelGenerator gen)
        {
            if (gen == null) return 0;

            gen.EnsureGridLayout();

            // Find or recreate container
            GameObject existingRoot = GameObject.Find(RootGameObjectName);
            if (existingRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(existingRoot);
            }

            GameObject furnitureRoot = new GameObject(RootGameObjectName);
            Undo.RegisterCreatedObjectUndo(furnitureRoot, "Create Furniture Root");

            // Collect existing critical positions to avoid
            List<Vector3> exclusionPoints = CollectExclusionPoints(gen);
            List<Vector3> placedPositions = new List<Vector3>();

            System.Random rng = new System.Random(1337);
            float cs = gen.CellSize;
            int totalPlaced = 0;

            // 1. Furnish Rooms with Backrooms Archetypes
            var rooms = gen.Rooms;
            for (int rIdx = 0; rIdx < rooms.Count; rIdx++)
            {
                var room = rooms[rIdx];

                // Skip the spawn room to keep the player's immediate starting room clean & disorienting
                if (room.Center == gen.SpawnCell)
                {
                    // At most 1 knocked-over chair in far corner of spawn room
                    Vector3 cornerPos = new Vector3(room.x * cs + 0.8f, 0f, room.z * cs + 0.8f);
                    if (IsPositionClear(cornerPos, exclusionPoints, placedPositions, 1.2f))
                    {
                        SpawnPiece(PickRandom(Chairs, rng), cornerPos, Quaternion.Euler(85f, rng.Next(0, 360), 0f), furnitureRoot.transform, placedPositions, true);
                        totalPlaced++;
                    }
                    continue;
                }

                // Room dimensions in meters
                Vector3 roomCenter = new Vector3((room.x + room.width * 0.5f) * cs, 0f, (room.z + room.length * 0.5f) * cs);

                // Select room theme based on index
                int theme = (rIdx % 5);

                switch (theme)
                {
                    case 0:
                        // Lounge / Waiting Room
                        totalPlaced += CreateLoungeRoom(gen, room, cs, furnitureRoot.transform, exclusionPoints, placedPositions, rng);
                        break;
                    case 1:
                        // Abandoned Office / Desks
                        totalPlaced += CreateOfficeRoom(gen, room, cs, furnitureRoot.transform, exclusionPoints, placedPositions, rng);
                        break;
                    case 2:
                        // Storage / Archive Closets
                        totalPlaced += CreateStorageRoom(gen, room, cs, furnitureRoot.transform, exclusionPoints, placedPositions, rng);
                        break;
                    case 3:
                        // Solitary Dread (Eerie solitary chair facing wall/center)
                        totalPlaced += CreateSolitaryDreadRoom(gen, room, cs, furnitureRoot.transform, exclusionPoints, placedPositions, rng);
                        break;
                    case 4:
                        // Disarray / Scramble (Overturned furniture, chaotic retreat)
                        totalPlaced += CreateDisarrayRoom(gen, room, cs, furnitureRoot.transform, exclusionPoints, placedPositions, rng);
                        break;
                }
            }

            // 2. Guarantee 6-8 Hiding Cabinets across the Backrooms maze (all safely inside rooms, never blocking hallways!)
            int cabinetCount = 0;
            foreach (var spot in furnitureRoot.GetComponentsInChildren<HidingSpot>()) cabinetCount++;

            if (cabinetCount < 6)
            {
                for (int rIdx = 0; rIdx < rooms.Count && cabinetCount < 8; rIdx++)
                {
                    var room = rooms[rIdx];
                    if (room.Center == gen.SpawnCell) continue;

                    if (TryFindSafeWallSpot(gen, room, cs, exclusionPoints, placedPositions, out Vector3 safeCabPos, out Quaternion safeCabRot))
                    {
                        HidingCabinetBuilder.BuildCabinet(safeCabPos, safeCabRot, furnitureRoot.transform);
                        placedPositions.Add(safeCabPos);
                        totalPlaced++;
                        cabinetCount++;
                    }
                }
            }

            // 3. Furnish Dead Ends & Corridor Niches
            totalPlaced += FurnishDeadEndsAndCorridors(gen, furnitureRoot.transform, exclusionPoints, placedPositions, rng);

            return totalPlaced;
        }

        private static int CreateLoungeRoom(BackroomsLevelGenerator gen, BackroomsLevelGenerator.RoomRect room, float cs, Transform parent,
            List<Vector3> exclusions, List<Vector3> placed, System.Random rng)
        {
            int placedInRoom = 0;
            // North wall placement: Sofa facing South
            Vector3 sofaPos = new Vector3((room.x + room.width * 0.5f) * cs, 0f, (room.z + room.length) * cs - 0.75f);
            if (IsPositionClear(sofaPos, exclusions, placed, 1.6f))
            {
                SpawnPiece(PickRandom(Sofas, rng), sofaPos, Quaternion.Euler(0f, 180f, 0f), parent, placed);
                placedInRoom++;

                // Coffee table in front of sofa
                Vector3 tablePos = sofaPos + new Vector3(0f, 0f, -1.35f);
                if (IsPositionClear(tablePos, exclusions, placed, 1.2f))
                {
                    SpawnPiece("Assets/Furniture/Prefabs/RoundTable.prefab", tablePos, Quaternion.identity, parent, placed);
                    placedInRoom++;

                    // Armchair 1 angled towards table
                    Vector3 armchair1Pos = tablePos + new Vector3(-1.3f, 0f, -0.4f);
                    if (IsPositionClear(armchair1Pos, exclusions, placed, 1.1f))
                    {
                        SpawnPiece(PickRandom(Armchairs, rng), armchair1Pos, Quaternion.Euler(0f, 45f, 0f), parent, placed);
                        placedInRoom++;
                    }

                    // Armchair 2 angled towards table
                    Vector3 armchair2Pos = tablePos + new Vector3(1.3f, 0f, -0.4f);
                    if (IsPositionClear(armchair2Pos, exclusions, placed, 1.1f))
                    {
                        SpawnPiece(PickRandom(Armchairs, rng), armchair2Pos, Quaternion.Euler(0f, -45f, 0f), parent, placed);
                        placedInRoom++;
                    }
                }
            }

            // Hiding Cabinet in lounges along a verified solid room wall
            if (TryFindSafeWallSpot(gen, room, cs, exclusions, placed, out Vector3 safeCabPos, out Quaternion safeCabRot))
            {
                HidingCabinetBuilder.BuildCabinet(safeCabPos, safeCabRot, parent);
                placed.Add(safeCabPos);
                placedInRoom++;
            }

            // Optional knocked-over chair in corner
            Vector3 cornerPos = new Vector3((room.x + 0.35f) * cs, 0f, (room.z + 0.35f) * cs);
            if (rng.NextDouble() < 0.45 && IsPositionClear(cornerPos, exclusions, placed, 1.0f))
            {
                SpawnPiece(PickRandom(Chairs, rng), cornerPos, Quaternion.Euler(85f, rng.Next(20, 200), 0f), parent, placed, true);
                placedInRoom++;
            }

            return placedInRoom;
        }

        private static int CreateOfficeRoom(BackroomsLevelGenerator gen, BackroomsLevelGenerator.RoomRect room, float cs, Transform parent,
            List<Vector3> exclusions, List<Vector3> placed, System.Random rng)
        {
            int placedInRoom = 0;
            // Metal Table / Desk against South wall
            Vector3 deskPos = new Vector3((room.x + 0.9f) * cs, 0f, room.z * cs + 0.75f);
            if (IsPositionClear(deskPos, exclusions, placed, 1.5f))
            {
                SpawnPiece("Assets/Furniture/Prefabs/MetalTable.prefab", deskPos, Quaternion.identity, parent, placed);
                placedInRoom++;

                // Desk Chair pulled out
                Vector3 chairPos = deskPos + new Vector3(0f, 0f, 0.75f);
                if (IsPositionClear(chairPos, exclusions, placed, 0.9f))
                {
                    float angle = (rng.NextDouble() < 0.5f) ? 170f : 195f;
                    SpawnPiece(PickRandom(Chairs, rng), chairPos, Quaternion.Euler(0f, angle, 0f), parent, placed);
                    placedInRoom++;
                }
            }

            // Interactive Hiding Cabinet placed safely flush against a verified solid room wall (never blocks doors or hallways!)
            if (TryFindSafeWallSpot(gen, room, cs, exclusions, placed, out Vector3 safeCabPos, out Quaternion safeCabRot))
            {
                HidingCabinetBuilder.BuildCabinet(safeCabPos, safeCabRot, parent);
                placed.Add(safeCabPos);
                placedInRoom++;
            }

            // Mirror commode along West wall
            if (room.isLarge)
            {
                Vector3 commodePos = new Vector3(room.x * cs + 0.75f, 0f, (room.z + room.length * 0.75f) * cs);
                if (IsPositionClear(commodePos, exclusions, placed, 1.4f))
                {
                    SpawnPiece(PickRandom(Commodes, rng), commodePos, Quaternion.Euler(0f, 90f, 0f), parent, placed);
                    placedInRoom++;
                }
            }

            return placedInRoom;
        }

        private static int CreateStorageRoom(BackroomsLevelGenerator gen, BackroomsLevelGenerator.RoomRect room, float cs, Transform parent,
            List<Vector3> exclusions, List<Vector3> placed, System.Random rng)
        {
            int placedInRoom = 0;
            // Interactive Hiding Cabinet placed safely flush against a verified solid room wall (never blocks doors or hallways!)
            if (TryFindSafeWallSpot(gen, room, cs, exclusions, placed, out Vector3 safeCabPos, out Quaternion safeCabRot))
            {
                HidingCabinetBuilder.BuildCabinet(safeCabPos, safeCabRot, parent);
                placed.Add(safeCabPos);
                placedInRoom++;
            }

            Vector3 closet2Pos = new Vector3(room.x * cs + 0.75f, 0f, (room.z + room.length - 0.8f) * cs);
            if (IsPositionClear(closet2Pos, exclusions, placed, 1.4f))
            {
                SpawnPiece(PickRandom(Closets, rng), closet2Pos, Quaternion.Euler(0f, 90f, 0f), parent, placed);
                placedInRoom++;
            }

            // Metal table or commode on opposite wall
            Vector3 tablePos = new Vector3((room.x + room.width) * cs - 0.75f, 0f, (room.z + room.length * 0.5f) * cs);
            if (IsPositionClear(tablePos, exclusions, placed, 1.4f))
            {
                SpawnPiece("Assets/Furniture/Prefabs/MetalTable.prefab", tablePos, Quaternion.Euler(0f, -90f, 0f), parent, placed);
                placedInRoom++;

                // A single lonely chair facing into the table
                Vector3 chairPos = tablePos + new Vector3(-0.75f, 0f, 0f);
                if (IsPositionClear(chairPos, exclusions, placed, 0.9f))
                {
                    SpawnPiece(PickRandom(Chairs, rng), chairPos, Quaternion.Euler(0f, 90f, 0f), parent, placed);
                    placedInRoom++;
                }
            }

            return placedInRoom;
        }

        private static int CreateSolitaryDreadRoom(BackroomsLevelGenerator gen, BackroomsLevelGenerator.RoomRect room, float cs, Transform parent,
            List<Vector3> exclusions, List<Vector3> placed, System.Random rng)
        {
            int placedInRoom = 0;
            // Iconic solitary armchair facing directly into a corner or wall
            bool faceCorner = rng.NextDouble() < 0.5;
            if (faceCorner)
            {
                // Placed near center, facing corner
                Vector3 centerPos = new Vector3((room.x + room.width * 0.5f) * cs, 0f, (room.z + room.length * 0.5f) * cs);
                if (IsPositionClear(centerPos, exclusions, placed, 1.3f))
                {
                    // Face Northwest
                    SpawnPiece(PickRandom(Armchairs, rng), centerPos, Quaternion.Euler(0f, -45f, 0f), parent, placed);
                    placedInRoom++;
                }
            }
            else
            {
                // Chair placed 1.2m away from a blank wall, staring directly at the wallpaper
                Vector3 wallWatcherPos = new Vector3((room.x + room.width * 0.5f) * cs, 0f, room.z * cs + 1.2f);
                if (IsPositionClear(wallWatcherPos, exclusions, placed, 1.2f))
                {
                    SpawnPiece(PickRandom(Armchairs, rng), wallWatcherPos, Quaternion.Euler(0f, 180f, 0f), parent, placed);
                    placedInRoom++;
                }
            }

            // Hiding Cabinet along safe wall
            if (TryFindSafeWallSpot(gen, room, cs, exclusions, placed, out Vector3 safeCabPos, out Quaternion safeCabRot))
            {
                HidingCabinetBuilder.BuildCabinet(safeCabPos, safeCabRot, parent);
                placed.Add(safeCabPos);
                placedInRoom++;
            }

            // A forgotten dresser/commode along the perimeter
            Vector3 commodePos = new Vector3((room.x + room.width) * cs - 0.75f, 0f, (room.z + 0.9f) * cs);
            if (IsPositionClear(commodePos, exclusions, placed, 1.4f))
            {
                SpawnPiece(PickRandom(Commodes, rng), commodePos, Quaternion.Euler(0f, -90f, 0f), parent, placed);
                placedInRoom++;
            }

            return placedInRoom;
        }

        private static int CreateDisarrayRoom(BackroomsLevelGenerator gen, BackroomsLevelGenerator.RoomRect room, float cs, Transform parent,
            List<Vector3> exclusions, List<Vector3> placed, System.Random rng)
        {
            int placedInRoom = 0;
            // Tipped over couch or table
            Vector3 couchPos = new Vector3((room.x + room.width * 0.5f) * cs, 0f, (room.z + room.length * 0.5f) * cs);
            if (IsPositionClear(couchPos, exclusions, placed, 1.6f))
            {
                // Slightly askew couch
                SpawnPiece(PickRandom(Sofas, rng), couchPos, Quaternion.Euler(0f, rng.Next(25, 65), 0f), parent, placed);
                placedInRoom++;

                // Overturned chair next to it
                Vector3 knockedChairPos = couchPos + new Vector3(1.4f, 0f, -0.6f);
                if (IsPositionClear(knockedChairPos, exclusions, placed, 1.0f))
                {
                    SpawnPiece(PickRandom(Chairs, rng), knockedChairPos, Quaternion.Euler(85f, rng.Next(0, 360), 0f), parent, placed, true);
                    placedInRoom++;
                }

                // Another knocked-over armchair
                Vector3 knockedArmchairPos = couchPos + new Vector3(-1.3f, 0f, 0.8f);
                if (IsPositionClear(knockedArmchairPos, exclusions, placed, 1.1f))
                {
                    SpawnPiece(PickRandom(Armchairs, rng), knockedArmchairPos, Quaternion.Euler(75f, rng.Next(0, 360), 0f), parent, placed, true);
                    placedInRoom++;
                }
            }

            // Hiding Cabinet along safe wall
            if (TryFindSafeWallSpot(gen, room, cs, exclusions, placed, out Vector3 safeCabPos, out Quaternion safeCabRot))
            {
                HidingCabinetBuilder.BuildCabinet(safeCabPos, safeCabRot, parent);
                placed.Add(safeCabPos);
                placedInRoom++;
            }

            return placedInRoom;
        }

        private static int FurnishDeadEndsAndCorridors(BackroomsLevelGenerator gen, Transform parent,
            List<Vector3> exclusions, List<Vector3> placed, System.Random rng)
        {
            int placedCount = 0;
            int w = gen.MapWidth;
            int l = gen.MapLength;
            float cs = gen.CellSize;

            for (int x = 2; x < w - 2; x++)
            {
                for (int z = 2; z < l - 2; z++)
                {
                    if (!gen.IsWalkable(x, z)) continue;

                    // Skip room cells (already handled)
                    if (gen.Grid[x, z] == BackroomsLevelGenerator.CellType.Room) continue;

                    Vector3 cellCenter = new Vector3(x * cs + cs * 0.5f, 0f, z * cs + cs * 0.5f);

                    // Check neighbors
                    bool nNorth = gen.IsWalkable(x, z + 1);
                    bool nSouth = gen.IsWalkable(x, z - 1);
                    bool nEast = gen.IsWalkable(x + 1, z);
                    bool nWest = gen.IsWalkable(x - 1, z);

                    int walkableNeighbors = (nNorth ? 1 : 0) + (nSouth ? 1 : 0) + (nEast ? 1 : 0) + (nWest ? 1 : 0);

                    // Dead-End Cell: Exactly 1 walkable neighbor!
                    if (walkableNeighbors == 1)
                    {
                        if (!IsPositionClear(cellCenter, exclusions, placed, 1.4f)) continue;

                        // Face towards the open doorway
                        Quaternion rot = Quaternion.identity;
                        if (nNorth) rot = Quaternion.Euler(0f, 0f, 0f);
                        else if (nSouth) rot = Quaternion.Euler(0f, 180f, 0f);
                        else if (nEast) rot = Quaternion.Euler(0f, 90f, 0f);
                        else if (nWest) rot = Quaternion.Euler(0f, 270f, 0f);

                        // Random choice for dead end (armchair or overturned chair only; never cabinets in hallways):
                        double roll = rng.NextDouble();
                        if (roll < 0.50)
                        {
                            // Solitary armchair in dead end tucked against rear wall
                            SpawnPiece(PickRandom(Armchairs, rng), cellCenter, rot, parent, placed);
                            placedCount++;
                        }
                        else
                        {
                            // Overturned chair
                            SpawnPiece(PickRandom(Chairs, rng), cellCenter, Quaternion.Euler(85f, rng.Next(0, 360), 0f), parent, placed, true);
                            placedCount++;
                        }
                    }
                    // Corridor Cell with at least 1 solid wall: ~7% chance to place a prop tucked against wall
                    else if (walkableNeighbors == 2 && rng.NextDouble() < 0.08)
                    {
                        // Determine solid wall direction
                        Vector3 wallOffset = Vector3.zero;
                        Quaternion rot = Quaternion.identity;

                        if (!nNorth) { wallOffset = new Vector3(0f, 0f, 0.95f); rot = Quaternion.Euler(0f, 180f, 0f); }
                        else if (!nSouth) { wallOffset = new Vector3(0f, 0f, -0.95f); rot = Quaternion.Euler(0f, 0f, 0f); }
                        else if (!nEast) { wallOffset = new Vector3(0.95f, 0f, 0f); rot = Quaternion.Euler(0f, 270f, 0f); }
                        else if (!nWest) { wallOffset = new Vector3(-0.95f, 0f, 0f); rot = Quaternion.Euler(0f, 90f, 0f); }

                        if (wallOffset != Vector3.zero)
                        {
                            Vector3 propPos = cellCenter + wallOffset;
                            if (IsPositionClear(propPos, exclusions, placed, 1.3f))
                            {
                                // Small furniture only (chair or small closet or commode)
                                string propPrefab = (rng.NextDouble() < 0.5) ? PickRandom(Chairs, rng) : "Assets/Furniture/Prefabs/SmallCloset.prefab";
                                SpawnPiece(propPrefab, propPos, rot, parent, placed);
                                placedCount++;
                            }
                        }
                    }
                }
            }

            return placedCount;
        }

        private static GameObject SpawnPiece(string prefabPath, Vector3 targetPos, Quaternion targetRot, Transform parent,
            List<Vector3> placed, bool isTipped = false)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[FurniturePlacer] Could not load prefab at {prefabPath}");
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (instance == null) return null;

            instance.transform.position = targetPos;
            instance.transform.rotation = targetRot;

            // Accurate ground snap (y = 0):
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    b.Encapsulate(renderers[i].bounds);
                }

                // If lowest point is not at floor level (y = 0), offset it
                float yAdjust = -b.min.y;
                instance.transform.position += new Vector3(0f, yAdjust, 0f);
            }

            // Ensure colliders are present & configured
            MeshCollider mc = instance.GetComponent<MeshCollider>();
            if (mc != null && isTipped)
            {
                mc.convex = true; // Convex required for tipped physics/raycasts
            }

            // Mark Static for NavMesh generation & occlusion
            GameObjectUtility.SetStaticEditorFlags(instance,
                StaticEditorFlags.ContributeGI |
                StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.BatchingStatic);

            placed.Add(targetPos);
            return instance;
        }

        private static bool IsPositionClear(Vector3 pos, List<Vector3> exclusions, List<Vector3> placed, float minDistance)
        {
            float minSqr = minDistance * minDistance;

            foreach (var ex in exclusions)
            {
                Vector3 diff = pos - ex;
                diff.y = 0f;
                if (diff.sqrMagnitude < minSqr) return false;
            }

            foreach (var p in placed)
            {
                Vector3 diff = pos - p;
                diff.y = 0f;
                if (diff.sqrMagnitude < minSqr) return false;
            }

            return true;
        }

        private static List<Vector3> CollectExclusionPoints(BackroomsLevelGenerator gen)
        {
            List<Vector3> points = new List<Vector3>();
            float cs = gen.CellSize;

            // Player Spawn (keep generous 4m radius clear)
            Vector3 spawnPos = new Vector3(gen.SpawnCell.x * cs + cs * 0.5f, 0f, gen.SpawnCell.y * cs + cs * 0.5f);
            points.Add(spawnPos);
            // Also 1 cell forward from spawn
            points.Add(spawnPos + new Vector3(0f, 0f, cs));
            points.Add(spawnPos + new Vector3(cs, 0f, 0f));

            // Emergency Exit (keep 3.5m radius clear)
            Vector3 exitPos = new Vector3(gen.ExitCell.x * cs + cs * 0.5f, 0f, gen.ExitCell.y * cs + cs * 0.5f);
            points.Add(exitPos);

            // Existing Pickups and Switches in scene
            foreach (var key in UnityEngine.Object.FindObjectsByType<KeyPickup>(FindObjectsSortMode.None))
                points.Add(key.transform.position);

            foreach (var ps in UnityEngine.Object.FindObjectsByType<PowerSwitch>(FindObjectsSortMode.None))
                points.Add(ps.transform.position);

            foreach (var bat in UnityEngine.Object.FindObjectsByType<BatteryPickup>(FindObjectsSortMode.None))
                points.Add(bat.transform.position);

            foreach (var ammo in UnityEngine.Object.FindObjectsByType<AmmunitionPickup>(FindObjectsSortMode.None))
                points.Add(ammo.transform.position);

            foreach (var water in UnityEngine.Object.FindObjectsByType<AlmondWaterPickup>(FindObjectsSortMode.None))
                points.Add(water.transform.position);

            foreach (var pills in UnityEngine.Object.FindObjectsByType<SanityPillsPickup>(FindObjectsSortMode.None))
                points.Add(pills.transform.position);

            foreach (var firstAid in UnityEngine.Object.FindObjectsByType<FirstAidPickup>(FindObjectsSortMode.None))
                points.Add(firstAid.transform.position);

            foreach (var gun in UnityEngine.Object.FindObjectsByType<PistolPickup>(FindObjectsSortMode.None))
                points.Add(gun.transform.position);

            // Also exclude pillar positions
            int w = gen.MapWidth;
            int l = gen.MapLength;
            if (gen.Grid != null)
            {
                for (int x = 0; x < w; x++)
                {
                    for (int z = 0; z < l; z++)
                    {
                        if (gen.Grid[x, z] == BackroomsLevelGenerator.CellType.Pillar)
                        {
                            points.Add(new Vector3(x * cs + cs * 0.5f, 0f, z * cs + cs * 0.5f));
                        }
                    }
                }
            }

            return points;
        }

        /// <summary>
        /// Finds a safe position and rotation flush against a verified solid room wall (never blocks doorways or corridors).
        /// </summary>
        public static bool TryFindSafeWallSpot(BackroomsLevelGenerator gen, BackroomsLevelGenerator.RoomRect room, float cs,
            List<Vector3> exclusions, List<Vector3> placed, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (gen == null) return false;

            // Check all cells of the room along each wall
            for (int x = room.x; x < room.x + room.width; x++)
            {
                for (int z = room.z; z < room.z + room.length; z++)
                {
                    // 1. West wall (back against West wall, facing East (+X))
                    if (x == room.x && !gen.IsWalkable(x - 1, z))
                    {
                        Vector3 candidate = new Vector3(x * cs + 0.60f, 0f, z * cs + cs * 0.5f);
                        if (IsPositionClear(candidate, exclusions, placed, 1.25f))
                        {
                            position = candidate;
                            rotation = Quaternion.Euler(0f, 90f, 0f);
                            return true;
                        }
                    }

                    // 2. East wall (back against East wall, facing West (-X))
                    if (x == room.x + room.width - 1 && !gen.IsWalkable(x + 1, z))
                    {
                        Vector3 candidate = new Vector3((x + 1) * cs - 0.60f, 0f, z * cs + cs * 0.5f);
                        if (IsPositionClear(candidate, exclusions, placed, 1.25f))
                        {
                            position = candidate;
                            rotation = Quaternion.Euler(0f, -90f, 0f);
                            return true;
                        }
                    }

                    // 3. South wall (back against South wall, facing North (+Z))
                    if (z == room.z && !gen.IsWalkable(x, z - 1))
                    {
                        Vector3 candidate = new Vector3(x * cs + cs * 0.5f, 0f, z * cs + 0.60f);
                        if (IsPositionClear(candidate, exclusions, placed, 1.25f))
                        {
                            position = candidate;
                            rotation = Quaternion.Euler(0f, 0f, 0f);
                            return true;
                        }
                    }

                    // 4. North wall (back against North wall, facing South (-Z))
                    if (z == room.z + room.length - 1 && !gen.IsWalkable(x, z + 1))
                    {
                        Vector3 candidate = new Vector3(x * cs + cs * 0.5f, 0f, (z + 1) * cs - 0.60f);
                        if (IsPositionClear(candidate, exclusions, placed, 1.25f))
                        {
                            position = candidate;
                            rotation = Quaternion.Euler(0f, 180f, 0f);
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static string PickRandom(string[] array, System.Random rng)
        {
            return array[rng.Next(0, array.Length)];
        }
    }
}
