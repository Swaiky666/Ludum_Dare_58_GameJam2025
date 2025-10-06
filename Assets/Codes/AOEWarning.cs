using UnityEngine;

/// <summary>
/// AOE预警效果 - 显示攻击范围并闪烁
/// </summary>
public class AOEWarning : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private SpriteRenderer warningSprite;
    [SerializeField] private Color warningColor = new Color(1f, 0f, 0f, 0.5f);
    [SerializeField] private float pulseSpeed = 3f;
    [SerializeField] private float minAlpha = 0.2f;
    [SerializeField] private float maxAlpha = 0.8f;

    [Header("Scale Settings")]
    [SerializeField] private float targetScale = 3f;  // AOE范围的显示大小
    [SerializeField] private float scaleSpeed = 2f;

    private float pulseTimer = 0f;
    private Vector3 initialScale;

    void Start()
    {
        if (warningSprite == null)
        {
            warningSprite = GetComponent<SpriteRenderer>();
        }

        if (warningSprite != null)
        {
            warningSprite.color = warningColor;
        }

        initialScale = Vector3.one * 0.1f;
        transform.localScale = initialScale;
    }

    void Update()
    {
        // 闪烁效果
        if (warningSprite != null)
        {
            pulseTimer += Time.deltaTime * pulseSpeed;
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, (Mathf.Sin(pulseTimer) + 1f) / 2f);
            Color color = warningSprite.color;
            color.a = alpha;
            warningSprite.color = color;
        }

        // 缩放效果
        Vector3 targetScaleVector = Vector3.one * targetScale;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScaleVector, scaleSpeed * Time.deltaTime);
    }
}