using UnityEngine;

/// <summary>
/// 玩家控制器（完整版 - 包含移动、朝向鼠标、装备系统、后坐力、震动等）
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

    bool CanMoveTo(Vector3 targetPosition)
    {
        if (roomGenerator == null || roomGenerator.FloorGrid == null)
        {
            return true;
        }

        if (!IsPositionWalkable(targetPosition))
        {
            return false;
        }

        Vector3[] checkPoints = new Vector3[8];
        checkPoints[0] = targetPosition + new Vector3(collisionRadius, 0, 0);
        checkPoints[1] = targetPosition + new Vector3(-collisionRadius, 0, 0);
        checkPoints[2] = targetPosition + new Vector3(0, 0, collisionRadius);
        checkPoints[3] = targetPosition + new Vector3(0, 0, -collisionRadius);
        checkPoints[4] = targetPosition + new Vector3(collisionRadius, 0, collisionRadius);
        checkPoints[5] = targetPosition + new Vector3(-collisionRadius, 0, collisionRadius);
        checkPoints[6] = targetPosition + new Vector3(collisionRadius, 0, -collisionRadius);
        checkPoints[7] = targetPosition + new Vector3(-collisionRadius, 0, -collisionRadius);

        foreach (Vector3 point in checkPoints)
        {
            if (!IsPositionWalkable(point))
            {
                return false;
            }
        }

        return true;
    }

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