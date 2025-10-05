using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CollectibleItemUI : MonoBehaviour
{
    [Header("UI组件")]
    public Image iconImage;
    public Image highlightBorder;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI idText;
    public TextMeshProUGUI countText;
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI descriptionText;

    [Header("装备按钮")]
    public Button equipButton;  // 装备按钮
    public TextMeshProUGUI equipButtonText;  // 按钮文字

    [Header("装备武器数据")]
    public EquippedWeaponData equippedWeaponData;  // 携带武器的 ScriptableObject

    private CollectibleData data;

    public void Setup(CollectibleData collectibleData)
    {
        this.data = collectibleData;
        UpdateDisplay();
        SetupEquipButton();
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
        if (data == null || equippedWeaponData == null)
        {
            Debug.LogWarning("数据为空，无法装备武器！");
            return;
        }

        // 检查是否已收集
        if (!data.isCollected)
        {
            Debug.LogWarning($"{data.itemName} 尚未收集，无法装备！");
            return;
        }

        // 装备武器
        equippedWeaponData.EquipWeapon(data);

        // 更新按钮显示
        UpdateEquipButton();
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

    public CollectibleData GetData()
    {
        return data;
    }
}