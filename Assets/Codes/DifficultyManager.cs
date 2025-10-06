using UnityEngine;
using TMPro;

/// <summary>
/// 难度管理系统 - 跟踪和控制游戏难度（带颜色渐变）
/// </summary>
public class DifficultyManager : MonoBehaviour
{
    public static DifficultyManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI difficultyText;

    [Header("Difficulty Settings")]
    [SerializeField] private int currentDifficulty = 0;
    [SerializeField] private float healthScalePerLevel = 0.15f;  // 每级增加15%血量
    [SerializeField] private float damageScalePerLevel = 0.1f;   // 每级增加10%伤害
    [SerializeField] private float spawnRateIncreasePerLevel = 0.03f;  // 每级增加3%生成率

    [Header("Color Settings (20 Levels)")]
    [SerializeField] private int maxDifficulty = 20;  // 最大难度（20关）
    [SerializeField] private Color easyColor = Color.green;  // 简单难度颜色（绿色）
    [SerializeField] private Color mediumColor = Color.yellow;  // 中等难度颜色（黄色）
    [SerializeField] private Color hardColor = Color.red;  // 困难难度颜色（红色）

    public int CurrentDifficulty => currentDifficulty;

    void Awake()
    {
        // 单例模式
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        UpdateDifficultyUI();
    }

    /// <summary>
    /// 增加难度（每完成一个房间调用）
    /// </summary>
    public void IncreaseDifficulty()
    {
        currentDifficulty++;
        UpdateDifficultyUI();
        Debug.Log($"<color=yellow>Difficulty increased to: {currentDifficulty}</color>");
    }

    /// <summary>
    /// 重置难度（游戏重新开始时调用）
    /// </summary>
    public void ResetDifficulty()
    {
        currentDifficulty = 0;
        UpdateDifficultyUI();
        Debug.Log("<color=green>Difficulty reset to 0</color>");
    }

    /// <summary>
    /// 更新UI显示（包含颜色渐变）
    /// </summary>
    void UpdateDifficultyUI()
    {
        if (difficultyText != null)
        {
            difficultyText.text = $"Current Difficulty: {currentDifficulty}";

            // 计算颜色渐变
            Color targetColor = GetDifficultyColor(currentDifficulty);
            difficultyText.color = targetColor;
        }
        else
        {
            Debug.LogWarning("Difficulty Text UI reference is missing!");
        }
    }

    /// <summary>
    /// 根据难度计算颜色（绿→黄→红）
    /// </summary>
    Color GetDifficultyColor(int difficulty)
    {
        // 限制在0-maxDifficulty范围内
        difficulty = Mathf.Clamp(difficulty, 0, maxDifficulty);

        // 计算归一化进度 (0.0 到 1.0)
        float progress = (float)difficulty / maxDifficulty;

        Color resultColor;

        if (progress <= 0.5f)
        {
            // 前半段：绿色 → 黄色 (0-10关)
            float t = progress * 2f; // 归一化到 0-1
            resultColor = Color.Lerp(easyColor, mediumColor, t);
        }
        else
        {
            // 后半段：黄色 → 红色 (11-20关)
            float t = (progress - 0.5f) * 2f; // 归一化到 0-1
            resultColor = Color.Lerp(mediumColor, hardColor, t);
        }

        return resultColor;
    }

    /// <summary>
    /// 获取缩放后的怪物血量
    /// </summary>
    public float GetScaledHealth(float baseHealth)
    {
        float multiplier = 1f + (currentDifficulty * healthScalePerLevel);
        return baseHealth * multiplier;
    }

    /// <summary>
    /// 获取缩放后的怪物伤害
    /// </summary>
    public float GetScaledDamage(float baseDamage)
    {
        float multiplier = 1f + (currentDifficulty * damageScalePerLevel);
        return baseDamage * multiplier;
    }

    /// <summary>
    /// 获取缩放后的怪物生成率
    /// </summary>
    public float GetScaledMonsterSpawnChance(float baseSpawnChance)
    {
        float increase = currentDifficulty * spawnRateIncreasePerLevel;
        float result = baseSpawnChance + increase;

        // 设置上限，避免生成率过高（最高1.0，即100%）
        return Mathf.Clamp(result, baseSpawnChance, 1f);
    }

    /// <summary>
    /// 获取难度倍数信息（用于调试）
    /// </summary>
    public string GetDifficultyInfo()
    {
        float healthMultiplier = 1f + (currentDifficulty * healthScalePerLevel);
        float damageMultiplier = 1f + (currentDifficulty * damageScalePerLevel);
        float spawnChanceIncrease = currentDifficulty * spawnRateIncreasePerLevel;

        return $"Difficulty {currentDifficulty} - Health: x{healthMultiplier:F2}, Damage: x{damageMultiplier:F2}, Spawn Rate: +{spawnChanceIncrease:F2}";
    }
}