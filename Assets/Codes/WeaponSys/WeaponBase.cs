using UnityEngine;

/// <summary>
/// 武器基类（抽象类）
/// </summary>
public abstract class WeaponBase : MonoBehaviour, IEquippable
{
    [Header("Basic Info")]
    [SerializeField] protected string weaponName = "武器";
    [SerializeField] protected Sprite weaponIcon;  // 新增：武器图标，在Inspector中设置

    [Header("Weapon Stats")]
    [SerializeField] protected float damage = 10f;
    [SerializeField] protected float cooldown = 0.5f;
    [SerializeField] protected float enemyKnockbackForce = 2f;
    [SerializeField] protected float playerRecoilForce = 1f;

    [Header("Audio")]
    [SerializeField] protected AudioClip fireSound;
    [SerializeField] protected float fireSoundVolume = 1f;

    [Header("References")]
    [SerializeField] protected Transform firePoint;

    protected float currentCooldown = 0f;
    protected PlayerController playerController;
    protected int slotIndex = -1;  // 槽位索引（0=左手，1=右手）

    // IEquippable 接口实现
    public string EquipmentName => weaponName;
    public Sprite Icon => weaponIcon;  // 武器图标
    public float Cooldown => cooldown;
    public float CurrentCooldown => currentCooldown;  // 当前冷却时间

    protected virtual void Awake()
    {
        if (firePoint == null)
        {
            firePoint = transform;
        }
    }

    /// <summary>
    /// 设置槽位索引
    /// </summary>
    public void SetSlotIndex(int index)
    {
        slotIndex = index;
        Debug.Log($"<color=cyan>{weaponName} 设置槽位索引: {slotIndex}</color>");
    }

    protected virtual void Start()
    {
        playerController = GetComponentInParent<PlayerController>();
        if (playerController == null)
        {
            Debug.LogWarning($"{weaponName}: 未找到PlayerController，部分功能可能无法使用。");
        }
    }

    public bool CanUse()
    {
        return currentCooldown <= 0f;
    }

    public abstract void Use(Vector3 direction, Vector3 origin);

    public void UpdateCooldown(float deltaTime)
    {
        if (currentCooldown > 0)
        {
            currentCooldown -= deltaTime;
        }
    }

    public virtual void OnEquip()
    {
        gameObject.SetActive(true);
        Debug.Log($"{weaponName} 已装备");
    }

    public virtual void OnUnequip()
    {
        Debug.Log($"{weaponName} 已卸下");
    }

    protected void PlayFireSound()
    {
        if (fireSound != null)
        {
            AudioSource.PlayClipAtPoint(fireSound, firePoint.position, fireSoundVolume);
        }
    }

    protected void ApplyRecoilToPlayer(Vector3 fireDirection)
    {
        if (playerController != null && playerRecoilForce > 0)
        {
            Vector3 recoilDirection = -fireDirection;
            playerController.ApplyRecoil(recoilDirection, playerRecoilForce);
        }
    }
}