using UnityEngine;

/// <summary>
/// 冲刺技能（可以装备到鼠标键）
/// </summary>
public class DashSkill : SkillBase
{
    [Header("Dash Settings")]
    [SerializeField] private float dashDistance = 5f;
    [SerializeField] private float dashDuration = 0.2f;

    public override void Use(Vector3 direction, Vector3 origin)
    {
        if (!CanUse() || playerController == null) return;

        // 执行冲刺
        playerController.StartDashInDirection(direction, dashDistance, dashDuration);

        // 重置冷却
        currentCooldown = cooldown;

        Debug.Log($"{skillName} 使用！");
    }
}