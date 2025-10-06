using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss AI - 多技能战斗系统
/// </summary>
public class BossAI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private MonsterHealth monsterHealth;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Boss Settings")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float rotationSpeed = 3f;

    [Header("Skill Cooldowns")]
    [SerializeField] private float skillCooldown = 1f;           // 技能间隔时间
    [SerializeField] private float minDistanceToPlayer = 5f;     // 最小追击距离

    [Header("Teleport Settings")]
    [SerializeField] private float maxDistanceFromPlayer = 20f;  // 最大距离，超过会瞬移
    [SerializeField] private float teleportDistance = 8f;        // 瞬移到玩家周围的距离

    [Header("Skill 1: Aimed Barrage")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private int aimedBulletCount = 5;           // 瞄准弹幕数量
    [SerializeField] private float aimedBulletInterval = 0.2f;   // 瞄准弹幕间隔
    [SerializeField] private float aimedBulletSpeed = 15f;
    [SerializeField] private float aimedBulletDamage = 15f;

    [Header("Skill 2: Dense Barrage")]
    [SerializeField] private int denseBulletCount = 24;          // 密集弹幕数量（360度均分）
    [SerializeField] private float denseBulletSpeed = 10f;
    [SerializeField] private float denseBulletDamage = 10f;

    [Header("Skill 3: AOE at Player")]
    [SerializeField] private GameObject aoePrefab;               // AOE预警+伤害的预制体
    [SerializeField] private float aoeWarningTime = 1.5f;        // AOE预警时间
    [SerializeField] private float aoeDamage = 30f;
    [SerializeField] private float aoeRadius = 3f;

    [Header("Skill 4: Summon Monsters")]
    [SerializeField] private List<GameObject> monsterPrefabs;    // 召唤的怪物预制体列表
    [SerializeField] private int summonCount = 3;                // 召唤数量
    [SerializeField] private float summonRadius = 8f;            // 召唤范围（在玩家周围）

    [Header("Skill 5: Charge Attack")]
    [SerializeField] private float chargeDistance = 15f;         // 冲撞距离
    [SerializeField] private float chargeDuration = 0.8f;        // 冲撞持续时间
    [SerializeField] private float chargeDamage = 25f;
    [SerializeField] private float chargeKnockback = 15f;
    [SerializeField] private float chargeActivateRange = 10f;    // 冲撞激活距离

    [Header("Skill 6: Spawn Obstacles")]
    [SerializeField] private GameObject obstaclePrefab;          // 阻碍物预制体
    [SerializeField] private int obstacleCount = 5;              // 阻碍物数量
    [SerializeField] private float obstacleSpawnRadius = 12f;    // 生成范围

    // 状态管理
    private enum BossState
    {
        Idle,
        SelectingSkill,
        ExecutingSkill,
        Cooldown
    }

    private BossState currentState = BossState.Idle;
    private bool isExecutingSkill = false;
    private bool hasSpawnedObstacles = false;  // 是否已经召唤过阻碍物（用于技能2的前置条件）
    private CharacterController characterController;
    private float stateTimer = 0f;

    void Start()
    {
        // 获取组件
        if (monsterHealth == null)
        {
            monsterHealth = GetComponent<MonsterHealth>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
            characterController.radius = 1f;
            characterController.height = 2f;
        }

        // 查找玩家
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                Debug.LogError("Boss AI: 未找到玩家！");
            }
        }

        if (firePoint == null)
        {
            firePoint = transform;
        }

        // 开始战斗
        currentState = BossState.Idle;
        stateTimer = 1f; // 初始延迟
    }

    void Update()
    {
        if (playerTransform == null) return;

        // 状态机
        switch (currentState)
        {
            case BossState.Idle:
                UpdateIdle();
                break;
            case BossState.SelectingSkill:
                SelectAndExecuteSkill();
                break;
            case BossState.ExecutingSkill:
                // 等待技能执行完成
                break;
            case BossState.Cooldown:
                UpdateCooldown();
                break;
        }

        // 朝向玩家
        LookAtPlayer();
    }

    void UpdateIdle()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0)
        {
            currentState = BossState.SelectingSkill;
        }
    }

    void UpdateCooldown()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0)
        {
            // 检查是否需要瞬移
            CheckTeleport();
            currentState = BossState.SelectingSkill;
        }
    }

    /// <summary>
    /// 选择并执行技能
    /// </summary>
    void SelectAndExecuteSkill()
    {
        List<int> availableSkills = GetAvailableSkills();

        if (availableSkills.Count == 0)
        {
            Debug.LogWarning("Boss: 没有可用技能！");
            currentState = BossState.Cooldown;
            stateTimer = skillCooldown;
            return;
        }

        // 随机选择一个技能
        int selectedSkill = availableSkills[Random.Range(0, availableSkills.Count)];

        Debug.Log($"<color=red>Boss 使用技能 {selectedSkill}</color>");

        // 执行技能
        currentState = BossState.ExecutingSkill;
        StartCoroutine(ExecuteSkill(selectedSkill));
    }

    /// <summary>
    /// 获取当前可用的技能列表
    /// </summary>
    List<int> GetAvailableSkills()
    {
        List<int> available = new List<int>();
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        // 技能1：瞄准弹幕（始终可用）
        available.Add(1);

        // 技能2：密集弹幕（需要先召唤过阻碍物）
        if (hasSpawnedObstacles)
        {
            available.Add(2);
        }

        // 技能3：玩家脚下AOE（始终可用）
        available.Add(3);

        // 技能4：召唤怪物（始终可用）
        if (monsterPrefabs != null && monsterPrefabs.Count > 0)
        {
            available.Add(4);
        }

        // 技能5：冲撞（需要玩家在一定范围内）
        if (distanceToPlayer <= chargeActivateRange)
        {
            available.Add(5);
        }

        // 技能6：召唤阻碍物（始终可用）
        if (obstaclePrefab != null)
        {
            available.Add(6);
        }

        return available;
    }

    /// <summary>
    /// 执行选中的技能
    /// </summary>
    IEnumerator ExecuteSkill(int skillIndex)
    {
        switch (skillIndex)
        {
            case 1:
                yield return StartCoroutine(Skill_AimedBarrage());
                break;
            case 2:
                yield return StartCoroutine(Skill_DenseBarrage());
                break;
            case 3:
                yield return StartCoroutine(Skill_AOEAtPlayer());
                break;
            case 4:
                yield return StartCoroutine(Skill_SummonMonsters());
                break;
            case 5:
                yield return StartCoroutine(Skill_ChargeAttack());
                break;
            case 6:
                yield return StartCoroutine(Skill_SpawnObstacles());
                break;
        }

        // 技能执行完毕，进入冷却
        currentState = BossState.Cooldown;
        stateTimer = skillCooldown;
    }

    #region 技能实现

    /// <summary>
    /// 技能1：瞄准弹幕射击
    /// </summary>
    IEnumerator Skill_AimedBarrage()
    {
        Debug.Log("<color=yellow>Boss: 瞄准弹幕射击！</color>");

        for (int i = 0; i < aimedBulletCount; i++)
        {
            if (playerTransform == null) break;

            // 计算朝向玩家的方向
            Vector3 direction = (playerTransform.position - firePoint.position).normalized;
            direction.y = 0;

            // 实例化子弹
            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.LookRotation(direction));
            Bullet bulletScript = bullet.GetComponent<Bullet>();
            if (bulletScript != null)
            {
                
                bulletScript.Initialize(direction, aimedBulletSpeed, aimedBulletDamage);
            }

            yield return new WaitForSeconds(aimedBulletInterval);
        }

        yield return new WaitForSeconds(0.5f);
    }

    /// <summary>
    /// 技能2：密集弹幕攻击（360度）
    /// </summary>
    IEnumerator Skill_DenseBarrage()
    {
        Debug.Log("<color=yellow>Boss: 密集弹幕攻击！</color>");

        float angleStep = 360f / denseBulletCount;

        for (int i = 0; i < denseBulletCount; i++)
        {
            float angle = i * angleStep;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;

            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.LookRotation(direction));
            Bullet bulletScript = bullet.GetComponent<Bullet>();
            if (bulletScript != null)
            {
                
                bulletScript.Initialize(direction, denseBulletSpeed, denseBulletDamage);
            }
        }

        yield return new WaitForSeconds(1f);
    }

    /// <summary>
    /// 技能3：玩家脚下召唤AOE
    /// </summary>
    IEnumerator Skill_AOEAtPlayer()
    {
        Debug.Log("<color=yellow>Boss: 玩家脚下AOE！</color>");

        if (playerTransform == null) yield break;

        Vector3 aoePosition = playerTransform.position;

        // 生成AOE预警
        GameObject aoeWarning = null;
        if (aoePrefab != null)
        {
            aoeWarning = Instantiate(aoePrefab, aoePosition, Quaternion.identity);
        }

        // 等待预警时间
        yield return new WaitForSeconds(aoeWarningTime);

        // 检测范围内的玩家并造成伤害
        Collider[] hits = Physics.OverlapSphere(aoePosition, aoeRadius);
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                PlayerController player = hit.GetComponent<PlayerController>();
                if (player != null)
                {
                    Vector3 knockbackDir = (hit.transform.position - aoePosition).normalized;
                    player.TakeDamage(aoeDamage, knockbackDir, 10f);
                    Debug.Log($"AOE击中玩家！伤害: {aoeDamage}");
                }
            }
        }

        // 销毁AOE预警
        if (aoeWarning != null)
        {
            Destroy(aoeWarning, 0.5f);
        }

        yield return new WaitForSeconds(0.5f);
    }

    /// <summary>
    /// 技能4：召唤怪物
    /// </summary>
    IEnumerator Skill_SummonMonsters()
    {
        Debug.Log("<color=yellow>Boss: 召唤怪物！</color>");

        if (playerTransform == null || monsterPrefabs == null || monsterPrefabs.Count == 0)
        {
            yield break;
        }

        for (int i = 0; i < summonCount; i++)
        {
            // 在玩家周围随机位置生成怪物
            Vector2 randomCircle = Random.insideUnitCircle * summonRadius;
            Vector3 summonPosition = playerTransform.position + new Vector3(randomCircle.x, 0, randomCircle.y);

            // 随机选择怪物预制体
            GameObject monsterPrefab = monsterPrefabs[Random.Range(0, monsterPrefabs.Count)];
            GameObject monster = Instantiate(monsterPrefab, summonPosition, Quaternion.identity);
            monster.name = $"SummonedMonster_{i}";

            Debug.Log($"召唤怪物 {i + 1}/{summonCount} 在 {summonPosition}");

            yield return new WaitForSeconds(0.3f);
        }

        yield return new WaitForSeconds(0.5f);
    }

    /// <summary>
    /// 技能5：冲撞攻击
    /// </summary>
    IEnumerator Skill_ChargeAttack()
    {
        Debug.Log("<color=yellow>Boss: 冲撞攻击！</color>");

        if (playerTransform == null) yield break;

        // 计算冲撞方向
        Vector3 chargeDirection = (playerTransform.position - transform.position).normalized;
        chargeDirection.y = 0;

        float chargeSpeed = chargeDistance / chargeDuration;
        float elapsed = 0f;

        // 冲撞过程
        while (elapsed < chargeDuration)
        {
            Vector3 movement = chargeDirection * chargeSpeed * Time.deltaTime;

            if (characterController != null)
            {
                characterController.Move(movement);
            }
            else
            {
                transform.position += movement;
            }

            // 检测是否撞到玩家
            Collider[] hits = Physics.OverlapSphere(transform.position, 1.5f);
            foreach (Collider hit in hits)
            {
                if (hit.CompareTag("Player"))
                {
                    PlayerController player = hit.GetComponent<PlayerController>();
                    if (player != null)
                    {
                        player.TakeDamage(chargeDamage, chargeDirection, chargeKnockback);
                        Debug.Log($"冲撞击中玩家！伤害: {chargeDamage}");
                        yield break; // 撞到玩家后结束冲撞
                    }
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);
    }

    /// <summary>
    /// 技能6：召唤阻碍物
    /// </summary>
    IEnumerator Skill_SpawnObstacles()
    {
        Debug.Log("<color=yellow>Boss: 召唤阻碍物！</color>");

        if (obstaclePrefab == null) yield break;

        for (int i = 0; i < obstacleCount; i++)
        {
            // 在Boss周围随机位置生成阻碍物
            Vector2 randomCircle = Random.insideUnitCircle * obstacleSpawnRadius;
            Vector3 spawnPosition = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);

            GameObject obstacle = Instantiate(obstaclePrefab, spawnPosition, Quaternion.identity);
            obstacle.name = $"BossObstacle_{i}";

            yield return new WaitForSeconds(0.2f);
        }

        hasSpawnedObstacles = true; // 标记已召唤过阻碍物
        Debug.Log("阻碍物召唤完成，技能2已解锁！");

        yield return new WaitForSeconds(0.5f);
    }

    #endregion

    /// <summary>
    /// 检查并执行瞬移
    /// </summary>
    void CheckTeleport()
    {
        if (playerTransform == null) return;

        float distance = Vector3.Distance(transform.position, playerTransform.position);

        if (distance > maxDistanceFromPlayer)
        {
            // 瞬移到玩家周围
            Vector2 randomCircle = Random.insideUnitCircle.normalized * teleportDistance;
            Vector3 teleportPosition = playerTransform.position + new Vector3(randomCircle.x, 0, randomCircle.y);

            if (characterController != null)
            {
                characterController.enabled = false;
                transform.position = teleportPosition;
                characterController.enabled = true;
            }
            else
            {
                transform.position = teleportPosition;
            }

            Debug.Log($"<color=cyan>Boss 瞬移到玩家附近！距离: {distance} -> {Vector3.Distance(transform.position, playerTransform.position)}</color>");
        }
    }

    /// <summary>
    /// 朝向玩家
    /// </summary>
    void LookAtPlayer()
    {
        if (playerTransform == null || currentState == BossState.ExecutingSkill) return;

        Vector3 direction = (playerTransform.position - transform.position).normalized;
        direction.y = 0;

        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (playerTransform == null) return;

        // 绘制冲撞激活范围
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, chargeActivateRange);

        // 绘制最大距离
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, maxDistanceFromPlayer);

        // 绘制瞬移距离
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, teleportDistance);

        // 绘制召唤范围
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(playerTransform.position, summonRadius);
    }
}