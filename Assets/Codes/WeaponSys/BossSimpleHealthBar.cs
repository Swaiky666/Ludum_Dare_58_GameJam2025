using UnityEngine;

/// <summary>
/// Boss简单血条 - 使用GL直接绘制在屏幕上方
/// </summary>
public class BossSimpleHealthBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MonsterHealth bossHealth;

    [Header("Health Bar Settings")]
    [SerializeField] private float barWidth = 800f;          // 血条宽度（像素）
    [SerializeField] private float barHeight = 40f;          // 血条高度（像素）
    [SerializeField] private float topOffset = 50f;          // 距离屏幕顶部的距离

    [Header("Colors")]
    [SerializeField] private Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color highHealthColor = Color.red;
    [SerializeField] private Color midHealthColor = Color.yellow;
    [SerializeField] private Color lowHealthColor = new Color(1f, 0.5f, 0f); // 橙色
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float lowHealthThreshold = 0.3f;
    [SerializeField] private float midHealthThreshold = 0.6f;

    [Header("Flash Settings")]
    [SerializeField] private float flashDuration = 0.15f;    // 闪烁持续时间

    private Material lineMaterial;
    private float currentHealthPercent = 1f;
    private float targetHealthPercent = 1f;
    private float smoothSpeed = 5f;

    private bool isFlashing = false;
    private float flashTimer = 0f;
    private float lastHealth = -1f;

    void Start()
    {
        if (bossHealth == null)
        {
            bossHealth = GetComponent<MonsterHealth>();
            if (bossHealth == null)
            {
                Debug.LogError("BossSimpleHealthBar: 未找到MonsterHealth组件！");
                enabled = false;
                return;
            }
        }

        CreateLineMaterial();
    }

    void Update()
    {
        if (bossHealth == null) return;

        // 获取当前血量
        float maxHealth = GetMaxHealth();
        float currentHealth = GetCurrentHealth();

        if (maxHealth <= 0) return;

        targetHealthPercent = Mathf.Clamp01(currentHealth / maxHealth);

        // 平滑过渡
        currentHealthPercent = Mathf.Lerp(currentHealthPercent, targetHealthPercent, smoothSpeed * Time.deltaTime);

        // 检测受伤并触发闪烁
        if (lastHealth > 0 && currentHealth < lastHealth)
        {
            TriggerFlash();
        }
        lastHealth = currentHealth;

        // 更新闪烁
        if (isFlashing)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0)
            {
                isFlashing = false;
            }
        }
    }

    void OnRenderObject()
    {
        if (bossHealth == null || lineMaterial == null) return;

        lineMaterial.SetPass(0);

        GL.PushMatrix();
        GL.LoadPixelMatrix();

        DrawHealthBar();

        GL.PopMatrix();
    }

    void DrawHealthBar()
    {
        // 计算血条位置（屏幕上方居中）
        float centerX = Screen.width / 2f;
        float barY = Screen.height - topOffset - barHeight;  // 从顶部往下计算
        float barLeft = centerX - barWidth / 2f;
        float barRight = centerX + barWidth / 2f;
        float barBottom = barY;
        float barTop = barY + barHeight;

        // 绘制背景（黑色边框）
        DrawQuad(barLeft - 2, barBottom - 2, barRight + 2, barTop + 2, Color.black);

        // 绘制背景
        DrawQuad(barLeft, barBottom, barRight, barTop, backgroundColor);

        // 计算当前血条的右边界
        float currentBarRight = Mathf.Lerp(barLeft, barRight, currentHealthPercent);

        // 确定血条颜色
        Color barColor;
        if (isFlashing)
        {
            barColor = flashColor;
        }
        else if (targetHealthPercent <= lowHealthThreshold)
        {
            barColor = lowHealthColor;
        }
        else if (targetHealthPercent <= midHealthThreshold)
        {
            barColor = midHealthColor;
        }
        else
        {
            barColor = highHealthColor;
        }

        // 绘制血条
        if (currentBarRight > barLeft)
        {
            DrawQuad(barLeft, barBottom, currentBarRight, barTop, barColor);
        }

        // 绘制分隔线（每25%一条）
        for (int i = 1; i < 4; i++)
        {
            float ratio = i * 0.25f;
            float lineX = Mathf.Lerp(barLeft, barRight, ratio);
            DrawLine(lineX, barBottom, lineX, barTop, new Color(0f, 0f, 0f, 0.5f), 2f);
        }
    }

    void DrawQuad(float x1, float y1, float x2, float y2, Color color)
    {
        GL.Begin(GL.QUADS);
        GL.Color(color);

        GL.Vertex3(x1, y1, 0);
        GL.Vertex3(x2, y1, 0);
        GL.Vertex3(x2, y2, 0);
        GL.Vertex3(x1, y2, 0);

        GL.End();
    }

    void DrawLine(float x1, float y1, float x2, float y2, Color color, float width = 1f)
    {
        GL.Begin(GL.QUADS);
        GL.Color(color);

        Vector2 perpendicular = new Vector2(-(y2 - y1), x2 - x1).normalized * (width / 2f);

        GL.Vertex3(x1 + perpendicular.x, y1 + perpendicular.y, 0);
        GL.Vertex3(x2 + perpendicular.x, y2 + perpendicular.y, 0);
        GL.Vertex3(x2 - perpendicular.x, y2 - perpendicular.y, 0);
        GL.Vertex3(x1 - perpendicular.x, y1 - perpendicular.y, 0);

        GL.End();
    }

    void CreateLineMaterial()
    {
        if (!lineMaterial)
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            lineMaterial = new Material(shader);
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
        }
    }

    void TriggerFlash()
    {
        isFlashing = true;
        flashTimer = flashDuration;
    }

    /// <summary>
    /// 使用反射获取最大血量
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

        return 100f;
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

        return 100f;
    }

    /// <summary>
    /// 公开方法：手动触发闪烁效果
    /// </summary>
    public void Flash()
    {
        TriggerFlash();
    }

    /// <summary>
    /// 公开方法：设置是否显示血条
    /// </summary>
    public void SetVisible(bool visible)
    {
        enabled = visible;
    }
}