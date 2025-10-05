using UnityEngine;

/// <summary>
/// 手动滚动控制器 - 通过手动输入参数计算滚动范围
/// 挂载到 Panel 上
/// </summary>
public class ManualScrollController : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private RectTransform content;
    [SerializeField] private RectTransform panel;

    [Header("布局参数")]
    [SerializeField] private float itemHeight = 150f;        // 每个物品的高度
    [SerializeField] private float spacing = 10f;            // 物品之间的间距
    [SerializeField] private float paddingTop = 10f;         // 顶部边距
    [SerializeField] private float paddingBottom = 10f;      // 底部边距

    [Header("滚动设置")]
    [SerializeField] private float scrollSpeed = 100f;       // 滚动速度

    private int itemCount = 0;          // 当前物品数量
    private float calculatedHeight = 0; // 计算出的 Content 高度
    private float maxScrollY = 0;       // 最大滚动距离

    private void Awake()
    {
        if (panel == null)
        {
            panel = GetComponent<RectTransform>();
        }
    }

    private void Start()
    {
        // 启动时重置到顶部
        ScrollToTop();
    }

    private void Update()
    {
        // 监听滚轮输入
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) > 0.01f)
        {
            Scroll(scroll);
        }
    }

    /// <summary>
    /// 根据物品数量更新 Content 高度和滚动范围
    /// </summary>
    public void UpdateContentHeight(int count)
    {
        if (content == null)
        {
            Debug.LogError("Content 引用为空！");
            return;
        }

        itemCount = count;

        // 计算 Content 总高度
        // 公式: 顶部边距 + (物品高度 × 数量) + (间距 × (数量-1)) + 底部边距
        calculatedHeight = paddingTop + paddingBottom + (itemHeight * itemCount);

        if (itemCount > 1)
        {
            calculatedHeight += spacing * (itemCount - 1);
        }

        // 设置 Content 高度
        content.sizeDelta = new Vector2(content.sizeDelta.x, calculatedHeight);

        // 计算最大滚动距离
        float panelHeight = panel.rect.height;
        maxScrollY = Mathf.Max(0, calculatedHeight - panelHeight);

        Debug.Log($"[滚动计算] 物品数量: {itemCount}, Content 高度: {calculatedHeight}, Panel 高度: {panelHeight}, 可滚动距离: {maxScrollY}");
    }

    /// <summary>
    /// 滚动
    /// </summary>
    private void Scroll(float scrollDelta)
    {
        if (content == null || maxScrollY <= 0)
        {
            if (maxScrollY <= 0)
            {
                Debug.Log("内容不足，无需滚动");
            }
            return;
        }

        // 计算新位置
        Vector2 pos = content.anchoredPosition;
        pos.y += scrollDelta * scrollSpeed;

        // 限制边界 (0 到 maxScrollY)
        pos.y = Mathf.Clamp(pos.y, 0, maxScrollY);

        content.anchoredPosition = pos;
    }

    /// <summary>
    /// 滚动到顶部
    /// </summary>
    public void ScrollToTop()
    {
        if (content == null) return;

        Vector2 pos = content.anchoredPosition;
        pos.y = 0;
        content.anchoredPosition = pos;
    }

    /// <summary>
    /// 滚动到底部
    /// </summary>
    public void ScrollToBottom()
    {
        if (content == null) return;

        Vector2 pos = content.anchoredPosition;
        pos.y = maxScrollY;
        content.anchoredPosition = pos;
    }

    /// <summary>
    /// 滚动到指定位置 (0-1，0是顶部，1是底部)
    /// </summary>
    public void ScrollToNormalized(float normalizedPosition)
    {
        if (content == null) return;

        Vector2 pos = content.anchoredPosition;
        pos.y = Mathf.Lerp(0, maxScrollY, Mathf.Clamp01(normalizedPosition));
        content.anchoredPosition = pos;
    }

    /// <summary>
    /// 重新计算（在运行时参数改变后调用）
    /// </summary>
    public void RecalculateHeight()
    {
        if (content != null)
        {
            int currentCount = content.childCount;
            UpdateContentHeight(currentCount);
        }
    }

    // 在 Inspector 中显示计算信息
    private void OnValidate()
    {
        if (Application.isPlaying && content != null)
        {
            RecalculateHeight();
        }
    }

    // 可视化调试
    private void OnDrawGizmosSelected()
    {
        if (content == null || panel == null) return;

        // 绘制 Panel 边界（绿色）
        Gizmos.color = Color.green;
        Vector3[] panelCorners = new Vector3[4];
        panel.GetWorldCorners(panelCorners);
        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawLine(panelCorners[i], panelCorners[(i + 1) % 4]);
        }

        // 绘制 Content 边界（蓝色）
        Gizmos.color = Color.blue;
        Vector3[] contentCorners = new Vector3[4];
        content.GetWorldCorners(contentCorners);
        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawLine(contentCorners[i], contentCorners[(i + 1) % 4]);
        }

        // 绘制可滚动区域（黄色虚线）
        if (maxScrollY > 0)
        {
            Gizmos.color = Color.yellow;
            Vector3 topLeft = panelCorners[1];
            Vector3 bottomLeft = panelCorners[0];
            Vector3 scrollEnd = bottomLeft + Vector3.down * (maxScrollY * panel.lossyScale.y);
            Gizmos.DrawLine(bottomLeft, scrollEnd);
        }
    }
}