using UnityEngine;

/// <summary>
/// 怪物血量管理（增加击退效果）
/// </summary>
public class MonsterHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Knockback Settings")]
    [SerializeField] private float knockbackDuration = 0.2f;

    private float currentHealth;
    private CharacterController characterController;
    private MonsterAI monsterAI;
    private bool isKnockedBack = false;

    void Start()
    {
        currentHealth = maxHealth;
        characterController = GetComponent<CharacterController>();
        monsterAI = GetComponent<MonsterAI>();
    }

    /// <summary>
    /// 受到伤害（带击退）
    /// </summary>
    public void TakeDamage(float damage, Vector3 knockbackDirection, float knockbackForce)
    {
        currentHealth -= damage;
        Debug.Log($"{gameObject.name} 受到 {damage} 伤害，剩余血量: {currentHealth}");

        // 应用击退效果
        if (knockbackForce > 0 && !isKnockedBack)
        {
            StartCoroutine(ApplyKnockback(knockbackDirection, knockbackForce));
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 应用击退
    /// </summary>
    System.Collections.IEnumerator ApplyKnockback(Vector3 direction, float force)
    {
        isKnockedBack = true;

        // 暂时禁用怪物AI
        if (monsterAI != null)
        {
            monsterAI.enabled = false;
        }

        float elapsed = 0;
        Vector3 knockbackDir = direction.normalized;
        knockbackDir.y = 0;

        while (elapsed < knockbackDuration)
        {
            float knockbackSpeed = force * (1 - elapsed / knockbackDuration);
            Vector3 knockbackMovement = knockbackDir * knockbackSpeed * Time.deltaTime;

            // 使用 CharacterController 移动
            if (characterController != null)
            {
                characterController.Move(knockbackMovement);
            }
            else
            {
                transform.position += knockbackMovement;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 恢复怪物AI
        if (monsterAI != null)
        {
            monsterAI.enabled = true;
        }

        isKnockedBack = false;
    }

    void Die()
    {
        Debug.Log($"{gameObject.name} 死亡！");
        Destroy(gameObject);
    }
}