using UnityEngine;

/// <summary>
/// 相机震动效果（挂载在 Main Camera 上，使用 localPosition）
/// </summary>
public class CameraShake : MonoBehaviour
{
    private Vector3 originalLocalPosition;
    private bool isShaking = false;

    // 全局震动开关
    private static bool screenShakeEnabled = true;

    void Start()
    {
        // 记录初始的本地坐标
        originalLocalPosition = transform.localPosition;
        Debug.Log($"CameraShake 初始 localPosition: {originalLocalPosition}");
    }

    /// <summary>
    /// 触发震动
    /// </summary>
    public void Shake(float duration, float magnitude)
    {
        if (!screenShakeEnabled)
        {
            return;
        }

        if (!isShaking)
        {
            StartCoroutine(ShakeCoroutine(duration, magnitude));
        }
    }

    private System.Collections.IEnumerator ShakeCoroutine(float duration, float magnitude)
    {
        isShaking = true;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // 只在局部空间震动，不影响世界坐标
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            transform.localPosition = originalLocalPosition + new Vector3(x, y, 0);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 恢复到初始本地位置
        transform.localPosition = originalLocalPosition;
        isShaking = false;
    }

    /// <summary>
    /// 设置屏幕震动开关
    /// </summary>
    public static void SetScreenShakeEnabled(bool enabled)
    {
        screenShakeEnabled = enabled;
        Debug.Log($"屏幕震动已{(enabled ? "开启" : "关闭")}");
    }

    /// <summary>
    /// 获取当前震动开关状态
    /// </summary>
    public static bool IsScreenShakeEnabled()
    {
        return screenShakeEnabled;
    }

    /// <summary>
    /// 切换震动开关
    /// </summary>
    public static void ToggleScreenShake()
    {
        screenShakeEnabled = !screenShakeEnabled;
        Debug.Log($"屏幕震动已{(screenShakeEnabled ? "开启" : "关闭")}");
    }

    // 如果有人在运行时改变了相机的 localPosition，可以重置
    public void ResetLocalPosition()
    {
        if (!isShaking)
        {
            transform.localPosition = originalLocalPosition;
        }
    }
}