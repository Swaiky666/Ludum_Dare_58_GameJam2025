using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包UI管理器 - 管理背包的打开/关闭和武器槽位显示
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject inventoryPanel;        // 背包主面板
    [SerializeField] private WeaponSlot leftHandSlot;          // 左手武器槽位
    [SerializeField] private WeaponSlot rightHandSlot;         // 右手武器槽位
    [SerializeField] private WeaponDetailPanel detailPanel;    // 武器详细信息面板

    [Header("Player References")]
    [SerializeField] private EquipmentManager equipmentManager;
    [SerializeField] private Transform leftFirePoint;          // 左手开火点（用于掉落位置）

    [Header("Weapon Drop")]
    [SerializeField] private GameObject weaponDropPrefab;      // 武器掉落物预制体
    [SerializeField] private float dropOffset = 0.5f;          // 掉落偏移距离（可以设小一点）

    [Header("Weapon Objects")]
    [SerializeField] private GameObject leftHandWeaponObj;   // 左手武器物体
    [SerializeField] private GameObject rightHandWeaponObj;  // 右手武器物体

    private bool isOpen = false;

    void Start()
    {
        // 获取EquipmentManager
        if (equipmentManager == null)
        {
            equipmentManager = FindObjectOfType<EquipmentManager>();
            if (equipmentManager == null)
            {
                Debug.LogError("InventoryUI: 未找到EquipmentManager！");
            }
        }

        // ⭐ 将武器显示物体引用传递给EquipmentManager
        if (equipmentManager != null)
        {
            equipmentManager.SetWeaponDisplayObjects(leftHandWeaponObj, rightHandWeaponObj);
        }

        // 尝试从EquipmentManager获取leftFirePoint
        if (leftFirePoint == null && equipmentManager != null)
        {
            // 使用反射获取私有字段
            var equipType = equipmentManager.GetType();
            var leftFirePointField = equipType.GetField("leftFirePoint",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (leftFirePointField != null)
            {
                leftFirePoint = leftFirePointField.GetValue(equipmentManager) as Transform;
                Debug.Log($"<color=green>成功从EquipmentManager获取leftFirePoint</color>");
            }
            else
            {
                Debug.LogWarning("InventoryUI: 无法从EquipmentManager获取leftFirePoint，请在Inspector中手动设置！");
            }
        }

        // 初始化时关闭背包
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }

        // DetailPanel保持显示但清空内容
        if (detailPanel != null)
        {
            detailPanel.gameObject.SetActive(true);
            detailPanel.ClearInfo(); // 初始化时清空
            detailPanel.OnWeaponDiscard += OnWeaponDiscard; // 订阅武器清除事件
        }

        // 设置槽位索引和事件
        if (leftHandSlot != null)
        {
            leftHandSlot.SetSlotIndex(0);
            leftHandSlot.OnSlotClicked += () => OnWeaponSlotClicked(0);
            leftHandSlot.OnWeaponSwap += OnWeaponSwap;
            leftHandSlot.OnDragStarted += OnAnySlotDragStarted; // 订阅拖动开始事件
        }

        if (rightHandSlot != null)
        {
            rightHandSlot.SetSlotIndex(1);
            rightHandSlot.OnSlotClicked += () => OnWeaponSlotClicked(1);
            rightHandSlot.OnWeaponSwap += OnWeaponSwap;
            rightHandSlot.OnDragStarted += OnAnySlotDragStarted; // 订阅拖动开始事件
        }

        // 初始化时更新武器贴图
        UpdateWeaponSprites();
    }

    void Update()
    {
        // 按Tab键切换背包
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleInventory();
        }

        // 按ESC关闭背包
        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseInventory();
        }
    }

    /// <summary>
    /// 切换背包显示状态
    /// </summary>
    void ToggleInventory()
    {
        isOpen = !isOpen;

        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(isOpen);
        }

        if (isOpen)
        {
            // 打开背包时更新武器显示
            UpdateWeaponSlots();

            // 清空详细面板信息（但不隐藏panel）
            if (detailPanel != null)
            {
                detailPanel.ClearInfo();
            }

            // 暂停游戏
            Time.timeScale = 0f;
            // 显示鼠标
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            // 恢复游戏
            Time.timeScale = 1f;
            // 隐藏鼠标（如果你的游戏需要）
            // Cursor.visible = false;
            // Cursor.lockState = CursorLockMode.Locked;
        }
    }

    /// <summary>
    /// 关闭背包
    /// </summary>
    void CloseInventory()
    {
        isOpen = false;

        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
        }

        if (detailPanel != null)
        {
            detailPanel.gameObject.SetActive(false);
        }

        Time.timeScale = 1f;
    }

    /// <summary>
    /// 更新武器槽位显示
    /// </summary>
    void UpdateWeaponSlots()
    {
        if (equipmentManager == null) return;

        // 更新左手槽位
        if (leftHandSlot != null)
        {
            IEquippable leftEquipment = equipmentManager.GetEquipment(0);
            if (leftEquipment != null)
            {
                leftHandSlot.SetWeapon(leftEquipment);
            }
            else
            {
                leftHandSlot.ClearSlot();
            }
        }

        // 更新右手槽位
        if (rightHandSlot != null)
        {
            IEquippable rightEquipment = equipmentManager.GetEquipment(1);
            if (rightEquipment != null)
            {
                rightHandSlot.SetWeapon(rightEquipment);
            }
            else
            {
                rightHandSlot.ClearSlot();
            }
        }

        // 更新武器贴图
        UpdateWeaponSprites();
    }

    /// <summary>
    /// 更新左右手武器物体的显示状态和贴图
    /// </summary>
    void UpdateWeaponSprites()
    {
        if (equipmentManager == null) return;

        // 更新左手武器
        IEquippable leftWeapon = equipmentManager.GetEquipment(0);
        if (leftHandWeaponObj != null)
        {
            if (leftWeapon != null && leftWeapon.Icon != null)
            {
                // 更新SpriteRenderer的sprite
                SpriteRenderer leftSR = leftHandWeaponObj.GetComponent<SpriteRenderer>();
                if (leftSR != null)
                {
                    leftSR.sprite = leftWeapon.Icon;
                }
                // 注意：active状态由EquipmentManager在开火时控制
            }
            else
            {
                leftHandWeaponObj.SetActive(false);
            }
        }

        // 更新右手武器
        IEquippable rightWeapon = equipmentManager.GetEquipment(1);
        if (rightHandWeaponObj != null)
        {
            if (rightWeapon != null && rightWeapon.Icon != null)
            {
                // 更新SpriteRenderer的sprite
                SpriteRenderer rightSR = rightHandWeaponObj.GetComponent<SpriteRenderer>();
                if (rightSR != null)
                {
                    rightSR.sprite = rightWeapon.Icon;
                }
                // 注意：active状态由EquipmentManager在开火时控制
            }
            else
            {
                rightHandWeaponObj.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 武器槽位被点击（需要传入槽位索引）
    /// </summary>
    void OnWeaponSlotClicked(int slotIndex)
    {
        if (equipmentManager == null || detailPanel == null) return;

        IEquippable equipment = equipmentManager.GetEquipment(slotIndex);

        if (equipment != null)
        {
            // ⭐ 显示详细信息，传入槽位索引以获取强化数据
            detailPanel.ShowWeaponDetails(equipment, slotIndex);

            Debug.Log($"<color=cyan>点击了槽位 {slotIndex}：{equipment.EquipmentName}</color>");
        }
        else
        {
            // 如果槽位为空，清空详细面板
            detailPanel.ClearInfo();
            Debug.Log($"<color=yellow>槽位 {slotIndex} 为空</color>");
        }
    }

    /// <summary>
    /// 任何槽位开始拖动时 - 清空详细面板
    /// </summary>
    void OnAnySlotDragStarted()
    {
        if (detailPanel != null)
        {
            detailPanel.ClearInfo();
            Debug.Log("<color=gray>[拖动] 清空详细面板信息</color>");
        }
    }

    /// <summary>
    /// 武器被丢弃到详细面板 - 清除该槽位的武器并生成掉落物
    /// </summary>
    void OnWeaponDiscard(int slotIndex)
    {
        if (equipmentManager == null) return;

        IEquippable weapon = equipmentManager.GetEquipment(slotIndex);

        if (weapon == null)
        {
            Debug.Log($"<color=yellow>槽位{slotIndex}为空，无需丢弃</color>");
            return;
        }

        // ⭐ 关键：从EquipmentManager获取原始预制体
        GameObject weaponPrefab = equipmentManager.GetEquipmentPrefab(slotIndex);

        if (weaponPrefab == null)
        {
            Debug.LogError($"<color=red>无法获取槽位{slotIndex}的武器预制体！</color>");
            return;
        }

        Debug.Log($"<color=red>[丢弃武器] 槽位{slotIndex} - {weapon.EquipmentName}</color>");

        // 生成武器掉落物（传递原始预制体和武器数据）
        SpawnWeaponDrop(weaponPrefab, weapon);

        // 清除槽位
        equipmentManager.ClearSlot(slotIndex);

        // 刷新UI显示
        UpdateWeaponSlots();

        // 清空详细面板
        if (detailPanel != null)
        {
            detailPanel.ClearInfo();
        }
    }

    /// <summary>
    /// 在leftFirePoint位置生成武器掉落物
    /// </summary>
    void SpawnWeaponDrop(GameObject weaponPrefab, IEquippable weapon)
    {
        if (weaponDropPrefab == null)
        {
            Debug.LogWarning("WeaponDropPrefab未设置，无法生成掉落物！");
            return;
        }

        if (leftFirePoint == null)
        {
            Debug.LogWarning("leftFirePoint未设置，无法生成掉落物！请在Inspector中设置或确保EquipmentManager有leftFirePoint字段");
            return;
        }

        if (weaponPrefab == null)
        {
            Debug.LogError("武器预制体为空，无法生成掉落物！");
            return;
        }

        // 在leftFirePoint位置稍微向前生成掉落物
        Vector3 dropPosition = leftFirePoint.position + leftFirePoint.forward * dropOffset;

        GameObject droppedItem = Instantiate(weaponDropPrefab, dropPosition, Quaternion.identity);
        droppedItem.name = $"Dropped_{weapon.EquipmentName}";

        // 设置掉落物的武器数据
        WeaponDroppedItem dropScript = droppedItem.GetComponent<WeaponDroppedItem>();
        if (dropScript != null)
        {
            // ⭐ 使用原始预制体和武器接口数据
            dropScript.SetWeaponData(weaponPrefab, weapon);
            Debug.Log($"<color=green>[生成掉落物] {weapon.EquipmentName} 使用原始预制体: {weaponPrefab.name}</color>");
        }
        else
        {
            Debug.LogError("WeaponDropPrefab 上没有 WeaponDroppedItem 组件！");
        }

        Debug.Log($"<color=green>[生成掉落物] {weapon.EquipmentName} 在leftFirePoint位置: {dropPosition}</color>");
    }

    /// <summary>
    /// 武器交换事件处理 - 支持空槽位交换
    /// </summary>
    void OnWeaponSwap(int fromSlot, int toSlot)
    {
        if (equipmentManager == null)
        {
            Debug.LogError("EquipmentManager 为空，无法交换武器！");
            return;
        }

        // 获取两个槽位的武器（可能为null）
        IEquippable fromWeapon = equipmentManager.GetEquipment(fromSlot);
        IEquippable toWeapon = equipmentManager.GetEquipment(toSlot);

        // ⭐ 获取原始预制体
        GameObject fromPrefab = equipmentManager.GetEquipmentPrefab(fromSlot);
        GameObject toPrefab = equipmentManager.GetEquipmentPrefab(toSlot);

        Debug.Log($"<color=green>===== 开始交换武器 =====</color>");
        Debug.Log($"<color=green>槽位{fromSlot}: {fromWeapon?.EquipmentName ?? "空"} (预制体: {fromPrefab?.name ?? "null"})</color>");
        Debug.Log($"<color=green>槽位{toSlot}: {toWeapon?.EquipmentName ?? "空"} (预制体: {toPrefab?.name ?? "null"})</color>");

        // 执行交换
        // 先清空两个槽位
        equipmentManager.ClearSlot(fromSlot);
        equipmentManager.ClearSlot(toSlot);

        // 重新装备（交换位置）- 使用原始预制体
        if (toPrefab != null)
        {
            equipmentManager.EquipToSlot(toPrefab, fromSlot);
            Debug.Log($"<color=cyan>将 {toWeapon.EquipmentName} 装备到槽位{fromSlot}</color>");
        }
        else
        {
            Debug.Log($"<color=cyan>槽位{fromSlot} 清空</color>");
        }

        if (fromPrefab != null)
        {
            equipmentManager.EquipToSlot(fromPrefab, toSlot);
            Debug.Log($"<color=cyan>将 {fromWeapon.EquipmentName} 装备到槽位{toSlot}</color>");
        }
        else
        {
            Debug.Log($"<color=cyan>槽位{toSlot} 清空</color>");
        }

        // 关键：立即刷新UI显示（在槽位回到原位后会显示新的武器或空状态）
        UpdateWeaponSlots();

        // 清空详细面板（因为武器位置已改变）
        if (detailPanel != null)
        {
            detailPanel.ClearInfo();
        }

        Debug.Log($"<color=green>===== 交换完成，UI已刷新 =====</color>");
    }
}