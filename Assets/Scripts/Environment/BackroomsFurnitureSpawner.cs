using System;
using System.Collections.Generic;
using UnityEngine;
using HorrorEscape.Interaction;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorEscape.Environment
{
    /// <summary>
    /// Runtime and Editor spawner for authentic Backrooms furniture:
    /// - If furniture was already baked into the scene by Editor tools, preserves the pre-baked setup.
    /// - If the scene starts without furniture (e.g. dynamic map generation at runtime), automatically
    ///   populates the rooms, corridors, and dead ends with atmospheric Backrooms furniture props.
    /// - Aligns bases to floor level (y = 0), respects doorway/pickup clearances, and configures colliders.
    /// </summary>
    [DefaultExecutionOrder(-450)]
    public class BackroomsFurnitureSpawner : MonoBehaviour
    {
        public const string RootGameObjectName = "Environment_Furniture";

        [Header("Furniture Prefabs")]
        [SerializeField] private GameObject[] sofaPrefabs;
        [SerializeField] private GameObject[] armchairPrefabs;
        [SerializeField] private GameObject[] chairPrefabs;
        [SerializeField] private GameObject[] tablePrefabs;
        [SerializeField] private GameObject[] commodePrefabs;
        [SerializeField] private GameObject[] closetPrefabs;

        [Header("Configuration")]
        [SerializeField] private int randomSeed = 1337;
        [SerializeField] private bool autoSpawnIfEmpty = true;

        private void Reset()
        {
            LoadDefaultPrefabs();
        }

        public void LoadDefaultPrefabs()
        {
#if UNITY_EDITOR
            sofaPrefabs = LoadPrefabs(new string[]
            {
                "Assets/Furniture/Prefabs/3Seat.prefab",
                "Assets/Furniture/Prefabs/3Seat2.prefab",
                "Assets/Furniture/Prefabs/3Seat3.prefab",
                "Assets/Furniture/Prefabs/3seat4.prefab",
                "Assets/Furniture/Prefabs/DoubleSeat.prefab",
                "Assets/Furniture/Prefabs/DoubleSeat2.prefab",
                "Assets/Furniture/Prefabs/Couch.prefab"
            });

            armchairPrefabs = LoadPrefabs(new string[]
            {
                "Assets/Furniture/Prefabs/Fotel.prefab",
                "Assets/Furniture/Prefabs/Fotel2.prefab",
                "Assets/Furniture/Prefabs/Fotel3.prefab",
                "Assets/Furniture/Prefabs/Fotel4.prefab"
            });

            chairPrefabs = LoadPrefabs(new string[]
            {
                "Assets/Furniture/Prefabs/chair.prefab"
            });

            tablePrefabs = LoadPrefabs(new string[]
            {
                "Assets/Furniture/Prefabs/RoundTable.prefab",
                "Assets/Furniture/Prefabs/MetalTable.prefab"
            });

            commodePrefabs = LoadPrefabs(new string[]
            {
                "Assets/Furniture/Prefabs/MirrorComode.prefab",
                "Assets/Furniture/Prefabs/MirrorComode2.prefab"
            });

            closetPrefabs = LoadPrefabs(new string[]
            {
                "Assets/Furniture/Prefabs/BigCloset.prefab",
                "Assets/Furniture/Prefabs/SmallCloset.prefab",
                "Assets/Furniture/Prefabs/closet2.prefab",
                "Assets/Furniture/Prefabs/Closet3.prefab"
            });
#endif
        }

#if UNITY_EDITOR
        private static GameObject[] LoadPrefabs(string[] paths)
        {
            List<GameObject> list = new List<GameObject>();
            foreach (var p in paths)
            {
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go != null) list.Add(go);
            }
            return list.ToArray();
        }
#endif

        private void Awake()
        {
            if (!autoSpawnIfEmpty) return;

            GameObject existing = GameObject.Find(RootGameObjectName);
            if (existing != null && existing.transform.childCount > 0)
            {
                Debug.Log($"[BackroomsFurnitureSpawner] {existing.transform.childCount} furniture props detected in scene. Keeping pre-placed furniture.");
                return;
            }

            SpawnFurniture();
        }

        public int SpawnFurniture()
        {
            BackroomsLevelGenerator gen = GetComponent<BackroomsLevelGenerator>();
            if (gen == null) gen = FindFirstObjectByType<BackroomsLevelGenerator>();
            if (gen == null)
            {
                Debug.LogWarning("[BackroomsFurnitureSpawner] BackroomsLevelGenerator not found!");
                return 0;
            }

            gen.EnsureGridLayout();

            GameObject root = GameObject.Find(RootGameObjectName);
            if (root == null)
            {
                root = new GameObject(RootGameObjectName);
            }
            else
            {
                // Clear any existing empty children
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    Destroy(root.transform.GetChild(i).gameObject);
                }
            }

            List<Vector3> exclusions = CollectExclusions(gen);
            List<Vector3> placed = new List<Vector3>();
            System.Random rng = new System.Random(randomSeed);
            float cs = gen.CellSize;
            int count = 0;

            var rooms = gen.Rooms;
            for (int rIdx = 0; rIdx < rooms.Count; rIdx++)
            {
                var room = rooms[rIdx];
                if (room.Center == gen.SpawnCell) continue;

                int theme = (rIdx % 5);
                switch (theme)
                {
                    case 0: // Lounge
                        count += SpawnLounge(room, cs, root.transform, exclusions, placed, rng);
                        break;
                    case 1: // Office
                        count += SpawnOffice(room, cs, root.transform, exclusions, placed, rng);
                        break;
                    case 2: // Storage
                        count += SpawnStorage(room, cs, root.transform, exclusions, placed, rng);
                        break;
                    case 3: // Solitary Dread
                        count += SpawnSolitary(room, cs, root.transform, exclusions, placed, rng);
                        break;
                    case 4: // Disarray
                        count += SpawnDisarray(room, cs, root.transform, exclusions, placed, rng);
                        break;
                }
            }

            // Dead ends
            count += SpawnDeadEnds(gen, root.transform, exclusions, placed, rng);

            Debug.Log($"[BackroomsFurnitureSpawner] Spawned {count} atmospheric furniture pieces across Backrooms!");
            return count;
        }

        private int SpawnLounge(BackroomsLevelGenerator.RoomRect room, float cs, Transform parent,
            List<Vector3> exclusions, List<Vector3> placed, System.Random rng)
        {
            int placedCount = 0;
            Vector3 sofaPos = new Vector3((room.x + room.width * 0.5f) * cs, 0f, (room.z + room.length) * cs - 0.75f);
            if (IsClear(sofaPos, exclusions, placed, 1.6f))
            {
                Spawn(Pick(sofaPrefabs, rng), sofaPos, Quaternion.Euler(0f, 180f, 0f), parent, placed);
                placedCount++;

                Vector3 tablePos = sofaPos + new Vector3(0f, 0f, -1.35f);
                if (IsClear(tablePos, exclusions, placed, 1.2f))
                {
                    Spawn(Pick(tablePrefabs, rng), tablePos, Quaternion.identity, parent, placed);
                    placedCount++;

                    Vector3 arm1 = tablePos + new Vector3(-1.3f, 0f, -0.4f);
                    if (IsClear(arm1, exclusions, placed, 1.1f))
                    {
                        Spawn(Pick(armchairPrefabs, rng), arm1, Quaternion.Euler(0f, 45f, 0f), parent, placed);
                        placedCount++;
                    }

                    Vector3 arm2 = tablePos + new Vector3(1.3f, 0f, -0.4f);
                    if (IsClear(arm2, exclusions, placed, 1.1f))
                    {
                        Spawn(Pick(armchairPrefabs, rng), arm2, Quaternion.Euler(0f, -45f, 0f), parent, placed);
                        placedCount++;
                    }
                }
            }
            return placedCount;
        }

        private int SpawnOffice(BackroomsLevelGenerator.RoomRect room, float cs, Transform parent,
            List<Vector3> exclusions, List<Vector3> placed, System.Random rng)
        {
            int placedCount = 0;
            Vector3 deskPos = new Vector3((room.x + 0.9f) * cs, 0f, room.z * cs + 0.75f);
            if (IsClear(deskPos, exclusions, placed, 1.5f))
            {
                Spawn(Pick(tablePrefabs, rng), deskPos, Quaternion.identity, parent, placed);
                placedCount++;

                Vector3 chairPos = deskPos + new Vector3(0f, 0f, 0.75f);
                if (IsClear(chairPos, exclusions, placed, 0.9f))
                {
                    Spawn(Pick(chairPrefabs, rng), chairPos, Quaternion.Euler(0f, 180f, 0f), parent, placed);
                    placedCount++;
                }
            }

            Vector3 closetPos = new Vector3((room.x + room.width) * cs - 0.75f, 0f, (room.z + room.length * 0.5f) * cs);
            if (IsClear(closetPos, exclusions, placed, 1.5f))
            {
                Spawn(Pick(closetPrefabs, rng), closetPos, Quaternion.Euler(0f, -90f, 0f), parent, placed);
                placedCount++;
            }
            return placedCount;
        }

        private int SpawnStorage(BackroomsLevelGenerator.RoomRect room, float cs, Transform parent,
            List<Vector3> exclusions, List<Vector3> placed, System.Random rng)
        {
            int placedCount = 0;
            Vector3 closet1 = new Vector3(room.x * cs + 0.75f, 0f, (room.z + 0.8f) * cs);
            if (IsClear(closet1, exclusions, placed, 1.4f))
            {
                Spawn(Pick(closetPrefabs, rng), closet1, Quaternion.Euler(0f, 90f, 0f), parent, placed);
                placedCount++;
            }

            Vector3 closet2 = new Vector3(room.x * cs + 0.75f, 0f, (room.z + room.length - 0.8f) * cs);
            if (IsClear(closet2, exclusions, placed, 1.4f))
            {
                Spawn(Pick(closetPrefabs, rng), closet2, Quaternion.Euler(0f, 90f, 0f), parent, placed);
                placedCount++;
            }
            return placedCount;
        }

        private int SpawnSolitary(BackroomsLevelGenerator.RoomRect room, float cs, Transform parent,
            List<Vector3> exclusions, List<Vector3> placed, System.Random rng)
        {
            int placedCount = 0;
            Vector3 centerPos = new Vector3((room.x + room.width * 0.5f) * cs, 0f, (room.z + room.length * 0.5f) * cs);
            if (IsClear(centerPos, exclusions, placed, 1.3f))
            {
                Spawn(Pick(armchairPrefabs, rng), centerPos, Quaternion.Euler(0f, -45f, 0f), parent, placed);
                placedCount++;
            }
            return placedCount;
        }

        private int SpawnDisarray(BackroomsLevelGenerator.RoomRect room, float cs, Transform parent,
            List<Vector3> exclusions, List<Vector3> placed, System.Random rng)
        {
            int placedCount = 0;
            Vector3 couchPos = new Vector3((room.x + room.width * 0.5f) * cs, 0f, (room.z + room.length * 0.5f) * cs);
            if (IsClear(couchPos, exclusions, placed, 1.6f))
            {
                Spawn(Pick(sofaPrefabs, rng), couchPos, Quaternion.Euler(0f, rng.Next(30, 60), 0f), parent, placed);
                placedCount++;

                Vector3 knocked = couchPos + new Vector3(1.3f, 0f, -0.6f);
                if (IsClear(knocked, exclusions, placed, 1.0f))
                {
                    Spawn(Pick(chairPrefabs, rng), knocked, Quaternion.Euler(85f, rng.Next(0, 360), 0f), parent, placed, true);
                    placedCount++;
                }
            }
            return placedCount;
        }

        private int SpawnDeadEnds(BackroomsLevelGenerator gen, Transform parent,
            List<Vector3> exclusions, List<Vector3> placed, System.Random rng)
        {
            int count = 0;
            int w = gen.MapWidth;
            int l = gen.MapLength;
            float cs = gen.CellSize;

            for (int x = 2; x < w - 2; x++)
            {
                for (int z = 2; z < l - 2; z++)
                {
                    if (!gen.IsWalkable(x, z)) continue;
                    if (gen.Grid[x, z] == BackroomsLevelGenerator.CellType.Room) continue;

                    Vector3 center = new Vector3(x * cs + cs * 0.5f, 0f, z * cs + cs * 0.5f);

                    int walkableNeighbors = 0;
                    Quaternion rot = Quaternion.identity;

                    if (gen.IsWalkable(x, z + 1)) { walkableNeighbors++; rot = Quaternion.Euler(0f, 0f, 0f); }
                    if (gen.IsWalkable(x, z - 1)) { walkableNeighbors++; rot = Quaternion.Euler(0f, 180f, 0f); }
                    if (gen.IsWalkable(x + 1, z)) { walkableNeighbors++; rot = Quaternion.Euler(0f, 90f, 0f); }
                    if (gen.IsWalkable(x - 1, z)) { walkableNeighbors++; rot = Quaternion.Euler(0f, 270f, 0f); }

                    if (walkableNeighbors == 1 && IsClear(center, exclusions, placed, 1.4f))
                    {
                        GameObject prefab = (rng.NextDouble() < 0.5) ? Pick(armchairPrefabs, rng) : Pick(closetPrefabs, rng);
                        Spawn(prefab, center, rot, parent, placed);
                        count++;
                    }
                }
            }
            return count;
        }

        private GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent, List<Vector3> placed, bool isTipped = false)
        {
            if (prefab == null) return null;

            GameObject inst = Instantiate(prefab, pos, rot, parent);
            if (inst == null) return null;

            // Ground alignment:
            Renderer[] renderers = inst.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                inst.transform.position += new Vector3(0f, -b.min.y, 0f);
            }

            MeshCollider mc = inst.GetComponent<MeshCollider>();
            if (mc != null && isTipped)
            {
                mc.convex = true;
            }

            placed.Add(pos);
            return inst;
        }

        private bool IsClear(Vector3 pos, List<Vector3> exclusions, List<Vector3> placed, float minDist)
        {
            float minSqr = minDist * minDist;
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

        private List<Vector3> CollectExclusions(BackroomsLevelGenerator gen)
        {
            List<Vector3> points = new List<Vector3>();
            float cs = gen.CellSize;

            points.Add(new Vector3(gen.SpawnCell.x * cs + cs * 0.5f, 0f, gen.SpawnCell.y * cs + cs * 0.5f));
            points.Add(new Vector3(gen.ExitCell.x * cs + cs * 0.5f, 0f, gen.ExitCell.y * cs + cs * 0.5f));

            foreach (var kp in FindObjectsByType<KeyPickup>(FindObjectsSortMode.None)) points.Add(kp.transform.position);
            foreach (var ps in FindObjectsByType<PowerSwitch>(FindObjectsSortMode.None)) points.Add(ps.transform.position);
            foreach (var bp in FindObjectsByType<BatteryPickup>(FindObjectsSortMode.None)) points.Add(bp.transform.position);
            foreach (var ap in FindObjectsByType<AmmunitionPickup>(FindObjectsSortMode.None)) points.Add(ap.transform.position);

            return points;
        }

        private GameObject Pick(GameObject[] array, System.Random rng)
        {
            if (array == null || array.Length == 0) return null;
            return array[rng.Next(0, array.Length)];
        }
    }
}
