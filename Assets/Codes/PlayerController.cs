using UnityEngine;

/// <summary>
/// 玩家控制器
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;           // 移动速度

    [Header("Dash Settings")]
    [SerializeField] private float dashDistance = 5f;        // 冲刺距离
    [SerializeField] private float dashDuration = 0.2f;      // 冲刺持续时间
    [SerializeField] private float dashCooldown = 1f;        // 冲刺冷却时间

    [Header("Health & Shield")]
    [SerializeField] private float maxHealth = 100f;         // 最大生命值
    [SerializeField] private float maxShield = 50f;          // 最大护盾值
    [SerializeField] private float shieldRegenRate = 5f;     // 护盾恢复速度（每秒）
    [SerializeField] private float armor = 10f;              // 护甲值

    [Header("References")]
    [SerializeField] private RoomGenerator roomGenerator;    // 房间生成器引用
    [SerializeField] private float collisionRadius = 0.4f;   // 碰撞检测半径

    // 当前状态
    private float currentHealth;
    private float currentShield;
    private bool isDashing;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector3 dashDirection;
    private CharacterController characterController;

    // 公共访问器
    public float CurrentHealth => currentHealth;
    public float CurrentShield => currentShield;
    public float MaxHealth => maxHealth;
    public float MaxShield => maxShield;
    public float Armor => armor;
    public bool IsDashing => isDashing;
    public float DashCooldownRatio => Mathf.Clamp01(dashCooldownTimer / dashCooldown); // 0=就绪, 1=刚冲刺完

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
            characterController.radius = 0.5f;
            characterController.height = 2f;
        }

        // 查找房间生成器
        if (roomGenerator == null)
        {
            roomGenerator = FindObjectOfType<RoomGenerator>();
            if (roomGenerator == null)
            {
                Debug.LogWarning("PlayerController: 未找到RoomGenerator，地形检测将无效！");
            }
        }

        // 初始化生命值和护盾
        currentHealth = maxHealth;
        currentShield = maxShield;
    }

    void Update()
    {
        HandleMovement();
        HandleDash();
        RegenerateShield();
    }

    /// <summary>
    /// 处理移动
    /// </summary>
    void HandleMovement()
    {
        if (isDashing) return;

        // 获取输入
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 inputDirection = new Vector3(horizontal, 0, vertical).normalized;

        // 移动
        if (inputDirection.magnitude > 0.1f)
        {
            Vector3 movement = inputDirection * moveSpeed * Time.deltaTime;
            Vector3 targetPosition = transform.position + movement;

            // 尝试移动到目标位置
            if (CanMoveTo(targetPosition))
            {
                // 可以直接移动
                characterController.Move(movement);
            }
            else
            {
                // 不能直接移动，尝试墙壁滑行
                TrySlideAlongWall(movement);
                // 滑行可能失败（完全卡住），但这也没关系
            }

            // 朝向移动方向
            transform.rotation = Quaternion.LookRotation(inputDirection);
        }
    }

    /// <summary>
    /// 处理冲刺
    /// </summary>
    void HandleDash()
    {
        // 更新冷却
        if (dashCooldownTimer > 0)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        // 冲刺中
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;

            // 执行冲刺移动（检测地形）
            float dashSpeed = dashDistance / dashDuration;
            Vector3 dashMovement = dashDirection * dashSpeed * Time.deltaTime;
            Vector3 targetPosition = transform.position + dashMovement;

            if (CanMoveTo(targetPosition))
            {
                characterController.Move(dashMovement);
            }
            else
            {
                // 碰到墙壁，尝试滑行
                if (!TrySlideAlongWall(dashMovement))
                {
                    // 完全阻挡，停止冲刺
                    isDashing = false;
                }
            }

            if (dashTimer <= 0)
            {
                isDashing = false;
            }
        }
        // 检测冲刺输入
        else if (Input.GetKeyDown(KeyCode.Space) && dashCooldownTimer <= 0)
        {
            StartDash();
        }
    }

    /// <summary>
    /// 尝试沿墙壁滑行
    /// </summary>
    bool TrySlideAlongWall(Vector3 movement)
    {
        // 分解移动向量为X和Z两个分量
        Vector3 movementX = new Vector3(movement.x, 0, 0);
        Vector3 movementZ = new Vector3(0, 0, movement.z);

        // 尝试只沿X轴移动
        Vector3 targetX = transform.position + movementX;
        if (CanMoveTo(targetX))
        {
            characterController.Move(movementX);
            return true;
        }

        // 尝试只沿Z轴移动
        Vector3 targetZ = transform.position + movementZ;
        if (CanMoveTo(targetZ))
        {
            characterController.Move(movementZ);
            return true;
        }

        // 两个方向都不行，完全阻挡
        return false;
    }

    /// <summary>
    /// 开始冲刺
    /// </summary>
    void StartDash()
    {
        // 获取冲刺方向
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 inputDirection = new Vector3(horizontal, 0, vertical).normalized;

        // 如果没有输入，使用当前朝向
        if (inputDirection.magnitude < 0.1f)
        {
            inputDirection = transform.forward;
        }

        dashDirection = inputDirection;
        isDashing = true;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;

        Debug.Log($"冲刺开始！方向: {dashDirection}");
    }

    /// <summary>
    /// 恢复护盾
    /// </summary>
    void RegenerateShield()
    {
        if (currentShield < maxShield)
        {
            currentShield += shieldRegenRate * Time.deltaTime;
            currentShield = Mathf.Min(currentShield, maxShield);
        }
    }

    /// <summary>
    /// 受到伤害
    /// </summary>
    public void TakeDamage(float damage, Vector3 knockbackDirection, float knockbackForce)
    {
        // 根据护甲计算实际伤害
        float damageReduction = armor / (armor + 100f);
        float actualDamage = damage * (1f - damageReduction);

        Debug.Log($"受到攻击 - 原始伤害: {damage}, 护甲减免: {damageReduction * 100f}%, 实际伤害: {actualDamage}");

        // 优先扣除护盾
        if (currentShield > 0)
        {
            if (currentShield >= actualDamage)
            {
                currentShield -= actualDamage;
                Debug.Log($"护盾吸收伤害 - 剩余护盾: {currentShield}");
            }
            else
            {
                float remainingDamage = actualDamage - currentShield;
                currentShield = 0;
                currentHealth -= remainingDamage;
                Debug.Log($"护盾破碎 - 生命值受到 {remainingDamage} 伤害，剩余生命: {currentHealth}");
            }
        }
        else
        {
            currentHealth -= actualDamage;
            Debug.Log($"生命值受到 {actualDamage} 伤害，剩余生命: {currentHealth}");
        }

        // 击退效果
        if (!isDashing && knockbackForce > 0)
        {
            StartCoroutine(ApplyKnockback(knockbackDirection, knockbackForce));
        }

        // 检查死亡
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 应用击退
    /// </summary>
    System.Collections.IEnumerator ApplyKnockback(Vector3 direction, float force)
    {
        float knockbackDuration = 0.2f;
        float elapsed = 0;

        while (elapsed < knockbackDuration)
        {
            float knockbackSpeed = force * (1 - elapsed / knockbackDuration); // 逐渐减弱
            Vector3 knockbackMovement = direction.normalized * knockbackSpeed * Time.deltaTime;
            Vector3 targetPosition = transform.position + knockbackMovement;

            // 尝试移动，如果碰墙则尝试滑行
            if (CanMoveTo(targetPosition))
            {
                characterController.Move(knockbackMovement);
            }
            else
            {
                // 尝试滑行，如果完全阻挡则停止击退
                if (!TrySlideAlongWall(knockbackMovement))
                {
                    break;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// 死亡
    /// </summary>
    void Die()
    {
        Debug.Log("玩家死亡！");
        // 这里可以添加死亡逻辑，但用户说不要实现额外内容
    }

    /// <summary>
    /// 检测是否可以移动到目标位置（考虑玩家半径）
    /// </summary>
    bool CanMoveTo(Vector3 targetPosition)
    {
        if (roomGenerator == null || roomGenerator.FloorGrid == null)
        {
            return true; // 如果没有地形数据，允许移动
        }

        // 检测中心点
        if (!IsPositionWalkable(targetPosition))
        {
            return false;
        }

        // 检测玩家周围8个方向的边缘点
        Vector3[] checkPoints = new Vector3[8];
        checkPoints[0] = targetPosition + new Vector3(collisionRadius, 0, 0);          // 右
        checkPoints[1] = targetPosition + new Vector3(-collisionRadius, 0, 0);         // 左
        checkPoints[2] = targetPosition + new Vector3(0, 0, collisionRadius);          // 前
        checkPoints[3] = targetPosition + new Vector3(0, 0, -collisionRadius);         // 后
        checkPoints[4] = targetPosition + new Vector3(collisionRadius, 0, collisionRadius);    // 右前
        checkPoints[5] = targetPosition + new Vector3(-collisionRadius, 0, collisionRadius);   // 左前
        checkPoints[6] = targetPosition + new Vector3(collisionRadius, 0, -collisionRadius);   // 右后
        checkPoints[7] = targetPosition + new Vector3(-collisionRadius, 0, -collisionRadius);  // 左后

        // 所有检测点都必须可通行
        foreach (Vector3 point in checkPoints)
        {
            if (!IsPositionWalkable(point))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 检测某个位置是否可通行
    /// </summary>
    bool IsPositionWalkable(Vector3 position)
    {
        if (roomGenerator == null || roomGenerator.FloorGrid == null)
        {
            return true;
        }

        Vector2Int gridPos = WorldToGrid(position);

        if (!IsValidGrid(gridPos))
        {
            return false;
        }

        Floor floor = roomGenerator.FloorGrid[gridPos.x, gridPos.y];
        return floor.type == FloorType.Walkable;
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
    /// 检查网格坐标是否有效
    /// </summary>
    bool IsValidGrid(Vector2Int gridPos)
    {
        if (roomGenerator == null) return false;

        Vector2Int roomSize = roomGenerator.RoomSize;
        return gridPos.x >= 0 && gridPos.x < roomSize.x &&
               gridPos.y >= 0 && gridPos.y < roomSize.y;
    }

    // 可视化碰撞半径
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // 绘制玩家碰撞半径
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, collisionRadius);
    }
}