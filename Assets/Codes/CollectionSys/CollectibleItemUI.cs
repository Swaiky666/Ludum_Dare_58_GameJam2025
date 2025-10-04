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

    private CollectibleRuntimeData runtimeData;

    public void Setup(CollectibleRuntimeData runtimeData)
    {
        this.runtimeData = runtimeData;
        UpdateDisplay();
    }

    public void UpdateDisplay()
    {
        if (runtimeData == null || runtimeData.data == null) return;

        CollectibleData data = runtimeData.data;

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
            if (runtimeData.isCollected)
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
            highlightBorder.gameObject.SetActive(runtimeData.isCollected);
        }

        // 设置收集次数
        if (countText != null)
        {
            if (runtimeData.isCollected)
                countText.text = $"Collected: {runtimeData.collectionCount}";
            else
                countText.text = "Not Collected";
        }

        // 设置收集时间
        if (timeText != null)
        {
            if (runtimeData.isCollected && runtimeData.collectionTime != System.DateTime.MinValue)
                timeText.text = $"Time: {runtimeData.collectionTime:yyyy/MM/dd HH:mm}";
            else
                timeText.text = "";
        }
    }

    public CollectibleRuntimeData GetRuntimeData()
    {
        return runtimeData;
    }
}