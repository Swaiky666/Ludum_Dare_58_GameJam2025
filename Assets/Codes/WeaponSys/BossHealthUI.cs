using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Boss血条UI - World Space显示
/// </summary>
public class BossHealthUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MonsterHealth bossHealth;
    [SerializeField] private Canvas canvas;

    [Header("UI Elements")]
    [SerializeField] private Image healthBarFill;          // 血条填充
    [SerializeField] private Image healthBarBackground;    // 血条背景
    [SerializeField] private TextMeshProUGUI bossNameText; // Boss名字
    [SerializeField] private TextMeshProUGUI healthText;   // 血量数字

    [Header("UI Settings")]
    [SerializeField] private string bossName = "Boss";
    [SerializeField] private Color fullHealthColor = Color.red;
    [SerializeField] private Color lowHealthColor = Color.yellow;
    [SerializeField] private float lowHealthThreshold = 0.3f;
    [SerializeField] private Vector3 uiOffset = new Vector3(0, 3f, 0);
    [SerializeField] private float uiScale = 0.01f;

    [Header("Animation")]
    [SerializeField] private bool smoothTransition = true;
    [SerializeField] private float transitionSpeed = 5f;

    private Camera mainCamera;
    private float targetFillAmount = 1f;
    private float currentFillAmount = 1f;

    void Start()
    {
        mainCamera = Camera.main;

        // 自动获取MonsterHealth组件
        if (bossHealth == null)
        {
            bossHealth = GetComponentInParent<MonsterHealth>();
            if (bossHealth == null)
            {
                Debug.LogError("BossHealthUI: 未找到MonsterHealth组件！");
                return;
            }
        }

        // 设置Canvas为World Space
        if (canvas == null)
        {
            canvas = GetComponent<Canvas>();
        }

        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = mainCamera;

            // 设置Canvas大小和缩放
            RectTransform rectTransform = canvas.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = new Vector2(300, 60);
                rectTransform.localScale = Vector3.one * uiScale;
            }
        }

        // 初始化文本
        if (bossNameText != null)
        {
            bossNameText.text = bossName;
        }

        // 初始化血条
        UpdateHealthBar(true);
    }

    void Update()
    {
        if (bossHealth == null || mainCamera == null) return;

        // 更新位置
        UpdatePosition();

        // 始终面向相机
        FaceCamera();

        // 更新血条
        UpdateHealthBar(false);
    }

    void UpdatePosition()
    {
        // UI跟随Boss并应用偏移
        transform.position = bossHealth.transform.position + uiOffset;
    }

    void FaceCamera()
    {
        // 始终面向相机
        if (mainCamera != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position);
        }
    }

    void UpdateHealthBar(bool instant)
    {
        if (bossHealth == null || healthBarFill == null) return;

        // 计算血量百分比（使用反射获取私有字段）
        float maxHealth = GetMaxHealth();
        float currentHealth = GetCurrentHealth();

        if (maxHealth <= 0)
        {
            Debug.LogWarning("BossHealthUI: MaxHealth为0！");
            return;
        }

        targetFillAmount = Mathf.Clamp01(currentHealth / maxHealth);

        // 平滑过渡或立即更新
        if (smoothTransition && !instant)
        {
            currentFillAmount = Mathf.Lerp(currentFillAmount, targetFillAmount, transitionSpeed * Time.deltaTime);
        }
        else
        {
            currentFillAmount = targetFillAmount;
        }

        healthBarFill.fillAmount = currentFillAmount;

        // 更新血条颜色（根据血量）
        if (currentFillAmount <= lowHealthThreshold)
        {
            healthBarFill.color = lowHealthColor;
        }
        else
        {
            healthBarFill.color = fullHealthColor;
        }

        // 更新血量文本
        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}";
        }
    }

    /// <summary>
    /// 使用反射获取最大血量（因为MonsterHealth的字段是私有的）
    /// </summary>
    float GetMaxHealth()
    {
        if (bossHealth == null) return 0f;

        var field = bossHealth.GetType().GetField("maxHealth",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            return (float)field.GetValue(bossHealth);
        }

        return 100f; // 默认值
    }

    /// <summary>
    /// 使用反射获取当前血量
    /// </summary>
    float GetCurrentHealth()
    {
        if (bossHealth == null) return 0f;

        var field = bossHealth.GetType().GetField("currentHealth",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            return (float)field.GetValue(bossHealth);
        }

        return 100f; // 默认值
    }

    /// <summary>
    /// 公开方法：设置Boss名字
    /// </summary>
    public void SetBossName(string name)
    {
        bossName = name;
        if (bossNameText != null)
        {
            bossNameText.text = name;
        }
    }

    /// <summary>
    /// 公开方法：隐藏/显示UI
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (canvas != null)
        {
            canvas.enabled = visible;
        }
    }
}