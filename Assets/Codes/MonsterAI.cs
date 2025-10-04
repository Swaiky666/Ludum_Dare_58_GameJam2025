using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 怪物状态枚举
/// </summary>
public enum MonsterState
{
    Patrol,   // 巡逻
    Chase     // 追击
}

/// <summary>
/// 怪物AI控制器
/// </summary>
public class MonsterAI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private RoomGenerator roomGenerator;

    [Header("Detection Settings")]
    [SerializeField] private float detectionRange = 10f;     // 检测范围
    [SerializeField] private float loseTargetRange = 15f;    // 失去目标范围

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 3f;           // 移动速度
    [SerializeField] private float rotationSpeed = 5f;       // 旋转速度
    [SerializeField] private float waypointReachDistance = 0.5f; // 到达路径点的距离

    [Header("Patrol Settings")]
    [SerializeField] private float minPatrolTime = 1f;       // 最小巡逻移动时间
    [SerializeField] private float maxPatrolTime = 3f;       // 最大巡逻移动时间
    [SerializeField] private float minWaitTime = 1f;         // 最小等待时间
    [SerializeField] private float maxWaitTime = 3f;         // 最大等待时间
    [SerializeField] private float patrolRadius = 5f;        // 巡逻半径

    [Header("Chase Settings")]
    [SerializeField] private float pathUpdateInterval = 0.5f; // 路径更新间隔
    [SerializeField] private float teleportDistance = 3f;     // 瞬移到玩家的距离
    [SerializeField] private int maxPathfindingAttempts = 3;  // 最大寻路尝试次数

    [Header("Attack Settings")]
    [SerializeField] private float attackRange = 2f;          // 攻击范围
    [SerializeField] private float attackDamage = 20f;        // 攻击伤害
    [SerializeField] private float attackCooldown = 1.5f;     // 攻击冷却时间
    [SerializeField] private float knockbackForce = 10f;      // 击退力度

    // 私有变量
    private MonsterState currentState = MonsterState.Patrol;
    private Vector3 patrolTarget;
    private float patrolTimer;
    private float waitTimer;
    private bool isWaiting;
    private List<Vector2Int> currentPath;
    private int currentPathIndex;
    private float pathUpdateTimer;
    private Vector3 lastKnownPlayerPosition;
    private int pathfindingFailCount;
    private CharacterController characterController;
    private Vector3 spawnPosition;
    private float attackCooldownTimer;
    private PlayerController targetPlayer;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            Debug.LogWarning($"{gameObject.name}: 没有CharacterController，添加一个");
            characterController = gameObject.AddComponent<CharacterController>();
            characterController.radius = 0.5f;
            characterController.height = 2f;
        }

        spawnPosition = transform.position;

        if (playerTransform == null)
        {
            playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        if (playerTransform != null)
        {
            targetPlayer = playerTransform.GetComponent<PlayerController>();
        }

        if (roomGenerator == null)
        {
            roomGenerator = FindObjectOfType<RoomGenerator>();
        }

        StartPatrol();
    }

    void Update()
    {
        // 检测玩家
        CheckPlayerDetection();

        // 更新攻击冷却
        if (attackCooldownTimer > 0)
        {
            attackCooldownTimer -= Time.deltaTime;
        }

        // 根据状态执行行为
        switch (currentState)
        {
            case MonsterState.Patrol:
                UpdatePatrol();
                break;
            case MonsterState.Chase:
                UpdateChase();
                break;
        }
    }

    /// <summary>
    /// 检测玩家（圆形范围）
    /// </summary>
    void CheckPlayerDetection()
    {
        if (playerTransform == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (currentState == MonsterState.Patrol)
        {
            // 巡逻状态：检测是否发现玩家
            if (distanceToPlayer <= detectionRange)
            {
                // 可选：检查是否有障碍物阻挡
                Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
                if (!Physics.Raycast(transform.position + Vector3.up, directionToPlayer, distanceToPlayer, LayerMask.GetMask("Obstacle")))
                {
                    SwitchToChase();
                }
            }
        }
        else if (currentState == MonsterState.Chase)
        {
            // 追击状态：检查是否失去目标
            if (distanceToPlayer > loseTargetRange)
            {
                SwitchToPatrol();
            }
        }
    }

    #region 巡逻状态

    /// <summary>
    /// 开始巡逻
    /// </summary>
    void StartPatrol()
    {
        isWaiting = true;
        waitTimer = Random.Range(minWaitTime, maxWaitTime);
    }

    /// <summary>
    /// 更新巡逻
    /// </summary>
    void UpdatePatrol()
    {
        if (isWaiting)
        {
            // 等待状态
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0)
            {
                // 等待结束，开始移动
                SetRandomPatrolTarget();
                isWaiting = false;
                patrolTimer = Random.Range(minPatrolTime, maxPatrolTime);
            }
        }
        else
        {
            // 移动状态
            patrolTimer -= Time.deltaTime;

            if (patrolTimer <= 0 || Vector3.Distance(transform.position, patrolTarget) < waypointReachDistance)
            {
                // 移动时间到或到达目标，开始等待
                isWaiting = true;
                waitTimer = Random.Range(minWaitTime, maxWaitTime);
            }
            else
            {
                // 继续移动
                MoveTowards(patrolTarget);
            }
        }
    }

    /// <summary>
    /// 设置随机巡逻目标
    /// </summary>
    void SetRandomPatrolTarget()
    {
        // 在巡逻半径内随机选择一个可通行的点
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection.y = 0;
        Vector3 targetPosition = spawnPosition + randomDirection;

        // 检查目标位置是否可通行
        Vector2Int gridPos = WorldToGrid(targetPosition);
        if (IsWalkable(gridPos))
        {
            patrolTarget = targetPosition;
            patrolTarget.y = transform.position.y; // 保持高度
        }
        else
        {
            // 如果不可通行，尝试找一个可通行的点
            for (int i = 0; i < 10; i++)
            {
                randomDirection = Random.insideUnitSphere * patrolRadius;
                randomDirection.y = 0;
                targetPosition = spawnPosition + randomDirection;
                gridPos = WorldToGrid(targetPosition);

                if (IsWalkable(gridPos))
                {
                    patrolTarget = targetPosition;
                    patrolTarget.y = transform.position.y;
                    return;
                }
            }

            // 如果还是找不到，就在原地等待
            patrolTarget = transform.position;
        }
    }

    #endregion

    #region 追击状态

    /// <summary>
    /// 切换到追击状态
    /// </summary>
    void SwitchToChase()
    {
        currentState = MonsterState.Chase;
        pathUpdateTimer = 0;
        pathfindingFailCount = 0;
        Debug.Log($"{gameObject.name}: 发现玩家，开始追击！");
    }

    /// <summary>
    /// 切换到巡逻状态
    /// </summary>
    void SwitchToPatrol()
    {
        currentState = MonsterState.Patrol;
        currentPath = null;
        StartPatrol();
        Debug.Log($"{gameObject.name}: 失去目标，返回巡逻");
    }

    /// <summary>
    /// 更新追击
    /// </summary>
    void UpdateChase()
    {
        if (playerTransform == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // 检查是否在攻击范围内
        if (distanceToPlayer <= attackRange)
        {
            // 停止移动，尝试攻击
            if (attackCooldownTimer <= 0)
            {
                AttackPlayer();
            }
            return;
        }

        // 更新路径
        pathUpdateTimer -= Time.deltaTime;
        if (pathUpdateTimer <= 0)
        {
            pathUpdateTimer = pathUpdateInterval;
            UpdatePathToPlayer();
        }

        // 沿路径移动
        if (currentPath != null && currentPath.Count > 0)
        {
            FollowPath();
        }
        else
        {
            // 没有路径，直接朝玩家移动（可能会卡住）
            MoveTowards(playerTransform.position);
        }
    }

    /// <summary>
    /// 攻击玩家
    /// </summary>
    void AttackPlayer()
    {
        if (targetPlayer == null) return;

        // 计算击退方向
        Vector3 knockbackDirection = (playerTransform.position - transform.position).normalized;

        // 对玩家造成伤害
        targetPlayer.TakeDamage(attackDamage, knockbackDirection, knockbackForce);

        // 重置攻击冷却
        attackCooldownTimer = attackCooldown;

        Debug.Log($"{gameObject.name} 攻击了玩家！伤害: {attackDamage}");
    }

    /// <summary>
    /// 更新到玩家的路径
    /// </summary>
    void UpdatePathToPlayer()
    {
        Vector2Int startGrid = WorldToGrid(transform.position);
        Vector2Int targetGrid = WorldToGrid(playerTransform.position);

        currentPath = FindPathAStar(startGrid, targetGrid);

        if (currentPath == null || currentPath.Count == 0)
        {
            pathfindingFailCount++;
            Debug.LogWarning($"{gameObject.name}: 无法找到路径到玩家 (失败次数: {pathfindingFailCount})");

            // 多次失败后瞬移
            if (pathfindingFailCount >= maxPathfindingAttempts)
            {
                TeleportNearPlayer();
                pathfindingFailCount = 0;
            }
        }
        else
        {
            pathfindingFailCount = 0;
            currentPathIndex = 0;
            lastKnownPlayerPosition = playerTransform.position;
        }
    }

    /// <summary>
    /// 沿路径移动
    /// </summary>
    void FollowPath()
    {
        if (currentPathIndex >= currentPath.Count)
        {
            currentPath = null;
            return;
        }

        Vector3 targetWorldPos = GridToWorld(currentPath[currentPathIndex]);
        targetWorldPos.y = transform.position.y;

        if (Vector3.Distance(transform.position, targetWorldPos) < waypointReachDistance)
        {
            currentPathIndex++;
        }
        else
        {
            MoveTowards(targetWorldPos);
        }
    }

    /// <summary>
    /// 瞬移到玩家附近
    /// </summary>
    void TeleportNearPlayer()
    {
        if (playerTransform == null) return;

        // 在玩家周围找一个可通行的位置
        Vector3 playerPos = playerTransform.position;
        List<Vector3> validPositions = new List<Vector3>();

        for (int angle = 0; angle < 360; angle += 45)
        {
            float rad = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)) * teleportDistance;
            Vector3 candidatePos = playerPos + offset;

            Vector2Int gridPos = WorldToGrid(candidatePos);
            if (IsWalkable(gridPos))
            {
                validPositions.Add(candidatePos);
            }
        }

        if (validPositions.Count > 0)
        {
            Vector3 teleportPos = validPositions[Random.Range(0, validPositions.Count)];
            teleportPos.y = transform.position.y;

            if (characterController != null)
            {
                characterController.enabled = false;
                transform.position = teleportPos;
                characterController.enabled = true;
            }
            else
            {
                transform.position = teleportPos;
            }

            Debug.Log($"{gameObject.name}: 瞬移到玩家附近 {teleportPos}");
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: 无法找到瞬移位置");
        }
    }

    #endregion

    #region A*寻路算法

    /// <summary>
    /// A*寻路
    /// </summary>
    List<Vector2Int> FindPathAStar(Vector2Int start, Vector2Int end)
    {
        if (roomGenerator == null || roomGenerator.FloorGrid == null) return null;

        List<MonsterPathNode> openList = new List<MonsterPathNode>();
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();

        MonsterPathNode startNode = new MonsterPathNode
        {
            position = start,
            gCost = 0,
            hCost = Vector2Int.Distance(start, end)
        };
        openList.Add(startNode);

        while (openList.Count > 0)
        {
            MonsterPathNode current = openList.OrderBy(n => n.fCost).First();

            if (current.position == end)
            {
                return ReconstructPath(current);
            }

            openList.Remove(current);
            closedSet.Add(current.position);

            foreach (Vector2Int neighbor in GetNeighbors(current.position))
            {
                if (closedSet.Contains(neighbor)) continue;
                if (!IsWalkable(neighbor)) continue;

                float tentativeG = current.gCost + Vector2Int.Distance(current.position, neighbor);

                MonsterPathNode neighborNode = openList.FirstOrDefault(n => n.position == neighbor);
                if (neighborNode == null)
                {
                    neighborNode = new MonsterPathNode
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

    /// <summary>
    /// 重建路径
    /// </summary>
    List<Vector2Int> ReconstructPath(MonsterPathNode endNode)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        MonsterPathNode current = endNode;

        while (current != null)
        {
            path.Add(current.position);
            current = current.parent;
        }

        path.Reverse();
        return path;
    }

    /// <summary>
    /// 获取邻居节点
    /// </summary>
    List<Vector2Int> GetNeighbors(Vector2Int pos)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();
        Vector2Int[] directions = {
            new Vector2Int(0, 1), new Vector2Int(1, 0),
            new Vector2Int(0, -1), new Vector2Int(-1, 0),
            // 对角线
            new Vector2Int(1, 1), new Vector2Int(1, -1),
            new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };

        foreach (var dir in directions)
        {
            neighbors.Add(pos + dir);
        }

        return neighbors;
    }

    #endregion

    #region 移动和辅助方法

    /// <summary>
    /// 朝目标移动
    /// </summary>
    void MoveTowards(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0;

        // 移动
        if (characterController != null)
        {
            characterController.Move(direction * moveSpeed * Time.deltaTime);
        }
        else
        {
            transform.position += direction * moveSpeed * Time.deltaTime;
        }

        // 旋转朝向移动方向
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    /// <summary>
    /// 世界坐标转网格坐标
    /// </summary>
    Vector2Int WorldToGrid(Vector3 worldPos)
    {
        if (roomGenerator == null) return Vector2Int.zero;

        float tileSize = roomGenerator.TileSize;
        int x = Mathf.RoundToInt(worldPos.x / tileSize);
        int z = Mathf.RoundToInt(worldPos.z / tileSize);

        Vector2Int roomSize = roomGenerator.RoomSize;
        return new Vector2Int(
            Mathf.Clamp(x, 0, roomSize.x - 1),
            Mathf.Clamp(z, 0, roomSize.y - 1)
        );
    }

    /// <summary>
    /// 网格坐标转世界坐标
    /// </summary>
    Vector3 GridToWorld(Vector2Int gridPos)
    {
        if (roomGenerator == null) return Vector3.zero;

        float tileSize = roomGenerator.TileSize;
        return new Vector3(gridPos.x * tileSize, 0, gridPos.y * tileSize);
    }

    /// <summary>
    /// 检查格子是否可通行
    /// </summary>
    bool IsWalkable(Vector2Int gridPos)
    {
        if (roomGenerator == null || roomGenerator.FloorGrid == null) return false;

        Vector2Int roomSize = roomGenerator.RoomSize;
        if (gridPos.x < 0 || gridPos.x >= roomSize.x || gridPos.y < 0 || gridPos.y >= roomSize.y)
            return false;

        return roomGenerator.FloorGrid[gridPos.x, gridPos.y].type == FloorType.Walkable;
    }

    #endregion

    #region Gizmos调试

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // 绘制检测范围（圆形）
        Gizmos.color = currentState == MonsterState.Patrol ? Color.yellow : Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // 绘制攻击范围
        if (currentState == MonsterState.Chase)
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }

        // 绘制巡逻目标
        if (currentState == MonsterState.Patrol && !isWaiting)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, patrolTarget);
            Gizmos.DrawWireSphere(patrolTarget, 0.5f);
        }

        // 绘制追击路径
        if (currentState == MonsterState.Chase && currentPath != null)
        {
            Gizmos.color = Color.red;
            for (int i = 0; i < currentPath.Count - 1; i++)
            {
                Vector3 start = GridToWorld(currentPath[i]);
                Vector3 end = GridToWorld(currentPath[i + 1]);
                start.y = transform.position.y;
                end.y = transform.position.y;
                Gizmos.DrawLine(start, end);
            }
        }
    }

    #endregion
}

/// <summary>
/// 怪物A*寻路节点
/// </summary>
public class MonsterPathNode
{
    public Vector2Int position;
    public float gCost;
    public float hCost;
    public float fCost => gCost + hCost;
    public MonsterPathNode parent;
}