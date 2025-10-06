using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Text;

/// <summary>
/// 武器详细信息面板 - 显示武器的详细属性，可接收拖放以清除武器
/// </summary>
public class WeaponDetailPanel : MonoBehaviour, IDropHandler
{
    [Header("Basic Info")]
    [SerializeField] private Image weaponIcon;                    // 武器大图标
    [SerializeField] private TextMeshProUGUI weaponNameText;      // 武器名称

    [Header("Hint Text")]
    [SerializeField] private TextMeshProUGUI hintText;            // 提示文本 "拖到此处丢弃"

    [Header("Stats Display")]
    [SerializeField] private TextMeshProUGUI damageText;          // 伤害
    [SerializeField] private TextMeshProUGUI fireRateText;        // 射速
    [SerializeField] private TextMeshProUGUI bulletsPerShotText;  // 每次射击子弹数
    [SerializeField] private TextMeshProUGUI bulletSpeedText;     // 子弹速度
    [SerializeField] private TextMeshProUGUI cooldownText;        // 冷却时间
    [SerializeField] private TextMeshProUGUI explosionDamageText; // 爆炸伤害
    [SerializeField] private TextMeshProUGUI maxBouncesText;      // 最大反弹次数
    [SerializeField] private TextMeshProUGUI piercingDamageText;  // 穿透伤害
    [SerializeField] private TextMeshProUGUI slowMultiplierText;  // 减速倍数

    // 武器清除事件（传递槽位索引）
    public event System.Action<int> OnWeaponDiscard;

    private int currentSlotIndex = -1; // 当前显示的槽位索引

    /// <summary>
    /// 显示武器详细信息（需要传入槽位索引以获取强化数据）
    /// </summary>
    public void ShowWeaponDetails(IEquippable weapon, int slotIndex)
    {
        if (weapon == null)
        {
            Debug.LogWarning("WeaponDetailPanel: Weapon is null!");
            return;
        }

        currentSlotIndex = slotIndex;

        // 隐藏提示文本
        if (hintText != null)
        {
            hintText.gameObject.SetActive(false);
            Debug.Log("<color=cyan>[ShowWeaponDetails] 已隐藏 HintText</color>");
        }
        else
        {
            Debug.LogError("<color=red>[ShowWeaponDetails] HintText 引用为空！请在Inspector中设置！</color>");
        }

        // 先清空所有特殊属性文本
        ClearSpecialStats();

        // 显示基本信息
        if (weaponIcon != null)
        {
            weaponIcon.sprite = weapon.Icon;
            weaponIcon.enabled = true;
        }

        if (weaponNameText != null)
        {
            weaponNameText.text = weapon.EquipmentName;
        }

        // 尝试获取Gun组件
        Gun gun = (weapon as MonoBehaviour)?.GetComponent<Gun>();
        if (gun != null)
        {
            ShowGunDetails(gun, slotIndex);
        }
        else
        {
            // 如果不是Gun，显示通用武器信息
            ShowGenericWeaponDetails(weapon, slotIndex);
        }
    }

    /// <summary>
    /// 清空所有显示信息（但不隐藏panel）- 显示提示文本
    /// </summary>
    public void ClearInfo()
    {
        currentSlotIndex = -1;

        // 显示提示文本
        if (hintText != null)
        {
            hintText.gameObject.SetActive(true);
        }

        // 清空图标
        if (weaponIcon != null)
        {
            weaponIcon.sprite = null;
            weaponIcon.enabled = false;
        }

        // 清空所有文本
        if (weaponNameText != null) weaponNameText.text = "";
        if (damageText != null) damageText.text = "";
        if (fireRateText != null) fireRateText.text = "";
        if (bulletsPerShotText != null) bulletsPerShotText.text = "";
        if (bulletSpeedText != null) bulletSpeedText.text = "";
        if (cooldownText != null) cooldownText.text = "";

        ClearSpecialStats();

        Debug.Log("<color=gray>[DetailPanel] 已清空所有信息，显示提示文本</color>");
    }

    /// <summary>
    /// 接收拖放 - 当槽位被拖到panel上时，清除该槽位的武器
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        // 获取被拖动的槽位
        WeaponSlot draggedSlot = eventData.pointerDrag?.GetComponent<WeaponSlot>();

        if (draggedSlot == null) return;

        int slotIndex = draggedSlot.GetSlotIndex();

        Debug.Log($"<color=red>[丢弃武器] 槽位{slotIndex} 的武器被拖到DetailPanel，准备清除</color>");

        // 触发武器清除事件
        OnWeaponDiscard?.Invoke(slotIndex);
    }

    /// <summary>
    /// 显示枪械详细信息（带强化数据）
    /// </summary>
    void ShowGunDetails(Gun gun, int slotIndex)
    {
        // 获取强化数据
        EnhancementData enhancement = null;
        if (EnhancementManager.Instance != null && slotIndex >= 0)
        {
            enhancement = EnhancementManager.Instance.GetEnhancement(slotIndex);
        }

        // 使用反射获取Gun的私有字段
        var gunType = gun.GetType();
        var weaponBaseType = gunType.BaseType; // WeaponBase

        // 从WeaponBase获取基础属性
        var damageField = weaponBaseType.GetField("damage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cooldownField = weaponBaseType.GetField("cooldown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // 从Gun获取枪械属性
        var bulletPrefabField = gunType.GetField("bulletPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var bulletSpeedField = gunType.GetField("bulletSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var bulletsPerShotField = gunType.GetField("bulletsPerShot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var fireRateField = gunType.GetField("fireRate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // 获取基础数值
        float baseDamage = damageField != null ? (float)damageField.GetValue(gun) : 0f;
        float baseFireRate = fireRateField != null ? (float)fireRateField.GetValue(gun) : 0f;
        int baseBulletsPerShot = bulletsPerShotField != null ? (int)bulletsPerShotField.GetValue(gun) : 0;
        float baseBulletSpeed = bulletSpeedField != null ? (float)bulletSpeedField.GetValue(gun) : 0f;
        float baseCooldown = cooldownField != null ? (float)cooldownField.GetValue(gun) : 0f;

        // 计算强化后的数值
        float enhancedDamage = baseDamage;
        float enhancedFireRate = baseFireRate;
        int enhancedBulletsPerShot = baseBulletsPerShot;
        float enhancedBulletSpeed = baseBulletSpeed;
        float enhancedCooldown = baseCooldown;

        if (enhancement != null)
        {
            enhancedDamage = baseDamage * enhancement.damageMultiplier;
            enhancedFireRate = baseFireRate * enhancement.fireRateMultiplier;
            enhancedBulletsPerShot = Mathf.RoundToInt(baseBulletsPerShot * enhancement.bulletsPerShotMultiplier);
            enhancedBulletSpeed = baseBulletSpeed * enhancement.bulletSpeedMultiplier;
            enhancedCooldown = baseCooldown / enhancement.fireRateMultiplier;
        }

        // 显示伤害
        if (damageText != null)
        {
            if (enhancement != null && enhancement.damageMultiplier != 1f)
            {
                float damageBonus = (enhancement.damageMultiplier - 1f) * 100f;
                damageText.text = $"Damage: {enhancedDamage:F1} (+{damageBonus:F0}%)";
            }
            else
            {
                damageText.text = $"Damage: {baseDamage:F1}";
            }
        }

        // 显示射速
        if (fireRateText != null)
        {
            if (enhancement != null && enhancement.fireRateMultiplier != 1f)
            {
                float fireRateBonus = (enhancement.fireRateMultiplier - 1f) * 100f;
                fireRateText.text = $"Fire Rate: {enhancedFireRate:F2} /s (+{fireRateBonus:F0}%)";
            }
            else
            {
                fireRateText.text = $"Fire Rate: {baseFireRate:F2} /s";
            }
        }

        // 显示每次子弹数
        if (bulletsPerShotText != null)
        {
            if (enhancement != null && enhancement.bulletsPerShotMultiplier != 1f)
            {
                bulletsPerShotText.text = $"Bullets Per Shot: {enhancedBulletsPerShot} (x{enhancement.bulletsPerShotMultiplier:F1})";
            }
            else
            {
                bulletsPerShotText.text = $"Bullets Per Shot: {baseBulletsPerShot}";
            }
        }

        // 显示子弹速度
        if (bulletSpeedText != null)
        {
            if (enhancement != null && enhancement.bulletSpeedMultiplier != 1f)
            {
                float speedBonus = (enhancement.bulletSpeedMultiplier - 1f) * 100f;
                string sign = speedBonus >= 0 ? "+" : "";
                bulletSpeedText.text = $"Bullet Speed: {enhancedBulletSpeed:F1} ({sign}{speedBonus:F0}%)";
            }
            else
            {
                bulletSpeedText.text = $"Bullet Speed: {baseBulletSpeed:F1}";
            }
        }

        // 显示冷却时间
        if (cooldownText != null)
        {
            if (enhancement != null && enhancement.fireRateMultiplier != 1f)
            {
                float cooldownReduction = (1f - 1f / enhancement.fireRateMultiplier) * 100f;
                cooldownText.text = $"Cooldown: {enhancedCooldown:F2}s (-{cooldownReduction:F0}%)";
            }
            else
            {
                cooldownText.text = $"Cooldown: {baseCooldown:F2}s";
            }
        }

        // 获取子弹预制体，分析特殊属性
        if (bulletPrefabField != null)
        {
            GameObject bulletPrefab = bulletPrefabField.GetValue(gun) as GameObject;
            if (bulletPrefab != null)
            {
                Bullet bullet = bulletPrefab.GetComponent<Bullet>();
                if (bullet != null)
                {
                    ShowBulletSpecialStats(bullet, enhancement, baseDamage);
                }
            }
        }
    }

    /// <summary>
    /// 显示子弹特殊属性（带强化数据）
    /// </summary>
    void ShowBulletSpecialStats(Bullet bullet, EnhancementData enhancement, float baseDamage)
    {
        var bulletType = bullet.GetType();

        // 检查爆炸属性
        var isExplosiveField = bulletType.GetField("isExplosive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        bool isExplosive = isExplosiveField != null && (bool)isExplosiveField.GetValue(bullet);

        // 强化可能启用爆炸
        if (enhancement != null && enhancement.enableExplosion)
        {
            isExplosive = true;
        }

        if (isExplosive)
        {
            var explosionDamageField = bulletType.GetField("explosionDamage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (explosionDamageText != null && explosionDamageField != null)
            {
                float baseExplosionDamage = (float)explosionDamageField.GetValue(bullet);

                if (enhancement != null && enhancement.damageMultiplier != 1f)
                {
                    float enhancedExplosionDamage = baseExplosionDamage * enhancement.damageMultiplier;
                    float explosionBonus = (enhancement.damageMultiplier - 1f) * 100f;
                    explosionDamageText.text = $"Explosion Damage: {enhancedExplosionDamage:F1} (+{explosionBonus:F0}%)";
                }
                else
                {
                    explosionDamageText.text = $"Explosion Damage: {baseExplosionDamage:F1}";
                }
            }
        }

        // 检查反弹属性
        var isBouncyField = bulletType.GetField("isBouncy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        bool isBouncy = isBouncyField != null && (bool)isBouncyField.GetValue(bullet);

        // 强化可能启用弹射
        if (enhancement != null && enhancement.bonusBounces > 0)
        {
            isBouncy = true;
        }

        if (isBouncy)
        {
            var maxBouncesField = bulletType.GetField("maxBounces", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (maxBouncesText != null && maxBouncesField != null)
            {
                int baseMaxBounces = (int)maxBouncesField.GetValue(bullet);

                if (enhancement != null && enhancement.bonusBounces > 0)
                {
                    int enhancedMaxBounces = baseMaxBounces + enhancement.bonusBounces;
                    maxBouncesText.text = $"Maximum Bounces: {enhancedMaxBounces} (+{enhancement.bonusBounces})";
                }
                else
                {
                    maxBouncesText.text = $"Maximum Bounces: {baseMaxBounces}";
                }
            }
        }

        // 检查穿透属性（只显示，不能强化）
        var isPiercingField = bulletType.GetField("isPiercing", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (isPiercingField != null && (bool)isPiercingField.GetValue(bullet))
        {
            var piercingDamageField = bulletType.GetField("piercingDamage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (piercingDamageText != null && piercingDamageField != null)
            {
                float basePiercingDamage = (float)piercingDamageField.GetValue(bullet);

                if (enhancement != null && enhancement.damageMultiplier != 1f)
                {
                    float enhancedPiercingDamage = basePiercingDamage * enhancement.damageMultiplier;
                    float piercingBonus = (enhancement.damageMultiplier - 1f) * 100f;
                    piercingDamageText.text = $"Piercing Damage: {enhancedPiercingDamage:F1} (+{piercingBonus:F0}%)";
                }
                else
                {
                    piercingDamageText.text = $"Piercing Damage: {basePiercingDamage:F1}";
                }
            }
        }

        // 检查减速属性
        var hasSlowEffectField = bulletType.GetField("hasSlowEffect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        bool hasSlowEffect = hasSlowEffectField != null && (bool)hasSlowEffectField.GetValue(bullet);

        // 强化可能启用减速
        if (enhancement != null && enhancement.slowMultiplierBonus > 0)
        {
            hasSlowEffect = true;
        }

        if (hasSlowEffect)
        {
            var slowMultiplierField = bulletType.GetField("slowMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (slowMultiplierText != null && slowMultiplierField != null)
            {
                float baseSlowMultiplier = (float)slowMultiplierField.GetValue(bullet);

                if (enhancement != null && enhancement.slowMultiplierBonus > 0)
                {
                    float enhancedSlowMultiplier = baseSlowMultiplier * (1f - enhancement.slowMultiplierBonus);
                    enhancedSlowMultiplier = Mathf.Clamp(enhancedSlowMultiplier, 0.1f, 1f);
                    float slowBonus = enhancement.slowMultiplierBonus * 100f;
                    slowMultiplierText.text = $"Slow Multiplier: {(enhancedSlowMultiplier * 100):F0}% (+{slowBonus:F0}% stronger)";
                }
                else
                {
                    slowMultiplierText.text = $"Slow Multiplier: {(baseSlowMultiplier * 100):F0}%";
                }
            }
        }
    }

    /// <summary>
    /// 显示通用武器信息（非枪械）
    /// </summary>
    void ShowGenericWeaponDetails(IEquippable weapon, int slotIndex)
    {
        // 显示基本信息
        if (damageText != null)
        {
            damageText.text = "Damage: N/A";
        }

        if (fireRateText != null)
        {
            fireRateText.text = "Fire Rate: N/A";
        }

        if (bulletsPerShotText != null)
        {
            bulletsPerShotText.text = "Bullets Per Shot: N/A";
        }

        if (bulletSpeedText != null)
        {
            bulletSpeedText.text = "Bullet Speed: N/A";
        }

        if (cooldownText != null)
        {
            cooldownText.text = $"Cooldown: {weapon.Cooldown:F2}s";
        }
    }

    /// <summary>
    /// 清空所有特殊属性文本
    /// </summary>
    void ClearSpecialStats()
    {
        if (explosionDamageText != null)
        {
            explosionDamageText.text = "";
        }

        if (maxBouncesText != null)
        {
            maxBouncesText.text = "";
        }

        if (piercingDamageText != null)
        {
            piercingDamageText.text = "";
        }

        if (slowMultiplierText != null)
        {
            slowMultiplierText.text = "";
        }
    }
}