using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum MonsterState
{
    Patrol,
    Chase
}

public class MonsterAI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private RoomGenerator roomGenerator;
    [SerializeField] private SpriteRenderer spriteRenderer;

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
    [SerializeField] private float pathUpdateInterval = 0.3f;
    [SerializeField] private float teleportDistance = 3f;
    [SerializeField] private int maxPathfindingAttempts = 3;
    [SerializeField] private float playerMovementThreshold = 3f;

    [Header("Attack Settings")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float baseAttackDamage = 20f;
    private float attackDamage;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private float attackScaleMultiplier = 1.3f;
    [SerializeField] private float attackScaleDuration = 0.2f;

    [Header("Visual Effects")]
    [SerializeField] private Color slowedColor = new Color(0.5f, 0.7f, 1f, 1f);

    public bool IsSlowed => isSlowed;
    public Color SlowedColor => slowedColor;
    public Color OriginalColor => originalColor;

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

    private bool isSlowed = false;
    private float originalMoveSpeed;
    private Coroutine slowCoroutine;

    private Color originalColor;
    private Vector3 originalScale;
    private Coroutine attackScaleCoroutine;

    // ⭐ 新增：房间偏移量
    private Vector3 roomOffset = Vector3.zero;

    private static readonly Vector2Int[] NeighborDirections = {
        new Vector2Int(0, 1), new Vector2Int(1, 0),
        new Vector2Int(0, -1), new Vector2Int(-1, 0),
        new Vector2Int(1, 1), new Vector2Int(1, -1),
        new Vector2Int(-1, 1), new Vector2Int(-1, -1)
    };

    void Start()
    {
        // ⭐ 获取房间偏移量（如果是房间容器的子物体）
        if (transform.parent != null)
        {
            roomOffset = transform.parent.position;
            Debug.Log($"{gameObject.name}: 房间偏移量 = {roomOffset}");
        }

        // 根据难度缩放攻击伤害
        if (DifficultyManager.Instance != null)
        {
            attackDamage = DifficultyManager.Instance.GetScaledDamage(baseAttackDamage);
            Debug.Log($"{gameObject.name} - Base Damage: {baseAttackDamage}, Scaled Damage: {attackDamage} (Difficulty: {DifficultyManager.Instance.CurrentDifficulty})");
        }
        else
        {
            attackDamage = baseAttackDamage;
            Debug.LogWarning($"{gameObject.name} - DifficultyManager not found, using base damage: {baseAttackDamage}");
        }

        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            Debug.LogWarning($"{gameObject.name}: 没有CharacterController，添加一个");
            characterController = gameObject.AddComponent<CharacterController>();
            characterController.radius = 0.5f;
            characterController.height = 2f;
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogWarning($"{gameObject.name}: 没有找到SpriteRenderer组件！");
            }
        }

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
            originalScale = spriteRenderer.transform.localScale;
        }

        spawnPosition = transform.position;
        originalMoveSpeed = moveSpeed;

        if (targetPlayer == null)
        {
            targetPlayer = FindObjectOfType<PlayerController>();
            if (targetPlayer != null)
            {
                playerTransform = targetPlayer.transform;
                Debug.Log($"{gameObject.name}: 找到玩家 {targetPlayer.gameObject.name}");
            }
            else
            {
                Debug.LogWarning($"{gameObject.name}: 未找到 PlayerController！");
            }
        }

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                targetPlayer = playerObj.GetComponentInParent<PlayerController>();
                if (targetPlayer == null)
                {
                    targetPlayer = playerObj.GetComponentInChildren<PlayerController>();
                }

                if (targetPlayer != null)
                {
                    playerTransform = targetPlayer.transform;
                    Debug.Log($"{gameObject.name}: 通过Tag找到玩家 {targetPlayer.gameObject.name}");
                }
            }
        }

        if (roomGenerator == null)
        {
            roomGenerator = FindObjectOfType<RoomGenerator>();
        }

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

    public void ApplySlow(float slowMultiplier, float duration)
    {
        if (slowCoroutine != null)
        {
            StopCoroutine(slowCoroutine);
        }

        slowCoroutine = StartCoroutine(SlowCoroutine(slowMultiplier, duration));
    }

    public void SetColor(Color color)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }
    }

    IEnumerator SlowCoroutine(float slowMultiplier, float duration)
    {
        if (!isSlowed)
        {
            isSlowed = true;
            moveSpeed = originalMoveSpeed * slowMultiplier;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = slowedColor;
            }

            Debug.Log($"{gameObject.name} 被减速！速度: {originalMoveSpeed} → {moveSpeed}，持续 {duration} 秒");
        }
        else
        {
            moveSpeed = originalMoveSpeed * slowMultiplier;
            Debug.Log($"{gameObject.name} 减速效果刷新！新速度: {moveSpeed}");
        }

        yield return new WaitForSeconds(duration);

        isSlowed = false;
        moveSpeed = originalMoveSpeed;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }

        Debug.Log($"{gameObject.name} 减速效果结束，速度恢复: {moveSpeed}");
    }

    #endregion

    #region 外部调用接口

    public void ForceChase()
    {
        if (currentState != MonsterState.Chase)
        {
            SwitchToChase();
            Debug.Log($"{gameObject.name}: 被攻击，强制进入追击状态！");
        }
    }

    #endregion

    void CheckPlayerDetection()
    {
        if (playerTransform == null)
        {
            targetPlayer = FindObjectOfType<PlayerController>();
            if (targetPlayer != null)
            {
                playerTransform = targetPlayer.transform;
            }
            return;
        }

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

    #region 追击状态（改进版）

    void SwitchToChase()
    {
        currentState = MonsterState.Chase;
        pathUpdateTimer = 0;
        pathfindingFailCount = 0;
        lastKnownPlayerPosition = Vector3.zero;
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
        bool needsPathUpdate = pathUpdateTimer <= 0;

        if (lastKnownPlayerPosition != Vector3.zero)
        {
            float playerMovedDistance = Vector3.Distance(lastKnownPlayerPosition, playerTransform.position);
            if (playerMovedDistance > playerMovementThreshold)
            {
                needsPathUpdate = true;
            }
        }

        if (needsPathUpdate)
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
            TryDirectApproach();
        }
    }

    void TryDirectApproach()
    {
        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
        directionToPlayer.y = 0;

        Vector3 nextPosition = transform.position + directionToPlayer * moveSpeed * Time.deltaTime;
        Vector2Int nextGridPos = WorldToGrid(nextPosition);

        if (IsWalkable(nextGridPos))
        {
            MoveTowards(playerTransform.position);
        }
        else
        {
            TryFindAlternativePath(directionToPlayer);
        }
    }

    void TryFindAlternativePath(Vector3 blockedDirection)
    {
        Vector3[] alternativeDirections = new Vector3[]
        {
            Quaternion.Euler(0, 45, 0) * blockedDirection,
            Quaternion.Euler(0, -45, 0) * blockedDirection,
            Quaternion.Euler(0, 90, 0) * blockedDirection,
            Quaternion.Euler(0, -90, 0) * blockedDirection,
            Quaternion.Euler(0, 135, 0) * blockedDirection,
            Quaternion.Euler(0, -135, 0) * blockedDirection
        };

        foreach (var dir in alternativeDirections)
        {
            Vector3 testPosition = transform.position + dir * moveSpeed * Time.deltaTime;
            Vector2Int testGridPos = WorldToGrid(testPosition);

            if (IsWalkable(testGridPos))
            {
                Vector3 targetPos = transform.position + dir.normalized * 2f;
                MoveTowards(targetPos);
                return;
            }
        }

        pathUpdateTimer = 0;
    }

    void UpdatePathToPlayer()
    {
        Vector2Int startGrid = WorldToGrid(transform.position);
        Vector2Int targetGrid = WorldToGrid(playerTransform.position);

        if (!IsWalkable(startGrid))
        {
            startGrid = FindNearestWalkableCell(startGrid);
        }

        if (!IsWalkable(targetGrid))
        {
            targetGrid = FindNearestWalkableCell(targetGrid);
        }

        currentPath = FindPathAStar(startGrid, targetGrid);

        if (currentPath == null || currentPath.Count == 0)
        {
            pathfindingFailCount++;

            if (pathfindingFailCount >= maxPathfindingAttempts)
            {
                float directDistance = Vector3.Distance(transform.position, playerTransform.position);
                if (directDistance > detectionRange * 0.8f)
                {
                    TeleportNearPlayer();
                }
                pathfindingFailCount = 0;
            }
        }
        else
        {
            pathfindingFailCount = 0;
            currentPathIndex = 0;
            lastKnownPlayerPosition = playerTransform.position;

            if (currentPath.Count > 20)
            {
                currentPath = currentPath.GetRange(0, 20);
            }
        }
    }

    Vector2Int FindNearestWalkableCell(Vector2Int center)
    {
        if (IsWalkable(center)) return center;

        for (int radius = 1; radius <= 5; radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (Mathf.Abs(x) == radius || Mathf.Abs(y) == radius)
                    {
                        Vector2Int testPos = center + new Vector2Int(x, y);
                        if (IsWalkable(testPos))
                        {
                            return testPos;
                        }
                    }
                }
            }
        }

        return center;
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

        float distanceToWaypoint = Vector3.Distance(transform.position, targetWorldPos);

        float reachDistance = waypointReachDistance;
        if (currentPathIndex == currentPath.Count - 1)
        {
            reachDistance = waypointReachDistance * 2f;
        }

        if (distanceToWaypoint < reachDistance)
        {
            currentPathIndex++;

            if (currentPathIndex < currentPath.Count - 2)
            {
                Vector3 nextWaypoint = GridToWorld(currentPath[currentPathIndex + 1]);
                float distanceToNext = Vector3.Distance(transform.position, nextWaypoint);
                if (distanceToNext < waypointReachDistance * 3f)
                {
                    currentPathIndex++;
                }
            }
        }
        else
        {
            MoveTowards(targetWorldPos);
        }
    }

    void AttackPlayer()
    {
        if (targetPlayer == null)
        {
            targetPlayer = FindObjectOfType<PlayerController>();
            if (targetPlayer != null)
            {
                playerTransform = targetPlayer.transform;
            }
            else
            {
                return;
            }
        }

        Vector3 knockbackDirection = (playerTransform.position - transform.position).normalized;
        targetPlayer.TakeDamage(attackDamage, knockbackDirection, knockbackForce);
        attackCooldownTimer = attackCooldown;

        if (spriteRenderer != null)
        {
            if (attackScaleCoroutine != null)
            {
                StopCoroutine(attackScaleCoroutine);
            }
            attackScaleCoroutine = StartCoroutine(AttackScaleAnimation());
        }

        Debug.Log($"{gameObject.name} 攻击了玩家！伤害: {attackDamage}");
    }

    IEnumerator AttackScaleAnimation()
    {
        if (spriteRenderer == null) yield break;

        Vector3 targetScale = originalScale * attackScaleMultiplier;
        float elapsed = 0f;
        float halfDuration = attackScaleDuration / 2f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            spriteRenderer.transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / halfDuration;
            spriteRenderer.transform.localScale = Vector3.Lerp(targetScale, originalScale, t);
            yield return null;
        }

        spriteRenderer.transform.localScale = originalScale;
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
    }

    #endregion

    #region A*寻路算法

    List<Vector2Int> FindPathAStar(Vector2Int start, Vector2Int end)
    {
        if (roomGenerator == null || roomGenerator.FloorGrid == null) return null;

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

    #region 移动和辅助方法（⭐修复：处理房间偏移）

    void MoveTowards(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0;

        if (characterController != null)
        {
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

    /// <summary>
    /// ⭐ 修复：世界坐标转格子坐标（处理房间偏移）
    /// </summary>
    Vector2Int WorldToGrid(Vector3 worldPos)
    {
        if (roomGenerator == null) return Vector2Int.zero;

        // ⭐ 减去房间偏移量，得到相对坐标
        Vector3 relativePos = worldPos - roomOffset;

        float tileSize = roomGenerator.TileSize;
        int x = Mathf.RoundToInt(relativePos.x / tileSize);
        int z = Mathf.RoundToInt(relativePos.z / tileSize);

        Vector2Int roomSize = roomGenerator.RoomSize;
        return new Vector2Int(
            Mathf.Clamp(x, 0, roomSize.x - 1),
            Mathf.Clamp(z, 0, roomSize.y - 1)
        );
    }

    /// <summary>
    /// ⭐ 修复：格子坐标转世界坐标（处理房间偏移）
    /// </summary>
    Vector3 GridToWorld(Vector2Int gridPos)
    {
        if (roomGenerator == null) return Vector3.zero;

        float tileSize = roomGenerator.TileSize;

        // ⭐ 加上房间偏移量，得到世界坐标
        return roomOffset + new Vector3(gridPos.x * tileSize, 0, gridPos.y * tileSize);
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

            if (playerTransform != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, playerTransform.position);

                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(playerTransform.position, 0.5f);
            }
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

public class MonsterPathNode
{
    public Vector2Int position;
    public float gCost;
    public float hCost;
    public float fCost => gCost + hCost;
    public MonsterPathNode parent;
}

public class PathNodeComparer : IComparer<MonsterPathNode>
{
    public int Compare(MonsterPathNode a, MonsterPathNode b)
    {
        int fCompare = a.fCost.CompareTo(b.fCost);
        if (fCompare != 0) return fCompare;

        int xCompare = a.position.x.CompareTo(b.position.x);
        if (xCompare != 0) return xCompare;

        return a.position.y.CompareTo(b.position.y);
    }
}