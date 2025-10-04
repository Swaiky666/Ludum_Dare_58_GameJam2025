using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// 地板类型枚举
/// </summary>
public enum FloorType
{
    Walkable,              // 可通行地板
    Unwalkable,            // 不可通行且不可穿透
    UnwalkableTransparent  // 不可通行但可穿透
}

/// <summary>
/// 地板数据
/// </summary>
public class Floor
{
    public FloorType type;
    public Vector2Int gridPosition;
    public Vector3 worldPosition;
    public GameObject floorObject;
}

/// <summary>
/// 出口门数据
/// </summary>
public class ExitDoor
{
    public Vector3 position;
    public int connectedRoomIndex;
    public GameObject doorObject;
    public BoxCollider triggerCollider;
}

/// <summary>
/// 四叉树节点
/// </summary>
public class QuadTreeNode
{
    public Rect bounds;                    // 节点边界
    public QuadTreeNode[] children;        // 四个子节点
    public bool isLeaf;                    // 是否为叶子节点
    public bool isWalkable;                // 如果是叶子节点，是否可通行

    public QuadTreeNode(Rect bounds)
    {
        this.bounds = bounds;
        this.isLeaf = true;
        this.isWalkable = true;
        this.children = null;
    }
}

/// <summary>
/// A*寻路节点
/// </summary>
public class PathNode
{
    public Vector2Int position;
    public float gCost;  // 从起点到当前节点的代价
    public float hCost;  // 从当前节点到终点的估计代价
    public float fCost => gCost + hCost;
    public PathNode parent;
}

/// <summary>
/// 房间地图生成器（基于四叉树）
/// </summary>
public class RoomGenerator : MonoBehaviour
{
    [Header("Room Settings")]
    [SerializeField] private Vector2Int roomSize = new Vector2Int(30, 20);
    [SerializeField] private float tileSize = 1f;

    [Header("Quadtree Settings")]
    [SerializeField, Range(1, 6)] private int maxDepth = 4;           // 四叉树最大深度
    [SerializeField, Range(0, 1)] private float splitChance = 0.7f;   // 分割概率
    [SerializeField, Range(2, 10)] private int minRegionSize = 3;     // 最小区域大小

    [Header("Density Settings")]
    [SerializeField, Range(0, 1)] private float obstacleDensity = 0.4f;           // 障碍物密度
    [SerializeField, Range(0, 1)] private float transparentDensity = 0.3f;        // 可穿透障碍物占障碍物的比例
    [SerializeField, Range(0, 0.3f)] private float scatterObstacleDensity = 0.05f; // 散落障碍物密度（在可通行区域）

    [Header("Path Settings")]
    [SerializeField, Range(1, 5)] private int pathWidth = 2;          // 路径宽度

    [Header("Detection Settings")]
    [SerializeField, Range(0.5f, 3f)] private float doorDetectionRange = 1.0f;  // 门的检测范围（倍数于tileSize）

    [Header("Monster Settings")]
    [SerializeField, Range(0, 1)] private float monsterSpawnChance = 0.05f;
    [SerializeField, Range(1, 10)] private int playerSafeRange = 5;            // 玩家起点安全范围（格子数）
    [SerializeField, Range(1, 5)] private int doorSafeRange = 3;              // 门周围安全范围（格子数）

    [Header("Prefabs")]
    [SerializeField] private GameObject walkableFloorPrefab;
    [SerializeField] private GameObject unwalkableFloorPrefab;
    [SerializeField] private GameObject transparentFloorPrefab;
    [SerializeField] private GameObject exitDoorPrefab;
    [SerializeField] private GameObject treasureChestPrefab;              // 宝箱预制体
    [SerializeField] private List<GameObject> monsterPrefabs = new List<GameObject>();
    [SerializeField] private List<GameObject> bossRoomPrefabs = new List<GameObject>(); // Boss房间预制体列表

    [Header("References")]
    [SerializeField] private RoomMapSystem roomMapSystem;
    [SerializeField] private Transform playerTransform;

    // 私有数据
    private Floor[,] floorGrid;
    private Vector3 playerSpawnPosition;
    private List<ExitDoor> exitDoors = new List<ExitDoor>();
    private GameObject currentRoomContainer;
    private int currentRoomId = -1;
    private bool hasCheckedPlayer = false;
    private bool isInitialized = false;
    private QuadTreeNode rootNode;

    // 公共访问器（用于小地图）
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
        // 等待RoomMapSystem初始化
        if (!isInitialized && roomMapSystem != null)
        {
            var connections = roomMapSystem.GetCurrentRoomConnections();
            if (connections != null && connections.Count > 0)
            {
                isInitialized = true;
                GenerateRoom();
            }
        }

        // 检测玩家进入门
        if (isInitialized && !hasCheckedPlayer && playerTransform != null && exitDoors.Count > 0)
        {
            CheckPlayerAtDoor();
        }
    }

    /// <summary>
    /// 生成房间
    /// </summary>
    void GenerateRoom()
    {
        ClearCurrentRoom();
        currentRoomContainer = new GameObject($"Room_{currentRoomId}");
        currentRoomId++;

        var connectedRooms = roomMapSystem.GetCurrentRoomConnections();
        var currentRoom = roomMapSystem.GetCurrentRoom();

        Debug.Log($"=== 生成新房间 - 类型: {currentRoom.type}, 连接数: {connectedRooms.Count} ===");

        // 检查是否是Boss房间
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

        // 1. 生成起点和门（传入连接的房间列表以保证顺序对应）
        GenerateSpawnAndExitPositions(connectedRooms);

        // 2. 使用四叉树生成地板
        GenerateFloorWithQuadTree();

        // 3. 确保路径可达
        EnsurePathsReachable();

        // 4. 在最外圈生成边界墙
        GenerateBoundaryWalls();

        // 5. 确保玩家起点和门的位置可通行
        EnsureSpawnAndDoorsWalkable();

        // 6. 实例化地板
        InstantiateFloors();

        // 7. 生成怪物
        GenerateMonsters();

        // 8. 如果是宝箱房，生成宝箱
        if (currentRoom.type == RoomType.Treasure)
        {
            GenerateTreasureChest();
        }

        // 9. 传送玩家
        TeleportPlayer();

        hasCheckedPlayer = false;
        Debug.Log($"房间生成完成 - Walkable: {CountFloorType(FloorType.Walkable)}, " +
                  $"Unwalkable: {CountFloorType(FloorType.Unwalkable)}, " +
                  $"Transparent: {CountFloorType(FloorType.UnwalkableTransparent)}");
    }

    /// <summary>
    /// 传送玩家到起点
    /// </summary>
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

        // 检查是否有CharacterController
        CharacterController characterController = playerTransform.GetComponent<CharacterController>();
        if (characterController != null)
        {
            Debug.Log("<color=green>检测到CharacterController，禁用后传送</color>");
            characterController.enabled = false;
            playerTransform.position = newPosition;
            characterController.enabled = true;
        }
        // 检查是否有Rigidbody
        else if (playerTransform.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            Debug.Log("<color=green>检测到Rigidbody，使用MovePosition传送</color>");
            rb.MovePosition(newPosition);
        }
        // 直接修改Transform
        else
        {
            Debug.Log("<color=green>直接修改Transform位置</color>");
            playerTransform.position = newPosition;
        }

        // 使用Coroutine延迟验证位置
        StartCoroutine(VerifyPlayerPosition(newPosition));
    }

    /// <summary>
    /// 验证玩家是否成功传送
    /// </summary>
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

                // 尝试强制传送
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

    /// <summary>
    /// 生成Boss房间（从prefab列表随机选择）
    /// </summary>
    void GenerateBossRoom()
    {
        Debug.Log("=== 生成Boss房间 ===");

        if (bossRoomPrefabs.Count == 0)
        {
            Debug.LogError("Boss房间Prefab列表为空！");
            // 如果没有prefab，生成一个简单的空房间
            playerSpawnPosition = new Vector3(roomSize.x * tileSize / 2f, 0, roomSize.y * tileSize / 2f);
            TeleportPlayer();
            return;
        }

        // 随机选择一个Boss房间prefab
        GameObject selectedPrefab = bossRoomPrefabs[Random.Range(0, bossRoomPrefabs.Count)];

        // 实例化Boss房间
        GameObject bossRoom = Instantiate(selectedPrefab, Vector3.zero, Quaternion.identity, currentRoomContainer.transform);
        bossRoom.name = "BossRoom";

        Debug.Log($"已生成Boss房间: {selectedPrefab.name}");

        // 在Boss房间中心生成玩家
        playerSpawnPosition = new Vector3(roomSize.x * tileSize * 0.2f, 0, roomSize.y * tileSize / 2f);

        // 尝试在Boss房间预制体中找到标记为"PlayerSpawn"的物体
        Transform spawnPoint = bossRoom.transform.Find("PlayerSpawn");
        if (spawnPoint != null)
        {
            playerSpawnPosition = spawnPoint.position;
            Debug.Log($"使用Boss房间中的PlayerSpawn位置: {playerSpawnPosition}");
        }

        TeleportPlayer();
        hasCheckedPlayer = false;
    }

    /// <summary>
    /// 生成起点和门（根据房间顺序对应）
    /// </summary>
    void GenerateSpawnAndExitPositions(List<Room> connectedRooms)
    {
        exitDoors.Clear();

        // 起点：X轴左边（小），Z随机，避开边界
        float leftX = tileSize * 3;
        float[] spawnZOptions = new float[3];
        spawnZOptions[0] = roomSize.y * tileSize * 0.25f;  // 下方
        spawnZOptions[1] = roomSize.y * tileSize * 0.5f;   // 中间
        spawnZOptions[2] = roomSize.y * tileSize * 0.75f;  // 上方
        float spawnZ = spawnZOptions[Random.Range(0, 3)];
        playerSpawnPosition = new Vector3(leftX, 0, spawnZ);

        Debug.Log($"玩家起点: X={leftX}, Z={spawnZ}");

        // 根据房间的row排序（从大到小，对应从上到下）
        // row小的房间在地图上方，应该对应Z坐标大（场景上方）
        List<Room> sortedRooms = connectedRooms.OrderByDescending(r => r.row).ToList();

        Debug.Log($"<color=cyan>连接的房间顺序（按row降序排序，地图上→下）:</color>");
        foreach (var room in sortedRooms)
        {
            Debug.Log($"  房间{room.id} - 列{room.column}, 行{room.row}, 类型{room.type}");
        }

        // 门：X轴右边（大），根据房间数量和顺序分配Z位置
        float rightX = (roomSize.x - 3) * tileSize;

        // 根据房间数量计算门的Z位置（从上到下）
        float[] doorZPositions;
        if (sortedRooms.Count == 1)
        {
            // 只有1个房间，门在中间
            doorZPositions = new float[] { roomSize.y * tileSize * 0.5f };
        }
        else if (sortedRooms.Count == 2)
        {
            // 2个房间，上下分布
            doorZPositions = new float[] {
                roomSize.y * tileSize * 0.3f,   // 下方（Z小）
                roomSize.y * tileSize * 0.7f    // 上方（Z大）
            };
        }
        else
        {
            // 3个或更多房间，从下到上均匀分布
            doorZPositions = new float[sortedRooms.Count];
            for (int i = 0; i < sortedRooms.Count; i++)
            {
                // i=0时在下方（Z小），i增加时往上（Z大）
                float ratio = sortedRooms.Count == 1 ? 0.5f : (float)i / (sortedRooms.Count - 1);
                doorZPositions[i] = roomSize.y * tileSize * (0.2f + ratio * 0.6f);
            }
        }

        // 按顺序生成门，门的索引对应到房间在列表中的原始索引
        for (int i = 0; i < sortedRooms.Count; i++)
        {
            Room targetRoom = sortedRooms[i];
            float doorZ = doorZPositions[i];
            Vector3 doorPos = new Vector3(rightX, 0, doorZ);

            // 找到这个房间在原始connectedRooms列表中的索引
            int originalIndex = connectedRooms.IndexOf(targetRoom);

            Debug.Log($"<color=green>门 {i}: 位置(X={rightX}, Z={doorZ}) -> 房间{targetRoom.id}(列{targetRoom.column},行{targetRoom.row}) [原始索引:{originalIndex}]</color>");

            ExitDoor door = new ExitDoor
            {
                position = doorPos,
                connectedRoomIndex = originalIndex  // 使用原始索引
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

    /// <summary>
    /// 使用四叉树生成地板
    /// </summary>
    void GenerateFloorWithQuadTree()
    {
        // 初始化网格
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

        // 创建四叉树
        rootNode = new QuadTreeNode(new Rect(0, 0, roomSize.x, roomSize.y));
        BuildQuadTree(rootNode, 0);

        // 根据四叉树设置地板类型
        ApplyQuadTreeToGrid(rootNode);

        // 根据密度调整障碍物类型
        AdjustFloorByDensity();
    }

    /// <summary>
    /// 递归构建四叉树
    /// </summary>
    void BuildQuadTree(QuadTreeNode node, int depth)
    {
        // 停止条件
        if (depth >= maxDepth ||
            node.bounds.width < minRegionSize ||
            node.bounds.height < minRegionSize ||
            Random.value > splitChance)
        {
            node.isLeaf = true;
            // 根据障碍物密度决定是否可通行
            node.isWalkable = Random.value > obstacleDensity;
            return;
        }

        // 分割成四个子节点
        node.isLeaf = false;
        node.children = new QuadTreeNode[4];

        float halfW = node.bounds.width / 2f;
        float halfH = node.bounds.height / 2f;

        node.children[0] = new QuadTreeNode(new Rect(node.bounds.x, node.bounds.y, halfW, halfH));
        node.children[1] = new QuadTreeNode(new Rect(node.bounds.x + halfW, node.bounds.y, halfW, halfH));
        node.children[2] = new QuadTreeNode(new Rect(node.bounds.x, node.bounds.y + halfH, halfW, halfH));
        node.children[3] = new QuadTreeNode(new Rect(node.bounds.x + halfW, node.bounds.y + halfH, halfW, halfH));

        // 递归构建子节点
        foreach (var child in node.children)
        {
            BuildQuadTree(child, depth + 1);
        }
    }

    /// <summary>
    /// 将四叉树应用到网格
    /// </summary>
    void ApplyQuadTreeToGrid(QuadTreeNode node)
    {
        if (node.isLeaf)
        {
            // 填充这个区域
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
            // 递归处理子节点
            foreach (var child in node.children)
            {
                ApplyQuadTreeToGrid(child);
            }
        }
    }

    /// <summary>
    /// 根据密度调整障碍物类型
    /// </summary>
    void AdjustFloorByDensity()
    {
        // 收集所有障碍物格子
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

        // 随机打乱
        obstacleTiles = obstacleTiles.OrderBy(x => Random.value).ToList();

        // 根据透明密度设置可穿透障碍物
        int transparentCount = Mathf.RoundToInt(obstacleTiles.Count * transparentDensity);

        for (int i = 0; i < transparentCount && i < obstacleTiles.Count; i++)
        {
            Vector2Int pos = obstacleTiles[i];
            floorGrid[pos.x, pos.y].type = FloorType.UnwalkableTransparent;
        }

        Debug.Log($"障碍物总数: {obstacleTiles.Count}, 其中可穿透: {transparentCount}");
    }

    /// <summary>
    /// 确保路径可达（使用A*寻路）
    /// </summary>
    void EnsurePathsReachable()
    {
        Vector2Int spawnGrid = WorldToGrid(playerSpawnPosition);

        foreach (var door in exitDoors)
        {
            Vector2Int doorGrid = WorldToGrid(door.position);

            // 使用A*寻找路径
            List<Vector2Int> path = FindPath(spawnGrid, doorGrid);

            if (path == null || path.Count == 0)
            {
                Debug.LogWarning($"无法找到到门的路径，强制创建路径");
                path = CreateForcedPath(spawnGrid, doorGrid);
            }

            // 将路径设为可通行，并加宽
            foreach (var tile in path)
            {
                WidenPath(tile, pathWidth);
            }
        }

        // 在可通行区域添加散落的障碍物
        AddScatteredObstacles();
    }

    /// <summary>
    /// 在可通行区域添加散落的障碍物
    /// </summary>
    void AddScatteredObstacles()
    {
        List<Vector2Int> walkableTiles = new List<Vector2Int>();
        Vector2Int spawnGrid = WorldToGrid(playerSpawnPosition);

        // 收集所有可通行且不在关键路径附近的格子
        for (int x = 0; x < roomSize.x; x++)
        {
            for (int y = 0; y < roomSize.y; y++)
            {
                if (floorGrid[x, y].type != FloorType.Walkable) continue;

                Vector2Int pos = new Vector2Int(x, y);

                // 不在起点附近
                if (Vector2Int.Distance(pos, spawnGrid) < 3) continue;

                // 不在门附近
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

        // 随机在这些格子上放置障碍物
        int scatterCount = Mathf.RoundToInt(walkableTiles.Count * scatterObstacleDensity);
        walkableTiles = walkableTiles.OrderBy(x => Random.value).ToList();

        for (int i = 0; i < scatterCount && i < walkableTiles.Count; i++)
        {
            Vector2Int pos = walkableTiles[i];

            // 50%概率是不可通行，50%是可穿透
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

    /// <summary>
    /// A*寻路算法
    /// </summary>
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
            // 找到fCost最小的节点
            PathNode current = openList.OrderBy(n => n.fCost).First();

            if (current.position == end)
            {
                return ReconstructPath(current);
            }

            openList.Remove(current);
            closedSet.Add(current.position);

            // 检查邻居
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

        return null; // 没找到路径
    }

    /// <summary>
    /// 重建路径
    /// </summary>
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

    /// <summary>
    /// 强制创建路径
    /// </summary>
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

    /// <summary>
    /// 加宽路径
    /// </summary>
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

    /// <summary>
    /// 获取邻居
    /// </summary>
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

    /// <summary>
    /// 在房间最外圈生成边界墙
    /// </summary>
    void GenerateBoundaryWalls()
    {
        // 上下边界
        for (int x = 0; x < roomSize.x; x++)
        {
            floorGrid[x, 0].type = FloorType.Unwalkable;
            floorGrid[x, roomSize.y - 1].type = FloorType.Unwalkable;
        }

        // 左右边界
        for (int y = 0; y < roomSize.y; y++)
        {
            floorGrid[0, y].type = FloorType.Unwalkable;
            floorGrid[roomSize.x - 1, y].type = FloorType.Unwalkable;
        }

        Debug.Log("已生成边界墙");
    }

    /// <summary>
    /// 确保玩家起点和门的位置周围可通行
    /// </summary>
    void EnsureSpawnAndDoorsWalkable()
    {
        // 确保玩家起点周围可通行
        Vector2Int spawnGrid = WorldToGrid(playerSpawnPosition);
        EnsureAreaWalkable(spawnGrid, 2); // 起点周围2格范围

        // 确保每个门周围可通行
        foreach (var door in exitDoors)
        {
            Vector2Int doorGrid = WorldToGrid(door.position);
            EnsureAreaWalkable(doorGrid, 2); // 门周围2格范围
        }

        Debug.Log("已确保起点和门周围可通行");
    }

    /// <summary>
    /// 确保指定位置周围一定区域是可通行的
    /// </summary>
    void EnsureAreaWalkable(Vector2Int center, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                Vector2Int pos = center + new Vector2Int(dx, dy);

                // 不要修改边界墙
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

    /// <summary>
    /// 生成宝箱（仅在宝箱房）- 优化版本使用Flood Fill
    /// </summary>
    void GenerateTreasureChest()
    {
        if (treasureChestPrefab == null)
        {
            Debug.LogWarning("宝箱Prefab未设置，跳过宝箱生成");
            return;
        }

        Vector2Int spawnGrid = WorldToGrid(playerSpawnPosition);

        // 使用Flood Fill一次性标记所有可达区域
        HashSet<Vector2Int> reachableTiles = FloodFillReachable(spawnGrid);

        List<Vector2Int> validPositions = new List<Vector2Int>();

        // 从可达区域中筛选有效位置
        foreach (Vector2Int pos in reachableTiles)
        {
            // 不在玩家起点附近
            if (Vector2Int.Distance(pos, spawnGrid) < playerSafeRange) continue;

            // 不在门附近
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

    /// <summary>
    /// Flood Fill算法：从起点标记所有可达的格子
    /// </summary>
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

    /// <summary>
    /// 生成怪物
    /// </summary>
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

                // 检查是否在玩家起点安全范围内
                if (Vector2Int.Distance(currentGrid, spawnGrid) < playerSafeRange)
                {
                    continue;
                }

                // 检查是否在门的安全范围内
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

                // 随机生成怪物
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

    /// <summary>
    /// 检测玩家进门
    /// </summary>
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