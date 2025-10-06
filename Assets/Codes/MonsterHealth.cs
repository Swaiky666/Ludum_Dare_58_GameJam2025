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

        // ✨ 受击后强制进入追击
        if (monsterAI != null)
        {
            monsterAI.ForceChase();
        }

        // 受伤闪烁
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

    IEnumerator HitFlashEffect()
    {
        if (spriteRenderer == null) yield break;

        if (useMaterialFlash && flashMaterial != null)
        {
            for (int i = 0; i < flashCount; i++)
            {
                spriteRenderer.material = flashMaterial;
                yield return new WaitForSeconds(flashDuration / (flashCount * 2f));

                spriteRenderer.material = originalMaterial;
                spriteRenderer.color = GetCurrentColor();
                yield return new WaitForSeconds(flashDuration / (flashCount * 2f));
            }
        }
        else if (useHDRFlash)
        {
            for (int i = 0; i < flashCount; i++)
            {
                spriteRenderer.color = flashColor;
                yield return new WaitForSeconds(flashDuration / (flashCount * 2f));

                spriteRenderer.color = GetCurrentColor();
                yield return new WaitForSeconds(flashDuration / (flashCount * 2f));
            }
        }

        spriteRenderer.material = originalMaterial;
        spriteRenderer.color = GetCurrentColor();
    }

    Color GetCurrentColor()
    {
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
            for (int i = 0; i < deathFlashCount; i++)
            {
                spriteRenderer.material = flashMaterial;
                float phaseStart = Time.time;
                while (Time.time - phaseStart < singleFlashTime)
                {
                    ApplyKnockbackMovement(knockbackDir, force, flashElapsed, deathFlashDuration);
                    flashElapsed += Time.deltaTime;
                    yield return null;
                }

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
            for (int i = 0; i < deathFlashCount; i++)
            {
                spriteRenderer.color = flashColor;
                float phaseStart = Time.time;
                while (Time.time - phaseStart < singleFlashTime)
                {
                    ApplyKnockbackMovement(knockbackDir, force, flashElapsed, deathFlashDuration);
                    flashElapsed += Time.deltaTime;
                    yield return null;
                }

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

        spriteRenderer.material = originalMaterial;

        StartCoroutine(DeathSequence(direction, force));
    }

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

        Color startColor = spriteRenderer != null ? spriteRenderer.color : originalColor;

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

        // 延迟销毁整个GameObject（给粒子留时间）
        Destroy(gameObject, particleDestroyDelay);
    }

    /// <summary>
    /// 清理怪物：重置Tag/Layer、停粒子、移除控制器与碰撞体、清掉子物体与脚本（保留Transform/粒子）
    /// </summary>
    void CleanupMonster()
    {
        // 0) 重置 tag / layer
        gameObject.tag = "Untagged";
        gameObject.layer = 0; // Default
        Debug.Log($"{gameObject.name} 已重置 Tag=Untagged 与 Layer=Default");

        // 1) 停止粒子发射（不清现有粒子）
        if (attachedParticleSystem != null)
        {
            var emission = attachedParticleSystem.emission;
            emission.enabled = false;

            ParticleSystem[] allParticleSystems = attachedParticleSystem.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in allParticleSystems)
            {
                var childEmission = ps.emission;
                childEmission.enabled = false;
            }

            // 如需脱离主体，可在这里分离
            if (detachParticleOnDeath)
            {
                attachedParticleSystem.transform.SetParent(null, true);
            }

            Debug.Log($"{gameObject.name} 停止粒子发射（并按需分离）");
        }

        // 2) 删除控制器与碰撞体
        // 2.1 CharacterController
        if (characterController != null)
        {
            Destroy(characterController);
            characterController = null;
            Debug.Log($"{gameObject.name} 删除 CharacterController");
        }
        else
        {
            // 防守式再查一次
            var cc = GetComponent<CharacterController>();
            if (cc != null)
            {
                Destroy(cc);
                Debug.Log($"{gameObject.name} 删除 CharacterController(延迟查找)");
            }
        }

        // 2.2 CapsuleCollider（可能有多个，全部移除）
        var capsuleColliders = GetComponents<CapsuleCollider>();
        if (capsuleColliders != null && capsuleColliders.Length > 0)
        {
            foreach (var cap in capsuleColliders)
            {
                if (cap != null) Destroy(cap);
            }
            Debug.Log($"{gameObject.name} 删除 {capsuleColliders.Length} 个 CapsuleCollider");
        }

        // 3) 删除除粒子外的所有子物体
        List<Transform> childrenToDestroy = new List<Transform>();
        foreach (Transform child in transform)
        {
            bool isParticleObject = false;

            if (attachedParticleSystem != null)
            {
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
        foreach (Transform child in childrenToDestroy)
        {
            Destroy(child.gameObject);
        }
        Debug.Log($"{gameObject.name} 删除了 {childrenToDestroy.Count} 个子物体（保留粒子）");

        // 4) 删除其他脚本（保留本脚本以完成销毁流程）
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
