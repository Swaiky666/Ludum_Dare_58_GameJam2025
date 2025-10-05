using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Knockback Settings")]
    [SerializeField] private float knockbackDuration = 0.2f;

    [Header("Death Settings")]
    [SerializeField] private float deathDelay = 0.5f;
    [SerializeField] private float deathKnockbackMultiplier = 0.15f;
    [SerializeField] private Color deathColor = new Color(0.2f, 0.2f, 0.2f, 1f); // 死亡颜色（深灰/黑色）
    [SerializeField] private float deathFadeDuration = 0.3f; // 渐变到死亡颜色的时间

    [Header("Death Flash Effect")]
    [SerializeField] private bool useDeathFlash = true; // 是否启用死亡闪烁
    [SerializeField] private int deathFlashCount = 4; // 死亡闪烁次数
    [SerializeField] private float deathFlashDuration = 0.2f; // 死亡闪烁总时长

    [Header("Death Particle Effect")]
    [SerializeField] private ParticleSystem attachedParticleSystem; // 挂在怪物身上的粒子系统
    [SerializeField] private bool detachParticleOnDeath = true; // 死亡时分离粒子
    [SerializeField] private float particleDestroyDelay = 3f; // 粒子延迟销毁时间

    [Header("Hit Flash Effect")]
    [SerializeField] private bool useHDRFlash = true;
    [SerializeField, ColorUsage(true, true)] private Color flashColor = new Color(3f, 3f, 3f, 1f);
    [SerializeField] private float flashDuration = 0.15f;
    [SerializeField] private int flashCount = 2;

    [Header("Alternative: Material Flash")]
    [SerializeField] private bool useMaterialFlash = false;
    [SerializeField] private Material flashMaterial;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    private float currentHealth;
    private CharacterController characterController;
    private MonsterAI monsterAI;
    private bool isKnockedBack = false;
    private bool isDying = false;
    private Vector3 lastKnockbackDirection; // 保存最后的击退方向（用于粒子特效）

    private Material originalMaterial;
    private Color originalColor;
    private Coroutine flashCoroutine;

    void Start()
    {
        currentHealth = maxHealth;
        characterController = GetComponent<CharacterController>();
        monsterAI = GetComponent<MonsterAI>();

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (spriteRenderer != null)
        {
            originalMaterial = spriteRenderer.material;
            originalColor = spriteRenderer.color;
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: 没有找到SpriteRenderer，受伤闪烁效果将不可用！");
        }
    }

    public void TakeDamage(float damage, Vector3 knockbackDirection, float knockbackForce)
    {
        if (isDying) return;

        currentHealth -= damage;
        Debug.Log($"{gameObject.name} 受到 {damage} 伤害，剩余血量: {currentHealth}");

        // 播放受伤闪烁效果
        if (spriteRenderer != null && !isDying)
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }
            flashCoroutine = StartCoroutine(HitFlashEffect());
        }

        if (currentHealth <= 0)
        {
            float deathKnockback = damage * deathKnockbackMultiplier;
            Die(knockbackDirection, deathKnockback);
        }
        else if (knockbackForce > 0 && !isKnockedBack)
        {
            StartCoroutine(ApplyKnockback(knockbackDirection, knockbackForce));
        }
    }

    /// <summary>
    /// 受伤闪烁效果（会考虑减速状态）
    /// </summary>
    IEnumerator HitFlashEffect()
    {
        if (spriteRenderer == null) yield break;

        if (useMaterialFlash && flashMaterial != null)
        {
            // 方法1：材质切换
            for (int i = 0; i < flashCount; i++)
            {
                spriteRenderer.material = flashMaterial;
                yield return new WaitForSeconds(flashDuration / (flashCount * 2f));

                spriteRenderer.material = originalMaterial;
                // 恢复颜色时检查减速状态
                spriteRenderer.color = GetCurrentColor();
                yield return new WaitForSeconds(flashDuration / (flashCount * 2f));
            }
        }
        else if (useHDRFlash)
        {
            // 方法2：HDR颜色闪烁
            for (int i = 0; i < flashCount; i++)
            {
                spriteRenderer.color = flashColor;
                yield return new WaitForSeconds(flashDuration / (flashCount * 2f));

                // 恢复颜色时检查减速状态
                spriteRenderer.color = GetCurrentColor();
                yield return new WaitForSeconds(flashDuration / (flashCount * 2f));
            }
        }

        // 最终确保恢复正确状态
        spriteRenderer.material = originalMaterial;
        spriteRenderer.color = GetCurrentColor();
    }

    /// <summary>
    /// 获取当前应该显示的颜色（考虑减速状态）
    /// </summary>
    Color GetCurrentColor()
    {
        // 如果怪物正在减速，返回减速颜色；否则返回原始颜色
        if (monsterAI != null && monsterAI.IsSlowed)
        {
            return monsterAI.SlowedColor;
        }
        return originalColor;
    }

    IEnumerator ApplyKnockback(Vector3 direction, float force)
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
        this.lastKnockbackDirection = lastKnockbackDirection;
        Debug.Log($"{gameObject.name} 死亡！击退力: {lastKnockbackForce}");

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.material = originalMaterial;
        }

        if (monsterAI != null)
        {
            monsterAI.enabled = false;
        }

        if (useDeathFlash)
        {
            StartCoroutine(DeathFlashSequence(lastKnockbackDirection, lastKnockbackForce));
        }
        else
        {
            StartCoroutine(DeathSequence(lastKnockbackDirection, lastKnockbackForce));
        }
    }

    /// <summary>
    /// 死亡闪烁序列（快速闪烁+击退）
    /// </summary>
    IEnumerator DeathFlashSequence(Vector3 direction, float force)
    {
        if (spriteRenderer == null)
        {
            StartCoroutine(DeathSequence(direction, force));
            yield break;
        }

        Color startColor = spriteRenderer.color;
        float singleFlashTime = deathFlashDuration / (deathFlashCount * 2f);

        Vector3 knockbackDir = direction.normalized;
        knockbackDir.y = 0;
        float flashElapsed = 0f;

        if (useMaterialFlash && flashMaterial != null)
        {
            // 使用材质闪烁 + 击退
            for (int i = 0; i < deathFlashCount; i++)
            {
                // 亮
                spriteRenderer.material = flashMaterial;
                float phaseStart = Time.time;
                while (Time.time - phaseStart < singleFlashTime)
                {
                    ApplyKnockbackMovement(knockbackDir, force, flashElapsed, deathFlashDuration);
                    flashElapsed += Time.deltaTime;
                    yield return null;
                }

                // 暗
                spriteRenderer.material = originalMaterial;
                spriteRenderer.color = startColor;
                phaseStart = Time.time;
                while (Time.time - phaseStart < singleFlashTime)
                {
                    ApplyKnockbackMovement(knockbackDir, force, flashElapsed, deathFlashDuration);
                    flashElapsed += Time.deltaTime;
                    yield return null;
                }
            }
        }
        else if (useHDRFlash)
        {
            // 使用HDR颜色闪烁 + 击退
            for (int i = 0; i < deathFlashCount; i++)
            {
                // 亮
                spriteRenderer.color = flashColor;
                float phaseStart = Time.time;
                while (Time.time - phaseStart < singleFlashTime)
                {
                    ApplyKnockbackMovement(knockbackDir, force, flashElapsed, deathFlashDuration);
                    flashElapsed += Time.deltaTime;
                    yield return null;
                }

                // 暗
                spriteRenderer.color = startColor;
                phaseStart = Time.time;
                while (Time.time - phaseStart < singleFlashTime)
                {
                    ApplyKnockbackMovement(knockbackDir, force, flashElapsed, deathFlashDuration);
                    flashElapsed += Time.deltaTime;
                    yield return null;
                }
            }
        }

        // 确保恢复材质
        spriteRenderer.material = originalMaterial;

        // 闪烁完成后执行死亡序列（变黑）
        StartCoroutine(DeathSequence(direction, force));
    }

    /// <summary>
    /// 应用击退移动（辅助方法）
    /// </summary>
    void ApplyKnockbackMovement(Vector3 direction, float force, float elapsed, float duration)
    {
        float knockbackSpeed = force * (1 - elapsed / duration);
        Vector3 knockbackMovement = direction * knockbackSpeed * Time.deltaTime;

        if (characterController != null)
        {
            characterController.Move(knockbackMovement);
        }
        else
        {
            transform.position += knockbackMovement;
        }
    }

    IEnumerator DeathSequence(Vector3 direction, float force)
    {
        float elapsed = 0;
        Vector3 knockbackDir = direction.normalized;
        knockbackDir.y = 0;

        // 获取当前颜色（可能是减速的蓝色或原始颜色）
        Color startColor = spriteRenderer != null ? spriteRenderer.color : originalColor;

        while (elapsed < deathDelay)
        {
            // 击退移动
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

            // 颜色渐变到死亡颜色
            if (spriteRenderer != null)
            {
                float fadeProgress = Mathf.Clamp01(elapsed / deathFadeDuration);
                spriteRenderer.color = Color.Lerp(startColor, deathColor, fadeProgress);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 死亡动画完成后，清理怪物（保留粒子系统）
        CleanupMonster();

        // 延迟销毁整个GameObject
        Destroy(gameObject, particleDestroyDelay);
    }

    /// <summary>
    /// 清理怪物（停止粒子，删除组件和子物体，保留粒子系统）
    /// </summary>
    void CleanupMonster()
    {
        // 1. 停止粒子发射（但不清除已有粒子）
        if (attachedParticleSystem != null)
        {
            var emission = attachedParticleSystem.emission;
            emission.enabled = false;

            // 处理所有子粒子系统
            ParticleSystem[] allParticleSystems = attachedParticleSystem.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in allParticleSystems)
            {
                var childEmission = ps.emission;
                childEmission.enabled = false;
            }

            Debug.Log($"{gameObject.name} 停止粒子发射");
        }

        // 2. 删除碰撞体
        if (characterController != null)
        {
            Destroy(characterController);
            Debug.Log($"{gameObject.name} 删除CharacterController");
        }

        // 3. 删除所有子物体，除了粒子系统
        List<Transform> childrenToDestroy = new List<Transform>();
        foreach (Transform child in transform)
        {
            // 如果子物体不是粒子系统或者不包含粒子系统，加入删除列表
            bool isParticleObject = false;

            if (attachedParticleSystem != null)
            {
                // 检查是否是粒子系统本身或其父物体
                if (child == attachedParticleSystem.transform ||
                    child.IsChildOf(attachedParticleSystem.transform) ||
                    attachedParticleSystem.transform.IsChildOf(child))
                {
                    isParticleObject = true;
                }
            }

            if (!isParticleObject)
            {
                childrenToDestroy.Add(child);
            }
        }

        // 删除标记的子物体
        foreach (Transform child in childrenToDestroy)
        {
            Destroy(child.gameObject);
        }

        Debug.Log($"{gameObject.name} 删除了 {childrenToDestroy.Count} 个子物体，保留粒子系统");

        // 4. 删除其他组件（保留Transform和粒子相关）
        MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script != null && script != this)
            {
                Destroy(script);
            }
        }
    }
}