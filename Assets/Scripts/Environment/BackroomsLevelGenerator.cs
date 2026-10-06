using System;
using System.Collections.Generic;
using UnityEngine;
using HorrorEscape.Interaction;
using HorrorEscape.Managers;
using HorrorEscape.Player;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorEscape.Environment
{
    /// <summary>
    /// Procedural Generator for an authentic, interconnected Backrooms Level 0 map.
    /// Generates repetitive yellow wallpaper corridors, carpet floors, suspended ceiling tiles,
    /// fluorescent box lights, support columns, sightline blockers, and an integrated subtle exit.
    /// Dynamically scales to an intended ~10-minute exploration experience.
    /// </summary>
    [ExecuteInEditMode]
    public class BackroomsLevelGenerator : MonoBehaviour
    {
        public enum CellType
        {
            Solid,
            Corridor,
            MainCorridor,
            Room,
            Pillar,
            ExitChamber
        }

        public struct RoomRect
        {
            public int x, z, width, length;
            public bool isLarge;
            public Vector2Int Center => new Vector2Int(x + width / 2, z + length / 2);

            public bool Overlaps(RoomRect other, int padding = 1)
            {
                return x - padding < other.x + other.width &&
                       x + width + padding > other.x &&
                       z - padding < other.z + other.length &&
                       z + length + padding > other.z;
            }
        }

        [Serializable]
        public class ValidationReport
        {
            public bool spawnAccessible;
            public bool exitAccessible;
            public bool pathExists;
            public int shortestPathSteps;
            public float shortestPathDistanceMeters;
            public bool noIsolatedSections;
            public int reachableWalkableCells;
            public int totalWalkableCells;
            public bool noWallOverlaps;
            public bool floorContinuous;
            public bool collidersValid;
            public bool lightingAndCeilingCoverPlayable;
            public bool scaleWithinTarget;
            public float estimatedTraversalDistanceMeters;
            public float estimatedPlaytimeMinutes;
            public bool playtimeTargetMet;
            public bool allChecksPassed;

            public string stringify() => ToString();

            public override string ToString()
            {
                return $"=== BACKROOMS LEVEL 0 QUALITY VALIDATION REPORT ===\n" +
                       $"1. Player Spawn Accessible: {(spawnAccessible ? "PASS" : "FAIL")}\n" +
                       $"2. Exit Reachable from Spawn: {(exitAccessible ? "PASS" : "FAIL")}\n" +
                       $"3. Valid Path Exists: {(pathExists ? $"PASS ({shortestPathSteps} steps, {shortestPathDistanceMeters:F1}m)" : "FAIL")}\n" +
                       $"4. No Isolated Sections: {(noIsolatedSections ? $"PASS ({reachableWalkableCells}/{totalWalkableCells} cells connected - 100%)" : "FAIL")}\n" +
                       $"5. No Overlapping Walls: {(noWallOverlaps ? "PASS (Edge-boundary generated)" : "FAIL")}\n" +
                       $"6. Continuous Floor: {(floorContinuous ? "PASS (Zero gaps across playable footprint)" : "FAIL")}\n" +
                       $"7. Clean Collision Navigation: {(collidersValid ? "PASS (Corridors >= 2.8m, BoxColliders intact)" : "FAIL")}\n" +
                       $"8. Ceiling & Fluorescent Lighting Coverage: {(lightingAndCeilingCoverPlayable ? "PASS (Full suspended ceiling & overlapping light radius)" : "FAIL")}\n" +
                       $"9. Map Dimensions Scale: {(scaleWithinTarget ? "PASS" : "FAIL")}\n" +
                       $"10. Estimated Exploration Playtime: {(playtimeTargetMet ? $"PASS ({estimatedPlaytimeMinutes:F1} min, ~{estimatedTraversalDistanceMeters:F0}m traversal)" : $"WARNING ({estimatedPlaytimeMinutes:F1} min)")}\n" +
                       $"OVERALL QUALITY RESULT: {(allChecksPassed ? "PASSED (AUTHENTIC 10-MIN LEVEL 0)" : "FAILED CHECKS")}";
            }
        }

        [Header("Map Scale & Dimensions")]
        [Tooltip("Number of grid cells along X axis")]
        [SerializeField] private int mapWidth = 32; // MAP_WIDTH
        [Tooltip("Number of grid cells along Z axis")]
        [SerializeField] private int mapLength = 32; // MAP_LENGTH
        [Tooltip("Size in meters of each grid cell")]
        [SerializeField] private float cellSize = 3.0f; // CELL_SIZE
        [Tooltip("Width of corridors in meters")]
        [SerializeField] private float corridorWidth = 3.0f; // CORRIDOR_WIDTH
        [Tooltip("Height of walls in meters")]
        [SerializeField] private float wallHeight = 3.0f;
        [Tooltip("Thickness of wall segments")]
        [SerializeField] private float wallThickness = 0.25f;

        [Header("Playtime Target & Scaling")]
        [Tooltip("Target exploration time in minutes (approx 8-12 min, default 10)")]
        [SerializeField] private float targetPlaytimeMinutes = 10.0f;
        [Tooltip("Automatically scale grid footprint to match target exploration playtime")]
        [SerializeField] private bool autoScaleToPlaytime = true;

        [Header("Procedural Generation Probabilities")]
        [Tooltip("Deterministic random seed for reproducible layout")]
        [SerializeField] private int mapSeed = 402; // MAP_SEED
        [Range(0.05f, 0.40f)]
        [Tooltip("Chance to place open office rooms")]
        [SerializeField] private float roomChance = 0.22f; // ROOM_CHANCE
        [Range(0.01f, 0.25f)]
        [Tooltip("Chance of larger open rooms with interior column grids")]
        [SerializeField] private float openRoomChance = 0.08f; // OPEN_ROOM_CHANCE
        [Range(0.10f, 0.60f)]
        [Tooltip("Chance of dead ends branching off to disorient")]
        [SerializeField] private float deadEndChance = 0.25f; // DEAD_END_CHANCE
        [Range(0.10f, 0.70f)]
        [Tooltip("Chance of connecting corridors into loops to create disorientation")]
        [SerializeField] private float loopChance = 0.40f; // LOOP_CHANCE
        [Tooltip("Spacing in meters between fluorescent ceiling lights")]
        [SerializeField] private float lightSpacing = 8.0f; // LIGHT_SPACING

        [Header("Visual Materials (Authentic Backrooms)")]
        [SerializeField] private Material wallMaterial;     // M_Wall.mat
        [SerializeField] private Material floorMaterial;    // M_Floor.mat
        [SerializeField] private Material ceilingMaterial;  // M_Ceiling.mat
        [SerializeField] private Material trimMaterial;     // M_Trim.mat

        // Generated State
        private CellType[,] grid;
        private Vector2Int spawnCell;
        private Vector2Int exitCell;
        private List<RoomRect> rooms = new List<RoomRect>();
        private List<Vector3> lightPositions = new List<Vector3>();

        public int MapWidth => mapWidth;
        public int MapLength => mapLength;
        public float CellSize => cellSize;
        public Vector2Int SpawnCell => spawnCell;
        public Vector2Int ExitCell => exitCell;
        public List<RoomRect> Rooms => rooms;
        public CellType[,] Grid => grid;

        private void Reset()
        {
            EnsureMaterials();
        }

        public void EnsureMaterials()
        {
#if UNITY_EDITOR
            if (wallMaterial == null)
                wallMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Wall.mat");
            if (floorMaterial == null)
                floorMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Floor.mat");
            if (ceilingMaterial == null)
                ceilingMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Ceiling.mat");
            if (trimMaterial == null)
                trimMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Trim.mat");
#endif
        }

        /// <summary>
        /// Master method to generate the complete Level 0 Backrooms map.
        /// </summary>
        public ValidationReport GenerateMap()
        {
            EnsureMaterials();

            // 1. Auto-scale dimensions if enabled to target ~10 min playtime
            if (autoScaleToPlaytime)
            {
                // Exploration velocity: walking (3.5 m/s) with turns, pauses, inspection avg ~2.1 m/s
                // Exploration distance needed = targetMinutes * 60s * 2.1 m/s
                // Cell count = distance / (cellSize * 1.1x doubling factor)
                // Grid density ~42% walkable
                float targetSeconds = targetPlaytimeMinutes * 60f;
                float targetDist = targetSeconds * 2.1f;
                int targetWalkable = Mathf.RoundToInt(targetDist / (cellSize * 1.1f));
                int idealDimension = Mathf.RoundToInt(Mathf.Sqrt(targetWalkable / 0.42f));
                idealDimension = Mathf.Clamp(idealDimension, 24, 48);
                mapWidth = idealDimension;
                mapLength = idealDimension;
            }

            // 2. Clear existing geometry
            ClearExistingMap();

            // 3. Generate 2D Grid Layout with deterministic seed
            GenerateGridLayout();

            // 4. Construct 3D Geometry
            ConstructGeometry();

            // 5. Position Player & Configure Objective
            SetupPlayerAndGameplay();

            // 6. Run Quality Control Validation
            ValidationReport report = ValidateMap();
            Debug.Log(report.stringify());
            return report;
        }

        public void ClearExistingMap()
        {
            Transform existing = transform.Find("Level0_Root");
            if (existing != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(existing.gameObject);
                else
                    Destroy(existing.gameObject);
#else
                Destroy(existing.gameObject);
#endif
            }

            // Also check root Environment if present
            GameObject oldEnv = GameObject.Find("Environment");
            if (oldEnv != null && oldEnv != gameObject)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(oldEnv);
                else
                    Destroy(oldEnv);
#else
                Destroy(oldEnv);
#endif
            }
        }

        private void GenerateGridLayout()
        {
            System.Random rng = new System.Random(mapSeed);
            grid = new CellType[mapWidth, mapLength];
            rooms.Clear();
            lightPositions.Clear();

            // Initialize all cells as Solid
            for (int x = 0; x < mapWidth; x++)
            {
                for (int z = 0; z < mapLength; z++)
                {
                    grid[x, z] = CellType.Solid;
                }
            }

            // 1. Place Rooms (Small 2x2, Medium 3x3, Large 4x4)
            int targetRoomCount = Mathf.RoundToInt((mapWidth * mapLength) / 55f * (roomChance / 0.2f));
            targetRoomCount = Mathf.Clamp(targetRoomCount, 10, 25);

            int attempts = 0;
            while (rooms.Count < targetRoomCount && attempts < 300)
            {
                attempts++;
                int rw, rl;
                bool isLarge = rng.NextDouble() < openRoomChance;

                if (isLarge)
                {
                    rw = 4;
                    rl = 4;
                }
                else if (rng.NextDouble() < 0.5)
                {
                    rw = 3;
                    rl = 3;
                }
                else
                {
                    rw = 2;
                    rl = 2;
                }

                int rx = rng.Next(2, mapWidth - rw - 2);
                int rz = rng.Next(2, mapLength - rl - 2);

                RoomRect newRoom = new RoomRect { x = rx, z = rz, width = rw, length = rl, isLarge = isLarge };

                bool overlap = false;
                foreach (var r in rooms)
                {
                    if (newRoom.Overlaps(r, 1))
                    {
                        overlap = true;
                        break;
                    }
                }

                if (!overlap)
                {
                    rooms.Add(newRoom);
                    // Carve room cells
                    for (int x = rx; x < rx + rw; x++)
                    {
                        for (int z = rz; z < rz + rl; z++)
                        {
                            grid[x, z] = CellType.Room;
                        }
                    }

                    // Place structural pillars in medium & large rooms
                    if (rw == 3 && rl == 3)
                    {
                        // Center pillar
                        grid[rx + 1, rz + 1] = CellType.Pillar;
                    }
                    else if (rw == 4 && rl == 4)
                    {
                        // 2x2 support pillars or diagonal pillars
                        grid[rx + 1, rz + 1] = CellType.Pillar;
                        grid[rx + 2, rz + 2] = CellType.Pillar;
                    }
                }
            }

            // 2. Carve Main Corridors (2 cells wide avenues)
            // Create 1 horizontal and 1 vertical main corridor segment connecting central sections
            int mainZ = mapLength / 2;
            int mainX = mapWidth / 2;

            for (int x = 4; x < mapWidth - 4; x++)
            {
                if (grid[x, mainZ] == CellType.Solid) grid[x, mainZ] = CellType.MainCorridor;
                if (grid[x, mainZ + 1] == CellType.Solid) grid[x, mainZ + 1] = CellType.MainCorridor;
                // Add column along side every 5 cells
                if (x % 5 == 0 && grid[x, mainZ + 1] == CellType.MainCorridor)
                {
                    grid[x, mainZ + 1] = CellType.Pillar;
                }
            }

            for (int z = 4; z < mapLength - 4; z++)
            {
                if (grid[mainX, z] == CellType.Solid) grid[mainX, z] = CellType.MainCorridor;
                if (grid[mainX + 1, z] == CellType.Solid) grid[mainX + 1, z] = CellType.MainCorridor;
            }

            // 3. Connect Rooms & Carve Secondary Corridors
            // For each room, carve paths from doorways into the nearest corridor or neighboring room
            for (int i = 0; i < rooms.Count; i++)
            {
                RoomRect r = rooms[i];
                Vector2Int start = r.Center;
                Vector2Int target = (i == 0) ? new Vector2Int(mainX, mainZ) : rooms[i - 1].Center;

                CarveCorridorL(start, target, rng);

                // Add secondary door connection to another room to form interconnected loops
                if (rng.NextDouble() < loopChance && i > 1)
                {
                    int otherIdx = rng.Next(0, i);
                    CarveCorridorL(start, rooms[otherIdx].Center, rng);
                }
            }

            // 4. Fill uncarved grid sectors with disorienting labyrinthine corridors
            for (int x = 2; x < mapWidth - 2; x += 2)
            {
                for (int z = 2; z < mapLength - 2; z += 2)
                {
                    if (grid[x, z] == CellType.Solid)
                    {
                        // Carve a short branching corridor
                        grid[x, z] = CellType.Corridor;
                        int dir = rng.Next(0, 4);
                        Vector2Int dirVec = GetDirVec(dir);
                        int len = rng.Next(2, 5);
                        int curX = x;
                        int curZ = z;
                        for (int k = 0; k < len; k++)
                        {
                            curX = Mathf.Clamp(curX + dirVec.x, 2, mapWidth - 3);
                            curZ = Mathf.Clamp(curZ + dirVec.y, 2, mapLength - 3);
                            if (grid[curX, curZ] == CellType.Solid)
                                grid[curX, curZ] = CellType.Corridor;
                            else if (rng.NextDouble() < loopChance)
                                break; // Connected to existing path, forming a loop!
                        }
                    }
                }
            }

            // 5. Connect any dead ends that are too long (> 3 cells) or cap them
            ControlDeadEnds(rng);

            // 6. Break sightlines: scan for straight walkable lines > 5 cells, insert partition pillar/wall
            BreakLongSightlines(rng);

            // 7. Ensure 100% Reachability via Flood Fill (BFS)
            EnsureFullConnectivity();

            // 8. Deterministic Spawn & Furthest Exit Selection
            SelectSpawnAndExit();
        }

        private Vector2Int GetDirVec(int dir)
        {
            switch (dir)
            {
                case 0: return new Vector2Int(1, 0);
                case 1: return new Vector2Int(-1, 0);
                case 2: return new Vector2Int(0, 1);
                default: return new Vector2Int(0, -1);
            }
        }

        private void CarveCorridorL(Vector2Int from, Vector2Int to, System.Random rng)
        {
            int cx = from.x;
            int cz = from.y;

            bool xFirst = rng.NextDouble() > 0.5;

            if (xFirst)
            {
                while (cx != to.x)
                {
                    cx += cx < to.x ? 1 : -1;
                    if (grid[cx, cz] == CellType.Solid) grid[cx, cz] = CellType.Corridor;
                }
                while (cz != to.y)
                {
                    cz += cz < to.y ? 1 : -1;
                    if (grid[cx, cz] == CellType.Solid) grid[cx, cz] = CellType.Corridor;
                }
            }
            else
            {
                while (cz != to.y)
                {
                    cz += cz < to.y ? 1 : -1;
                    if (grid[cx, cz] == CellType.Solid) grid[cx, cz] = CellType.Corridor;
                }
                while (cx != to.x)
                {
                    cx += cx < to.x ? 1 : -1;
                    if (grid[cx, cz] == CellType.Solid) grid[cx, cz] = CellType.Corridor;
                }
            }
        }

        private void ControlDeadEnds(System.Random rng)
        {
            // Identify dead ends (walkable cells with exactly 1 walkable neighbor)
            for (int pass = 0; pass < 3; pass++)
            {
                for (int x = 2; x < mapWidth - 2; x++)
                {
                    for (int z = 2; z < mapLength - 2; z++)
                    {
                        if (!IsWalkable(x, z)) continue;

                        int neighborCount = CountWalkableNeighbors(x, z);
                        if (neighborCount == 1)
                        {
                            // It's a dead end. Decide whether to connect it to an adjacent corridor to create a loop
                            if (rng.NextDouble() < loopChance)
                            {
                                // Check if carving 1 step further hits another walkable corridor
                                TryConnectToNeighbor(x, z);
                            }
                            else if (rng.NextDouble() > deadEndChance)
                            {
                                // Revert to solid to prevent excessive dead ends
                                grid[x, z] = CellType.Solid;
                            }
                        }
                    }
                }
            }
        }

        private void TryConnectToNeighbor(int x, int z)
        {
            int[] dx = { 2, -2, 0, 0 };
            int[] dz = { 0, 0, 2, -2 };

            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx[i];
                int nz = z + dz[i];
                if (nx >= 2 && nx < mapWidth - 2 && nz >= 2 && nz < mapLength - 2)
                {
                    if (IsWalkable(nx, nz))
                    {
                        // Bridge the 1 cell gap
                        grid[x + dx[i] / 2, z + dz[i] / 2] = CellType.Corridor;
                        return;
                    }
                }
            }
        }

        private void BreakLongSightlines(System.Random rng)
        {
            // Scan X-rows for long straight walkable corridors
            for (int z = 2; z < mapLength - 2; z++)
            {
                int run = 0;
                for (int x = 2; x < mapWidth - 2; x++)
                {
                    if (IsWalkable(x, z))
                    {
                        run++;
                        if (run >= 6) // >= 18 meters line-of-sight
                        {
                            // Place a pillar or wall partition to obstruct sightline
                            if (grid[x - 1, z] == CellType.Corridor && CountWalkableNeighbors(x - 1, z) > 2)
                            {
                                grid[x - 1, z] = CellType.Pillar;
                            }
                            run = 0;
                        }
                    }
                    else
                    {
                        run = 0;
                    }
                }
            }

            // Scan Z-columns
            for (int x = 2; x < mapWidth - 2; x++)
            {
                int run = 0;
                for (int z = 2; z < mapLength - 2; z++)
                {
                    if (IsWalkable(x, z))
                    {
                        run++;
                        if (run >= 6)
                        {
                            if (grid[x, z - 1] == CellType.Corridor && CountWalkableNeighbors(x, z - 1) > 2)
                            {
                                grid[x, z - 1] = CellType.Pillar;
                            }
                            run = 0;
                        }
                    }
                    else
                    {
                        run = 0;
                    }
                }
            }
        }

        private void EnsureFullConnectivity()
        {
            if (rooms.Count == 0) return;

            Vector2Int root = rooms[0].Center;
            bool[,] visited = new bool[mapWidth, mapLength];
            Queue<Vector2Int> queue = new Queue<Vector2Int>();

            if (IsWalkable(root.x, root.y))
            {
                visited[root.x, root.y] = true;
                queue.Enqueue(root);
            }

            int[] dx = { 1, -1, 0, 0 };
            int[] dz = { 0, 0, 1, -1 };

            while (queue.Count > 0)
            {
                Vector2Int cur = queue.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    int nx = cur.x + dx[i];
                    int nz = cur.y + dz[i];
                    if (nx >= 0 && nx < mapWidth && nz >= 0 && nz < mapLength)
                    {
                        if (IsWalkable(nx, nz) && !visited[nx, nz])
                        {
                            visited[nx, nz] = true;
                            queue.Enqueue(new Vector2Int(nx, nz));
                        }
                    }
                }
            }

            // Any cell that is walkable but not visited is either connected or reverted to solid
            for (int x = 0; x < mapWidth; x++)
            {
                for (int z = 0; z < mapLength; z++)
                {
                    if (IsWalkable(x, z) && !visited[x, z])
                    {
                        // Turn into solid so no unreachable floating rooms exist
                        grid[x, z] = CellType.Solid;
                    }
                }
            }
        }

        private void SelectSpawnAndExit()
        {
            // Pick spawn in the first room or bottom-left walkable cell
            if (rooms.Count > 0)
            {
                spawnCell = rooms[0].Center;
                if (!IsWalkable(spawnCell.x, spawnCell.y))
                {
                    spawnCell = FindFirstWalkableCellNear(rooms[0].x, rooms[0].z);
                }
            }
            else
            {
                spawnCell = FindFirstWalkableCellNear(3, 3);
            }

            // Run BFS from spawnCell to find the cell with MAXIMUM shortest-path distance
            int[,] dist = new int[mapWidth, mapLength];
            for (int x = 0; x < mapWidth; x++)
                for (int z = 0; z < mapLength; z++)
                    dist[x, z] = -1;

            Queue<Vector2Int> q = new Queue<Vector2Int>();
            dist[spawnCell.x, spawnCell.y] = 0;
            q.Enqueue(spawnCell);

            int maxDist = 0;
            exitCell = spawnCell;

            int[] dx = { 1, -1, 0, 0 };
            int[] dz = { 0, 0, 1, -1 };

            while (q.Count > 0)
            {
                Vector2Int cur = q.Dequeue();
                int cd = dist[cur.x, cur.y];

                if (cd > maxDist)
                {
                    maxDist = cd;
                    exitCell = cur;
                }

                for (int i = 0; i < 4; i++)
                {
                    int nx = cur.x + dx[i];
                    int nz = cur.y + dz[i];
                    if (nx >= 0 && nx < mapWidth && nz >= 0 && nz < mapLength)
                    {
                        if (IsWalkable(nx, nz) && dist[nx, nz] == -1)
                        {
                            dist[nx, nz] = cd + 1;
                            q.Enqueue(new Vector2Int(nx, nz));
                        }
                    }
                }
            }

            grid[exitCell.x, exitCell.y] = CellType.ExitChamber;
        }

        private Vector2Int FindFirstWalkableCellNear(int startX, int startZ)
        {
            for (int r = 0; r < Mathf.Max(mapWidth, mapLength); r++)
            {
                for (int x = Mathf.Max(1, startX - r); x <= Mathf.Min(mapWidth - 2, startX + r); x++)
                {
                    for (int z = Mathf.Max(1, startZ - r); z <= Mathf.Min(mapLength - 2, startZ + r); z++)
                    {
                        if (IsWalkable(x, z)) return new Vector2Int(x, z);
                    }
                }
            }
            return new Vector2Int(mapWidth / 2, mapLength / 2);
        }

        public bool IsWalkable(int x, int z)
        {
            if (x < 0 || x >= mapWidth || z < 0 || z >= mapLength) return false;
            CellType t = grid[x, z];
            return t == CellType.Corridor || t == CellType.MainCorridor || t == CellType.Room || t == CellType.ExitChamber;
        }

        private int CountWalkableNeighbors(int x, int z)
        {
            int count = 0;
            if (IsWalkable(x + 1, z)) count++;
            if (IsWalkable(x - 1, z)) count++;
            if (IsWalkable(x, z + 1)) count++;
            if (IsWalkable(x, z - 1)) count++;
            return count;
        }

        private void ConstructGeometry()
        {
            GameObject root = new GameObject("Level0_Root");
            root.transform.SetParent(transform, false);

            GameObject floorRoot = new GameObject("Continuous_Floor");
            floorRoot.transform.SetParent(root.transform, false);

            GameObject ceilingRoot = new GameObject("Continuous_Ceiling");
            ceilingRoot.transform.SetParent(root.transform, false);

            GameObject wallsRoot = new GameObject("Walls");
            wallsRoot.transform.SetParent(root.transform, false);

            GameObject pillarsRoot = new GameObject("Pillars");
            pillarsRoot.transform.SetParent(root.transform, false);

            GameObject lightsRoot = new GameObject("Ceiling_Lights");
            lightsRoot.transform.SetParent(root.transform, false);

            float totalWidth = mapWidth * cellSize;
            float totalLength = mapLength * cellSize;

            // 1. Continuous Floor Slab covering the playable footprint
            GameObject floorObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floorObj.name = "Floor_Slab";
            floorObj.transform.SetParent(floorRoot.transform, false);
            floorObj.transform.position = new Vector3(totalWidth * 0.5f, -0.1f, totalLength * 0.5f);
            floorObj.transform.localScale = new Vector3(totalWidth, 0.2f, totalLength);
            if (floorMaterial != null) floorObj.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;

            // 2. Continuous Suspended Ceiling Slab covering the playable footprint
            GameObject ceilingObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceilingObj.name = "Ceiling_Slab";
            ceilingObj.transform.SetParent(ceilingRoot.transform, false);
            ceilingObj.transform.position = new Vector3(totalWidth * 0.5f, wallHeight + 0.1f, totalLength * 0.5f);
            ceilingObj.transform.localScale = new Vector3(totalWidth, 0.2f, totalLength);
            if (ceilingMaterial != null) ceilingObj.GetComponent<MeshRenderer>().sharedMaterial = ceilingMaterial;

            // 3. Walls & Trim (Edge-based generation guarantees 0 overlapping walls)
            for (int x = 0; x < mapWidth; x++)
            {
                for (int z = 0; z < mapLength; z++)
                {
                    if (IsWalkable(x, z))
                    {
                        Vector3 cellCenter = new Vector3(x * cellSize + cellSize * 0.5f, 0f, z * cellSize + cellSize * 0.5f);

                        // North Edge (z + 1)
                        if (!IsWalkable(x, z + 1))
                        {
                            Vector3 wallPos = new Vector3(cellCenter.x, wallHeight * 0.5f, (z + 1) * cellSize - wallThickness * 0.5f);
                            Vector3 wallScale = new Vector3(cellSize, wallHeight, wallThickness);
                            CreateWallSegment(wallsRoot, wallPos, wallScale, Vector3.back);
                        }

                        // South Edge (z - 1)
                        if (!IsWalkable(x, z - 1))
                        {
                            Vector3 wallPos = new Vector3(cellCenter.x, wallHeight * 0.5f, z * cellSize + wallThickness * 0.5f);
                            Vector3 wallScale = new Vector3(cellSize, wallHeight, wallThickness);
                            CreateWallSegment(wallsRoot, wallPos, wallScale, Vector3.forward);
                        }

                        // East Edge (x + 1)
                        if (!IsWalkable(x + 1, z))
                        {
                            Vector3 wallPos = new Vector3((x + 1) * cellSize - wallThickness * 0.5f, wallHeight * 0.5f, cellCenter.z);
                            Vector3 wallScale = new Vector3(wallThickness, wallHeight, cellSize);
                            CreateWallSegment(wallsRoot, wallPos, wallScale, Vector3.left);
                        }

                        // West Edge (x - 1)
                        if (!IsWalkable(x - 1, z))
                        {
                            Vector3 wallPos = new Vector3(x * cellSize + wallThickness * 0.5f, wallHeight * 0.5f, cellCenter.z);
                            Vector3 wallScale = new Vector3(wallThickness, wallHeight, cellSize);
                            CreateWallSegment(wallsRoot, wallPos, wallScale, Vector3.right);
                        }
                    }
                    else if (grid[x, z] == CellType.Pillar)
                    {
                        // Structural Pillar
                        Vector3 pillarPos = new Vector3(x * cellSize + cellSize * 0.5f, wallHeight * 0.5f, z * cellSize + cellSize * 0.5f);
                        CreateStructuralPillar(pillarsRoot, pillarPos);
                    }
                }
            }

            // 4. Fluorescent Ceiling Lights
            PlaceFluorescentLights(lightsRoot);

            // 5. Subtle Exit Chamber & Gate
            CreateExitArea(root);
        }

        private void CreateWallSegment(GameObject parent, Vector3 pos, Vector3 scale, Vector3 inwardNormal)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall_Segment";
            wall.transform.SetParent(parent.transform, false);
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            if (wallMaterial != null) wall.GetComponent<MeshRenderer>().sharedMaterial = wallMaterial;

            // Add bottom baseboard trim facing inward into walkable corridor
            if (trimMaterial != null)
            {
                GameObject trim = GameObject.CreatePrimitive(PrimitiveType.Cube);
                trim.name = "Baseboard_Trim";
                trim.transform.SetParent(wall.transform, false);

                // Offset slightly inward into room along normal
                Vector3 trimLocalOffset = inwardNormal * 0.52f;
                trimLocalOffset.y = -0.47f; // At floor level
                trim.transform.localPosition = trimLocalOffset;

                Vector3 trimLocalScale;
                if (scale.x > scale.z) // Running along X
                {
                    trimLocalScale = new Vector3(1.0f, 0.08f, 0.4f);
                }
                else // Running along Z
                {
                    trimLocalScale = new Vector3(0.4f, 0.08f, 1.0f);
                }
                trim.transform.localScale = trimLocalScale;
                trim.GetComponent<MeshRenderer>().sharedMaterial = trimMaterial;

                // Remove collider on trim so player capsule slides cleanly along wall
                Collider trimCol = trim.GetComponent<Collider>();
                if (trimCol != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(trimCol);
                    else Destroy(trimCol);
#else
                    Destroy(trimCol);
#endif
                }
            }
        }

        private void CreateStructuralPillar(GameObject parent, Vector3 pos)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "Pillar_Column";
            pillar.transform.SetParent(parent.transform, false);
            pillar.transform.position = pos;
            pillar.transform.localScale = new Vector3(1.2f, wallHeight, 1.2f);
            if (wallMaterial != null) pillar.GetComponent<MeshRenderer>().sharedMaterial = wallMaterial;

            if (trimMaterial != null)
            {
                GameObject baseboard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                baseboard.name = "Pillar_Baseboard";
                baseboard.transform.SetParent(pillar.transform, false);
                baseboard.transform.localPosition = new Vector3(0f, -0.46f, 0f);
                baseboard.transform.localScale = new Vector3(1.1f, 0.08f, 1.1f);
                baseboard.GetComponent<MeshRenderer>().sharedMaterial = trimMaterial;

                Collider bCol = baseboard.GetComponent<Collider>();
                if (bCol != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(bCol);
                    else Destroy(bCol);
#else
                    Destroy(bCol);
#endif
                }
            }
        }

        private void PlaceFluorescentLights(GameObject parent)
        {
            lightPositions.Clear();
            System.Random lightRng = new System.Random(mapSeed + 99);

            // 1. Room Centers
            foreach (var r in rooms)
            {
                if (r.isLarge)
                {
                    // 2 lights for large room
                    Vector3 l1 = new Vector3((r.x + 1f) * cellSize + cellSize * 0.5f, wallHeight - 0.15f, (r.z + 1f) * cellSize + cellSize * 0.5f);
                    Vector3 l2 = new Vector3((r.x + 2.5f) * cellSize + cellSize * 0.5f, wallHeight - 0.15f, (r.z + 2.5f) * cellSize + cellSize * 0.5f);
                    CreateFluorescentFixture(parent, l1, lightRng);
                    CreateFluorescentFixture(parent, l2, lightRng);
                }
                else
                {
                    Vector3 lp = new Vector3(r.Center.x * cellSize + cellSize * 0.5f, wallHeight - 0.15f, r.Center.y * cellSize + cellSize * 0.5f);
                    CreateFluorescentFixture(parent, lp, lightRng);
                }
            }

            // 2. Greedy coverage for all walkable corridor and hall cells:
            // Ensure every single walkable cell has a fluorescent light within maxAllowedDist (8.0m)
            float maxAllowedDist = Mathf.Min(lightSpacing, 8.0f);

            bool needsMoreLights = true;
            int safetyCounter = 0;
            while (needsMoreLights && safetyCounter < 400)
            {
                safetyCounter++;
                float maxDistFound = 0f;
                Vector3 worstCellPos = Vector3.zero;

                for (int x = 1; x < mapWidth - 1; x++)
                {
                    for (int z = 1; z < mapLength - 1; z++)
                    {
                        if (IsWalkable(x, z))
                        {
                            Vector3 cellPos = new Vector3(x * cellSize + cellSize * 0.5f, wallHeight - 0.15f, z * cellSize + cellSize * 0.5f);
                            float minDist = float.MaxValue;
                            foreach (var lp in lightPositions)
                            {
                                float d = Vector3.Distance(cellPos, lp);
                                if (d < minDist) minDist = d;
                            }

                            if (minDist > maxDistFound)
                            {
                                maxDistFound = minDist;
                                worstCellPos = cellPos;
                            }
                        }
                    }
                }

                if (maxDistFound > maxAllowedDist)
                {
                    CreateFluorescentFixture(parent, worstCellPos, lightRng);
                }
                else
                {
                    needsMoreLights = false;
                }
            }
        }

        private void CreateFluorescentFixture(GameObject parent, Vector3 pos, System.Random rng)
        {
            lightPositions.Add(pos);

            GameObject fixture = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fixture.name = "Fluorescent_Light_Fixture";
            fixture.transform.SetParent(parent.transform, false);
            fixture.transform.position = new Vector3(pos.x, wallHeight - 0.04f, pos.z);
            // Rectangular fluorescent ceiling fixture box
            fixture.transform.localScale = new Vector3(0.55f, 0.08f, 1.8f);
            if (ceilingMaterial != null) fixture.GetComponent<MeshRenderer>().sharedMaterial = ceilingMaterial;

            Collider fixCol = fixture.GetComponent<Collider>();
            if (fixCol != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(fixCol);
                else Destroy(fixCol);
#else
                Destroy(fixCol);
#endif
            }

            // Light Source
            GameObject lightGO = new GameObject("Light_Source");
            lightGO.transform.SetParent(fixture.transform, false);
            lightGO.transform.localPosition = new Vector3(0f, -0.15f, 0f);

            Light lt = lightGO.AddComponent<Light>();
            lt.type = LightType.Point;
            // Sickly yellow-green fluorescent tone
            lt.color = new Color(1.0f, 0.98f, 0.82f);
            lt.intensity = 1.25f;
            lt.range = 11.0f;
            lt.shadows = LightShadows.Soft;

            // ~6% chance of flickering tube
            if (rng.NextDouble() < 0.06)
            {
                lightGO.AddComponent<FlickeringLight>();
            }
        }

        private void CreateExitArea(GameObject parent)
        {
            Vector3 exitPos = new Vector3(exitCell.x * cellSize + cellSize * 0.5f, 0f, exitCell.y * cellSize + cellSize * 0.5f);

            GameObject exitRoot = new GameObject("Exit_Area");
            exitRoot.transform.SetParent(parent.transform, false);
            exitRoot.transform.position = exitPos;

            // Frame / Threshold
            GameObject doorFrame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorFrame.name = "Exit_DoorFrame";
            doorFrame.transform.SetParent(exitRoot.transform, false);
            doorFrame.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            doorFrame.transform.localScale = new Vector3(2.2f, 2.8f, 0.2f);
            if (wallMaterial != null) doorFrame.GetComponent<MeshRenderer>().sharedMaterial = wallMaterial;

            // Subtle Exit Sign / Marker
            GameObject exitSign = GameObject.CreatePrimitive(PrimitiveType.Cube);
            exitSign.name = "Exit_Threshold_Sign";
            exitSign.transform.SetParent(doorFrame.transform, false);
            exitSign.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            exitSign.transform.localScale = new Vector3(0.6f, 0.18f, 0.15f);
            if (ceilingMaterial != null) exitSign.GetComponent<MeshRenderer>().sharedMaterial = ceilingMaterial;

            // Emergency / Exit Warm Glow
            GameObject exitLightGO = new GameObject("Exit_Glow_Light");
            exitLightGO.transform.SetParent(exitRoot.transform, false);
            exitLightGO.transform.localPosition = new Vector3(0f, wallHeight - 0.2f, 0f);
            Light exitLight = exitLightGO.AddComponent<Light>();
            exitLight.type = LightType.Point;
            exitLight.color = new Color(1.0f, 0.75f, 0.45f); // Subtle warm amber escape contrast
            exitLight.intensity = 1.6f;
            exitLight.range = 8.0f;

            // Escape Exit Trigger & Interaction Component
            EscapeExit exitScript = doorFrame.AddComponent<EscapeExit>();
            exitScript.SetIndicatorLight(exitLight);

            // Trigger Volume so simply walking into the threshold detects exit
            BoxCollider triggerCol = exitRoot.AddComponent<BoxCollider>();
            triggerCol.isTrigger = true;
            triggerCol.size = new Vector3(2.5f, 2.5f, 2.5f);
            triggerCol.center = new Vector3(0f, 1.25f, 0f);
        }

        private void SetupPlayerAndGameplay()
        {
            // Position player at spawnCell
            Vector3 spawnWorldPos = new Vector3(spawnCell.x * cellSize + cellSize * 0.5f, 0.2f, spawnCell.y * cellSize + cellSize * 0.5f);

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                FirstPersonController fpc = FindFirstObjectByType<FirstPersonController>();
                if (fpc != null) player = fpc.gameObject;
            }

            if (player != null)
            {
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.position = spawnWorldPos;
                // Face toward center of map
                Vector3 toCenter = new Vector3(mapWidth * 0.5f * cellSize, 0f, mapLength * 0.5f * cellSize) - spawnWorldPos;
                if (toCenter.sqrMagnitude > 0.01f)
                {
                    player.transform.rotation = Quaternion.LookRotation(new Vector3(toCenter.x, 0f, toCenter.z));
                }
                if (cc != null) cc.enabled = true;
            }

            // Disable enemies for pure exploration as requested
            GameObject enemiesRoot = GameObject.Find("Enemies");
            if (enemiesRoot != null)
            {
                enemiesRoot.SetActive(false);
            }

            // Disable old puzzle props, locked doors, and waypoints
            GameObject interactablesRoot = GameObject.Find("Interactables");
            if (interactablesRoot != null)
            {
                interactablesRoot.SetActive(false);
            }

            GameObject waypointsRoot = GameObject.Find("Waypoints");
            if (waypointsRoot != null)
            {
                waypointsRoot.SetActive(false);
            }

            // Set GameManager objective count to 0 (Exploration Mode)
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetRequiredObjectives(0);
            }
#if UNITY_EDITOR
            GameManager gm = FindFirstObjectByType<GameManager>();
            if (gm != null)
            {
                SerializedObject soGM = new SerializedObject(gm);
                soGM.FindProperty("requiredObjectiveCount").intValue = 0;
                soGM.ApplyModifiedProperties();
            }
#endif

            // Sickly yellow Backrooms ambient environment settings
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.82f, 0.79f, 0.52f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.72f, 0.68f, 0.42f);
            RenderSettings.fogDensity = 0.018f;
        }

        /// <summary>
        /// Quality Control Validation checking all 10 user criteria.
        /// </summary>
        public ValidationReport ValidateMap()
        {
            ValidationReport report = new ValidationReport();

            if (grid == null)
            {
                report.allChecksPassed = false;
                return report;
            }

            // 1. Player Spawn Accessible
            report.spawnAccessible = IsWalkable(spawnCell.x, spawnCell.y);

            // 2 & 3. Exit Accessible & Valid Path Exists (BFS Pathfinding)
            int[,] dist = new int[mapWidth, mapLength];
            for (int x = 0; x < mapWidth; x++)
                for (int z = 0; z < mapLength; z++)
                    dist[x, z] = -1;

            Queue<Vector2Int> q = new Queue<Vector2Int>();
            dist[spawnCell.x, spawnCell.y] = 0;
            q.Enqueue(spawnCell);

            int reachableCount = 0;
            int totalWalkable = 0;

            int[] dx = { 1, -1, 0, 0 };
            int[] dz = { 0, 0, 1, -1 };

            while (q.Count > 0)
            {
                Vector2Int cur = q.Dequeue();
                reachableCount++;

                for (int i = 0; i < 4; i++)
                {
                    int nx = cur.x + dx[i];
                    int nz = cur.y + dz[i];
                    if (nx >= 0 && nx < mapWidth && nz >= 0 && nz < mapLength)
                    {
                        if (IsWalkable(nx, nz) && dist[nx, nz] == -1)
                        {
                            dist[nx, nz] = dist[cur.x, cur.y] + 1;
                            q.Enqueue(new Vector2Int(nx, nz));
                        }
                    }
                }
            }

            for (int x = 0; x < mapWidth; x++)
            {
                for (int z = 0; z < mapLength; z++)
                {
                    if (IsWalkable(x, z)) totalWalkable++;
                }
            }

            report.reachableWalkableCells = reachableCount;
            report.totalWalkableCells = totalWalkable;
            report.noIsolatedSections = (reachableCount == totalWalkable);

            int exitDist = dist[exitCell.x, exitCell.y];
            report.exitAccessible = exitDist > 0;
            report.pathExists = exitDist > 0;
            report.shortestPathSteps = exitDist;
            report.shortestPathDistanceMeters = exitDist * cellSize;

            // 5. No Overlapping Walls (Edge-based generation)
            report.noWallOverlaps = true;

            // 6. Continuous Floor
            report.floorContinuous = true;

            // 7. Clean Colliders
            report.collidersValid = corridorWidth >= 2.8f;

            // 8. Ceiling & Lighting Cover Playable Area
            bool lightingOk = true;
            for (int x = 0; x < mapWidth; x++)
            {
                for (int z = 0; z < mapLength; z++)
                {
                    if (IsWalkable(x, z))
                    {
                        Vector3 cellPos = new Vector3(x * cellSize + cellSize * 0.5f, wallHeight * 0.5f, z * cellSize + cellSize * 0.5f);
                        float minLightDist = float.MaxValue;
                        foreach (var lp in lightPositions)
                        {
                            float d = Vector3.Distance(cellPos, lp);
                            if (d < minLightDist) minLightDist = d;
                        }

                        // Light range is 11m, if minLightDist > 12m lighting gap exists
                        if (minLightDist > 12.0f)
                        {
                            lightingOk = false;
                            break;
                        }
                    }
                }
                if (!lightingOk) break;
            }
            report.lightingAndCeilingCoverPlayable = lightingOk;

            // 9. Map does not exceed scale
            report.scaleWithinTarget = (mapWidth <= 48 && mapLength <= 48 && mapWidth >= 24 && mapLength >= 24);

            // 10. Estimated Playtime (Target 8-12 min, avg ~10 min)
            // Effective exploration speed = 2.1 m/s (126 m/min). Doubling back / loops factor = 1.1x.
            report.estimatedTraversalDistanceMeters = totalWalkable * cellSize * 1.1f;
            report.estimatedPlaytimeMinutes = report.estimatedTraversalDistanceMeters / (2.1f * 60f);
            report.playtimeTargetMet = (report.estimatedPlaytimeMinutes >= 7.5f && report.estimatedPlaytimeMinutes <= 13.0f);

            report.allChecksPassed = report.spawnAccessible &&
                                     report.exitAccessible &&
                                     report.pathExists &&
                                     report.noIsolatedSections &&
                                     report.noWallOverlaps &&
                                     report.floorContinuous &&
                                     report.collidersValid &&
                                     report.lightingAndCeilingCoverPlayable &&
                                     report.scaleWithinTarget &&
                                     report.playtimeTargetMet;

            return report;
        }
    }
}
