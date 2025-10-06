using UnityEngine;

/// <summary>
/// 强化数据 - 存储单个强化器的所有强化效果
/// </summary>
[System.Serializable]
public class EnhancementData
{
    [Header("武器强化")]
    public float damageMultiplier = 1f;           // 伤害倍率（默认1.0，1.8表示+80%）
    public int bonusBounces = 0;                  // 额外弹射次数（加算）
    public float fireRateMultiplier = 1f;         // 攻速倍率（默认1.0，1.5表示+50%）
    public float bulletsPerShotMultiplier = 1f;   // 每次子弹数量倍率（默认1.0，3.0表示x3）
    public float slowMultiplierBonus = 0f;        // 减速权重加成（0.2表示减速效果+20%）
    public float bulletSpeedMultiplier = 1f;      // 子弹速度倍率（默认1.0，1.3表示+30%）
    public bool enableHoming = false;             // 启用追踪
    public bool enableExplosion = false;          // 启用爆炸

    [Header("玩家属性强化")]
    public float healthMultiplier = 1f;           // 生命值倍率
    public float shieldMultiplier = 1f;           // 护盾倍率
    public float moveSpeedMultiplier = 1f;        // 移动速度倍率
    public float shieldRegenMultiplier = 1f;      // 护盾恢复速度倍率
    public float armorMultiplier = 1f;            // 护甲倍率
    public float dashCooldownReduction = 0f;      // 冲刺冷却缩减（秒）

    /// <summary>
    /// 重置所有强化
    /// </summary>
    public void Reset()
    {
        // 武器强化
        damageMultiplier = 1f;
        bonusBounces = 0;
        fireRateMultiplier = 1f;
        bulletsPerShotMultiplier = 1f;
        slowMultiplierBonus = 0f;
        bulletSpeedMultiplier = 1f;
        enableHoming = false;
        enableExplosion = false;

        // 玩家属性强化
        healthMultiplier = 1f;
        shieldMultiplier = 1f;
        moveSpeedMultiplier = 1f;
        shieldRegenMultiplier = 1f;
        armorMultiplier = 1f;
        dashCooldownReduction = 0f;
    }

    /// <summary>
    /// 克隆当前强化数据
    /// </summary>
    public EnhancementData Clone()
    {
        return (EnhancementData)this.MemberwiseClone();
    }
}