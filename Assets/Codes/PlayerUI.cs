using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家UI显示系统
/// </summary>
public class PlayerUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController playerController;

    [Header("Health & Shield UI")]
    [SerializeField] private Slider healthSlider;       // 生命值滑条
    [SerializeField] private Slider shieldSlider;       // 护盾滑条

    [Header("Dash UI")]
    [SerializeField] private Image dashIcon;            // 冲刺图标
    [SerializeField] private float minAlpha = 0.3f;     // 最小透明度
    [SerializeField] private float maxAlpha = 1f;       // 最大透明度

    private Color dashIconColor;

    void Start()
    {
        if (playerController == null)
        {
            playerController = FindObjectOfType<PlayerController>();
            if (playerController == null)
            {
                Debug.LogError("PlayerUI: 未找到PlayerController！");
                return;
            }
        }

        // 初始化冲刺图标颜色
        if (dashIcon != null)
        {
            dashIconColor = dashIcon.color;
        }

        // 初始化滑条
        InitializeSliders();
    }

    void Update()
    {
        if (playerController == null) return;

        UpdateHealthUI();
        UpdateShieldUI();
        UpdateDashUI();
    }

    /// <summary>
    /// 初始化滑条
    /// </summary>
    void InitializeSliders()
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = playerController.MaxHealth;
            healthSlider.value = playerController.CurrentHealth;
        }

        if (shieldSlider != null)
        {
            shieldSlider.maxValue = playerController.MaxShield;
            shieldSlider.value = playerController.CurrentShield;
        }
    }

    /// <summary>
    /// 更新生命值UI
    /// </summary>
    void UpdateHealthUI()
    {
        if (healthSlider != null)
        {
            healthSlider.value = playerController.CurrentHealth;
        }
    }

    /// <summary>
    /// 更新护盾UI
    /// </summary>
    void UpdateShieldUI()
    {
        if (shieldSlider != null)
        {
            shieldSlider.value = playerController.CurrentShield;
        }
    }

    /// <summary>
    /// 更新冲刺UI（透明度）
    /// </summary>
    void UpdateDashUI()
    {
        if (dashIcon == null) return;

        // 计算透明度：冷却中逐渐从最小值恢复到最大值
        float dashCooldownRatio = playerController.DashCooldownRatio; // 0=就绪, 1=刚冲刺完
        float alpha = Mathf.Lerp(maxAlpha, minAlpha, dashCooldownRatio);

        // 设置颜色
        dashIconColor.a = alpha;
        dashIcon.color = dashIconColor;
    }
}