using UnityEngine;

public class MonsterHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Knockback Settings")]
    [SerializeField] private float knockbackDuration = 0.2f;

    [Header("Death Settings")]
    [SerializeField] private float deathDelay = 0.5f;
    [SerializeField] private float deathKnockbackMultiplier = 0.15f;  // 死亡击退 = 伤害 × 此倍率

    private float currentHealth;
    private CharacterController characterController;
    private MonsterAI monsterAI;
    private bool isKnockedBack = false;
    private bool isDying = false;

    void Start()
    {
        currentHealth = maxHealth;
        characterController = GetComponent<CharacterController>();
        monsterAI = GetComponent<MonsterAI>();
    }

    public void TakeDamage(float damage, Vector3 knockbackDirection, float knockbackForce)
    {
        if (isDying) return;

        currentHealth -= damage;
        Debug.Log($"{gameObject.name} 受到 {damage} 伤害，剩余血量: {currentHealth}");

        if (currentHealth <= 0)
        {
            // 死亡时：根据致命一击的伤害计算击退力
            float deathKnockback = damage * deathKnockbackMultiplier;
            Die(knockbackDirection, deathKnockback);
        }
        else if (knockbackForce > 0 && !isKnockedBack)
        {
            // 平时受伤：使用子弹设置的击退力
            StartCoroutine(ApplyKnockback(knockbackDirection, knockbackForce));
        }
    }

    System.Collections.IEnumerator ApplyKnockback(Vector3 direction, float force)
    {
        isKnockedBack = true;

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

        if (monsterAI != null)
        {
            monsterAI.enabled = true;
        }

        isKnockedBack = false;
    }

    void Die(Vector3 lastKnockbackDirection, float lastKnockbackForce)
    {
        isDying = true;
        Debug.Log($"{gameObject.name} 死亡！击退力: {lastKnockbackForce}");

        if (monsterAI != null)
        {
            monsterAI.enabled = false;
        }

        StartCoroutine(DeathSequence(lastKnockbackDirection, lastKnockbackForce));
    }

    System.Collections.IEnumerator DeathSequence(Vector3 direction, float force)
    {
        float elapsed = 0;
        Vector3 knockbackDir = direction.normalized;
        knockbackDir.y = 0;

        while (elapsed < deathDelay)
        {
            float knockbackSpeed = force * (1 - elapsed / deathDelay);
            Vector3 knockbackMovement = knockbackDir * knockbackSpeed * Time.deltaTime;

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

        Destroy(gameObject);
    }
}