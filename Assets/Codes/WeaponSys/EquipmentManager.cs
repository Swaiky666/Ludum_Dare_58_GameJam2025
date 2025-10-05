using UnityEngine;

/// <summary>
/// 装备管理器 - 管理玩家的左右键装备
/// </summary>
public class EquipmentManager : MonoBehaviour
{
    [Header("Equipment Slots")]
    [SerializeField] private GameObject leftHandEquipmentPrefab;   // 左键装备预制体
    [SerializeField] private GameObject rightHandEquipmentPrefab;  // 右键装备预制体

    [Header("Fire Points")]
    [SerializeField] private Transform leftFirePoint;   // 左键开火点
    [SerializeField] private Transform rightFirePoint;  // 右键开火点

    private IEquippable leftHandEquipment;   // 左键装备实例
    private IEquippable rightHandEquipment;  // 右键装备实例

    // 公共访问器
    public IEquippable LeftHandEquipment => leftHandEquipment;
    public IEquippable RightHandEquipment => rightHandEquipment;

    void Start()
    {
        // 初始化装备
        if (leftHandEquipmentPrefab != null)
        {
            EquipToSlot(leftHandEquipmentPrefab, 0);
        }

        if (rightHandEquipmentPrefab != null)
        {
            EquipToSlot(rightHandEquipmentPrefab, 1);
        }
    }

    void Update()
    {
        // 更新冷却时间
        leftHandEquipment?.UpdateCooldown(Time.deltaTime);
        rightHandEquipment?.UpdateCooldown(Time.deltaTime);
    }

    /// <summary>
    /// 装备到指定槽位
    /// </summary>
    public void EquipToSlot(GameObject equipmentPrefab, int slot)
    {
        if (equipmentPrefab == null) return;

        Transform firePoint = slot == 0 ? leftFirePoint : rightFirePoint;

        // 卸载旧装备
        if (slot == 0 && leftHandEquipment != null)
        {
            leftHandEquipment.OnUnequip();
            Destroy((leftHandEquipment as MonoBehaviour)?.gameObject);
        }
        else if (slot == 1 && rightHandEquipment != null)
        {
            rightHandEquipment.OnUnequip();
            Destroy((rightHandEquipment as MonoBehaviour)?.gameObject);
        }

        // 实例化新装备
        GameObject equipmentObj = Instantiate(equipmentPrefab, firePoint.position, firePoint.rotation, firePoint);
        IEquippable equipment = equipmentObj.GetComponent<IEquippable>();

        if (equipment == null)
        {
            Debug.LogError($"装备预制体 {equipmentPrefab.name} 没有实现 IEquippable 接口！");
            Destroy(equipmentObj);
            return;
        }

        // 设置装备
        if (slot == 0)
        {
            leftHandEquipment = equipment;
        }
        else
        {
            rightHandEquipment = equipment;
        }

        equipment.OnEquip();
        Debug.Log($"装备 {equipment.EquipmentName} 到槽位 {slot}");
    }

    /// <summary>
    /// 使用指定槽位的装备
    /// </summary>
    public void UseEquipment(int slot, Vector3 direction, Vector3 origin)
    {
        IEquippable equipment = slot == 0 ? leftHandEquipment : rightHandEquipment;

        if (equipment != null && equipment.CanUse())
        {
            equipment.Use(direction, origin);
        }
    }

    /// <summary>
    /// 获取指定槽位的装备
    /// </summary>
    public IEquippable GetEquipment(int slot)
    {
        return slot == 0 ? leftHandEquipment : rightHandEquipment;
    }
}