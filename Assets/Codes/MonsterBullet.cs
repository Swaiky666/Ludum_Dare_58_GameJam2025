using UnityEngine;

/// <summary>
/// 怪物子弹 - 专门用于Boss和怪物攻击玩家
/// </summary>
public class MonsterBullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    [SerializeField] private float lifetime = 5f;

    [Header("Explosion Settings")]
    [SerializeField] private bool isExplosive = false;
    [SerializeField] private float explosionRadius = 3f;
    [SerializeField] private float explosionDamage = 15f;
    [SerializeField] private float explosionKnockback = 8f;
    [SerializeField] private bool damageDecay = true;

    [Header("Bounce Settings")]
    [SerializeField] private bool isBouncy = false;
    [SerializeField] private int maxBounces = 3;
    [SerializeField] private float bounceSpeedMultiplier = 0.8f;
    [SerializeField] private GameObject bulletPrefab;

    [Header("Homing Settings")]
    [SerializeField] private bool isHoming = false;
    [SerializeField] private float homingTurnSpeed = 180f;
    [SerializeField] private float homingRange = 15f;
    [SerializeField] private float homingActivationDelay = 0.1f;
    [SerializeField] private bool usePredictiveAiming = true;
    [SerializeField] private float maxTrackingAngle = 60f;
    [SerializeField] private float giveUpAngle = 90f;

    private Vector3 direction;
    private float speed;
    private float damage;
    private float knockbackForce;
    private int currentBounces = 0;

    private Rigidbody rb;
    private bool hasCollided = false;

    private Transform targetPlayer;
    private float homingTimer = 0f;
    private Vector3 lastTargetPosition;
    private Vector3 lastTargetVelocity;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
        }

        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // 设置Layer为MonsterBullet
        SetupBulletLayer();
    }

    void Start()
    {
        IgnoreOtherBullets();

        if (isHoming)
        {
            FindPlayer();
        }
    }

    void SetupBulletLayer()
    {
        int monsterBulletLayer = LayerMask.NameToLayer("MonsterBullet");
        if (monsterBulletLayer == -1)
        {
            monsterBulletLayer = LayerMask.NameToLayer("Bullet");
            if (monsterBulletLayer == -1)
            {
                Debug.LogWarning("未找到 'MonsterBullet' 或 'Bullet' Layer！");
                return;
            }
        }

        gameObject.layer = monsterBulletLayer;

        // 怪物子弹忽略怪物层
        int monsterLayer = LayerMask.NameToLayer("Monster");
        if (monsterLayer != -1)
        {
            Physics.IgnoreLayerCollision(monsterBulletLayer, monsterLayer, true);
        }

        // 确保不忽略玩家层
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer != -1)
        {
            Physics.IgnoreLayerCollision(monsterBulletLayer, playerLayer, false);
        }
    }

    void Update()
    {
        if (isHoming && homingTimer >= homingActivationDelay && targetPlayer != null)
        {
            homingTimer += Time.deltaTime;
            UpdateHoming();
        }
        else if (isHoming)
        {
            homingTimer += Time.deltaTime;
        }
    }

    void IgnoreOtherBullets()
    {
        // 忽略所有其他子弹（包括Bullet和MonsterBullet）
        Bullet[] playerBullets = FindObjectsOfType<Bullet>();
        MonsterBullet[] monsterBullets = FindObjectsOfType<MonsterBullet>();
        Collider myCollider = GetComponent<Collider>();

        if (myCollider == null) return;

        foreach (Bullet otherBullet in playerBullets)
        {
            Collider otherCollider = otherBullet.GetComponent<Collider>();
            if (otherCollider != null)
            {
                Physics.IgnoreCollision(myCollider, otherCollider, true);
            }
        }

        foreach (MonsterBullet otherBullet in monsterBullets)
        {
            if (otherBullet == this) continue;

            Collider otherCollider = otherBullet.GetComponent<Collider>();
            if (otherCollider != null)
            {
                Physics.IgnoreCollision(myCollider, otherCollider, true);
            }
        }
    }

    public void Initialize(Vector3 dir, float spd, float dmg, float knockback = 0f, int bounces = 0)
    {
        direction = dir.normalized;
        speed = spd;
        damage = dmg;
        knockbackForce = knockback;
        currentBounces = bounces;

        rb.velocity = direction * speed;

        Destroy(gameObject, lifetime);
    }

    void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            targetPlayer = player.transform;
            lastTargetPosition = targetPlayer.position;
            lastTargetPosition.y = transform.position.y;
            lastTargetVelocity = Vector3.zero;
        }
    }

    void UpdateHoming()
    {
        if (targetPlayer == null)
        {
            FindPlayer();
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, targetPlayer.position);
        if (distanceToTarget > homingRange)
        {
            targetPlayer = null;
            return;
        }

        Vector3 currentTargetPosition = targetPlayer.position;
        currentTargetPosition.y = transform.position.y;

        Vector3 toTarget = currentTargetPosition - transform.position;
        toTarget.y = 0;

        if (toTarget.sqrMagnitude < 0.01f) return;

        Vector3 currentDirection = rb.velocity;
        currentDirection.y = 0;

        if (currentDirection.sqrMagnitude < 0.01f)
        {
            currentDirection = direction;
            currentDirection.y = 0;
        }

        currentDirection = currentDirection.normalized;

        float angleToTarget = Vector3.Angle(currentDirection, toTarget.normalized);

        if (angleToTarget > giveUpAngle)
        {
            targetPlayer = null;
            return;
        }

        if (angleToTarget > maxTrackingAngle) return;

        Vector3 targetVelocity = (currentTargetPosition - lastTargetPosition) / Time.deltaTime;
        targetVelocity.y = 0;

        targetVelocity = Vector3.Lerp(lastTargetVelocity, targetVelocity, 0.5f);
        lastTargetVelocity = targetVelocity;
        lastTargetPosition = currentTargetPosition;

        Vector3 targetPosition;
        if (usePredictiveAiming && targetVelocity.magnitude > 0.1f)
        {
            targetPosition = CalculateInterceptPoint(currentTargetPosition, targetVelocity);
        }
        else
        {
            targetPosition = currentTargetPosition;
        }

        Vector3 toTargetFinal = targetPosition - transform.position;
        toTargetFinal.y = 0;
        Vector3 desiredDirection = toTargetFinal.normalized;

        float maxRotationDelta = homingTurnSpeed * Time.deltaTime;

        Vector3 newDirection = Vector3.RotateTowards(
            currentDirection,
            desiredDirection,
            maxRotationDelta * Mathf.Deg2Rad,
            0f
        );

        newDirection.y = 0;
        newDirection = newDirection.normalized;

        rb.velocity = new Vector3(newDirection.x * speed, 0, newDirection.z * speed);
        transform.rotation = Quaternion.LookRotation(newDirection);
    }

    Vector3 CalculateInterceptPoint(Vector3 targetPos, Vector3 targetVel)
    {
        Vector3 toTarget = targetPos - transform.position;
        toTarget.y = 0;

        float a = targetVel.sqrMagnitude - speed * speed;
        float b = 2 * Vector3.Dot(targetVel, toTarget);
        float c = toTarget.sqrMagnitude;

        float discriminant = b * b - 4 * a * c;

        if (discriminant < 0) return targetPos;

        float t1 = (-b + Mathf.Sqrt(discriminant)) / (2 * a);
        float t2 = (-b - Mathf.Sqrt(discriminant)) / (2 * a);

        float t = (t1 > 0 && t2 > 0) ? Mathf.Min(t1, t2) : Mathf.Max(t1, t2);

        if (t < 0) return targetPos;

        Vector3 interceptPoint = targetPos + targetVel * t;
        interceptPoint.y = transform.position.y;
        return interceptPoint;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.contacts.Length == 0) return;

        // 忽略其他子弹
        if (collision.gameObject.GetComponent<Bullet>() != null ||
            collision.gameObject.GetComponent<MonsterBullet>() != null)
        {
            return;
        }

        Vector3 hitPoint = collision.contacts[0].point;
        Vector3 hitNormal = collision.contacts[0].normal;

        // 击中玩家
        if (collision.gameObject.CompareTag("Player"))
        {
            if (hasCollided) return;
            hasCollided = true;

            PlayerController player = collision.gameObject.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(damage, direction, knockbackForce);
                Debug.Log($"<color=red>怪物子弹击中玩家！伤害: {damage}</color>");
            }

            if (isExplosive)
            {
                TriggerExplosion(hitPoint);
            }

            Destroy(gameObject);
            return;
        }

        // 碰撞障碍物或墙壁
        if (collision.gameObject.CompareTag("Obstacle") || collision.gameObject.CompareTag("Wall"))
        {
            if (hasCollided) return;
            hasCollided = true;

            if (isExplosive)
            {
                TriggerExplosion(hitPoint);
            }

            if (isBouncy && currentBounces < maxBounces)
            {
                CreateBounceBullet(hitPoint, hitNormal);
                Destroy(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    void CreateBounceBullet(Vector3 bouncePosition, Vector3 bounceNormal)
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("反弹子弹预制体未设置！");
            return;
        }

        Vector3 reflectDirection = Vector3.Reflect(direction, bounceNormal);
        float newSpeed = speed * bounceSpeedMultiplier;

        GameObject bounceBullet = Instantiate(bulletPrefab, bouncePosition + reflectDirection * 0.1f, Quaternion.LookRotation(reflectDirection));

        MonsterBullet bulletScript = bounceBullet.GetComponent<MonsterBullet>();
        if (bulletScript != null)
        {
            bulletScript.Initialize(reflectDirection, newSpeed, damage, knockbackForce, currentBounces + 1);
        }
    }

    void TriggerExplosion(Vector3 explosionCenter)
    {
        Collider[] hitColliders = Physics.OverlapSphere(explosionCenter, explosionRadius);

        foreach (Collider hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Player"))
            {
                PlayerController player = hitCollider.GetComponent<PlayerController>();
                if (player != null)
                {
                    float distance = Vector3.Distance(explosionCenter, hitCollider.transform.position);

                    float finalDamage = explosionDamage;
                    float finalKnockback = explosionKnockback;

                    if (damageDecay)
                    {
                        float damageMultiplier = 1f - (distance / explosionRadius);
                        finalDamage *= damageMultiplier;
                        finalKnockback *= damageMultiplier;
                    }

                    Vector3 knockbackDirection = (hitCollider.transform.position - explosionCenter).normalized;
                    player.TakeDamage(finalDamage, knockbackDirection, finalKnockback);
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (isExplosive)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }

        if (isBouncy)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
        }

        if (isHoming)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, homingRange);

            if (targetPlayer != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, targetPlayer.position);
            }
        }

        // 红色方框标识怪物子弹
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
    }
}