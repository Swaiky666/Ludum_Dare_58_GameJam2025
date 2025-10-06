using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public enum FloorType
{
    Walkable,
    Unwalkable,
    UnwalkableTransparent
}

public class Floor
{
    public FloorType type;
    public Vector2Int gridPosition;
    public Vector3 worldPosition;
    public GameObject floorObject;
}

public class ExitDoor
{
    public Vector3 position;
    public int connectedRoomIndex;
    public GameObject doorObject;
    public BoxCollider triggerCollider;
}

public class QuadTreeNode
{
    public Rect bounds;
    public QuadTreeNode[] children;
    public bool isLeaf;
    public bool isWalkable;

    public QuadTreeNode(Rect bounds)
    {
        this.bounds = bounds;
        this.isLeaf = true;
        this.isWalkable = true;
        this.children = null;
    }
}

public class PathNode
{
    public Vector2Int position;
    public float gCost;
    public float hCost;
    public float fCost => gCost + hCost;
    public PathNode parent;
}

public class RoomGenerator : MonoBehaviour
{
    [Header("Room Settings")]
    [SerializeField] private Vector2Int roomSize = new Vector2Int(30, 20);
    [SerializeField] private float tileSize = 1f;

    [Header("Quadtree Settings")]
    [SerializeField, Range(1, 6)] private int maxDepth = 4;
    [SerializeField, Range(0, 1)] private float splitChance = 0.7f;
    [SerializeField, Range(2, 10)] private int minRegionSize = 3;

    [Header("Density Settings")]
    [SerializeField, Range(0, 1)] private float obstacleDensity = 0.4f;
    [SerializeField, Range(0, 1)] private float transparentDensity = 0.3f;
    [SerializeField, Range(0, 0.3f)] private float scatterObstacleDensity = 0.05f;

    [Header("Path Settings")]
    [SerializeField, Range(1, 5)] private int pathWidth = 2;

    [Header("Detection Settings")]
    [SerializeField, Range(0.5f, 3f)] private float doorDetectionRange = 1.0f;

    [Header("Monster Settings")]
    [SerializeField, Range(0, 1)] private float monsterSpawnChance = 0.05f;
    [SerializeField, Range(1, 10)] private int playerSafeRange = 5;
    [SerializeField, Range(1, 5)] private int doorSafeRange = 3;

    [Header("Prefabs")]
    [SerializeField] private GameObject walkableFloorPrefab;
    [SerializeField] private GameObject unwalkableFloorPrefab;
    [SerializeField] private GameObject transparentFloorPrefab;
    [SerializeField] private GameObject exitDoorPrefab;
    [SerializeField] private GameObject treasureChestPrefab;
    [SerializeField] private List<GameObject> monsterPrefabs = new List<GameObject>();
    [SerializeField] private List<GameObject> bossRoomPrefabs = new List<GameObject>();

    [Header("Boss Settings")]
    [SerializeField] private List<GameObject> bossPrefabs = new List<GameObject>();  // ⭐ Boss预制体列表
    [SerializeField] private float bossSpawnDistance = 10f;                          // Boss生成距离
    [SerializeField] private GameObject bossExitDoorPrefab;                          // Boss击败后生成的传送门

    [Header("References")]
    [SerializeField] private RoomMapSystem roomMapSystem;
    [SerializeField] private Transform playerTransform;

    private Floor[,] floorGrid;
    private Vector3 playerSpawnPosition;
    private List<ExitDoor> exitDoors = new List<ExitDoor>();
    private GameObject currentRoomContainer;
    private int currentRoomId = -1;
    private bool hasCheckedPlayer = false;
    private bool isInitialized = false;
    private QuadTreeNode rootNode;

    // ⭐ Boss相关
    private GameObject currentBoss;           // 当前Boss实例
    private MonsterHealth currentBossHealth;  // Boss血量组件
    private bool isBossRoom = false;          // 是否是Boss房间
    private bool hasBossBeenDefeated = false; // Boss是否已被击败

    public Floor[,] FloorGrid => floorGrid;
    public Vector2Int RoomSize => roomSize;
    public float TileSize => tileSize;
    public Vector3 PlayerSpawnPosition => playerSpawnPosition;

    void Start()
    {
        if (roomMapSystem == null)
        {
            Debug.LogError("RoomMapSystem reference is missing!");
            return;
        }
    }

    void Update()
    {
        if (!isInitialized && roomMapSystem != null)
        {
            var connections = roomMapSystem.GetCurrentRoomConnections();
            if (connections != null && connections.Count > 0)
            {
                isInitialized = true;
                GenerateRoom();
            }
        }

        if (isInitialized && !hasCheckedPlayer && playerTransform != null && exitDoors.Count > 0)
        {
            CheckPlayerAtDoor();
        }

        // ⭐ 监控Boss血量
        if (isBossRoom && currentBoss != null && !hasBossBeenDefeated)
        {
            MonitorBossHealth();
        }
    }

    /// <summary>
    /// ⭐ 公共方法：重置并重新生成房间（用于新一轮）
    /// </summary>
    public void ResetAndRegenerateRoom()
    {
        Debug.Log("<color=yellow>RoomGenerator: 重置状态并重新生成房间</color>");

        // 重置初始化状态
        isInitialized = false;
        currentRoomId = -1;
        hasCheckedPlayer = false;

        // 清空当前房间
        ClearCurrentRoom();

        // 强制触发生成
        if (roomMapSystem != null)
        {
            var connections = roomMapSystem.GetCurrentRoomConnections();
            if (connections != null && connections.Count > 0)
            {
                isInitialized = true;
                GenerateRoom();
            }
            else
            {
                Debug.LogWarning("新一轮地图尚未准备好连接信息！");
            }
        }
    }

    /// <summary>
    /// ⭐ 监控Boss血量
    /// </summary>
    void MonitorBossHealth()
    {
        if (currentBossHealth == null)
        {
            currentBossHealth = currentBoss.GetComponent<MonsterHealth>();
            if (currentBossHealth == null)
            {
                Debug.LogWarning("Boss没有MonsterHealth组件！");
                return;
            }
        }

        // ⭐ 修正：检查Boss的CurrentHealth是否<=0
        if (currentBossHealth.CurrentHealth <= 0)
        {
            Debug.Log("<color=yellow>检测到Boss血量<=0，准备生成传送门...</color>");
            OnBossDefeated();
        }
    }

    /// <summary>
    /// ⭐ Boss被击败时调用
    /// </summary>
    void OnBossDefeated()
    {
        if (hasBossBeenDefeated) return;
        hasBossBeenDefeated = true;

        Debug.Log("<color=yellow>========== Boss被击败！==========</color>");

        // 获取Boss位置（如果Boss已被销毁，使用房间中心）
        Vector3 doorPosition;
        if (currentBoss != null)
        {
            doorPosition = currentBoss.transform.position;
        }
        else
        {
            doorPosition = new Vector3(roomSize.x * tileSize / 2f, 0, roomSize.y * tileSize / 2f);
        }

        // 生成传送门
        SpawnBossExitDoor(doorPosition);
    }

    /// <summary>
    /// ⭐ 生成Boss传送门
    /// </summary>
    void SpawnBossExitDoor(Vector3 position)
    {
        if (bossExitDoorPrefab == null)
        {
            Debug.LogError("Boss传送门预制体未设置！请在Inspector中设置 bossExitDoorPrefab");
            return;
        }

        // 确保位置在地面上
        position.y = 0;

        GameObject door = Instantiate(bossExitDoorPrefab, position, Quaternion.identity, currentRoomContainer.transform);
        door.name = "BossExitDoor";

        // ⭐ 使用公共方法设置 RoomMapSystem 引用
        BossExitDoor doorScript = door.GetComponent<BossExitDoor>();
        if (doorScript != null && roomMapSystem != null)
        {
            doorScript.SetRoomMapSystem(roomMapSystem);
        }
        else
        {
            Debug.LogError("无法设置BossExitDoor的RoomMapSystem引用！");
        }

        Debug.Log($"<color=cyan>Boss传送门已生成在: {position}</color>");
    }

    /// <summary>
    /// 生成房间 - 添加难度系统和强化选择
    /// </summary>
    void GenerateRoom()
    {
        // ✨ 增加难度（每次进入新房间时）
        if (DifficultyManager.Instance != null)
        {
            if (currentRoomId >= 0)
            {
                DifficultyManager.Instance.IncreaseDifficulty();
                Debug.Log($"<color=cyan>通过门进入新房间，难度提升！{DifficultyManager.Instance.GetDifficultyInfo()}</color>");
            }
            else
            {
                Debug.Log($"<color=cyan>起始房间（第1个房间），当前难度: 0</color>");
            }
        }

        ClearCurrentRoom();
        currentRoomContainer = new GameObject($"Room_{currentRoomId}");
        currentRoomId++;

        var connectedRooms = roomMapSystem.GetCurrentRoomConnections();
        var currentRoom = roomMapSystem.GetCurrentRoom();

        Debug.Log($"=== 生成新房间 - 类型: {currentRoom.type}, 连接数: {connectedRooms.Count} ===");

        if (currentRoom.type == RoomType.Boss)
        {
            GenerateBossRoom();
            return;
        }

        if (connectedRooms.Count == 0)
        {
            Debug.LogWarning("没有连接房间");
            playerSpawnPosition = new Vector3(roomSize.x * tileSize / 2f, 0, roomSize.y * tileSize / 2f);
            TeleportPlayer();
            return;
        }

        GenerateSpawnAndExitPositions(connectedRooms);
        GenerateFloorWithQuadTree();
        EnsurePathsReachable();
        GenerateBoundaryWalls();
        EnsureSpawnAndDoorsWalkable();
        InstantiateFloors();
        GenerateMonsters();

        if (currentRoom.type == RoomType.Treasure)
        {
            GenerateTreasureChest();
        }

        TeleportPlayer();

        hasCheckedPlayer = false;
        Debug.Log($"房间生成完成 - Walkable: {CountFloorType(FloorType.Walkable)}, " +
                  $"Unwalkable: {CountFloorType(FloorType.Unwalkable)}, " +
                  $"Transparent: {CountFloorType(FloorType.UnwalkableTransparent)}");

        // ⭐ 新增：触发强化选择UI
        if (EnhancementSelectionUI.Instance != null)
        {
            // 延迟0.5秒后显示强化选择面板，确保玩家传送完成
            StartCoroutine(ShowEnhancementSelectionDelayed());
        }
        else
        {
            Debug.LogWarning("EnhancementSelectionUI 不存在！");
        }
    }

    /// <summary>
    /// 延迟显示强化选择面板
    /// </summary>
    System.Collections.IEnumerator ShowEnhancementSelectionDelayed()
    {
        yield return new WaitForSecondsRealtime(0.3f); // 使用Realtime因为可能会暂停

        if (EnhancementSelectionUI.Instance != null)
        {
            EnhancementSelectionUI.Instance.ShowSelectionPanel();
        }
    }

    void TeleportPlayer()
    {
        if (playerTransform == null)
        {
            Debug.LogError("PlayerTransform 引用为空！请在Inspector中设置Player Transform引用。");
            return;
        }

        Vector3 newPosition = new Vector3(playerSpawnPosition.x, playerTransform.position.y, playerSpawnPosition.z);

        Debug.Log($"<color=yellow>准备传送玩家: {playerTransform.name}</color>");
        Debug.Log($"<color=yellow>传送前位置: {playerTransform.position}</color>");
        Debug.Log($"<color=yellow>目标位置: {newPosition}</color>");

        CharacterController characterController = playerTransform.GetComponent<CharacterController>();
        if (characterController != null)
        {
            Debug.Log("<color=green>检测到CharacterController，禁用后传送</color>");
            characterController.enabled = false;
            playerTransform.position = newPosition;
            characterController.enabled = true;
        }
        else if (playerTransform.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            Debug.Log("<color=green>检测到Rigidbody，使用MovePosition传送</color>");
            rb.MovePosition(newPosition);
        }
        else
        {
            Debug.Log("<color=green>直接修改Transform位置</color>");
            playerTransform.position = newPosition;
        }

        StartCoroutine(VerifyPlayerPosition(newPosition));
    }

    System.Collections.IEnumerator VerifyPlayerPosition(Vector3 targetPosition)
    {
        yield return new WaitForEndOfFrame();

        if (playerTransform != null)
        {
            float distance = Vector3.Distance(playerTransform.position, targetPosition);

            if (distance < 0.1f)
            {
                Debug.Log($"<color=cyan>✓ 玩家成功传送到: {playerTransform.position}</color>");
            }
            else
            {
                Debug.LogWarning($"<color=orange>✗ 玩家传送失败！当前位置: {playerTransform.position}, 目标位置: {targetPosition}, 距离: {distance}</color>");
                Debug.LogWarning($"<color=orange>玩家身上的组件: </color>");

                foreach (var component in playerTransform.GetComponents<Component>())
                {
                    Debug.LogWarning($"  - {component.GetType().Name}");
                }

                Debug.LogWarning("<color=orange>尝试强制传送...</color>");
                CharacterController cc = playerTransform.GetComponent<CharacterController>();
                if (cc != null)
                {
                    cc.enabled = false;
                }
                playerTransform.position = targetPosition;
                if (cc != null)
                {
                    cc.enabled = true;
                }
            }
        }
    }

    void GenerateBossRoom()
    {
        Debug.Log($"<color=red>=== 生成Boss房间（第 {roomMapSystem.CurrentRound}/{roomMapSystem.MaxRounds} 轮）===</color>");

        // ⭐ 标记为Boss房间
        isBossRoom = true;
        hasBossBeenDefeated = false;
        currentBoss = null;
        currentBossHealth = null;

        // 1. 生成Boss房间场景（可选）
        if (bossRoomPrefabs.Count > 0)
        {
            GameObject selectedPrefab = bossRoomPrefabs[Random.Range(0, bossRoomPrefabs.Count)];
            GameObject bossRoom = Instantiate(selectedPrefab, Vector3.zero, Quaternion.identity, currentRoomContainer.transform);
            bossRoom.name = "BossRoom";
            Debug.Log($"已生成Boss房间: {selectedPrefab.name}");
        }

        // 2. 设置玩家出生点
        playerSpawnPosition = new Vector3(roomSize.x * tileSize * 0.2f, 0, roomSize.y * tileSize / 2f);

        // 检查是否有自定义出生点
        Transform spawnPoint = GameObject.Find("PlayerSpawn")?.transform;
        if (spawnPoint != null)
        {
            playerSpawnPosition = spawnPoint.position;
            Debug.Log($"使用Boss房间中的PlayerSpawn位置: {playerSpawnPosition}");
        }

        TeleportPlayer();

        // 3. ⭐ 延迟生成Boss（等玩家传送完成）
        StartCoroutine(SpawnBossAfterDelay());

        hasCheckedPlayer = false;
    }

    /// <summary>
    /// ⭐ 延迟生成Boss
    /// </summary>
    System.Collections.IEnumerator SpawnBossAfterDelay()
    {
        yield return new WaitForSeconds(1f); // 等待1秒确保玩家传送完成

        if (bossPrefabs.Count == 0)
        {
            Debug.LogError("<color=red>Boss预制体列表为空！请在RoomGenerator的Inspector中添加Boss预制体</color>");
            yield break;
        }

        // 随机选择一个Boss
        GameObject selectedBossPrefab = bossPrefabs[Random.Range(0, bossPrefabs.Count)];

        // 计算Boss生成位置（在玩家周围一定距离）
        Vector3 bossSpawnPosition = CalculateBossSpawnPosition();

        // 生成Boss
        currentBoss = Instantiate(selectedBossPrefab, bossSpawnPosition, Quaternion.identity, currentRoomContainer.transform);
        currentBoss.name = $"Boss_Round{roomMapSystem.CurrentRound}";

        // 获取Boss的Health组件
        currentBossHealth = currentBoss.GetComponent<MonsterHealth>();
        if (currentBossHealth == null)
        {
            Debug.LogError($"<color=red>Boss预制体 {selectedBossPrefab.name} 没有MonsterHealth组件！</color>");
        }

        Debug.Log($"<color=green>✓ Boss已生成: {selectedBossPrefab.name} 在位置 {bossSpawnPosition}</color>");
    }

    /// <summary>
    /// ⭐ 计算Boss生成位置（在玩家周围，但保持一定距离）
    /// </summary>
    Vector3 CalculateBossSpawnPosition()
    {
        if (playerTransform == null)
        {
            // 如果没有玩家引用，在房间中心生成
            return new Vector3(roomSize.x * tileSize / 2f, 0, roomSize.y * tileSize / 2f);
        }

        // 在玩家周围随机方向生成
        Vector2 randomCircle = Random.insideUnitCircle.normalized * bossSpawnDistance;
        Vector3 spawnPos = playerTransform.position + new Vector3(randomCircle.x, 0, randomCircle.y);

        // 确保在房间范围内
        spawnPos.x = Mathf.Clamp(spawnPos.x, tileSize * 2, (roomSize.x - 2) * tileSize);
        spawnPos.z = Mathf.Clamp(spawnPos.z, tileSize * 2, (roomSize.y - 2) * tileSize);
        spawnPos.y = 0;

        return spawnPos;
    }

    void GenerateSpawnAndExitPositions(List<Room> connectedRooms)
    {
        exitDoors.Clear();

        float leftX = tileSize * 3;
        float[] spawnZOptions = new float[3];
        spawnZOptions[0] = roomSize.y * tileSize * 0.25f;
        spawnZOptions[1] = roomSize.y * tileSize * 0.5f;
        spawnZOptions[2] = roomSize.y * tileSize * 0.75f;
        float spawnZ = spawnZOptions[Random.Range(0, 3)];
        playerSpawnPosition = new Vector3(leftX, 0, spawnZ);

        Debug.Log($"玩家起点: X={leftX}, Z={spawnZ}");

        List<Room> sortedRooms = connectedRooms.OrderByDescending(r => r.row).ToList();

        Debug.Log($"<color=cyan>连接的房间顺序（按row降序排序，地图上→下）:</color>");
        foreach (var room in sortedRooms)
        {
            Debug.Log($"  房间{room.id} - 列{room.column}, 行{room.row}, 类型{room.type}");
        }

        float rightX = (roomSize.x - 3) * tileSize;

        float[] doorZPositions;
        if (sortedRooms.Count == 1)
        {
            doorZPositions = new float[] { roomSize.y * tileSize * 0.5f };
        }
        else if (sortedRooms.Count == 2)
        {
            doorZPositions = new float[] {
                roomSize.y * tileSize * 0.3f,
                roomSize.y * tileSize * 0.7f
            };
        }
        else
        {
            doorZPositions = new float[sortedRooms.Count];
            for (int i = 0; i < sortedRooms.Count; i++)
            {
                float ratio = sortedRooms.Count == 1 ? 0.5f : (float)i / (sortedRooms.Count - 1);
                doorZPositions[i] = roomSize.y * tileSize * (0.2f + ratio * 0.6f);
            }
        }

        for (int i = 0; i < sortedRooms.Count; i++)
        {
            Room targetRoom = sortedRooms[i];
            float doorZ = doorZPositions[i];
            Vector3 doorPos = new Vector3(rightX, 0, doorZ);

            int originalIndex = connectedRooms.IndexOf(targetRoom);

            Debug.Log($"<color=green>门 {i}: 位置(X={rightX}, Z={doorZ}) -> 房间{targetRoom.id}(列{targetRoom.column},行{targetRoom.row}) [原始索引:{originalIndex}]</color>");

            ExitDoor door = new ExitDoor
            {
                position = doorPos,
                connectedRoomIndex = originalIndex
            };

            if (exitDoorPrefab != null)
            {
                door.doorObject = Instantiate(exitDoorPrefab, doorPos, Quaternion.identity, currentRoomContainer.transform);
                door.doorObject.name = $"Door_{i}_ToRoom{targetRoom.id}_Row{targetRoom.row}";
                door.triggerCollider = door.doorObject.GetComponent<BoxCollider>();
                if (door.triggerCollider == null)
                {
                    door.triggerCollider = door.doorObject.AddComponent<BoxCollider>();
                }
                door.triggerCollider.isTrigger = true;
                door.triggerCollider.size = new Vector3(tileSize * 2, tileSize * 2, tileSize * 2);
            }

            exitDoors.Add(door);
        }
    }

    void GenerateFloorWithQuadTree()
    {
        floorGrid = new Floor[roomSize.x, roomSize.y];
        for (int x = 0; x < roomSize.x; x++)
        {
            for (int y = 0; y < roomSize.y; y++)
            {
                floorGrid[x, y] = new Floor
                {
                    type = FloorType.Walkable,
                    gridPosition = new Vector2Int(x, y),
                    worldPosition = new Vector3(x * tileSize, 0, y * tileSize)
                };
            }
        }

        rootNode = new QuadTreeNode(new Rect(0, 0, roomSize.x, roomSize.y));
        BuildQuadTree(rootNode, 0);

        ApplyQuadTreeToGrid(rootNode);

        AdjustFloorByDensity();
    }

    void BuildQuadTree(QuadTreeNode node, int depth)
    {
        if (depth >= maxDepth ||
            node.bounds.width < minRegionSize ||
            node.bounds.height < minRegionSize ||
            Random.value > splitChance)
        {
            node.isLeaf = true;
            node.isWalkable = Random.value > obstacleDensity;
            return;
        }

        node.isLeaf = false;
        node.children = new QuadTreeNode[4];

        float halfW = node.bounds.width / 2f;
        float halfH = node.bounds.height / 2f;

        node.children[0] = new QuadTreeNode(new Rect(node.bounds.x, node.bounds.y, halfW, halfH));
        node.children[1] = new QuadTreeNode(new Rect(node.bounds.x + halfW, node.bounds.y, halfW, halfH));
        node.children[2] = new QuadTreeNode(new Rect(node.bounds.x, node.bounds.y + halfH, halfW, halfH));
        node.children[3] = new QuadTreeNode(new Rect(node.bounds.x + halfW, node.bounds.y + halfH, halfW, halfH));

        foreach (var child in node.children)
        {
            BuildQuadTree(child, depth + 1);
        }
    }

    void ApplyQuadTreeToGrid(QuadTreeNode node)
    {
        if (node.isLeaf)
        {
            int startX = Mathf.FloorToInt(node.bounds.x);
            int startY = Mathf.FloorToInt(node.bounds.y);
            int endX = Mathf.CeilToInt(node.bounds.x + node.bounds.width);
            int endY = Mathf.CeilToInt(node.bounds.y + node.bounds.height);

            for (int x = startX; x < endX && x < roomSize.x; x++)
            {
                for (int y = startY; y < endY && y < roomSize.y; y++)
                {
                    if (node.isWalkable)
                    {
                        floorGrid[x, y].type = FloorType.Walkable;
                    }
                    else
                    {
                        floorGrid[x, y].type = FloorType.Unwalkable;
                    }
                }
            }
        }
        else
        {
            foreach (var child in node.children)
            {
                ApplyQuadTreeToGrid(child);
            }
        }
    }

    void AdjustFloorByDensity()
    {
        List<Vector2Int> obstacleTiles = new List<Vector2Int>();
        for (int x = 0; x < roomSize.x; x++)
        {
            for (int y = 0; y < roomSize.y; y++)
            {
                if (floorGrid[x, y].type == FloorType.Unwalkable)
                {
                    obstacleTiles.Add(new Vector2Int(x, y));
                }
            }
        }

        obstacleTiles = obstacleTiles.OrderBy(x => Random.value).ToList();

        int transparentCount = Mathf.RoundToInt(obstacleTiles.Count * transparentDensity);

        for (int i = 0; i < transparentCount && i < obstacleTiles.Count; i++)
        {
            Vector2Int pos = obstacleTiles[i];
            floorGrid[pos.x, pos.y].type = FloorType.UnwalkableTransparent;
        }

        Debug.Log($"障碍物总数: {obstacleTiles.Count}, 其中可穿透: {transparentCount}");
    }

    void EnsurePathsReachable()
    {
        Vector2Int spawnGrid = WorldToGrid(playerSpawnPosition);

        foreach (var door in exitDoors)
        {
            Vector2Int doorGrid = WorldToGrid(door.position);

            List<Vector2Int> path = FindPath(spawnGrid, doorGrid);

            if (path == null || path.Count == 0)
            {
                Debug.LogWarning($"无法找到到门的路径，强制创建路径");
                path = CreateForcedPath(spawnGrid, doorGrid);
            }

            foreach (var tile in path)
            {
                WidenPath(tile, pathWidth);
            }
        }

        AddScatteredObstacles();
    }

    void AddScatteredObstacles()
    {
        List<Vector2Int> walkableTiles = new List<Vector2Int>();
        Vector2Int spawnGrid = WorldToGrid(playerSpawnPosition);

        for (int x = 0; x < roomSize.x; x++)
        {
            for (int y = 0; y < roomSize.y; y++)
            {
                if (floorGrid[x, y].type != FloorType.Walkable) continue;

                Vector2Int pos = new Vector2Int(x, y);

                if (Vector2Int.Distance(pos, spawnGrid) < 3) continue;

                bool nearDoor = false;
                foreach (var door in exitDoors)
                {
                    Vector2Int doorGrid = WorldToGrid(door.position);
                    if (Vector2Int.Distance(pos, doorGrid) < 3)
                    {
                        nearDoor = true;
                        break;
                    }
                }
                if (nearDoor) continue;

                walkableTiles.Add(pos);
            }
        }

        int scatterCount = Mathf.RoundToInt(walkableTiles.Count * scatterObstacleDensity);
        walkableTiles = walkableTiles.OrderBy(x => Random.value).ToList();

        for (int i = 0; i < scatterCount && i < walkableTiles.Count; i++)
        {
            Vector2Int pos = walkableTiles[i];

            if (Random.value < 0.5f)
            {
                floorGrid[pos.x, pos.y].type = FloorType.Unwalkable;
            }
            else
            {
                floorGrid[pos.x, pos.y].type = FloorType.UnwalkableTransparent;
            }
        }

        Debug.Log($"添加了 {scatterCount} 个散落障碍物");
    }

    List<Vector2Int> FindPath(Vector2Int start, Vector2Int end)
    {
        List<PathNode> openList = new List<PathNode>();
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();

        PathNode startNode = new PathNode
        {
            position = start,
            gCost = 0,
            hCost = Vector2Int.Distance(start, end)
        };
        openList.Add(startNode);

        while (openList.Count > 0)
        {
            PathNode current = openList.OrderBy(n => n.fCost).First();

            if (current.position == end)
            {
                return ReconstructPath(current);
            }

            openList.Remove(current);
            closedSet.Add(current.position);

            foreach (Vector2Int neighbor in GetNeighbors(current.position))
            {
                if (closedSet.Contains(neighbor)) continue;
                if (!IsValidGrid(neighbor)) continue;
                if (floorGrid[neighbor.x, neighbor.y].type != FloorType.Walkable) continue;

                float tentativeG = current.gCost + Vector2Int.Distance(current.position, neighbor);

                PathNode neighborNode = openList.FirstOrDefault(n => n.position == neighbor);
                if (neighborNode == null)
                {
                    neighborNode = new PathNode
                    {
                        position = neighbor,
                        gCost = tentativeG,
                        hCost = Vector2Int.Distance(neighbor, end),
                        parent = current
                    };
                    openList.Add(neighborNode);
                }
                else if (tentativeG < neighborNode.gCost)
                {
                    neighborNode.gCost = tentativeG;
                    neighborNode.parent = current;
                }
            }
        }

        return null;
    }

    List<Vector2Int> ReconstructPath(PathNode endNode)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        PathNode current = endNode;

        while (current != null)
        {
            path.Add(current.position);
            current = current.parent;
        }

        path.Reverse();
        return path;
    }

    List<Vector2Int> CreateForcedPath(Vector2Int start, Vector2Int end)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        Vector2Int current = start;

        while (current != end)
        {
            path.Add(current);

            if (current.x != end.x)
            {
                current.x += (end.x > current.x) ? 1 : -1;
            }
            else if (current.y != end.y)
            {
                current.y += (end.y > current.y) ? 1 : -1;
            }
        }

        path.Add(end);
        return path;
    }

    void WidenPath(Vector2Int center, int width)
    {
        int radius = width / 2;
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                Vector2Int pos = center + new Vector2Int(dx, dy);
                if (IsValidGrid(pos))
                {
                    floorGrid[pos.x, pos.y].type = FloorType.Walkable;
                }
            }
        }
    }

    List<Vector2Int> GetNeighbors(Vector2Int pos)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();
        Vector2Int[] directions = {
            new Vector2Int(0, 1), new Vector2Int(1, 0),
            new Vector2Int(0, -1), new Vector2Int(-1, 0)
        };

        foreach (var dir in directions)
        {
            neighbors.Add(pos + dir);
        }

        return neighbors;
    }

    void GenerateBoundaryWalls()
    {
        for (int x = 0; x < roomSize.x; x++)
        {
            floorGrid[x, 0].type = FloorType.Unwalkable;
            floorGrid[x, roomSize.y - 1].type = FloorType.Unwalkable;
        }

        for (int y = 0; y < roomSize.y; y++)
        {
            floorGrid[0, y].type = FloorType.Unwalkable;
            floorGrid[roomSize.x - 1, y].type = FloorType.Unwalkable;
        }

        Debug.Log("已生成边界墙");
    }

    void EnsureSpawnAndDoorsWalkable()
    {
        Vector2Int spawnGrid = WorldToGrid(playerSpawnPosition);
        EnsureAreaWalkable(spawnGrid, 2);

        foreach (var door in exitDoors)
        {
            Vector2Int doorGrid = WorldToGrid(door.position);
            EnsureAreaWalkable(doorGrid, 2);
        }

        Debug.Log("已确保起点和门周围可通行");
    }

    void EnsureAreaWalkable(Vector2Int center, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                Vector2Int pos = center + new Vector2Int(dx, dy);

                if (pos.x == 0 || pos.x == roomSize.x - 1 ||
                    pos.y == 0 || pos.y == roomSize.y - 1)
                {
                    continue;
                }

                if (IsValidGrid(pos))
                {
                    floorGrid[pos.x, pos.y].type = FloorType.Walkable;
                }
            }
        }
    }

    void InstantiateFloors()
    {
        for (int x = 0; x < roomSize.x; x++)
        {
            for (int y = 0; y < roomSize.y; y++)
            {
                Floor floor = floorGrid[x, y];
                GameObject prefab = floor.type switch
                {
                    FloorType.Walkable => walkableFloorPrefab,
                    FloorType.Unwalkable => unwalkableFloorPrefab,
                    FloorType.UnwalkableTransparent => transparentFloorPrefab,
                    _ => null
                };

                if (prefab != null)
                {
                    floor.floorObject = Instantiate(prefab, floor.worldPosition, Quaternion.identity, currentRoomContainer.transform);
                    floor.floorObject.name = $"Floor_{x}_{y}";
                }
            }
        }
    }

    void GenerateTreasureChest()
    {
        if (treasureChestPrefab == null)
        {
            Debug.LogWarning("宝箱Prefab未设置，跳过宝箱生成");
            return;
        }

        Vector2Int spawnGrid = WorldToGrid(playerSpawnPosition);

        HashSet<Vector2Int> reachableTiles = FloodFillReachable(spawnGrid);

        List<Vector2Int> validPositions = new List<Vector2Int>();

        foreach (Vector2Int pos in reachableTiles)
        {
            if (Vector2Int.Distance(pos, spawnGrid) < playerSafeRange) continue;

            bool tooCloseToExit = false;
            foreach (var door in exitDoors)
            {
                Vector2Int doorGrid = WorldToGrid(door.position);
                if (Vector2Int.Distance(pos, doorGrid) < doorSafeRange)
                {
                    tooCloseToExit = true;
                    break;
                }
            }
            if (tooCloseToExit) continue;

            validPositions.Add(pos);
        }

        if (validPositions.Count > 0)
        {
            Vector2Int chestPos = validPositions[Random.Range(0, validPositions.Count)];
            Vector3 worldPos = floorGrid[chestPos.x, chestPos.y].worldPosition;

            GameObject chest = Instantiate(treasureChestPrefab, worldPos, Quaternion.identity, currentRoomContainer.transform);
            chest.name = "TreasureChest";

            Debug.Log($"<color=yellow>宝箱生成在: ({chestPos.x}, {chestPos.y}), 可达格子数: {reachableTiles.Count}, 有效位置数: {validPositions.Count}</color>");
        }
        else
        {
            Debug.LogWarning("没有找到合适的宝箱生成位置！");
        }
    }

    HashSet<Vector2Int> FloodFillReachable(Vector2Int start)
    {
        HashSet<Vector2Int> reachable = new HashSet<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        queue.Enqueue(start);
        reachable.Add(start);

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            foreach (Vector2Int neighbor in GetNeighbors(current))
            {
                if (reachable.Contains(neighbor)) continue;
                if (!IsValidGrid(neighbor)) continue;
                if (floorGrid[neighbor.x, neighbor.y].type != FloorType.Walkable) continue;

                reachable.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        return reachable;
    }

    void GenerateMonsters()
    {
        if (monsterPrefabs.Count == 0) return;

        Vector2Int spawnGrid = WorldToGrid(playerSpawnPosition);
        int monstersSpawned = 0;

        for (int x = 0; x < roomSize.x; x++)
        {
            for (int y = 0; y < roomSize.y; y++)
            {
                if (floorGrid[x, y].type != FloorType.Walkable) continue;

                Vector2Int currentGrid = new Vector2Int(x, y);

                if (Vector2Int.Distance(currentGrid, spawnGrid) < playerSafeRange)
                {
                    continue;
                }

                bool tooCloseToExit = false;
                foreach (var door in exitDoors)
                {
                    Vector2Int doorGrid = WorldToGrid(door.position);
                    if (Vector2Int.Distance(currentGrid, doorGrid) < doorSafeRange)
                    {
                        tooCloseToExit = true;
                        break;
                    }
                }
                if (tooCloseToExit) continue;

                if (Random.value < monsterSpawnChance)
                {
                    Vector3 worldPos = floorGrid[x, y].worldPosition;
                    GameObject prefab = monsterPrefabs[Random.Range(0, monsterPrefabs.Count)];
                    Instantiate(prefab, worldPos, Quaternion.identity, currentRoomContainer.transform);
                    monstersSpawned++;
                }
            }
        }

        Debug.Log($"生成了 {monstersSpawned} 个怪物（玩家安全范围: {playerSafeRange}格, 门安全范围: {doorSafeRange}格）");
    }

    void CheckPlayerAtDoor()
    {
        if (playerTransform == null) return;

        for (int i = 0; i < exitDoors.Count; i++)
        {
            Vector3 playerXZ = new Vector3(playerTransform.position.x, 0, playerTransform.position.z);
            Vector3 doorXZ = new Vector3(exitDoors[i].position.x, 0, exitDoors[i].position.z);

            if (Vector3.Distance(playerXZ, doorXZ) < tileSize * doorDetectionRange)
            {
                Debug.Log($"玩家进入门 {i}");
                roomMapSystem.MoveToConnectedRoom(exitDoors[i].connectedRoomIndex);
                GenerateRoom();
                return;
            }
        }
    }

    void ClearCurrentRoom()
    {
        if (currentRoomContainer != null) Destroy(currentRoomContainer);
        floorGrid = null;
        exitDoors.Clear();

        // ⭐ 清除Boss相关状态
        isBossRoom = false;
        hasBossBeenDefeated = false;
        currentBoss = null;
        currentBossHealth = null;
    }

    Vector2Int WorldToGrid(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.Clamp(Mathf.RoundToInt(worldPos.x / tileSize), 0, roomSize.x - 1),
            Mathf.Clamp(Mathf.RoundToInt(worldPos.z / tileSize), 0, roomSize.y - 1)
        );
    }

    bool IsValidGrid(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < roomSize.x && pos.y >= 0 && pos.y < roomSize.y;
    }

    int CountFloorType(FloorType type)
    {
        int count = 0;
        for (int x = 0; x < roomSize.x; x++)
            for (int y = 0; y < roomSize.y; y++)
                if (floorGrid[x, y].type == type) count++;
        return count;
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || floorGrid == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(playerSpawnPosition, tileSize * 0.5f);

        Gizmos.color = Color.red;
        foreach (var door in exitDoors)
        {
            Gizmos.DrawWireCube(door.position, new Vector3(tileSize, tileSize * 0.5f, tileSize));
        }
    }
}