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

    /// <summary>
    /// 显示武器详细信息
    /// </summary>
    public void ShowWeaponDetails(IEquippable weapon)
    {
        if (weapon == null)
        {
            Debug.LogWarning("WeaponDetailPanel: Weapon is null!");
            return;
        }

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
            ShowGunDetails(gun);
        }
        else
        {
            // 如果不是Gun，显示通用武器信息
            ShowGenericWeaponDetails(weapon);
        }
    }

    /// <summary>
    /// 清空所有显示信息（但不隐藏panel）- 显示提示文本
    /// </summary>
    public void ClearInfo()
    {
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
    /// 显示枪械详细信息
    /// </summary>
    void ShowGunDetails(Gun gun)
    {
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

        // 显示基础属性
        if (damageText != null && damageField != null)
        {
            float damage = (float)damageField.GetValue(gun);
            damageText.text = $"Damage: {damage:F1}";
        }

        if (fireRateText != null && fireRateField != null)
        {
            float fireRate = (float)fireRateField.GetValue(gun);
            fireRateText.text = $"Fire Rate: {fireRate:F2} /s";
        }

        if (bulletsPerShotText != null && bulletsPerShotField != null)
        {
            int bulletsPerShot = (int)bulletsPerShotField.GetValue(gun);
            bulletsPerShotText.text = $"Bullets Per Shot: {bulletsPerShot}";
        }

        if (bulletSpeedText != null && bulletSpeedField != null)
        {
            float bulletSpeed = (float)bulletSpeedField.GetValue(gun);
            bulletSpeedText.text = $"Bullet Speed: {bulletSpeed:F1}";
        }

        if (cooldownText != null && cooldownField != null)
        {
            float cooldown = (float)cooldownField.GetValue(gun);
            cooldownText.text = $"Cooldown: {cooldown:F2}s";
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
                    ShowBulletSpecialStats(bullet);
                }
            }
        }
    }

    /// <summary>
    /// 显示子弹特殊属性
    /// </summary>
    void ShowBulletSpecialStats(Bullet bullet)
    {
        var bulletType = bullet.GetType();

        // 检查爆炸属性
        var isExplosiveField = bulletType.GetField("isExplosive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (isExplosiveField != null && (bool)isExplosiveField.GetValue(bullet))
        {
            var explosionDamageField = bulletType.GetField("explosionDamage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (explosionDamageText != null && explosionDamageField != null)
            {
                float explosionDamage = (float)explosionDamageField.GetValue(bullet);
                explosionDamageText.text = $"Explosion Damage: {explosionDamage:F1}";
            }
        }

        // 检查反弹属性
        var isBouncyField = bulletType.GetField("isBouncy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (isBouncyField != null && (bool)isBouncyField.GetValue(bullet))
        {
            var maxBouncesField = bulletType.GetField("maxBounces", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (maxBouncesText != null && maxBouncesField != null)
            {
                int maxBounces = (int)maxBouncesField.GetValue(bullet);
                maxBouncesText.text = $"Maximum Bounces: {maxBounces}";
            }
        }

        // 检查穿透属性
        var isPiercingField = bulletType.GetField("isPiercing", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (isPiercingField != null && (bool)isPiercingField.GetValue(bullet))
        {
            var piercingDamageField = bulletType.GetField("piercingDamage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (piercingDamageText != null && piercingDamageField != null)
            {
                float piercingDamage = (float)piercingDamageField.GetValue(bullet);
                piercingDamageText.text = $"Piercing Damage: {piercingDamage:F1}";
            }
        }

        // 检查减速属性
        var hasSlowEffectField = bulletType.GetField("hasSlowEffect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (hasSlowEffectField != null && (bool)hasSlowEffectField.GetValue(bullet))
        {
            var slowMultiplierField = bulletType.GetField("slowMultiplier", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (slowMultiplierText != null && slowMultiplierField != null)
            {
                float slowMultiplier = (float)slowMultiplierField.GetValue(bullet);
                slowMultiplierText.text = $"Slow Multiplier: {(slowMultiplier * 100):F0}%";
            }
        }
    }

    /// <summary>
    /// 显示通用武器信息（非枪械）
    /// </summary>
    void ShowGenericWeaponDetails(IEquippable weapon)
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