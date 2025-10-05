using UnityEngine;

/// <summary>
/// 子弹游戏逻辑（配合 ProjectileMover 使用）
/// </summary>
public class Bullet : MonoBehaviour
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

    [Header("Slow Effect Settings")]
    [SerializeField] private bool hasSlowEffect = false;
    [SerializeField] private float slowMultiplier = 0.5f;
    [SerializeField] private float slowDuration = 2f;

    [Header("Homing Settings")]
    [SerializeField] private bool isHoming = false;
    [SerializeField] private float homingTurnSpeed = 180f;
    [SerializeField] private float homingRange = 15f;
    [SerializeField] private float homingActivationDelay = 0.1f;
    [SerializeField] private bool usePredictiveAiming = true;
    [SerializeField] private float maxTrackingAngle = 60f;
    [SerializeField] private float giveUpAngle = 90f;

    [Header("Piercing & AOE Damage Settings")]
    [SerializeField] private bool isPiercing = false;
    [SerializeField] private float piercingDamageRadius = 2f;
    [SerializeField] private float piercingDamageInterval = 0.2f;
    [SerializeField] private float piercingDamage = 10f;
    [SerializeField] private float piercingLifetime = 3f;

    private Vector3 direction;
    private float speed;
    private float damage;
    private float knockbackForce;
    private int currentBounces = 0;

    private Rigidbody rb;
    private ProjectileMover projectileMover;
    private bool hasCollided = false;

    private Transform targetEnemy;
    private float homingTimer = 0f;
    private Vector3 lastTargetPosition;
    private Vector3 lastTargetVelocity;

    private float piercingDamageTimer = 0f;
    private float piercingExistTime = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
        }

        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        projectileMover = GetComponent<ProjectileMover>();

        // 设置子弹Layer并配置忽略碰撞
        SetupBulletLayer();
    }

    void Start()
    {
        IgnoreOtherBullets();

        if (isHoming)
        {
            FindNearestEnemy();
        }
    }

    /// <summary>
    /// 设置子弹Layer并配置忽略Player层的碰撞
    /// </summary>
    void SetupBulletLayer()
    {
        // 获取Player层
        int playerLayer = LayerMask.NameToLayer("Player");

        if (isPiercing)
        {
            // 穿透子弹：设置到 "PiercingBullet" Layer
            int piercingLayer = LayerMask.NameToLayer("PiercingBullet");
            if (piercingLayer == -1)
            {
                // 如果没有PiercingBullet Layer，使用默认子弹Layer
                piercingLayer = LayerMask.NameToLayer("Bullet");
                if (piercingLayer == -1)
                {
                    Debug.LogWarning("未找到 'PiercingBullet' 或 'Bullet' Layer，请在项目设置中创建！");
                    piercingLayer = gameObject.layer;
                }
            }

            gameObject.layer = piercingLayer;

            // 忽略 PiercingBullet Layer 与 Monster Layer 之间的碰撞
            int monsterLayer = LayerMask.NameToLayer("Monster");
            if (monsterLayer != -1)
            {
                Physics.IgnoreLayerCollision(piercingLayer, monsterLayer, true);
            }

            // 忽略 PiercingBullet Layer 与 Player Layer 之间的碰撞
            if (playerLayer != -1)
            {
                Physics.IgnoreLayerCollision(piercingLayer, playerLayer, true);
            }
        }
        else
        {
            // 普通子弹：设置到 "Bullet" Layer
            int bulletLayer = LayerMask.NameToLayer("Bullet");
            if (bulletLayer == -1)
            {
                Debug.LogWarning("未找到 'Bullet' Layer，请在项目设置中创建！");
                bulletLayer = gameObject.layer;
            }

            gameObject.layer = bulletLayer;

            // 忽略 Bullet Layer 与 Player Layer 之间的碰撞
            if (playerLayer != -1)
            {
                Physics.IgnoreLayerCollision(bulletLayer, playerLayer, true);
            }
        }
    }

    void Update()
    {
        if (isHoming)
        {
            homingTimer += Time.deltaTime;

            if (homingTimer >= homingActivationDelay && targetEnemy != null)
            {
                UpdateHoming();
            }
        }

        if (isPiercing)
        {
            piercingExistTime += Time.deltaTime;
            piercingDamageTimer += Time.deltaTime;

            if (piercingDamageTimer >= piercingDamageInterval)
            {
                DealPiercingDamage();
                piercingDamageTimer = 0f;
            }

            if (piercingExistTime >= piercingLifetime)
            {
                Destroy(gameObject);
            }
        }
    }

    void IgnoreOtherBullets()
    {
        Bullet[] allBullets = FindObjectsOfType<Bullet>();
        Collider myCollider = GetComponent<Collider>();

        if (myCollider == null) return;

        foreach (Bullet otherBullet in allBullets)
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

        if (projectileMover != null)
        {
            projectileMover.speed = speed;
        }
        else
        {
            rb.velocity = direction * speed;
        }

        if (isPiercing)
        {
            Destroy(gameObject, piercingLifetime);
        }
        else
        {
            Destroy(gameObject, lifetime);
        }
    }

    /// <summary>
    /// 供 ProjectileMover 调用，判断是否是穿透子弹
    /// </summary>
    public bool IsPiercing()
    {
        return isPiercing;
    }

    void DealPiercingDamage()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, piercingDamageRadius);

        foreach (Collider hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Monster"))
            {
                MonsterHealth monsterHealth = hitCollider.GetComponent<MonsterHealth>();
                if (monsterHealth != null)
                {
                    Vector3 damageDirection = (hitCollider.transform.position - transform.position).normalized;
                    monsterHealth.TakeDamage(piercingDamage, damageDirection, knockbackForce * 0.3f);

                    if (hasSlowEffect)
                    {
                        MonsterAI monsterAI = hitCollider.GetComponent<MonsterAI>();
                        if (monsterAI != null)
                        {
                            monsterAI.ApplySlow(slowMultiplier, slowDuration);
                        }
                    }
                }
            }
        }
    }

    void FindNearestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Monster");
        float closestDistance = homingRange;
        Transform closestEnemy = null;

        foreach (GameObject enemy in enemies)
        {
            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = enemy.transform;
            }
        }

        if (closestEnemy != null)
        {
            targetEnemy = closestEnemy;
            lastTargetPosition = targetEnemy.position;
            lastTargetPosition.y = transform.position.y;
            lastTargetVelocity = Vector3.zero;
        }
    }

    void UpdateHoming()
    {
        if (targetEnemy == null)
        {
            FindNearestEnemy();
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, targetEnemy.position);
        if (distanceToTarget > homingRange)
        {
            targetEnemy = null;
            return;
        }

        Vector3 currentTargetPosition = targetEnemy.position;
        currentTargetPosition.y = transform.position.y;

        Vector3 toTarget = currentTargetPosition - transform.position;
        toTarget.y = 0;

        if (toTarget.sqrMagnitude < 0.01f)
        {
            return;
        }

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
            targetEnemy = null;
            return;
        }

        if (angleToTarget > maxTrackingAngle)
        {
            return;
        }

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

        if (projectileMover != null)
        {
            projectileMover.speed = speed;
            transform.rotation = Quaternion.LookRotation(newDirection);
        }
    }

    Vector3 CalculateInterceptPoint(Vector3 targetPos, Vector3 targetVel)
    {
        Vector3 toTarget = targetPos - transform.position;
        toTarget.y = 0;

        float a = targetVel.sqrMagnitude - speed * speed;
        float b = 2 * Vector3.Dot(targetVel, toTarget);
        float c = toTarget.sqrMagnitude;

        float discriminant = b * b - 4 * a * c;

        if (discriminant < 0)
        {
            return targetPos;
        }

        float t1 = (-b + Mathf.Sqrt(discriminant)) / (2 * a);
        float t2 = (-b - Mathf.Sqrt(discriminant)) / (2 * a);

        float t = (t1 > 0 && t2 > 0) ? Mathf.Min(t1, t2) : Mathf.Max(t1, t2);

        if (t < 0)
        {
            return targetPos;
        }

        Vector3 interceptPoint = targetPos + targetVel * t;
        interceptPoint.y = transform.position.y;
        return interceptPoint;
    }

    public bool ShouldBounce(Collision collision)
    {
        if (hasCollided) return false;

        // 确保不会与Player层发生任何交互
        if (collision.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            return false;
        }

        if (collision.gameObject.CompareTag("Obstacle") || collision.gameObject.CompareTag("Wall"))
        {
            return isBouncy && currentBounces < maxBounces;
        }

        return false;
    }

    void OnCollisionEnter(Collision collision)
    {
        // 双重检查：即使Layer设置了忽略，这里也再次检查
        if (collision.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            return;
        }

        if (collision.gameObject.GetComponent<Bullet>() != null)
        {
            return;
        }

        if (collision == null || collision.contacts.Length == 0)
        {
            return;
        }

        Vector3 hitPoint = collision.contacts[0].point;
        Vector3 hitNormal = collision.contacts[0].normal;

        if (collision.gameObject.CompareTag("Monster"))
        {
            // 穿透子弹通过Layer忽略，理论上不会碰到怪物
            if (isPiercing)
            {
                Debug.LogWarning("穿透子弹碰到了怪物！请检查Layer设置。");
                return;
            }

            // 非穿透模式：正常碰撞销毁
            if (hasCollided) return;
            hasCollided = true;

            MonsterHealth monsterHealth = collision.gameObject.GetComponent<MonsterHealth>();
            if (monsterHealth != null)
            {
                monsterHealth.TakeDamage(damage, direction, knockbackForce);

                if (hasSlowEffect)
                {
                    MonsterAI monsterAI = collision.gameObject.GetComponent<MonsterAI>();
                    if (monsterAI != null)
                    {
                        monsterAI.ApplySlow(slowMultiplier, slowDuration);
                    }
                }
            }

            if (isExplosive)
            {
                TriggerExplosion(hitPoint);
            }

            Destroy(gameObject);
        }
        else if (collision.gameObject.CompareTag("Obstacle") || collision.gameObject.CompareTag("Wall"))
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

        Bullet bulletScript = bounceBullet.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.Initialize(reflectDirection, newSpeed, damage, knockbackForce, currentBounces + 1);
        }
    }

    void TriggerExplosion(Vector3 explosionCenter)
    {
        Collider[] hitColliders = Physics.OverlapSphere(explosionCenter, explosionRadius);

        int hitCount = 0;
        foreach (Collider hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Monster"))
            {
                MonsterHealth monsterHealth = hitCollider.GetComponent<MonsterHealth>();
                if (monsterHealth != null)
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

                    monsterHealth.TakeDamage(finalDamage, knockbackDirection, finalKnockback);

                    if (hasSlowEffect)
                    {
                        MonsterAI monsterAI = hitCollider.GetComponent<MonsterAI>();
                        if (monsterAI != null)
                        {
                            monsterAI.ApplySlow(slowMultiplier, slowDuration);
                        }
                    }

                    hitCount++;
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

        if (hasSlowEffect)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }

        if (isHoming)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, homingRange);

            if (targetEnemy != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, targetEnemy.position);
            }
        }

        if (isPiercing)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, piercingDamageRadius);
        }
    }
}