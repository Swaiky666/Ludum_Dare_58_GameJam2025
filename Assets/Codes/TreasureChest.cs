using UnityEngine;

public class TreasureChest : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject interactPanel;      // "按E打开"的World Space Canvas面板
    [SerializeField] private float interactDistance = 3f;   // 交互距离

    [Header("Effects")]
    [SerializeField] private ParticleSystem disappearEffect; // 消失粒子特效
    [SerializeField] private AudioClip openSound;            // 打开音效
    [SerializeField] private float effectDestroyDelay = 3f;  // 特效销毁延迟

    [Header("References")]
    [SerializeField] private CollectionManager collectionManager;

    [Header("Drop Settings")]
    [SerializeField] private float dropHeight = 1f;          // 掉落物生成高度偏移

    private Transform playerTransform;
    private bool playerInRange = false;
    private bool isOpened = false;                           // 防止重复打开

    void Start()
    {
        // 找到玩家（通过Tag）
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogWarning("未找到带有'Player'标签的对象！");
        }

        // 获取CollectionManager实例
        if (collectionManager == null)
        {
            collectionManager = CollectionManager.Instance;
            if (collectionManager == null)
            {
                Debug.LogError("CollectionManager实例不存在！");
            }
        }

        // 隐藏交互面板
        if (interactPanel != null)
        {
            interactPanel.SetActive(false);
        }
        else
        {
            Debug.LogWarning("InteractPanel未设置！请在Inspector中指定UI面板。");
        }
    }

    void Update()
    {
        if (isOpened || playerTransform == null) return;

        // 检查玩家距离（只计算XZ平面距离，忽略Y轴）
        Vector3 playerPos = new Vector3(playerTransform.position.x, transform.position.y, playerTransform.position.z);
        float distance = Vector3.Distance(transform.position, playerPos);
        playerInRange = distance <= interactDistance;

        // 显示/隐藏交互提示
        if (interactPanel != null)
        {
            interactPanel.SetActive(playerInRange);
        }

        // 检测E键输入
        if (playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            OpenChest();
        }
    }

    /// <summary>
    /// 打开宝箱
    /// </summary>
    void OpenChest()
    {
        if (isOpened) return;
        isOpened = true;

        // 隐藏交互面板
        if (interactPanel != null)
        {
            interactPanel.SetActive(false);
        }

        if (collectionManager == null)
        {
            Debug.LogError("CollectionManager引用为空！无法打开宝箱。");
            Destroy(gameObject);
            return;
        }

        // 获取所有收集品
        var allCollectibles = collectionManager.GetAllCollectibles();

        if (allCollectibles == null || allCollectibles.Count == 0)
        {
            Debug.LogWarning("CollectionManager中没有可用的收集品！");
            PlayEffectsAndDestroy();
            return;
        }

        // 随机选择一个收集品
        CollectibleData randomCollectible = allCollectibles[Random.Range(0, allCollectibles.Count)];

        Debug.Log($"<color=cyan>宝箱打开！抽取到: {randomCollectible.itemName} (ID: {randomCollectible.id})</color>");

        // 生成武器掉落物（使用dropPrefab）
        if (randomCollectible.dropPrefab != null)
        {
            Vector3 dropPosition = transform.position + Vector3.up * dropHeight;
            GameObject drop = Instantiate(randomCollectible.dropPrefab, dropPosition, Quaternion.identity);
            drop.name = $"Drop_{randomCollectible.itemName}";

            // 设置掉落物的武器数据
            WeaponDroppedItem dropScript = drop.GetComponent<WeaponDroppedItem>();
            if (dropScript != null)
            {
                dropScript.SetWeaponData(randomCollectible);
            }

            Debug.Log($"<color=green>✓ 已生成武器掉落物: {randomCollectible.itemName}</color>");
        }
        else
        {
            Debug.LogWarning($"<color=orange>收集品 '{randomCollectible.itemName}' 没有设置 dropPrefab！</color>");
        }

        // 播放特效和销毁宝箱
        PlayEffectsAndDestroy();
    }

    /// <summary>
    /// 播放特效并销毁宝箱
    /// </summary>
    void PlayEffectsAndDestroy()
    {
        // 播放粒子特效
        if (disappearEffect != null)
        {
            // 实例化并播放粒子系统
            ParticleSystem effect = Instantiate(disappearEffect, transform.position, disappearEffect.transform.rotation);
            effect.Play();

            // 延迟销毁特效
            Destroy(effect.gameObject, Mathf.Max(effect.main.duration, effectDestroyDelay));

            Debug.Log("播放宝箱消失特效");
        }

        // 播放音效
        if (openSound != null)
        {
            AudioSource.PlayClipAtPoint(openSound, transform.position);
            Debug.Log("播放宝箱打开音效");
        }

        // 销毁宝箱
        Destroy(gameObject);
    }

    /// <summary>
    /// 在Scene视图中显示交互范围（用于调试）
    /// </summary>
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);

        // 显示掉落物生成位置
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * dropHeight, 0.3f);
    }
}