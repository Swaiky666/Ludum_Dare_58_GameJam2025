using UnityEngine;

/// <summary>
/// Boss击败后生成的传送门 - 玩家靠近后进入下一轮
/// </summary>
public class BossExitDoor : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float detectionRadius = 2f;
    [SerializeField] private string playerTag = "Player";

    [Header("References")]
    [SerializeField] private RoomMapSystem roomMapSystem;

    [Header("Visual Effects")]
    [SerializeField] private bool rotateEffect = true;
    [SerializeField] private float rotationSpeed = 50f;

    private Transform playerTransform;
    private bool hasTriggered = false;

    void Start()
    {
        // 查找玩家
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null)
        {
            playerTransform = player.transform;
        }

        // 查找 RoomMapSystem
        if (roomMapSystem == null)
        {
            roomMapSystem = FindObjectOfType<RoomMapSystem>();
            if (roomMapSystem == null)
            {
                Debug.LogError("BossExitDoor: 未找到 RoomMapSystem！");
            }
        }

        Debug.Log("<color=cyan>Boss传送门已生成！靠近即可进入下一轮</color>");
    }

    void Update()
    {
        if (hasTriggered) return;

        // 旋转特效
        if (rotateEffect)
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }

        // 检测玩家距离
        if (playerTransform != null)
        {
            float distance = Vector3.Distance(transform.position, playerTransform.position);
            if (distance <= detectionRadius)
            {
                TriggerNextRound();
            }
        }
    }

    void TriggerNextRound()
    {
        if (hasTriggered) return;
        hasTriggered = true;

        Debug.Log("<color=yellow>玩家进入Boss传送门！进入下一轮...</color>");

        if (roomMapSystem != null)
        {
            roomMapSystem.AdvanceToNextRound();
        }
        else
        {
            Debug.LogError("无法进入下一轮：RoomMapSystem 引用丢失！");
        }

        // 销毁传送门
        Destroy(gameObject, 0.5f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}