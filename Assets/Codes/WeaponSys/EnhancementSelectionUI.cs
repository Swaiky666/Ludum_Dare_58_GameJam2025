using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 强化选择UI系统 - 每次进入房间时让玩家选择武器强化
/// </summary>
public class EnhancementSelectionUI : MonoBehaviour
{
    public static EnhancementSelectionUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject selectionPanel;
    [SerializeField] private Button[] leftHandButtons = new Button[3];   // 左手强化按钮
    [SerializeField] private Button[] rightHandButtons = new Button[3];  // 右手强化按钮
    [SerializeField] private Button confirmButton;                       // 确认按钮

    [Header("Button Text")]
    [SerializeField] private TextMeshProUGUI[] leftHandTexts = new TextMeshProUGUI[3];
    [SerializeField] private TextMeshProUGUI[] rightHandTexts = new TextMeshProUGUI[3];

    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightColor = new Color(1f, 0.8f, 0f, 1f); // 金色高亮

    [Header("Enhancement Settings")]
    [SerializeField] private float damageBonus = 0.8f;           // 伤害提升80% (1.8倍)
    [SerializeField] private float fireRateBonus = 0.5f;         // 攻速提升50% (1.5倍)
    [SerializeField] private float bulletsPerShotMultiplier = 3f; // 子弹数量x3
    [SerializeField] private int bonusBounces = 1;               // 额外弹射1次（改为加算）
    [SerializeField] private float slowEffectBonus = 0.2f;       // 减速效果+20%
    [SerializeField] private float bulletSpeedUpMultiplier = 1.3f;   // 子弹速度提升30%
    [SerializeField] private float bulletSpeedDownMultiplier = 0.7f; // 子弹速度降低30%

    // 所有可用的武器强化类型（移除Piercing）
    private string[] allEnhancements = new string[]
    {
        "Damage",
        "FireRate",
        "BulletsPerShot",
        "Bounce",
        "SlowEffect",
        "BulletSpeedUp",
        "BulletSpeedDown",
        "Homing",
        "Explosion"
    };

    // 当前选择的强化类型
    private string[] leftHandEnhancements = new string[3];
    private string[] rightHandEnhancements = new string[3];

    // 当前选中的按钮索引 (-1表示未选中)
    private int selectedLeftIndex = -1;
    private int selectedRightIndex = -1;

    private bool isSelecting = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 初始隐藏面板
        if (selectionPanel != null)
        {
            selectionPanel.SetActive(false);
        }
    }

    void Start()
    {
        // 绑定按钮事件
        for (int i = 0; i < 3; i++)
        {
            int index = i; // 捕获索引
            leftHandButtons[i].onClick.AddListener(() => OnLeftHandButtonClicked(index));
            rightHandButtons[i].onClick.AddListener(() => OnRightHandButtonClicked(index));
        }

        confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    /// <summary>
    /// 显示强化选择面板
    /// </summary>
    public void ShowSelectionPanel()
    {
        if (isSelecting) return;

        isSelecting = true;

        // 暂停游戏
        Time.timeScale = 0f;

        // 随机抽取强化类型
        RandomizeEnhancements();

        // 重置选择状态
        selectedLeftIndex = -1;
        selectedRightIndex = -1;

        // 更新UI显示
        UpdateButtonVisuals();

        // 显示面板
        if (selectionPanel != null)
        {
            selectionPanel.SetActive(true);
        }

        Debug.Log("<color=cyan>[强化选择] 面板已显示</color>");
    }

    /// <summary>
    /// 随机抽取强化类型
    /// </summary>
    void RandomizeEnhancements()
    {
        // 复制所有强化类型
        List<string> availableEnhancements = new List<string>(allEnhancements);

        // 为左手抽取3个
        for (int i = 0; i < 3; i++)
        {
            int randomIndex = Random.Range(0, availableEnhancements.Count);
            leftHandEnhancements[i] = availableEnhancements[randomIndex];
            availableEnhancements.RemoveAt(randomIndex);
        }

        // 重新填充可用列表
        availableEnhancements = new List<string>(allEnhancements);

        // 为右手抽取3个
        for (int i = 0; i < 3; i++)
        {
            int randomIndex = Random.Range(0, availableEnhancements.Count);
            rightHandEnhancements[i] = availableEnhancements[randomIndex];
            availableEnhancements.RemoveAt(randomIndex);
        }

        Debug.Log($"<color=yellow>[强化选择] 左手: {string.Join(", ", leftHandEnhancements)}</color>");
        Debug.Log($"<color=yellow>[强化选择] 右手: {string.Join(", ", rightHandEnhancements)}</color>");
    }

    /// <summary>
    /// 更新按钮显示
    /// </summary>
    void UpdateButtonVisuals()
    {
        // 更新左手按钮
        for (int i = 0; i < 3; i++)
        {
            if (leftHandTexts[i] != null)
            {
                leftHandTexts[i].text = GetEnhancementDisplayName(leftHandEnhancements[i]);
            }

            ColorBlock colors = leftHandButtons[i].colors;
            colors.normalColor = (i == selectedLeftIndex) ? highlightColor : normalColor;
            leftHandButtons[i].colors = colors;
        }

        // 更新右手按钮
        for (int i = 0; i < 3; i++)
        {
            if (rightHandTexts[i] != null)
            {
                rightHandTexts[i].text = GetEnhancementDisplayName(rightHandEnhancements[i]);
            }

            ColorBlock colors = rightHandButtons[i].colors;
            colors.normalColor = (i == selectedRightIndex) ? highlightColor : normalColor;
            rightHandButtons[i].colors = colors;
        }
    }

    /// <summary>
    /// 获取强化的显示名称
    /// </summary>
    string GetEnhancementDisplayName(string enhancementType)
    {
        switch (enhancementType)
        {
            case "Damage":
                return $"Damage +{damageBonus * 100:F0}%";
            case "FireRate":
                return $"Fire Rate +{fireRateBonus * 100:F0}%";
            case "BulletsPerShot":
                return $"Bullets x{bulletsPerShotMultiplier:F0}";
            case "Bounce":
                return $"Bounce +{bonusBounces}";
            case "SlowEffect":
                return $"Slow Effect +{slowEffectBonus * 100:F0}%";
            case "BulletSpeedUp":
                return $"Bullet Speed +{(bulletSpeedUpMultiplier - 1) * 100:F0}%";
            case "BulletSpeedDown":
                return $"Bullet Speed -{(1 - bulletSpeedDownMultiplier) * 100:F0}%";
            case "Homing":
                return "Homing";
            case "Explosion":
                return "Explosion";
            default:
                return enhancementType;
        }
    }

    /// <summary>
    /// 左手按钮点击
    /// </summary>
    void OnLeftHandButtonClicked(int index)
    {
        selectedLeftIndex = index;
        UpdateButtonVisuals();
        Debug.Log($"<color=green>[强化选择] 左手选择: {leftHandEnhancements[index]}</color>");
    }

    /// <summary>
    /// 右手按钮点击
    /// </summary>
    void OnRightHandButtonClicked(int index)
    {
        selectedRightIndex = index;
        UpdateButtonVisuals();
        Debug.Log($"<color=green>[强化选择] 右手选择: {rightHandEnhancements[index]}</color>");
    }

    /// <summary>
    /// 确认按钮点击
    /// </summary>
    void OnConfirmClicked()
    {
        // 如果漏选，随机选择
        if (selectedLeftIndex == -1)
        {
            selectedLeftIndex = Random.Range(0, 3);
            Debug.Log($"<color=orange>[强化选择] 左手未选择，随机选择: {leftHandEnhancements[selectedLeftIndex]}</color>");
        }

        if (selectedRightIndex == -1)
        {
            selectedRightIndex = Random.Range(0, 3);
            Debug.Log($"<color=orange>[强化选择] 右手未选择，随机选择: {rightHandEnhancements[selectedRightIndex]}</color>");
        }

        // 应用强化
        ApplyEnhancements();

        // 隐藏面板
        HidePanel();
    }

    /// <summary>
    /// 应用选择的强化
    /// </summary>
    void ApplyEnhancements()
    {
        if (EnhancementManager.Instance == null)
        {
            Debug.LogError("[强化选择] EnhancementManager 不存在！");
            return;
        }

        string leftEnhancement = leftHandEnhancements[selectedLeftIndex];
        string rightEnhancement = rightHandEnhancements[selectedRightIndex];

        Debug.Log($"<color=cyan>[强化选择] 应用强化 - 左手: {leftEnhancement}, 右手: {rightEnhancement}</color>");

        // 应用左手强化
        ApplySingleEnhancement(0, leftEnhancement);

        // 应用右手强化
        ApplySingleEnhancement(1, rightEnhancement);
    }

    /// <summary>
    /// 应用单个强化
    /// </summary>
    void ApplySingleEnhancement(int slot, string enhancementType)
    {
        switch (enhancementType)
        {
            case "Damage":
                EnhancementManager.Instance.AddWeaponEnhancement(slot, "Damage", 1f + damageBonus);
                break;
            case "FireRate":
                EnhancementManager.Instance.AddWeaponEnhancement(slot, "FireRate", 1f + fireRateBonus);
                break;
            case "BulletsPerShot":
                EnhancementManager.Instance.AddWeaponEnhancement(slot, "BulletsPerShot", bulletsPerShotMultiplier);
                break;
            case "Bounce":
                EnhancementManager.Instance.AddWeaponEnhancement(slot, "Bounce", bonusBounces);
                break;
            case "SlowEffect":
                EnhancementManager.Instance.AddWeaponEnhancement(slot, "SlowEffect", slowEffectBonus);
                break;
            case "BulletSpeedUp":
                EnhancementManager.Instance.AddWeaponEnhancement(slot, "BulletSpeed", bulletSpeedUpMultiplier);
                break;
            case "BulletSpeedDown":
                EnhancementManager.Instance.AddWeaponEnhancement(slot, "BulletSpeed", bulletSpeedDownMultiplier);
                break;
            case "Homing":
                EnhancementManager.Instance.AddWeaponEnhancement(slot, "Homing", 1f);
                break;
            case "Explosion":
                EnhancementManager.Instance.AddWeaponEnhancement(slot, "Explosion", 1f);
                break;
        }
    }

    /// <summary>
    /// 隐藏面板并继续游戏
    /// </summary>
    void HidePanel()
    {
        if (selectionPanel != null)
        {
            selectionPanel.SetActive(false);
        }

        // 恢复游戏
        Time.timeScale = 1f;
        isSelecting = false;

        Debug.Log("<color=cyan>[强化选择] 面板已隐藏，游戏继续</color>");
    }

    /// <summary>
    /// 检查是否正在选择强化
    /// </summary>
    public bool IsSelecting()
    {
        return isSelecting;
    }
}