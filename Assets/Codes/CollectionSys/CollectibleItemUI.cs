using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class CollectibleItemUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI组件")]
    public Image iconImage;
    public Image highlightBorder;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI idText;
    public TextMeshProUGUI countText;
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI descriptionText;

    [Header("选择高亮")]
    public Image selectionHighlightImage;  // 用于显示选中状态的Image
    public Material selectionMaterial;     // 选中时的Material

    [Header("装备按钮")]
    public Button equipButton;  // 装备按钮
    public TextMeshProUGUI equipButtonText;  // 按钮文字

    [Header("装备武器数据")]
    public EquippedWeaponData equippedWeaponData;  // 携带武器的 ScriptableObject

    private CollectibleData data;

    // 静态变量：跟踪当前选中的item
    private static CollectibleItemUI currentSelectedItem;

    public void Setup(CollectibleData collectibleData)
    {
        this.data = collectibleData;
        UpdateDisplay();
        SetupEquipButton();

        // ⭐ 检查是否是当前装备的武器，如果是则自动选中
        if (equippedWeaponData != null && data != null &&
            equippedWeaponData.weaponId == data.id && data.isCollected)
        {
            // 延迟选中，确保UI完全初始化
            Invoke(nameof(SelectWithoutEquipping), 0.1f);
        }
        else
        {
            // 初始时取消选中状态
            Deselect();
        }
    }

    /// <summary>
    /// 选中但不触发装备（用于初始化时同步显示）
    /// </summary>
    private void SelectWithoutEquipping()
    {
        // 如果已经有其他item被选中了，不要覆盖
        if (currentSelectedItem != null && currentSelectedItem != this)
        {
            return;
        }

        // 设置当前item为选中状态
        currentSelectedItem = this;

        // 仅应用Material，不触发装备逻辑
        if (selectionHighlightImage != null && selectionMaterial != null)
        {
            selectionHighlightImage.material = selectionMaterial;
            Canvas.ForceUpdateCanvases();
            Debug.Log($"<color=cyan>初始化时自动选中已装备武器: {data?.itemName}</color>");
        }
    }

    private void SetupEquipButton()
    {
        if (equipButton != null)
        {
            // 移除旧的监听器
            equipButton.onClick.RemoveAllListeners();

            // 添加新的监听器
            equipButton.onClick.AddListener(OnEquipButtonClicked);

            // 更新按钮状态
            UpdateEquipButton();
        }
    }

    private void OnEquipButtonClicked()
    {
        // ⭐ 装备按钮现在直接调用Select()方法
        // 这样点击按钮和点击item的效果是一致的
        Select();
    }

    private void UpdateEquipButton()
    {
        if (equipButton == null) return;

        // 如果未收集，禁用按钮
        if (data == null || !data.isCollected)
        {
            equipButton.interactable = false;
            if (equipButtonText != null)
            {
                equipButtonText.text = "未收集";
            }
        }
        else
        {
            equipButton.interactable = true;

            // 检查是否是当前装备的武器
            if (equippedWeaponData != null && equippedWeaponData.weaponId == data.id)
            {
                if (equipButtonText != null)
                {
                    equipButtonText.text = "已装备";
                }
                // 可以给按钮换个颜色表示已装备
                ColorBlock colors = equipButton.colors;
                colors.normalColor = Color.green;
                equipButton.colors = colors;
            }
            else
            {
                if (equipButtonText != null)
                {
                    equipButtonText.text = "装备";
                }
                // 恢复默认颜色
                ColorBlock colors = equipButton.colors;
                colors.normalColor = Color.white;
                equipButton.colors = colors;
            }
        }
    }

    public void UpdateDisplay()
    {
        if (data == null) return;

        // 设置基本信息
        if (nameText != null)
            nameText.text = data.itemName;

        if (idText != null)
            idText.text = $"#{data.id:D3}";

        if (descriptionText != null)
            descriptionText.text = data.description;

        // 设置图标
        if (iconImage != null && data.icon != null)
        {
            iconImage.sprite = data.icon;

            // 根据是否收集设置图片颜色
            if (data.isCollected)
            {
                iconImage.color = Color.white; // 正常颜色
            }
            else
            {
                iconImage.color = new Color(0.3f, 0.3f, 0.3f, 1f); // 灰暗色
            }
        }

        // 设置高亮边框
        if (highlightBorder != null)
        {
            highlightBorder.gameObject.SetActive(data.isCollected);
        }

        // 设置收集次数
        if (countText != null)
        {
            if (data.isCollected)
                countText.text = $"Collected: {data.collectionCount}";
            else
                countText.text = "Not Collected";
        }

        // 设置收集时间
        if (timeText != null)
        {
            if (data.isCollected && !string.IsNullOrEmpty(data.collectionTime))
                timeText.text = $"Time: {data.collectionTime}";
            else
                timeText.text = "";
        }

        // 更新装备按钮
        UpdateEquipButton();
    }

    /// <summary>
    /// 处理点击事件 - 选中这个item
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        // 点击任意位置都可以选中
        Select();
    }

    /// <summary>
    /// 选中这个item（同时装备武器）
    /// </summary>
    public void Select()
    {
        // 如果已经是选中状态，则不做处理
        if (currentSelectedItem == this)
        {
            return;
        }

        // 检查是否已收集
        if (data == null || !data.isCollected)
        {
            Debug.LogWarning($"未收集的物品无法选择和装备！");
            return;
        }

        // 取消之前选中的item
        if (currentSelectedItem != null)
        {
            currentSelectedItem.Deselect();
        }

        // 设置当前item为选中状态
        currentSelectedItem = this;

        // 应用Material
        if (selectionHighlightImage != null && selectionMaterial != null)
        {
            selectionHighlightImage.material = selectionMaterial;

            // 强制Canvas立即更新
            Canvas.ForceUpdateCanvases();

            Debug.Log($"<color=yellow>选中收集品: {data?.itemName}</color>");
        }
        else
        {
            if (selectionHighlightImage == null)
                Debug.LogWarning($"CollectibleItemUI ({data?.itemName}): selectionHighlightImage 未设置！");
            if (selectionMaterial == null)
                Debug.LogWarning($"CollectibleItemUI ({data?.itemName}): selectionMaterial 未设置！");
        }

        // ⭐ 关键：选中时自动装备武器
        if (equippedWeaponData != null && data != null)
        {
            equippedWeaponData.EquipWeapon(data);
            Debug.Log($"<color=green>已装备武器: {data.itemName}</color>");

            // 更新装备按钮显示（如果有的话）
            UpdateEquipButton();
        }
    }

    /// <summary>
    /// 取消选中
    /// </summary>
    public void Deselect()
    {
        // 移除Material
        if (selectionHighlightImage != null)
        {
            selectionHighlightImage.material = null;

            // 强制Canvas立即更新
            Canvas.ForceUpdateCanvases();
        }

        // 如果这个item是当前选中的，清空静态引用
        if (currentSelectedItem == this)
        {
            currentSelectedItem = null;
        }
    }

    /// <summary>
    /// 检查是否被选中
    /// </summary>
    public bool IsSelected()
    {
        return currentSelectedItem == this;
    }

    /// <summary>
    /// 获取当前选中的item（静态方法）
    /// </summary>
    public static CollectibleItemUI GetCurrentSelectedItem()
    {
        return currentSelectedItem;
    }

    /// <summary>
    /// 清除所有选中状态（静态方法）
    /// </summary>
    public static void ClearAllSelection()
    {
        if (currentSelectedItem != null)
        {
            currentSelectedItem.Deselect();
            currentSelectedItem = null;
        }
    }

    public CollectibleData GetData()
    {
        return data;
    }

    private void OnDestroy()
    {
        // 销毁时，如果是当前选中的item，清空引用
        if (currentSelectedItem == this)
        {
            currentSelectedItem = null;
        }
    }
}