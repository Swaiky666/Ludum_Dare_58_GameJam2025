using UnityEngine;

/// <summary>
/// 装备管理器 - 管理玩家的左右键装备（从 EquippedWeaponData 初始化左手装备）
/// </summary>
public class EquipmentManager : MonoBehaviour
{
    [Header("Equipment Prefabs (Fallback)")]
    [SerializeField] private GameObject leftHandEquipmentPrefab;   // 备用：左手默认预制体（当SO里没有时使用）
    [SerializeField] private GameObject rightHandEquipmentPrefab;  // 备用：右手默认预制体（本版本启动时不装备）

    [Header("Fire Points")]
    [SerializeField] private Transform leftFirePoint;   // 左手开火点 / 武器父节点
    [SerializeField] private Transform rightFirePoint;  // 右手开火点 / 武器父节点

    [Header("Equipped Data (ScriptableObject)")]
    [SerializeField] private EquippedWeaponData equippedWeaponData; // 与主菜单共用的SO，启动时从这里同步装备

    [Header("Weapon Display Objects")]
    private GameObject leftHandWeaponObj;   // 左手武器显示物体（由InventoryUI设置）
    private GameObject rightHandWeaponObj;  // 右手武器显示物体（由InventoryUI设置）
    [SerializeField] private float weaponDisplayDuration = 0.5f; // 武器显示持续时间（秒）

    private IEquippable leftHandEquipment;   // 左手装备实例
    private IEquippable rightHandEquipment;  // 右手装备实例

    // ⭐ 新增：保存原始预制体引用
    private GameObject leftHandPrefab;   // 左手武器的原始预制体
    private GameObject rightHandPrefab;  // 右手武器的原始预制体

    // 协程引用，用于取消之前的隐藏操作
    private Coroutine leftWeaponHideCoroutine;
    private Coroutine rightWeaponHideCoroutine;

    // 公共只读访问器（其他系统查询当前装备）
    public IEquippable LeftHandEquipment => leftHandEquipment;
    public IEquippable RightHandEquipment => rightHandEquipment;

    /// <summary>
    /// 由InventoryUI调用，设置武器显示物体引用
    /// </summary>
    public void SetWeaponDisplayObjects(GameObject leftObj, GameObject rightObj)
    {
        leftHandWeaponObj = leftObj;
        rightHandWeaponObj = rightObj;

        Debug.Log($"<color=green>[EquipmentManager] 武器显示物体已设置: Left={leftObj?.name}, Right={rightObj?.name}</color>");

        // 初始化时隐藏
        if (leftHandWeaponObj != null) leftHandWeaponObj.SetActive(false);
        if (rightHandWeaponObj != null) rightHandWeaponObj.SetActive(false);
    }

    private void Start()
    {
        // —— 左手：优先用 SO 的武器
        GameObject leftPrefabToUse = null;
        if (equippedWeaponData != null && equippedWeaponData.IsEquipped() && equippedWeaponData.weaponPrefab != null)
        {
            leftPrefabToUse = equippedWeaponData.weaponPrefab;
        }
        else
        {
            // 若 SO 为空或未设置，则退回到 Inspector 里的备用预制体
            leftPrefabToUse = leftHandEquipmentPrefab;
        }

        if (leftPrefabToUse != null)
        {
            EquipToSlot(leftPrefabToUse, 0);
        }
        else
        {
            ClearSlot(0); // 左手清空
        }

        // —— 右手：按你的最新需求，启动时不装备任何东西
        ClearSlot(1);
    }

    private void Update()
    {
        // 更新冷却
        leftHandEquipment?.UpdateCooldown(Time.deltaTime);
        rightHandEquipment?.UpdateCooldown(Time.deltaTime);
    }

    /// <summary>
    /// 装备到指定槽位（0=左手，1=右手）
    /// </summary>
    public void EquipToSlot(GameObject equipmentPrefab, int slot)
    {
        if (equipmentPrefab == null) return;

        Transform firePoint = (slot == 0) ? leftFirePoint : rightFirePoint;
        if (firePoint == null)
        {
            Debug.LogError($"EquipToSlot失败：槽位{slot}的 FirePoint 未设置。");
            return;
        }

        // 先卸下旧装备
        ClearSlot(slot);

        // 实例化新装备
        GameObject equipmentObj = Instantiate(equipmentPrefab, firePoint.position, firePoint.rotation, firePoint);
        IEquippable equipment = equipmentObj.GetComponent<IEquippable>();
        if (equipment == null)
        {
            Debug.LogError($"装备预制体 {equipmentPrefab.name} 没有实现 IEquippable 接口！");
            Destroy(equipmentObj);
            return;
        }

        // ⭐ 保存原始预制体引用
        if (slot == 0)
        {
            leftHandEquipment = equipment;
            leftHandPrefab = equipmentPrefab;
        }
        else
        {
            rightHandEquipment = equipment;
            rightHandPrefab = equipmentPrefab;
        }

        equipment.OnEquip();
        Debug.Log($"[EquipmentManager] 装备 {equipment.EquipmentName} 到槽位 {slot}");
    }

    /// <summary>
    /// 清空指定槽位（0=左手，1=右手）
    /// </summary>
    public void ClearSlot(int slot)
    {
        if (slot == 0 && leftHandEquipment != null)
        {
            leftHandEquipment.OnUnequip();
            Destroy((leftHandEquipment as MonoBehaviour)?.gameObject);
            leftHandEquipment = null;
            leftHandPrefab = null; // ⭐ 清空预制体引用
        }
        else if (slot == 1 && rightHandEquipment != null)
        {
            rightHandEquipment.OnUnequip();
            Destroy((rightHandEquipment as MonoBehaviour)?.gameObject);
            rightHandEquipment = null;
            rightHandPrefab = null; // ⭐ 清空预制体引用
        }

        // 把子物体都清掉，避免残留
        Transform firePoint = (slot == 0) ? leftFirePoint : rightFirePoint;
        if (firePoint != null)
        {
            for (int i = firePoint.childCount - 1; i >= 0; i--)
            {
                Destroy(firePoint.GetChild(i).gameObject);
            }
        }
    }

    /// <summary>
    /// 使用指定槽位的装备（供 PlayerController 调用）
    /// </summary>
    public void UseEquipment(int slot, Vector3 direction, Vector3 origin)
    {
        var equipment = (slot == 0) ? leftHandEquipment : rightHandEquipment;
        if (equipment != null && equipment.CanUse())
        {
            equipment.Use(direction, origin);

            // ⭐ 开火时显示武器物体
            ShowWeaponDisplay(slot);
        }
    }

    /// <summary>
    /// 显示武器物体（开火时调用）
    /// </summary>
    void ShowWeaponDisplay(int slot)
    {
        GameObject weaponObj = (slot == 0) ? leftHandWeaponObj : rightHandWeaponObj;
        IEquippable equipment = (slot == 0) ? leftHandEquipment : rightHandEquipment;

        if (weaponObj != null && equipment != null && equipment.Icon != null)
        {
            // 更新SpriteRenderer的sprite
            SpriteRenderer sr = weaponObj.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = equipment.Icon;
            }

            // 取消之前的隐藏协程
            if (slot == 0 && leftWeaponHideCoroutine != null)
            {
                StopCoroutine(leftWeaponHideCoroutine);
            }
            else if (slot == 1 && rightWeaponHideCoroutine != null)
            {
                StopCoroutine(rightWeaponHideCoroutine);
            }

            // 显示武器
            weaponObj.SetActive(true);

            // 启动隐藏协程
            if (slot == 0)
            {
                leftWeaponHideCoroutine = StartCoroutine(HideWeaponAfterDelay(weaponObj));
            }
            else
            {
                rightWeaponHideCoroutine = StartCoroutine(HideWeaponAfterDelay(weaponObj));
            }
        }
    }

    /// <summary>
    /// 延迟隐藏武器物体
    /// </summary>
    System.Collections.IEnumerator HideWeaponAfterDelay(GameObject weaponObj)
    {
        yield return new WaitForSeconds(weaponDisplayDuration);
        if (weaponObj != null)
        {
            weaponObj.SetActive(false);
        }
    }

    /// <summary>
    /// 获取指定槽位的装备
    /// </summary>
    public IEquippable GetEquipment(int slot)
    {
        return slot == 0 ? leftHandEquipment : rightHandEquipment;
    }

    /// <summary>
    /// ⭐ 新增：获取指定槽位的原始预制体
    /// </summary>
    public GameObject GetEquipmentPrefab(int slot)
    {
        return slot == 0 ? leftHandPrefab : rightHandPrefab;
    }
}