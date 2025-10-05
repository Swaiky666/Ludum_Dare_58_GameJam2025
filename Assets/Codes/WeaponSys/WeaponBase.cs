using UnityEngine;

/// <summary>
/// 武器基类（增加后坐力）
/// </summary>
public abstract class WeaponBase : MonoBehaviour, IEquippable
{
    [Header("Base Weapon Stats")]
    [SerializeField] protected string weaponName = "Weapon";
    [SerializeField] protected Sprite icon;
    [SerializeField] protected float cooldown = 0.5f;
    [SerializeField] protected float damage = 10f;

    [Header("Recoil Settings")]
    [SerializeField] protected float playerRecoilForce = 2f;      // 对玩家的后坐力
    [SerializeField] protected float enemyKnockbackForce = 5f;    // 对敌人的击退力

    [Header("Fire Point")]
    [SerializeField] protected Transform firePoint;

    [Header("Audio")]
    [SerializeField] protected AudioClip fireSound;

    protected float currentCooldown = 0f;
    protected AudioSource audioSource;
    protected PlayerController playerController;

    // 接口实现
    public string EquipmentName => weaponName;
    public Sprite Icon => icon;
    public float Cooldown => cooldown;
    public float CurrentCooldown => currentCooldown;

    protected virtual void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (firePoint == null)
        {
            firePoint = transform;
        }

        // 获取玩家控制器引用
        playerController = GetComponentInParent<PlayerController>();
    }

    public virtual void OnEquip()
    {
        gameObject.SetActive(true);
    }

    public virtual void OnUnequip()
    {
        gameObject.SetActive(false);
    }

    public virtual void UpdateCooldown(float deltaTime)
    {
        if (currentCooldown > 0)
        {
            currentCooldown -= deltaTime;
        }
    }

    public virtual bool CanUse()
    {
        return currentCooldown <= 0;
    }

    public abstract void Use(Vector3 direction, Vector3 origin);

    protected virtual void PlayFireSound()
    {
        if (fireSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(fireSound);
        }
    }

    /// <summary>
    /// 应用后坐力到玩家
    /// </summary>
    protected virtual void ApplyRecoilToPlayer(Vector3 shootDirection)
    {
        if (playerController != null && playerRecoilForce > 0)
        {
            // 后坐力方向与射击方向相反
            Vector3 recoilDirection = -shootDirection;
            playerController.ApplyRecoil(recoilDirection, playerRecoilForce);
        }
    }
}