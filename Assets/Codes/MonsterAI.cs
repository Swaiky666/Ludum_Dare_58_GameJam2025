using System.Collections;
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
/// 怪物AI控制器（添加减速系统）
/// </summary>
public class MonsterAI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private RoomGenerator roomGenerator;

    [Header("Detection Settings")]
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private float loseTargetRange = 15f;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private float waypointReachDistance = 0.5f;

    [Header("Patrol Settings")]
    [SerializeField] private float minPatrolTime = 1f;
    [SerializeField] private float maxPatrolTime = 3f;
    [SerializeField] private float minWaitTime = 1f;
    [SerializeField] private float maxWaitTime = 3f;
    [SerializeField] private float patrolRadius = 5f;

    [Header("Chase Settings")]
    [SerializeField] private float pathUpdateInterval = 0.5f;
    [SerializeField] private float teleportDistance = 3f;
    [SerializeField] private int maxPathfindingAttempts = 3;

    [Header("Attack Settings")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackDamage = 20f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float knockbackForce = 10f;

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

    // 减速系统变量
    private bool isSlowed = false;
    private float originalMoveSpeed;
    private Coroutine slowCoroutine;

    // 静态缓存（所有怪物共享）
    private static readonly Vector2Int[] NeighborDirections = {
        new Vector2Int(0, 1), new Vector2Int(1, 0),
        new Vector2Int(0, -1), new Vector2Int(-1, 0),
        new Vector2Int(1, 1), new Vector2Int(1, -1),
        new Vector2Int(-1, 1), new Vector2Int(-1, -1)
    };

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
        originalMoveSpeed = moveSpeed; // 保存原始速度

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

        // 错开路径更新时间，避免所有怪物同时更新
        pathUpdateTimer = Random.Range(0f, pathUpdateInterval);

        StartPatrol();
    }

    void Update()
    {
        CheckPlayerDetection();

        if (attackCooldownTimer > 0)
        {
            attackCooldownTimer -= Time.deltaTime;
        }

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

    #region 减速系统

    /// <summary>
    /// 应用减速效果（由子弹调用）
    /// </summary>
    public void ApplySlow(float slowMultiplier, float duration)
    {
        // 如果已经有减速效果，先停止旧的
        if (slowCoroutine != null)
        {
            StopCoroutine(slowCoroutine);
        }

        slowCoroutine = StartCoroutine(SlowCoroutine(slowMultiplier, duration));
    }

    IEnumerator SlowCoroutine(float slowMultiplier, float duration)
    {
        // 应用减速
        if (!isSlowed)
        {
            isSlowed = true;
            moveSpeed = originalMoveSpeed * slowMultiplier;
            Debug.Log($"{gameObject.name} 被减速！速度: {originalMoveSpeed} → {moveSpeed}，持续 {duration} 秒");
        }
        else
        {
            // 已经减速，更新速度
            moveSpeed = originalMoveSpeed * slowMultiplier;
            Debug.Log($"{gameObject.name} 减速效果刷新！新速度: {moveSpeed}");
        }

        // 等待持续时间
        yield return new WaitForSeconds(duration);

        // 恢复速度
        isSlowed = false;
        moveSpeed = originalMoveSpeed;
        Debug.Log($"{gameObject.name} 减速效果结束，速度恢复: {moveSpeed}");
    }

    #endregion

    void CheckPlayerDetection()
    {
        if (playerTransform == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (currentState == MonsterState.Patrol)
        {
            if (distanceToPlayer <= detectionRange)
            {
                Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
                if (!Physics.Raycast(transform.position + Vector3.up, directionToPlayer, distanceToPlayer, LayerMask.GetMask("Obstacle")))
                {
                    SwitchToChase();
                }
            }
        }
        else if (currentState == MonsterState.Chase)
        {
            if (distanceToPlayer > loseTargetRange)
            {
                SwitchToPatrol();
            }
        }
    }

    #region 巡逻状态

    void StartPatrol()
    {
        isWaiting = true;
        waitTimer = Random.Range(minWaitTime, maxWaitTime);
    }

    void UpdatePatrol()
    {
        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0)
            {
                SetRandomPatrolTarget();
                isWaiting = false;
                patrolTimer = Random.Range(minPatrolTime, maxPatrolTime);
            }
        }
        else
        {
            patrolTimer -= Time.deltaTime;

            if (patrolTimer <= 0 || Vector3.Distance(transform.position, patrolTarget) < waypointReachDistance)
            {
                isWaiting = true;
                waitTimer = Random.Range(minWaitTime, maxWaitTime);
            }
            else
            {
                MoveTowards(patrolTarget);
            }
        }
    }

    void SetRandomPatrolTarget()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection.y = 0;
        Vector3 targetPosition = spawnPosition + randomDirection;

        Vector2Int gridPos = WorldToGrid(targetPosition);
        if (IsWalkable(gridPos))
        {
            patrolTarget = targetPosition;
            patrolTarget.y = transform.position.y;
        }
        else
        {
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

            patrolTarget = transform.position;
        }
    }

    #endregion

    #region 追击状态

    void SwitchToChase()
    {
        currentState = MonsterState.Chase;
        pathUpdateTimer = 0;
        pathfindingFailCount = 0;
        Debug.Log($"{gameObject.name}: 发现玩家，开始追击！");
    }

    void SwitchToPatrol()
    {
        currentState = MonsterState.Patrol;
        currentPath = null;
        StartPatrol();
        Debug.Log($"{gameObject.name}: 失去目标，返回巡逻");
    }

    void UpdateChase()
    {
        if (playerTransform == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer <= attackRange)
        {
            if (attackCooldownTimer <= 0)
            {
                AttackPlayer();
            }
            return;
        }

        pathUpdateTimer -= Time.deltaTime;
        if (pathUpdateTimer <= 0)
        {
            pathUpdateTimer = pathUpdateInterval;
            UpdatePathToPlayer();
        }

        if (currentPath != null && currentPath.Count > 0)
        {
            FollowPath();
        }
        else
        {
            MoveTowards(playerTransform.position);
        }
    }

    void AttackPlayer()
    {
        if (targetPlayer == null) return;

        Vector3 knockbackDirection = (playerTransform.position - transform.position).normalized;
        targetPlayer.TakeDamage(attackDamage, knockbackDirection, knockbackForce);
        attackCooldownTimer = attackCooldown;

        Debug.Log($"{gameObject.name} 攻击了玩家！伤害: {attackDamage}");
    }

    void UpdatePathToPlayer()
    {
        Vector2Int startGrid = WorldToGrid(transform.position);
        Vector2Int targetGrid = WorldToGrid(playerTransform.position);

        currentPath = FindPathAStar(startGrid, targetGrid);

        if (currentPath == null || currentPath.Count == 0)
        {
            pathfindingFailCount++;
            Debug.LogWarning($"{gameObject.name}: 无法找到路径到玩家 (失败次数: {pathfindingFailCount})");

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

    void TeleportNearPlayer()
    {
        if (playerTransform == null) return;

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

    #region A*寻路算法（优化版）

    /// <summary>
    /// A*寻路 - 使用优化的节点选择
    /// </summary>
    List<Vector2Int> FindPathAStar(Vector2Int start, Vector2Int end)
    {
        if (roomGenerator == null || roomGenerator.FloorGrid == null) return null;

        // 使用 SortedSet 替代 List + OrderBy，自动保持排序
        var openSet = new SortedSet<MonsterPathNode>(new PathNodeComparer());
        var openSetLookup = new Dictionary<Vector2Int, MonsterPathNode>();
        var closedSet = new HashSet<Vector2Int>();

        MonsterPathNode startNode = new MonsterPathNode
        {
            position = start,
            gCost = 0,
            hCost = Vector2Int.Distance(start, end)
        };

        openSet.Add(startNode);
        openSetLookup[start] = startNode;

        while (openSet.Count > 0)
        {
            // 直接获取最小节点（已排序）
            MonsterPathNode current = openSet.Min;

            if (current.position == end)
            {
                return ReconstructPath(current);
            }

            openSet.Remove(current);
            openSetLookup.Remove(current.position);
            closedSet.Add(current.position);

            foreach (Vector2Int neighbor in GetNeighborsOptimized(current.position))
            {
                if (closedSet.Contains(neighbor)) continue;
                if (!IsWalkable(neighbor)) continue;

                float tentativeG = current.gCost + Vector2Int.Distance(current.position, neighbor);

                if (openSetLookup.TryGetValue(neighbor, out MonsterPathNode neighborNode))
                {
                    if (tentativeG < neighborNode.gCost)
                    {
                        // 需要更新节点，先移除再重新添加
                        openSet.Remove(neighborNode);
                        neighborNode.gCost = tentativeG;
                        neighborNode.parent = current;
                        openSet.Add(neighborNode);
                    }
                }
                else
                {
                    neighborNode = new MonsterPathNode
                    {
                        position = neighbor,
                        gCost = tentativeG,
                        hCost = Vector2Int.Distance(neighbor, end),
                        parent = current
                    };
                    openSet.Add(neighborNode);
                    openSetLookup[neighbor] = neighborNode;
                }
            }
        }

        return null;
    }

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
    /// 获取邻居节点（优化版 - 使用缓存的方向数组）
    /// </summary>
    List<Vector2Int> GetNeighborsOptimized(Vector2Int pos)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>(8);

        foreach (var dir in NeighborDirections)
        {
            neighbors.Add(pos + dir);
        }

        return neighbors;
    }

    #endregion

    #region 移动和辅助方法

    void MoveTowards(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0;

        if (characterController != null)
        {
            // 使用当前速度（可能被减速影响）
            characterController.Move(direction * moveSpeed * Time.deltaTime);
        }
        else
        {
            transform.position += direction * moveSpeed * Time.deltaTime;
        }

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

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

    Vector3 GridToWorld(Vector2Int gridPos)
    {
        if (roomGenerator == null) return Vector3.zero;

        float tileSize = roomGenerator.TileSize;
        return new Vector3(gridPos.x * tileSize, 0, gridPos.y * tileSize);
    }

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

        // 减速状态用蓝色圈显示
        if (isSlowed)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, detectionRange * 0.3f);
        }

        Gizmos.color = currentState == MonsterState.Patrol ? Color.yellow : Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        if (currentState == MonsterState.Chase)
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }

        if (currentState == MonsterState.Patrol && !isWaiting)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, patrolTarget);
            Gizmos.DrawWireSphere(patrolTarget, 0.5f);
        }

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

/// <summary>
/// 路径节点比较器（用于SortedSet自动排序）
/// </summary>
public class PathNodeComparer : IComparer<MonsterPathNode>
{
    public int Compare(MonsterPathNode a, MonsterPathNode b)
    {
        int fCompare = a.fCost.CompareTo(b.fCost);
        if (fCompare != 0) return fCompare;

        // fCost相同时，比较位置（避免被认为是重复）
        int xCompare = a.position.x.CompareTo(b.position.x);
        if (xCompare != 0) return xCompare;

        return a.position.y.CompareTo(b.position.y);
    }
}