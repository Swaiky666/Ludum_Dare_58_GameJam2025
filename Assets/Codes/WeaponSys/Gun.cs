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

    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem muzzleFlash;

    [Header("Camera Shake")]
    [SerializeField] private float shakeDuration = 0.1f;
    [SerializeField] private float shakeMagnitude = 0.05f;

    private CameraShake cameraShake;

    protected override void Awake()
    {
        base.Awake();

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

    public override void Use(Vector3 direction, Vector3 origin)
    {
        if (!CanUse()) return;

        // 发射子弹
        for (int i = 0; i < bulletsPerShot; i++)
        {
            Vector3 shootDirection = direction;

            // 添加散射
            if (spreadAngle > 0)
            {
                float angle = Random.Range(-spreadAngle, spreadAngle);
                shootDirection = Quaternion.Euler(0, angle, 0) * direction;
            }

            // 实例化子弹
            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.LookRotation(shootDirection));

            Bullet bulletScript = bullet.GetComponent<Bullet>();
            if (bulletScript != null)
            {
                // 传递伤害和击退力
                bulletScript.Initialize(shootDirection, bulletSpeed, damage, enemyKnockbackForce);
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

        // 重置冷却
        currentCooldown = cooldown;

        Debug.Log($"{weaponName} 开火！");
    }
}