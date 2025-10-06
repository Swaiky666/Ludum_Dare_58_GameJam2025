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

    [Header("Spawn Settings")]
    [SerializeField] private float spawnSearchRadius = 15f;     // 生成时搜索玩家的半径
    [SerializeField] private float spawnDistance = 10f;         // 在玩家周围生成的距离
    [SerializeField] private float spawnDelay = 2f;             // 生成后延迟行动的时间
    [SerializeField] private float playerSearchInterval = 0.5f; // 搜索玩家的间隔

    [Header("Skill Cooldowns")]
    [SerializeField] private float skillCooldown = 1f;
    [SerializeField] private float minDistanceToPlayer = 5f;

    [Header("Teleport Settings")]
    [SerializeField] private float maxDistanceFromPlayer = 20f;
    [SerializeField] private float teleportDistance = 8f;

    [Header("Skill 1: Aimed Barrage")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private int aimedBulletCount = 5;
    [SerializeField] private float aimedBulletInterval = 0.2f;
    [SerializeField] private float aimedBulletSpeed = 15f;
    [SerializeField] private float aimedBulletDamage = 15f;

    [Header("Skill 2: Dense Barrage")]
    [SerializeField] private int denseBulletCount = 24;
    [SerializeField] private float denseBulletSpeed = 10f;
    [SerializeField] private float denseBulletDamage = 10f;
    [SerializeField] private int minBarrageRounds = 1;          // 最小轮数
    [SerializeField] private int maxBarrageRounds = 3;          // 最大轮数
    [SerializeField] private float barrageRoundInterval = 0.8f; // 每轮之间的间隔

    [Header("Skill 3: AOE at Player")]
    [SerializeField] private GameObject aoePrefab;
    [SerializeField] private float aoeWarningTime = 1.5f;
    [SerializeField] private float aoeDamage = 30f;
    [SerializeField] private float aoeRadius = 3f;

    [Header("Skill 4: Summon Monsters")]
    [SerializeField] private List<GameObject> monsterPrefabs;
    [SerializeField] private int summonCount = 3;
    [SerializeField] private float summonRadius = 8f;

    [Header("Skill 5: Charge Attack")]
    [SerializeField] private float chargeDistance = 15f;
    [SerializeField] private float chargeDuration = 0.8f;
    [SerializeField] private float chargeDamage = 25f;
    [SerializeField] private float chargeKnockback = 15f;
    [SerializeField] private float chargeActivateRange = 10f;

    [Header("Skill 6: Spawn Obstacles")]
    [SerializeField] private GameObject obstaclePrefab;
    [SerializeField] private int obstacleCount = 5;
    [SerializeField] private float obstacleSpawnRadius = 12f;

    private enum BossState
    {
        Spawning,        // 新增：生成状态
        Idle,
        SelectingSkill,
        ExecutingSkill,
        Cooldown
    }

    private BossState currentState = BossState.Spawning;
    private bool isExecutingSkill = false;
    private CharacterController characterController;
    private float stateTimer = 0f;
    private float playerSearchTimer = 0f;
    private bool hasFoundPlayer = false;

    void Start()
    {
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

        if (firePoint == null)
        {
            firePoint = transform;
        }

        // 开始生成流程
        currentState = BossState.Spawning;
        stateTimer = spawnDelay;
        StartCoroutine(SpawnSequence());
    }

    /// <summary>
    /// 生成序列：搜索玩家并移动到附近
    /// </summary>
    IEnumerator SpawnSequence()
    {
        Debug.Log("<color=cyan>Boss 开始生成序列...</color>");

        float elapsed = 0f;
        while (elapsed < spawnDelay)
        {
            // 定期搜索玩家（但只更新一次位置）
            playerSearchTimer += Time.deltaTime;
            if (playerSearchTimer >= playerSearchInterval && !hasFoundPlayer)
            {
                SearchForPlayer();
                playerSearchTimer = 0f;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 如果找到了玩家，移动到玩家附近
        if (hasFoundPlayer && playerTransform != null)
        {
            Vector2 randomCircle = Random.insideUnitCircle.normalized * spawnDistance;
            Vector3 spawnPosition = playerTransform.position + new Vector3(randomCircle.x, 0, randomCircle.y);

            if (characterController != null)
            {
                characterController.enabled = false;
                transform.position = spawnPosition;
                characterController.enabled = true;
            }
            else
            {
                transform.position = spawnPosition;
            }

            Debug.Log($"<color=cyan>Boss 移动到玩家附近: {spawnPosition}</color>");
        }

        // 开始战斗
        currentState = BossState.Idle;
        stateTimer = 1f;
        Debug.Log("<color=cyan>Boss 开始战斗！</color>");
    }

    /// <summary>
    /// 搜索玩家（只更新一次）
    /// </summary>
    void SearchForPlayer()
    {
        if (hasFoundPlayer) return;

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                hasFoundPlayer = true;
                Debug.Log("<color=green>Boss 找到玩家！</color>");
            }
        }
        else
        {
            hasFoundPlayer = true;
        }
    }

    void Update()
    {
        if (currentState == BossState.Spawning)
        {
            // 生成状态中不执行其他逻辑
            return;
        }

        if (playerTransform == null) return;

        switch (currentState)
        {
            case BossState.Idle:
                UpdateIdle();
                break;
            case BossState.SelectingSkill:
                SelectAndExecuteSkill();
                break;
            case BossState.ExecutingSkill:
                break;
            case BossState.Cooldown:
                UpdateCooldown();
                break;
        }

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
            CheckTeleport();
            currentState = BossState.SelectingSkill;
        }
    }

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

        int selectedSkill = availableSkills[Random.Range(0, availableSkills.Count)];
        Debug.Log($"<color=red>Boss 使用技能 {selectedSkill}</color>");

        currentState = BossState.ExecutingSkill;
        StartCoroutine(ExecuteSkill(selectedSkill));
    }

    List<int> GetAvailableSkills()
    {
        List<int> available = new List<int>();
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        available.Add(1); // 技能1：瞄准弹幕（始终可用）
        available.Add(2); // 技能2：密集弹幕（始终可用，已移除前置条件）
        available.Add(3); // 技能3：玩家脚下AOE（始终可用）

        if (monsterPrefabs != null && monsterPrefabs.Count > 0)
        {
            available.Add(4); // 技能4：召唤怪物
        }

        if (distanceToPlayer <= chargeActivateRange)
        {
            available.Add(5); // 技能5：冲撞
        }

        if (obstaclePrefab != null)
        {
            available.Add(6); // 技能6：召唤阻碍物（可多次触发）
        }

        return available;
    }

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

        currentState = BossState.Cooldown;
        stateTimer = skillCooldown;
    }

    #region 技能实现

    IEnumerator Skill_AimedBarrage()
    {
        Debug.Log("<color=yellow>Boss: 瞄准弹幕射击！</color>");

        for (int i = 0; i < aimedBulletCount; i++)
        {
            if (playerTransform == null) break;

            Vector3 direction = (playerTransform.position - firePoint.position).normalized;
            direction.y = 0;

            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.LookRotation(direction));
            MonsterBullet bulletScript = bullet.GetComponent<MonsterBullet>();
            if (bulletScript != null)
            {
                bulletScript.Initialize(direction, aimedBulletSpeed, aimedBulletDamage);
            }

            yield return new WaitForSeconds(aimedBulletInterval);
        }

        yield return new WaitForSeconds(0.5f);
    }

    IEnumerator Skill_DenseBarrage()
    {
        // 随机决定轮数
        int rounds = Random.Range(minBarrageRounds, maxBarrageRounds + 1);
        Debug.Log($"<color=yellow>Boss: 密集弹幕攻击！{rounds} 轮</color>");

        for (int round = 0; round < rounds; round++)
        {
            float angleStep = 360f / denseBulletCount;

            for (int i = 0; i < denseBulletCount; i++)
            {
                float angle = i * angleStep;
                Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;

                GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.LookRotation(direction));
                MonsterBullet bulletScript = bullet.GetComponent<MonsterBullet>();
                if (bulletScript != null)
                {
                    bulletScript.Initialize(direction, denseBulletSpeed, denseBulletDamage);
                }
            }

            // 如果还有下一轮，等待间隔
            if (round < rounds - 1)
            {
                yield return new WaitForSeconds(barrageRoundInterval);
            }
        }

        yield return new WaitForSeconds(1f);
    }

    IEnumerator Skill_AOEAtPlayer()
    {
        Debug.Log("<color=yellow>Boss: 玩家脚下AOE！</color>");

        if (playerTransform == null) yield break;

        Vector3 aoePosition = playerTransform.position;
        GameObject aoeWarning = null;
        if (aoePrefab != null)
        {
            aoeWarning = Instantiate(aoePrefab, aoePosition, Quaternion.identity);
        }

        yield return new WaitForSeconds(aoeWarningTime);

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

        if (aoeWarning != null)
        {
            Destroy(aoeWarning, 0.5f);
        }

        yield return new WaitForSeconds(0.5f);
    }

    IEnumerator Skill_SummonMonsters()
    {
        Debug.Log("<color=yellow>Boss: 召唤怪物！</color>");

        if (playerTransform == null || monsterPrefabs == null || monsterPrefabs.Count == 0)
        {
            yield break;
        }

        for (int i = 0; i < summonCount; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * summonRadius;
            Vector3 summonPosition = playerTransform.position + new Vector3(randomCircle.x, 0, randomCircle.y);

            GameObject monsterPrefab = monsterPrefabs[Random.Range(0, monsterPrefabs.Count)];
            GameObject monster = Instantiate(monsterPrefab, summonPosition, Quaternion.identity);
            monster.name = $"SummonedMonster_{i}";

            Debug.Log($"召唤怪物 {i + 1}/{summonCount} 在 {summonPosition}");

            yield return new WaitForSeconds(0.3f);
        }

        yield return new WaitForSeconds(0.5f);
    }

    IEnumerator Skill_ChargeAttack()
    {
        Debug.Log("<color=yellow>Boss: 冲撞攻击！</color>");

        if (playerTransform == null) yield break;

        Vector3 chargeDirection = (playerTransform.position - transform.position).normalized;
        chargeDirection.y = 0;

        float chargeSpeed = chargeDistance / chargeDuration;
        float elapsed = 0f;

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
                        yield break;
                    }
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);
    }

    IEnumerator Skill_SpawnObstacles()
    {
        Debug.Log("<color=yellow>Boss: 召唤阻碍物！</color>");

        if (obstaclePrefab == null) yield break;

        for (int i = 0; i < obstacleCount; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * obstacleSpawnRadius;
            Vector3 spawnPosition = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);

            GameObject obstacle = Instantiate(obstaclePrefab, spawnPosition, Quaternion.identity);
            obstacle.name = $"BossObstacle_{Time.time}_{i}";

            yield return new WaitForSeconds(0.2f);
        }

        Debug.Log("阻碍物召唤完成！");

        yield return new WaitForSeconds(0.5f);
    }

    #endregion

    void CheckTeleport()
    {
        if (playerTransform == null) return;

        float distance = Vector3.Distance(transform.position, playerTransform.position);

        if (distance > maxDistanceFromPlayer)
        {
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

            Debug.Log($"<color=cyan>Boss 瞬移到玩家附近！</color>");
        }
    }

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

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, chargeActivateRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, maxDistanceFromPlayer);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, teleportDistance);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(playerTransform.position, summonRadius);

        // 生成时的搜索范围
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, spawnSearchRadius);
    }
}