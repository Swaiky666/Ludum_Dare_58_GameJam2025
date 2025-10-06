using UnityEngine;

/// <summary>
/// 枪械武器（完整版 - 包含后坐力和相机震动）
/// </summary>
public class Gun : WeaponBase
{
    [Header("Gun Settings")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletSpeed = 20f;
    [SerializeField] private int bulletsPerShot = 1;        // 每次射击的子弹数
    [SerializeField] private float spreadAngle = 0f;        // 散射角度

    [Header("Fire Rate")]
    [SerializeField] private float fireRate = 2f;           // 射速：每秒射击次数（新增）

    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem muzzleFlash;

    [Header("Camera Shake")]
    [SerializeField] private float shakeDuration = 0.1f;
    [SerializeField] private float shakeMagnitude = 0.05f;

    private CameraShake cameraShake;

    protected override void Awake()
    {
        base.Awake();

        // 根据射速计算冷却时间
        cooldown = 1f / fireRate;

        // 获取相机震动组件
        if (Camera.main != null)
        {
            cameraShake = Camera.main.GetComponent<CameraShake>();
            if (cameraShake == null)
            {
                cameraShake = Camera.main.gameObject.AddComponent<CameraShake>();
            }
        }
    }

    void OnValidate()
    {
        // 在编辑器中修改 fireRate 时自动更新 cooldown
        if (fireRate > 0)
        {
            cooldown = 1f / fireRate;
        }
    }

    public override void Use(Vector3 direction, Vector3 origin)
    {
        if (!CanUse()) return;

        // 获取强化数据
        EnhancementData enhancement = null;
        if (EnhancementManager.Instance != null && slotIndex >= 0)
        {
            enhancement = EnhancementManager.Instance.GetEnhancement(slotIndex);
        }

        // 应用强化：计算实际发射参数
        float enhancedDamage = damage;
        int enhancedBulletsPerShot = bulletsPerShot;
        float enhancedSpreadAngle = spreadAngle;

        if (enhancement != null)
        {
            // 伤害强化
            enhancedDamage *= enhancement.damageMultiplier;

            // 子弹数量强化
            enhancedBulletsPerShot = Mathf.RoundToInt(bulletsPerShot * enhancement.bulletsPerShotMultiplier);

            // 如果子弹数量不是1，修改散射角度为8度
            if (enhancedBulletsPerShot != 1 && spreadAngle == 0f)
            {
                enhancedSpreadAngle = 8f;
            }
        }

        // 发射子弹
        for (int i = 0; i < enhancedBulletsPerShot; i++)
        {
            Vector3 shootDirection = direction;

            // 添加散射
            if (enhancedSpreadAngle > 0)
            {
                float angle = Random.Range(-enhancedSpreadAngle, enhancedSpreadAngle);
                shootDirection = Quaternion.Euler(0, angle, 0) * direction;
            }

            // 实例化子弹
            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.LookRotation(shootDirection));

            Bullet bulletScript = bullet.GetComponent<Bullet>();
            if (bulletScript != null)
            {
                // 初始化子弹基础属性
                bulletScript.Initialize(shootDirection, bulletSpeed, enhancedDamage, enemyKnockbackForce);

                // 应用强化效果到子弹
                if (enhancement != null)
                {
                    ApplyEnhancementToBullet(bulletScript, enhancement, enhancedDamage);
                }
            }
        }

        // 应用后坐力到玩家
        ApplyRecoilToPlayer(direction);

        // 相机震动
        if (cameraShake != null)
        {
            cameraShake.Shake(shakeDuration, shakeMagnitude);
        }

        // 特效
        if (muzzleFlash != null)
        {
            muzzleFlash.Play();
        }

        // 音效
        PlayFireSound();

        // 重置冷却（应用攻速强化）
        float enhancedCooldown = cooldown;
        if (enhancement != null)
        {
            enhancedCooldown = cooldown / enhancement.fireRateMultiplier;
        }
        currentCooldown = enhancedCooldown;

        Debug.Log($"{weaponName} 开火！伤害:{enhancedDamage:F1}, 子弹数:{enhancedBulletsPerShot}, 冷却:{enhancedCooldown:F2}s");
    }

    /// <summary>
    /// 应用强化效果到子弹
    /// </summary>
    void ApplyEnhancementToBullet(Bullet bullet, EnhancementData enhancement, float enhancedDamage)
    {
        var bulletType = bullet.GetType();

        // 1. 爆炸伤害强化
        var isExplosiveField = bulletType.GetField("isExplosive",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var explosionDamageField = bulletType.GetField("explosionDamage",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        bool wasExplosive = (bool)isExplosiveField.GetValue(bullet);

        if (enhancement.enableExplosion && !wasExplosive)
        {
            isExplosiveField.SetValue(bullet, true);
            Debug.Log($"<color=orange>[强化] 启用爆炸效果</color>");
        }

        if ((bool)isExplosiveField.GetValue(bullet) && explosionDamageField != null)
        {
            float originalExplosionDamage = (float)explosionDamageField.GetValue(bullet);
            explosionDamageField.SetValue(bullet, originalExplosionDamage * enhancement.damageMultiplier);
        }

        // 2. 穿透伤害强化
        var isPiercingField = bulletType.GetField("isPiercing",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var piercingDamageField = bulletType.GetField("piercingDamage",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        bool wasPiercing = (bool)isPiercingField.GetValue(bullet);

        if (enhancement.enablePiercing && !wasPiercing)
        {
            isPiercingField.SetValue(bullet, true);
            Debug.Log($"<color=orange>[强化] 启用穿透效果</color>");
        }

        if ((bool)isPiercingField.GetValue(bullet) && piercingDamageField != null)
        {
            float originalPiercingDamage = (float)piercingDamageField.GetValue(bullet);
            piercingDamageField.SetValue(bullet, originalPiercingDamage * enhancement.damageMultiplier);
        }

        // 3. 弹射次数强化
        var isBouncyField = bulletType.GetField("isBouncy",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var maxBouncesField = bulletType.GetField("maxBounces",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        bool wasBouncy = (bool)isBouncyField.GetValue(bullet);

        if (enhancement.bonusBounces > 0)
        {
            if (!wasBouncy)
            {
                isBouncyField.SetValue(bullet, true);
                Debug.Log($"<color=orange>[强化] 启用弹射效果</color>");
            }

            int originalBounces = (int)maxBouncesField.GetValue(bullet);
            maxBouncesField.SetValue(bullet, originalBounces + enhancement.bonusBounces);
        }

        // 4. 减速效果强化
        var hasSlowEffectField = bulletType.GetField("hasSlowEffect",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var slowMultiplierField = bulletType.GetField("slowMultiplier",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        bool hadSlowEffect = (bool)hasSlowEffectField.GetValue(bullet);

        if (enhancement.slowMultiplierBonus > 0)
        {
            if (!hadSlowEffect)
            {
                hasSlowEffectField.SetValue(bullet, true);
                Debug.Log($"<color=orange>[强化] 启用减速效果</color>");
            }

            float originalSlowMultiplier = (float)slowMultiplierField.GetValue(bullet);
            // 减速权重增强：例如原来0.5，+20%后变成0.4（减速更强）
            float enhancedSlowMultiplier = originalSlowMultiplier * (1f - enhancement.slowMultiplierBonus);
            slowMultiplierField.SetValue(bullet, Mathf.Clamp(enhancedSlowMultiplier, 0.1f, 1f));
        }

        // 5. 追踪效果强化
        if (enhancement.enableHoming)
        {
            var isHomingField = bulletType.GetField("isHoming",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            bool wasHoming = (bool)isHomingField.GetValue(bullet);

            if (!wasHoming)
            {
                isHomingField.SetValue(bullet, true);
                Debug.Log($"<color=orange>[强化] 启用追踪效果</color>");
            }
        }

        // 6. 爆炸范围强化
        if (enhancement.explosionRadiusMultiplier > 1f)
        {
            var explosionRadiusField = bulletType.GetField("explosionRadius",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (explosionRadiusField != null)
            {
                float originalRadius = (float)explosionRadiusField.GetValue(bullet);
                explosionRadiusField.SetValue(bullet, originalRadius * enhancement.explosionRadiusMultiplier);
            }
        }
    }
}