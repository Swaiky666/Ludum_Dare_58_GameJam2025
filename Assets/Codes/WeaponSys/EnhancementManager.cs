using UnityEngine;

/// <summary>
/// 强化管理器 - 管理左右手独立的强化数据
/// </summary>
public class EnhancementManager : MonoBehaviour
{
    private static EnhancementManager instance;
    public static EnhancementManager Instance => instance;

    [Header("强化数据")]
    [SerializeField] private EnhancementData leftHandEnhancement = new EnhancementData();
    [SerializeField] private EnhancementData rightHandEnhancement = new EnhancementData();

    [Header("玩家引用")]
    [SerializeField] private PlayerController playerController;

    private float baseMaxHealth;
    private float baseMaxShield;
    private float baseMoveSpeed;
    private float baseShieldRegenRate;
    private float baseArmor;
    private float baseDashCooldown;

    void Awake()
    {
        // 单例模式
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // 自动查找玩家
        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
        }

        if (playerController != null)
        {
            // 保存玩家基础属性
            CachePlayerBaseStats();

            // 初始应用强化
            ApplyPlayerEnhancements();
        }
        else
        {
            Debug.LogWarning("EnhancementManager: 未找到PlayerController！");
        }
    }

    /// <summary>
    /// 缓存玩家基础属性
    /// </summary>
    void CachePlayerBaseStats()
    {
        if (playerController == null) return;

        // 使用反射获取私有字段
        var playerType = playerController.GetType();

        var maxHealthField = playerType.GetField("maxHealth",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var maxShieldField = playerType.GetField("maxShield",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var moveSpeedField = playerType.GetField("moveSpeed",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var shieldRegenRateField = playerType.GetField("shieldRegenRate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var armorField = playerType.GetField("armor",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var dashCooldownField = playerType.GetField("dashCooldown",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (maxHealthField != null) baseMaxHealth = (float)maxHealthField.GetValue(playerController);
        if (maxShieldField != null) baseMaxShield = (float)maxShieldField.GetValue(playerController);
        if (moveSpeedField != null) baseMoveSpeed = (float)moveSpeedField.GetValue(playerController);
        if (shieldRegenRateField != null) baseShieldRegenRate = (float)shieldRegenRateField.GetValue(playerController);
        if (armorField != null) baseArmor = (float)armorField.GetValue(playerController);
        if (dashCooldownField != null) baseDashCooldown = (float)dashCooldownField.GetValue(playerController);

        Debug.Log($"<color=cyan>[EnhancementManager] 已缓存玩家基础属性 - HP:{baseMaxHealth}, Shield:{baseMaxShield}, Speed:{baseMoveSpeed}</color>");
    }

    /// <summary>
    /// 获取指定槽位的强化数据
    /// </summary>
    public EnhancementData GetEnhancement(int slot)
    {
        return slot == 0 ? leftHandEnhancement : rightHandEnhancement;
    }

    /// <summary>
    /// 应用玩家属性强化（合并左右手的强化效果）
    /// </summary>
    public void ApplyPlayerEnhancements()
    {
        if (playerController == null) return;

        // 合并左右手的玩家属性强化（取最大值或叠加）
        float finalHealthMult = Mathf.Max(leftHandEnhancement.healthMultiplier, rightHandEnhancement.healthMultiplier);
        float finalShieldMult = Mathf.Max(leftHandEnhancement.shieldMultiplier, rightHandEnhancement.shieldMultiplier);
        float finalSpeedMult = Mathf.Max(leftHandEnhancement.moveSpeedMultiplier, rightHandEnhancement.moveSpeedMultiplier);
        float finalRegenMult = Mathf.Max(leftHandEnhancement.shieldRegenMultiplier, rightHandEnhancement.shieldRegenMultiplier);
        float finalArmorMult = Mathf.Max(leftHandEnhancement.armorMultiplier, rightHandEnhancement.armorMultiplier);
        float finalCooldownReduction = Mathf.Max(leftHandEnhancement.dashCooldownReduction, rightHandEnhancement.dashCooldownReduction);

        // 使用反射修改玩家属性
        var playerType = playerController.GetType();

        SetPlayerField(playerType, "maxHealth", baseMaxHealth * finalHealthMult);
        SetPlayerField(playerType, "maxShield", baseMaxShield * finalShieldMult);
        SetPlayerField(playerType, "moveSpeed", baseMoveSpeed * finalSpeedMult);
        SetPlayerField(playerType, "shieldRegenRate", baseShieldRegenRate * finalRegenMult);
        SetPlayerField(playerType, "armor", baseArmor * finalArmorMult);
        SetPlayerField(playerType, "dashCooldown", Mathf.Max(0.1f, baseDashCooldown - finalCooldownReduction));

        Debug.Log($"<color=green>[EnhancementManager] 已应用玩家强化 - HP:{baseMaxHealth * finalHealthMult}, Speed:{baseMoveSpeed * finalSpeedMult}</color>");
    }

    /// <summary>
    /// 设置玩家字段值
    /// </summary>
    void SetPlayerField(System.Type playerType, string fieldName, float value)
    {
        var field = playerType.GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            field.SetValue(playerController, value);
        }
    }

    /// <summary>
    /// 添加武器强化（示例方法）
    /// </summary>
    public void AddWeaponEnhancement(int slot, string enhancementType, float value)
    {
        EnhancementData enhancement = GetEnhancement(slot);

        switch (enhancementType)
        {
            case "Damage":
                enhancement.damageMultiplier *= value;
                Debug.Log($"<color=yellow>槽位{slot} 伤害倍率提升至: {enhancement.damageMultiplier}</color>");
                break;
            case "FireRate":
                enhancement.fireRateMultiplier *= value;
                Debug.Log($"<color=yellow>槽位{slot} 攻速倍率提升至: {enhancement.fireRateMultiplier}</color>");
                break;
            case "BulletsPerShot":
                enhancement.bulletsPerShotBonus += (int)value;
                Debug.Log($"<color=yellow>槽位{slot} 额外子弹数量: {enhancement.bulletsPerShotBonus}</color>");
                break;
            case "Bounce":
                enhancement.bonusBounces += (int)value;
                Debug.Log($"<color=yellow>槽位{slot} 额外弹射次数: {enhancement.bonusBounces}</color>");
                break;
            case "SlowEffect":
                enhancement.slowMultiplierBonus += value;
                Debug.Log($"<color=yellow>槽位{slot} 减速效果加成: {enhancement.slowMultiplierBonus}</color>");
                break;
            case "BulletSpeed":
                enhancement.bulletSpeedMultiplier *= value;
                Debug.Log($"<color=yellow>槽位{slot} 子弹速度倍率提升至: {enhancement.bulletSpeedMultiplier}</color>");
                break;
            case "Homing":
                enhancement.enableHoming = true;
                Debug.Log($"<color=yellow>槽位{slot} 启用追踪</color>");
                break;
            case "Explosion":
                enhancement.enableExplosion = true;
                Debug.Log($"<color=yellow>槽位{slot} 启用爆炸</color>");
                break;
        }
    }

    /// <summary>
    /// 添加玩家属性强化（示例方法）
    /// </summary>
    public void AddPlayerEnhancement(int slot, string enhancementType, float value)
    {
        EnhancementData enhancement = GetEnhancement(slot);

        switch (enhancementType)
        {
            case "Health":
                enhancement.healthMultiplier *= value;
                ApplyPlayerEnhancements();
                Debug.Log($"<color=yellow>槽位{slot} 生命值倍率提升至: {enhancement.healthMultiplier}</color>");
                break;
            case "Shield":
                enhancement.shieldMultiplier *= value;
                ApplyPlayerEnhancements();
                Debug.Log($"<color=yellow>槽位{slot} 护盾倍率提升至: {enhancement.shieldMultiplier}</color>");
                break;
            case "MoveSpeed":
                enhancement.moveSpeedMultiplier *= value;
                ApplyPlayerEnhancements();
                Debug.Log($"<color=yellow>槽位{slot} 移动速度倍率提升至: {enhancement.moveSpeedMultiplier}</color>");
                break;
            case "ShieldRegen":
                enhancement.shieldRegenMultiplier *= value;
                ApplyPlayerEnhancements();
                Debug.Log($"<color=yellow>槽位{slot} 护盾恢复倍率提升至: {enhancement.shieldRegenMultiplier}</color>");
                break;
            case "Armor":
                enhancement.armorMultiplier *= value;
                ApplyPlayerEnhancements();
                Debug.Log($"<color=yellow>槽位{slot} 护甲倍率提升至: {enhancement.armorMultiplier}</color>");
                break;
            case "DashCooldown":
                enhancement.dashCooldownReduction += value;
                ApplyPlayerEnhancements();
                Debug.Log($"<color=yellow>槽位{slot} 冲刺冷却缩减: {enhancement.dashCooldownReduction}秒</color>");
                break;
        }
    }

    /// <summary>
    /// 重置所有强化（返回主菜单或退出游戏时调用）
    /// </summary>
    public void ResetAllEnhancements()
    {
        leftHandEnhancement.Reset();
        rightHandEnhancement.Reset();

        if (playerController != null)
        {
            ApplyPlayerEnhancements();
        }

        Debug.Log("<color=red>[EnhancementManager] 已重置所有强化</color>");
    }

    /// <summary>
    /// 在场景卸载时重置
    /// </summary>
    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}