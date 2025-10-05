using UnityEngine;

/// <summary>
/// 子弹（增加击退效果）
/// </summary>
public class Bullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private LayerMask hitLayers;

    [Header("Visual Effects")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private TrailRenderer trailRenderer;

    private Vector3 direction;
    private float speed;
    private float damage;
    private float knockbackForce;
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
        }
    }

    public void Initialize(Vector3 dir, float spd, float dmg, float knockback = 0f)
    {
        direction = dir.normalized;
        speed = spd;
        damage = dmg;
        knockbackForce = knockback;

        rb.velocity = direction * speed;

        // 自动销毁
        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter(Collider other)
    {
        // 检查是否击中敌人
        if (other.CompareTag("Monster"))
        {
            // 对怪物造成伤害
            MonsterHealth monsterHealth = other.GetComponent<MonsterHealth>();
            if (monsterHealth != null)
            {
                monsterHealth.TakeDamage(damage, direction, knockbackForce);
            }

            // 显示击中特效
            SpawnHitEffect();

            // 销毁子弹
            Destroy(gameObject);
        }
        // 检查是否击中障碍物
        else if (other.CompareTag("Obstacle") || other.CompareTag("Wall"))
        {
            SpawnHitEffect();
            Destroy(gameObject);
        }
    }

    void SpawnHitEffect()
    {
        if (hitEffectPrefab != null)
        {
            Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
        }
    }
}