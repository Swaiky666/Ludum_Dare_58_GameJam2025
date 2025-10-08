using UnityEngine;

/// <summary>
/// 玩家控制器（完整版 - 包含移动、朝向鼠标、装备系统、后坐力、震动、死亡处理等）
/// + 混合判定：网格可走性（地砖） & 指定Layer的物理体阻挡
/// + 无砖块兜底：脚下无砖块时依然可移动，但仍受物理阻挡
/// + 死亡返回主菜单：显示"YOU DIED"并自动返回
/// + ⭐ 修复：使用RoomGenerator的坐标转换，正确处理房间偏移
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
    [SerializeField] private float rotationSpeed = 720f;

    [Header("References")]
    [SerializeField] private RoomGenerator roomGenerator;
    [SerializeField] private float collisionRadius = 0.4f;
    [SerializeField] private EquipmentManager equipmentManager;
    [SerializeField] private Camera mainCamera;

    [Header("Camera Shake")]
    [SerializeField] private float damageShakeDuration = 0.3f;
    [SerializeField] private float damageShakeMagnitude = 0.2f;

    [Header("Physics Blocking (Layers)")]
    [Tooltip("会阻挡玩家的层（Walls/Environment/Props/Enemies 等）")]
    [SerializeField] private LayerMask blockingLayers;
    [Tooltip("是否把触发器也当成阻挡")]
    [SerializeField] private bool checkTriggers = false;
    [Tooltip("仅用扫掠，避免目标重叠提前拦截导致可见缝隙大")]
    [SerializeField] private bool sweepOnly = true;
    [Tooltip("检测半径微调（负值可让视觉更贴墙，建议 -0.01 ~ -0.03）")]
    [SerializeField] private float radiusBias = -0.01f;

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
    private bool canRotate = true;
    private CameraShake cameraShake;

    // 死亡UI相关
    private bool showDeathScreen = false;
    private float deathScreenAlpha = 0f;

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
    /// 绘制死亡画面
    /// </summary>
    void OnGUI()
    {
        if (!showDeathScreen) return;

        // 保存原始颜色
        Color originalColor = GUI.color;

        // 绘制半透明黑色背景
        GUI.color = new Color(0, 0, 0, deathScreenAlpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        // 设置文字颜色为白色（带透明度）
        GUI.color = new Color(1, 1, 1, deathScreenAlpha);

        // 设置文字样式
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = Mathf.RoundToInt(Screen.height * 0.1f);
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.white;
        style.fontStyle = FontStyle.Bold;

        // 绘制"YOU DIED"文字在屏幕中心
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "YOU DIED", style);

        // 恢复原始颜色
        GUI.color = originalColor;
    }

    void HandleRotation()
    {
        if (!canRotate || mainCamera == null) return;

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

    void HandleShooting()
    {
        if (equipmentManager == null) return;

        Vector3 shootDirection = transform.forward;
        Vector3 shootOrigin = transform.position;

        if (Input.GetMouseButton(0))
        {
            equipmentManager.UseEquipment(0, shootDirection, shootOrigin);
        }

        if (Input.GetMouseButton(1))
        {
            equipmentManager.UseEquipment(1, shootDirection, shootOrigin);
        }
    }

    void HandleMovement()
    {
        if (isDashing) return;

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

    System.Collections.IEnumerator ApplyKnockback(Vector3 direction, float force)
    {
        canRotate = false;
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

        canRotate = true;
    }

    public void ApplyRecoil(Vector3 recoilDirection, float recoilForce)
    {
        if (isDashing) return;
        StartCoroutine(ApplyRecoilEffect(recoilDirection, recoilForce));
    }

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

    /// <summary>
    /// 玩家死亡处理
    /// </summary>
    void Die()
    {
        Debug.Log("<color=red>玩家死亡！</color>");

        // 禁用玩家控制
        enabled = false;

        // 启动死亡处理协程
        StartCoroutine(HandleDeath());
    }

    /// <summary>
    /// 死亡处理协程
    /// </summary>
    System.Collections.IEnumerator HandleDeath()
    {
        // 显示死亡画面
        showDeathScreen = true;

        // 淡入效果（0.5秒）
        float fadeInDuration = 0.5f;
        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            deathScreenAlpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }

        deathScreenAlpha = 1f;

        // 暂停游戏并等待2秒
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(2f);

        // 淡出效果（0.5秒）
        float fadeOutDuration = 0.5f;
        elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            deathScreenAlpha = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
            yield return null;
        }

        showDeathScreen = false;
        deathScreenAlpha = 0f;

        // 恢复时间流速
        Time.timeScale = 1f;

        // 保存武器和重置系统
        SaveEquippedWeaponToCollection();

        if (EnhancementManager.Instance != null)
        {
            EnhancementManager.Instance.ResetAllEnhancements();
        }

        if (DifficultyManager.Instance != null)
        {
            DifficultyManager.Instance.ResetDifficulty();
        }

        // 返回主菜单
        if (GameSceneManager.Instance != null)
        {
            Debug.Log("<color=yellow>返回主菜单...</color>");
            GameSceneManager.Instance.LoadMainMenu();
        }
        else
        {
            Debug.LogError("GameSceneManager.Instance 为空！无法返回主菜单");
        }
    }

    /// <summary>
    /// 保存武器到收集系统
    /// </summary>
    void SaveEquippedWeaponToCollection()
    {
        if (equipmentManager == null) return;

        int savedCount = 0;

        // 保存左手装备（槽位0）
        IEquippable leftEquipment = equipmentManager.GetEquipment(0);
        GameObject leftPrefab = equipmentManager.GetEquipmentPrefab(0);
        if (leftEquipment != null && leftPrefab != null)
        {
            SaveWeaponFromPrefab(leftPrefab, "左手");
            savedCount++;
        }

        // 保存右手装备（槽位1）
        IEquippable rightEquipment = equipmentManager.GetEquipment(1);
        GameObject rightPrefab = equipmentManager.GetEquipmentPrefab(1);
        if (rightEquipment != null && rightPrefab != null)
        {
            SaveWeaponFromPrefab(rightPrefab, "右手");
            savedCount++;
        }

        if (savedCount > 0)
        {
            Debug.Log($"<color=cyan>玩家死亡 - 共保存了 {savedCount} 个武器到收集系统</color>");
        }
    }

    void SaveWeaponFromPrefab(GameObject weaponPrefab, string slotName)
    {
        if (weaponPrefab == null || CollectionManager.Instance == null)
            return;

        var allCollectibles = CollectionManager.Instance.GetAllCollectibles();

        foreach (CollectibleData collectible in allCollectibles)
        {
            if (collectible != null && collectible.prefab == weaponPrefab)
            {
                CollectionManager.Instance.CollectItem(collectible.id);
                Debug.Log($"<color=green>✓ {slotName}武器 {collectible.itemName} 已记录到收集系统</color>");
                return;
            }
        }
    }

    bool CanMoveTo(Vector3 targetPosition)
    {
        bool hasGrid = (roomGenerator != null && roomGenerator.FloorGrid != null);

        if (hasGrid)
        {
            if (TryGetFloorAtUnclamped(targetPosition, out Floor targetFloor, out bool targetInBounds))
            {
                if (targetInBounds && targetFloor != null)
                {
                    if (targetFloor.type == FloorType.Unwalkable ||
                        targetFloor.type == FloorType.UnwalkableTransparent)
                    {
                        return false;
                    }

                    if (targetFloor.type == FloorType.Walkable)
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

                            if (TryGetFloorAtUnclamped(p, out Floor cornerFloor, out bool cornerInBounds))
                            {
                                if (cornerInBounds && cornerFloor != null)
                                {
                                    if (cornerFloor.type == FloorType.Unwalkable ||
                                        cornerFloor.type == FloorType.UnwalkableTransparent)
                                    {
                                        return false;
                                    }
                                }
                            }
                        }
                        return true;
                    }
                }
                else if (!targetInBounds)
                {
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

    bool HitsPhysicsBlocking(Vector3 targetPosition)
    {
        float radius = (characterController != null) ? characterController.radius : 0.5f;
        float height = (characterController != null) ? characterController.height : 2f;
        Vector3 ccCenterWorld = transform.position + (characterController != null ? characterController.center : Vector3.up);

        Vector3 curBottom = ccCenterWorld + Vector3.up * (-(height * 0.5f) + radius);
        Vector3 curTop = ccCenterWorld + Vector3.up * (+(height * 0.5f) - radius);

        Vector3 tgtCenterWorld = targetPosition + (characterController != null ? characterController.center : Vector3.up);
        Vector3 tgtBottom = tgtCenterWorld + Vector3.up * (-(height * 0.5f) + radius);
        Vector3 tgtTop = tgtCenterWorld + Vector3.up * (+(height * 0.5f) - radius);

        QueryTriggerInteraction trig = checkTriggers ? QueryTriggerInteraction.Collide
                                                     : QueryTriggerInteraction.Ignore;

        float ccSkin = (characterController != null) ? characterController.skinWidth : 0.08f;
        float shrink = Mathf.Max(0.0f, ccSkin * 0.5f) - radiusBias;
        float castRadius = Mathf.Max(0.01f, radius - shrink);

        Vector3 delta = targetPosition - transform.position;
        float distance = delta.magnitude;
        if (distance > 1e-4f)
        {
            if (Physics.CapsuleCast(curBottom, curTop, castRadius, delta.normalized, out RaycastHit hit, distance, blockingLayers, trig))
                return true;
        }

        if (!sweepOnly)
        {
            if (Physics.CheckCapsule(tgtBottom, tgtTop, castRadius, blockingLayers, trig))
                return true;
        }

        return false;
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

    /// <summary>
    /// ⭐ 修复：使用RoomGenerator的坐标转换（已处理偏移）
    /// </summary>
    Vector2Int WorldToGrid(Vector3 worldPos)
    {
        if (roomGenerator == null)
        {
            Debug.LogWarning("RoomGenerator为空，无法转换坐标！");
            return Vector2Int.zero;
        }

        // ⭐ 使用RoomGenerator的公共方法，它会正确处理偏移
        return roomGenerator.WorldToGridPublic(worldPos);
    }

    bool IsValidGrid(Vector2Int gridPos)
    {
        if (roomGenerator == null) return false;

        Vector2Int roomSize = roomGenerator.RoomSize;
        return gridPos.x >= 0 && gridPos.x < roomSize.x &&
               gridPos.y >= 0 && gridPos.y < roomSize.y;
    }

    /// <summary>
    /// ⭐ 修复：使用RoomGenerator的坐标转换获取地板信息
    /// </summary>
    bool TryGetFloorAtUnclamped(Vector3 worldPos, out Floor floor, out bool inBounds)
    {
        floor = null;
        inBounds = false;

        if (roomGenerator == null || roomGenerator.FloorGrid == null)
        {
            return false;
        }

        // ⭐ 使用RoomGenerator的坐标转换（已处理偏移）
        Vector2Int gridPos = roomGenerator.WorldToGridPublic(worldPos);

        Vector2Int size = roomGenerator.RoomSize;

        // 检查是否在范围内
        if (gridPos.x < 0 || gridPos.y < 0 || gridPos.x >= size.x || gridPos.y >= size.y)
        {
            inBounds = false;
            return false;
        }

        inBounds = true;
        floor = roomGenerator.FloorGrid[gridPos.x, gridPos.y];
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

        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, collisionRadius);

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);
    }
}