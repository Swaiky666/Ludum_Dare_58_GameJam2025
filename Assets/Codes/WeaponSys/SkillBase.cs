using UnityEngine;

/// <summary>
/// 技能基类
/// </summary>
public abstract class SkillBase : MonoBehaviour, IEquippable
{
    [Header("Base Skill Stats")]
    [SerializeField] protected string skillName = "Skill";
    [SerializeField] protected Sprite icon;
    [SerializeField] protected float cooldown = 2f;

    protected float currentCooldown = 0f;
    protected PlayerController playerController;

    // 接口实现
    public string EquipmentName => skillName;
    public Sprite Icon => icon;
    public float Cooldown => cooldown;
    public float CurrentCooldown => currentCooldown;

    protected virtual void Awake()
    {
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
}