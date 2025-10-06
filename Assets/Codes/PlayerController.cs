using UnityEngine;

/// <summary>
/// 玩家控制器（完整版 - 包含移动、朝向鼠标、装备系统、后坐力、震动等）
/// + 混合判定：网格可走性（地砖） & 指定Layer的物理体阻挡
/// + 无砖块兜底：脚下无砖块时依然可移动，但仍受物理阻挡
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Dash Settings")]
    [SerializeField] private float dashDistance = 5f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;

    [Header("Health & Shield")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float maxShield = 50f;
    [SerializeField] private float shieldRegenRate = 5f;
    [SerializeField] private float armor = 10f;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 720f;  // 度/秒

    [Header("References")]
    [SerializeField] private RoomGenerator roomGenerator;
    [SerializeField] private float collisionRadius = 0.4f;
    [SerializeField] private EquipmentManager equipmentManager;
    [SerializeField] private Camera mainCamera;

    [Header("Camera Shake")]
    [SerializeField] private float damageShakeDuration = 0.3f;
    [SerializeField] private float damageShakeMagnitude = 0.2f;

    // ========= 物理阻挡层设置（新增 + 可选微调） =========
    [Header("Physics Blocking (Layers)")]
    [Tooltip("会阻挡玩家的层（Walls/Environment/Props/Enemies 等）")]
    [SerializeField] private LayerMask blockingLayers;
    [Tooltip("是否把触发器也当成阻挡")]
    [SerializeField] private bool checkTriggers = false;
    [Tooltip("仅用扫掠，避免目标重叠提前拦截导致可见缝隙大")]
    [SerializeField] private bool sweepOnly = true;
    [Tooltip("检测半径微调（负值可让视觉更贴墙，建议 -0.01 ~ -0.03）")]
    [SerializeField] private float radiusBias = -0.01f;

    // ========= 无砖块兜底开关（新增） =========
    [Header("No-Tile Fallback")]
    [Tooltip("脚下无砖块也允许移动，但依然受物理阻挡")]
    [SerializeField] private bool allowMoveWithoutTile = true;

    // 当前状态
    private float currentHealth;
    private float currentShield;
    private bool isDashing;
    private float dashTimer;
    private float dashCooldownTimer;
    private Vector3 dashDirection;
    private CharacterController characterController;
    private bool canRotate = true;  // 控制旋转锁定
    private CameraShake cameraShake;

    // 公共访问器
    public float CurrentHealth => currentHealth;
    public float CurrentShield => currentShield;
    public float MaxHealth => maxHealth;
    public float MaxShield => maxShield;
    public float Armor => armor;
    public bool IsDashing => isDashing;
    public float DashCooldownRatio => Mathf.Clamp01(dashCooldownTimer / dashCooldown);

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
            characterController.radius = 0.5f;
            characterController.height = 2f;
        }

        if (roomGenerator == null)
        {
            roomGenerator = FindObjectOfType<RoomGenerator>();
            if (roomGenerator == null)
            {
                Debug.LogWarning("PlayerController: 未找到RoomGenerator，地形检测将无效！");
            }
        }

        if (equipmentManager == null)
        {
            equipmentManager = GetComponent<EquipmentManager>();
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        // 获取或添加相机震动组件
        if (mainCamera != null)
        {
            cameraShake = mainCamera.GetComponent<CameraShake>();
            if (cameraShake == null)
            {
                cameraShake = mainCamera.gameObject.AddComponent<CameraShake>();
            }
        }

        currentHealth = maxHealth;
        currentShield = maxShield;
    }

    void Update()
    {
        if (Time.timeScale == 0) return;

        HandleRotation();
        HandleMovement();
        HandleDash();
        HandleShooting();
        RegenerateShield();
    }

    /// <summary>
    /// 处理朝向鼠标
    /// </summary>
    void HandleRotation()
    {
        if (!canRotate || mainCamera == null) return;

        // 获取鼠标在世界中的位置
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, transform.position);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 hitPoint = ray.GetPoint(distance);
            Vector3 lookDirection = hitPoint - transform.position;
            lookDirection.y = 0;

            if (lookDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }
        }
    }

    /// <summary>
    /// 处理射击输入（支持按住持续射击）
    /// </summary>
    void HandleShooting()
    {
        if (equipmentManager == null) return;

        Vector3 shootDirection = transform.forward;
        Vector3 shootOrigin = transform.position;

        // 左键 - 按住持续使用槽位0的装备
        if (Input.GetMouseButton(0))
        {
            equipmentManager.UseEquipment(0, shootDirection, shootOrigin);
        }

        // 右键 - 按住持续使用槽位1的装备
        if (Input.GetMouseButton(1))
        {
            equipmentManager.UseEquipment(1, shootDirection, shootOrigin);
        }
    }

    /// <summary>
    /// 处理移动 - 使用GetAxisRaw去除惯性
    /// </summary>
    void HandleMovement()
    {
        if (isDashing) return;

        // 使用GetAxisRaw替代GetAxis，立即响应，无惯性
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 inputDirection = new Vector3(horizontal, 0, vertical).normalized;

        if (inputDirection.magnitude > 0.1f)
        {
            Vector3 movement = inputDirection * moveSpeed * Time.deltaTime;
            Vector3 targetPosition = transform.position + movement;

            if (CanMoveTo(targetPosition))
            {
                characterController.Move(movement);
            }
            else
            {
                TrySlideAlongWall(movement);
            }
        }
    }

    /// <summary>
    /// 处理冲刺
    /// </summary>
    void HandleDash()
    {
        if (dashCooldownTimer > 0)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;

            float dashSpeed = dashDistance / dashDuration;
            Vector3 dashMovement = dashDirection * dashSpeed * Time.deltaTime;
            Vector3 targetPosition = transform.position + dashMovement;

            if (CanMoveTo(targetPosition))
            {
                characterController.Move(dashMovement);
            }
            else
            {
                if (!TrySlideAlongWall(dashMovement))
                {
                    isDashing = false;
                }
            }

            if (dashTimer <= 0)
            {
                isDashing = false;
            }
        }
        else if (Input.GetKeyDown(KeyCode.Space) && dashCooldownTimer <= 0)
        {
            StartDash();
        }
    }

    void StartDash()
    {
        // 冲刺也使用GetAxisRaw，确保响应灵敏
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 inputDirection = new Vector3(horizontal, 0, vertical).normalized;

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
    /// 外部调用的冲刺方法（供技能使用）
    /// </summary>
    public void StartDashInDirection(Vector3 direction, float distance, float duration)
    {
        dashDirection = direction.normalized;
        dashDistance = distance;
        dashDuration = duration;
        isDashing = true;
        dashTimer = duration;
    }

    bool TrySlideAlongWall(Vector3 movement)
    {
        Vector3 movementX = new Vector3(movement.x, 0, 0);
        Vector3 movementZ = new Vector3(0, 0, movement.z);

        Vector3 targetX = transform.position + movementX;
        if (CanMoveTo(targetX))
        {
            characterController.Move(movementX);
            return true;
        }

        Vector3 targetZ = transform.position + movementZ;
        if (CanMoveTo(targetZ))
        {
            characterController.Move(movementZ);
            return true;
        }

        return false;
    }

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
        float damageReduction = armor / (armor + 100f);
        float actualDamage = damage * (1f - damageReduction);

        Debug.Log($"受到攻击 - 原始伤害: {damage}, 护甲减免: {damageReduction * 100f}%, 实际伤害: {actualDamage}");

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

        // 相机震动 - 受伤效果
        if (cameraShake != null)
        {
            cameraShake.Shake(damageShakeDuration, damageShakeMagnitude);
        }

        if (!isDashing && knockbackForce > 0)
        {
            StartCoroutine(ApplyKnockback(knockbackDirection, knockbackForce));
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 应用击退（击退时禁用旋转）
    /// </summary>
    System.Collections.IEnumerator ApplyKnockback(Vector3 direction, float force)
    {
        canRotate = false;  // 禁用鼠标旋转
        float knockbackDuration = 0.2f;
        float elapsed = 0;

        while (elapsed < knockbackDuration)
        {
            float knockbackSpeed = force * (1 - elapsed / knockbackDuration);
            Vector3 knockbackMovement = direction.normalized * knockbackSpeed * Time.deltaTime;
            Vector3 targetPosition = transform.position + knockbackMovement;

            if (CanMoveTo(targetPosition))
            {
                characterController.Move(knockbackMovement);
            }
            else
            {
                if (!TrySlideAlongWall(knockbackMovement))
                {
                    break;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        canRotate = true;  // 恢复鼠标旋转
    }

    /// <summary>
    /// 应用武器后坐力
    /// </summary>
    public void ApplyRecoil(Vector3 recoilDirection, float recoilForce)
    {
        if (isDashing) return;

        StartCoroutine(ApplyRecoilEffect(recoilDirection, recoilForce));
    }

    /// <summary>
    /// 后坐力效果
    /// </summary>
    private System.Collections.IEnumerator ApplyRecoilEffect(Vector3 direction, float force)
    {
        float recoilDuration = 0.1f;
        float elapsed = 0;

        direction.y = 0;

        while (elapsed < recoilDuration)
        {
            float recoilSpeed = force * (1 - elapsed / recoilDuration);
            Vector3 recoilMovement = direction.normalized * recoilSpeed * Time.deltaTime;
            Vector3 targetPosition = transform.position + recoilMovement;

            if (CanMoveTo(targetPosition))
            {
                characterController.Move(recoilMovement);
            }
            else
            {
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    void Die()
    {
        Debug.Log("玩家死亡！");
    }

    // ================== 网格 + 物理 的混合可走判定（右侧空气墙修复版） ==================

    bool CanMoveTo(Vector3 targetPosition)
    {
        bool hasGrid = (roomGenerator != null && roomGenerator.FloorGrid != null);

        // 先检查目标中心位置的砖块类型
        if (hasGrid)
        {
            if (TryGetFloorAtUnclamped(targetPosition, out Floor targetFloor, out bool targetInBounds))
            {
                if (targetInBounds && targetFloor != null)
                {
                    // ⭐ 拦截所有不可穿越的砖块类型
                    if (targetFloor.type == FloorType.Unwalkable ||
                        targetFloor.type == FloorType.UnwalkableTransparent)
                    {
                        return false;
                    }

                    // 如果是 Walkable，继续检测
                    if (targetFloor.type == FloorType.Walkable)
                    {
                        // 物理检测
                        if (HitsPhysicsBlocking(targetPosition)) return false;

                        // ⭐ 关键修复：8点边角检测 - 同时检查网格和物理
                        float r = collisionRadius;
                        Vector3[] checkPoints = new Vector3[8];
                        checkPoints[0] = targetPosition + new Vector3(r, 0, 0);
                        checkPoints[1] = targetPosition + new Vector3(-r, 0, 0);
                        checkPoints[2] = targetPosition + new Vector3(0, 0, r);
                        checkPoints[3] = targetPosition + new Vector3(0, 0, -r);
                        checkPoints[4] = targetPosition + new Vector3(r, 0, r);
                        checkPoints[5] = targetPosition + new Vector3(-r, 0, r);
                        checkPoints[6] = targetPosition + new Vector3(r, 0, -r);
                        checkPoints[7] = targetPosition + new Vector3(-r, 0, -r);

                        foreach (Vector3 p in checkPoints)
                        {
                            // 先检查物理
                            if (HitsPhysicsBlocking(p)) return false;

                            // ⭐ 新增：检查边角点的网格类型
                            if (TryGetFloorAtUnclamped(p, out Floor cornerFloor, out bool cornerInBounds))
                            {
                                if (cornerInBounds && cornerFloor != null)
                                {
                                    if (cornerFloor.type == FloorType.Unwalkable ||
                                        cornerFloor.type == FloorType.UnwalkableTransparent)
                                    {
                                        return false; // 边角进入不可穿越区域
                                    }
                                }
                            }
                        }
                        return true;
                    }
                }
                else if (!targetInBounds)
                {
                    // 出界处理
                    if (allowMoveWithoutTile)
                    {
                        if (HitsPhysicsBlocking(targetPosition)) return false;

                        float r = collisionRadius;
                        Vector3[] checkPoints = new Vector3[8];
                        checkPoints[0] = targetPosition + new Vector3(r, 0, 0);
                        checkPoints[1] = targetPosition + new Vector3(-r, 0, 0);
                        checkPoints[2] = targetPosition + new Vector3(0, 0, r);
                        checkPoints[3] = targetPosition + new Vector3(0, 0, -r);
                        checkPoints[4] = targetPosition + new Vector3(r, 0, r);
                        checkPoints[5] = targetPosition + new Vector3(-r, 0, r);
                        checkPoints[6] = targetPosition + new Vector3(r, 0, -r);
                        checkPoints[7] = targetPosition + new Vector3(-r, 0, -r);

                        foreach (Vector3 p in checkPoints)
                        {
                            if (HitsPhysicsBlocking(p)) return false;
                        }
                        return true;
                    }
                    return false;
                }
            }
        }

        // 没有网格系统的情况（兜底）
        if (!hasGrid)
        {
            if (HitsPhysicsBlocking(targetPosition)) return false;

            float r = collisionRadius;
            Vector3[] checkPoints = new Vector3[8];
            checkPoints[0] = targetPosition + new Vector3(r, 0, 0);
            checkPoints[1] = targetPosition + new Vector3(-r, 0, 0);
            checkPoints[2] = targetPosition + new Vector3(0, 0, r);
            checkPoints[3] = targetPosition + new Vector3(0, 0, -r);
            checkPoints[4] = targetPosition + new Vector3(r, 0, r);
            checkPoints[5] = targetPosition + new Vector3(-r, 0, r);
            checkPoints[6] = targetPosition + new Vector3(r, 0, -r);
            checkPoints[7] = targetPosition + new Vector3(-r, 0, -r);

            foreach (Vector3 p in checkPoints)
            {
                if (HitsPhysicsBlocking(p)) return false;
            }
            return true;
        }

        return false;
    }

    // ===== 物理层阻挡检测（与 CharacterController 胶囊精确对齐；支持扫掠+半径微调） =====
    bool HitsPhysicsBlocking(Vector3 targetPosition)
    {
        // 读取胶囊尺寸与中心
        float radius = (characterController != null) ? characterController.radius : 0.5f;
        float height = (characterController != null) ? characterController.height : 2f;
        Vector3 ccCenterWorld = transform.position + (characterController != null ? characterController.center : Vector3.up);

        // 当前端点
        Vector3 curBottom = ccCenterWorld + Vector3.up * (-(height * 0.5f) + radius);
        Vector3 curTop = ccCenterWorld + Vector3.up * (+(height * 0.5f) - radius);

        // 目标端点
        Vector3 tgtCenterWorld = targetPosition + (characterController != null ? characterController.center : Vector3.up);
        Vector3 tgtBottom = tgtCenterWorld + Vector3.up * (-(height * 0.5f) + radius);
        Vector3 tgtTop = tgtCenterWorld + Vector3.up * (+(height * 0.5f) - radius);

        QueryTriggerInteraction trig = checkTriggers ? QueryTriggerInteraction.Collide
                                                     : QueryTriggerInteraction.Ignore;

        // 与 CC.skinWidth 协同的检测半径（radiusBias 建议为负数，略微缩小检测）
        float ccSkin = (characterController != null) ? characterController.skinWidth : 0.08f;
        float shrink = Mathf.Max(0.0f, ccSkin * 0.5f) - radiusBias; // radiusBias<0 → 实际更小
        float castRadius = Mathf.Max(0.01f, radius - shrink);

        // 扫掠（防穿透，尽量贴边）
        Vector3 delta = targetPosition - transform.position;
        float distance = delta.magnitude;
        if (distance > 1e-4f)
        {
            if (Physics.CapsuleCast(curBottom, curTop, castRadius, delta.normalized, out RaycastHit hit, distance, blockingLayers, trig))
                return true;
        }

        // 可选：若仍希望兜底做目标重叠（可能更早拦截），把 sweepOnly 设为 false
        if (!sweepOnly)
        {
            if (Physics.CheckCapsule(tgtBottom, tgtTop, castRadius, blockingLayers, trig))
                return true;
        }

        return false;
    }

    // ================== 网格判定（保持你的原逻辑；供其他功能使用） ==================

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

    bool IsValidGrid(Vector2Int gridPos)
    {
        if (roomGenerator == null) return false;

        Vector2Int roomSize = roomGenerator.RoomSize;
        return gridPos.x >= 0 && gridPos.x < roomSize.x &&
               gridPos.y >= 0 && gridPos.y < roomSize.y;
    }

    // ================== 仅供本类内部使用：不改动你的 WorldToGrid，但能准确识别“无砖/越界” ==================

    bool TryGetFloorAtUnclamped(Vector3 worldPos, out Floor floor, out bool inBounds)
    {
        floor = null;
        inBounds = false;

        if (roomGenerator == null || roomGenerator.FloorGrid == null) return false;

        float tileSize = roomGenerator.TileSize;

        // ⭐ 修正：改用 RoundToInt，和 WorldToGrid 保持一致
        int x = Mathf.RoundToInt(worldPos.x / tileSize);
        int z = Mathf.RoundToInt(worldPos.z / tileSize);

        Vector2Int size = roomGenerator.RoomSize;
        if (x < 0 || z < 0 || x >= size.x || z >= size.y)
        {
            inBounds = false;
            return false;
        }

        inBounds = true;
        floor = roomGenerator.FloorGrid[x, z];
        return floor != null;
    }

    bool IsWalkableTileUnclamped(Vector3 worldPos)
    {
        if (!TryGetFloorAtUnclamped(worldPos, out Floor f, out bool inBounds)) return false;
        if (!inBounds) return false;
        return f.type == FloorType.Walkable;
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // 绘制玩家碰撞半径
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, collisionRadius);

        // 绘制朝向
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);
    }
}
