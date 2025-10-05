using UnityEngine;

/// <summary>
/// 武器掉落物 - 玩家丢弃武器或宝箱开出后生成的可拾取物品
/// </summary>
public class WeaponDroppedItem : MonoBehaviour
{
    [Header("Weapon Data")]
    [SerializeField] private GameObject weaponPrefab;  // 武器预制体
    [SerializeField] private int weaponId = -1;        // 武器ID
    [SerializeField] private string weaponName = "";   // 武器名称
    [SerializeField] private Sprite weaponIcon;        // 武器图标

    [Header("Visual Model")]
    [SerializeField] private Transform visualModel;         // 带有Sprite的子物体
    [SerializeField] private SpriteRenderer spriteRenderer; // Sprite渲染器
    [SerializeField] private Vector3 rotationAxis = Vector3.up; // 旋转轴（默认Y轴）
    [SerializeField] private float rotationSpeed = 90f;     // 旋转速度（度/秒）
    [SerializeField] private float fixedXRotation = 90f;    // 固定的X轴旋转角度

    [Header("Interaction")]
    [SerializeField] private GameObject interactPrompt;     // 交互提示UI（可选）
    [SerializeField] private float interactDistance = 2f;   // 交互距离
    [SerializeField] private KeyCode pickupKey = KeyCode.E; // 拾取按键

    [Header("Audio")]
    [SerializeField] private AudioClip pickupSound;         // 拾取音效

    private Transform playerTransform;
    private bool playerInRange = false;
    private EquipmentManager equipmentManager;

    void Start()
    {
        // 查找玩家
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Debug.Log($"<color=green>找到玩家: {player.name}</color>");
            playerTransform = player.transform;
            equipmentManager = player.GetComponent<EquipmentManager>();

            if (equipmentManager == null)
            {
                Debug.LogError($"<color=red>玩家 {player.name} 上没有 EquipmentManager 组件！</color>");
            }
            else
            {
                Debug.Log("<color=green>成功获取 EquipmentManager</color>");
            }
        }
        else
        {
            Debug.LogError("WeaponDroppedItem: 未找到带有'Player'标签的对象！");
        }

        // 初始隐藏交互提示
        if (interactPrompt != null)
        {
            interactPrompt.SetActive(false);
        }

        // 自动查找SpriteRenderer（如果未设置）
        if (spriteRenderer == null && visualModel != null)
        {
            spriteRenderer = visualModel.GetComponent<SpriteRenderer>();
        }

        // 设置初始X轴旋转为90度
        if (visualModel != null)
        {
            Vector3 rotation = visualModel.localEulerAngles;
            rotation.x = fixedXRotation;
            visualModel.localEulerAngles = rotation;
        }
    }

    void Update()
    {
        if (playerTransform == null) return;

        // 旋转视觉模型
        RotateVisualModel();

        // 检查玩家距离
        float distance = Vector3.Distance(transform.position, playerTransform.position);
        playerInRange = distance <= interactDistance;

        // 显示/隐藏交互提示
        if (interactPrompt != null)
        {
            interactPrompt.SetActive(playerInRange);
        }

        // 检测拾取输入
        if (playerInRange && Input.GetKeyDown(pickupKey))
        {
            TryPickup();
        }
    }

    /// <summary>
    /// 旋转视觉模型（X轴固定为90度，只旋转Y轴）
    /// </summary>
    void RotateVisualModel()
    {
        if (visualModel == null) return;

        // 只围绕Y轴旋转，保持X轴固定
        Vector3 currentRotation = visualModel.localEulerAngles;
        currentRotation.y += rotationSpeed * Time.deltaTime;
        currentRotation.x = fixedXRotation; // 保持X轴固定在90度
        visualModel.localEulerAngles = currentRotation;
    }

    /// <summary>
    /// 设置武器数据（从外部调用）
    /// </summary>
    public void SetWeaponData(GameObject prefab, int id, string name)
    {
        weaponPrefab = prefab;
        weaponId = id;
        weaponName = name;

        Debug.Log($"<color=yellow>[WeaponDrop] 已设置武器数据: {name} (ID: {id})</color>");
    }

    /// <summary>
    /// 从CollectibleData设置武器数据（推荐使用）
    /// </summary>
    public void SetWeaponData(CollectibleData collectible)
    {
        if (collectible == null)
        {
            Debug.LogError("WeaponDroppedItem: CollectibleData为空！");
            return;
        }

        weaponPrefab = collectible.prefab;
        weaponId = collectible.id;
        weaponName = collectible.itemName;
        weaponIcon = collectible.icon;

        // 更新Sprite显示
        UpdateSprite();

        Debug.Log($"<color=yellow>[WeaponDrop] 从CollectibleData设置: {weaponName} (ID: {weaponId})</color>");
    }

    /// <summary>
    /// 从IEquippable设置武器数据
    /// </summary>
    public void SetWeaponData(GameObject prefab, IEquippable weapon)
    {
        if (weapon == null)
        {
            Debug.LogError("WeaponDroppedItem: IEquippable为空！");
            return;
        }

        weaponPrefab = prefab;
        weaponName = weapon.EquipmentName;
        weaponIcon = weapon.Icon;

        // 更新Sprite显示
        UpdateSprite();

        Debug.Log($"<color=yellow>[WeaponDrop] 从IEquippable设置: {weaponName}</color>");
    }

    /// <summary>
    /// 更新Sprite显示
    /// </summary>
    void UpdateSprite()
    {
        if (spriteRenderer != null && weaponIcon != null)
        {
            spriteRenderer.sprite = weaponIcon;
            Debug.Log($"<color=green>[WeaponDrop] 已更新Sprite: {weaponName}</color>");
        }
        else if (spriteRenderer == null)
        {
            Debug.LogWarning("WeaponDroppedItem: SpriteRenderer未设置，无法显示武器图标！");
        }
    }

    /// <summary>
    /// 尝试拾取武器
    /// </summary>
    void TryPickup()
    {
        if (weaponPrefab == null)
        {
            Debug.LogError("WeaponDroppedItem: 武器预制体为空，无法拾取！");
            Destroy(gameObject);
            return;
        }

        if (equipmentManager == null)
        {
            Debug.LogError("WeaponDroppedItem: 未找到EquipmentManager，无法拾取！");
            return;
        }

        // 检查背包是否有空位
        bool leftEmpty = equipmentManager.GetEquipment(0) == null;
        bool rightEmpty = equipmentManager.GetEquipment(1) == null;

        if (!leftEmpty && !rightEmpty)
        {
            Debug.Log("<color=orange>背包已满！无法拾取武器。</color>");
            // 可以在这里显示"背包已满"的UI提示
            return;
        }

        // 装备到空槽位
        int targetSlot = leftEmpty ? 0 : 1;
        equipmentManager.EquipToSlot(weaponPrefab, targetSlot);

        Debug.Log($"<color=green>[拾取] 已拾取 {weaponName} 到槽位{targetSlot}</color>");

        // 播放拾取音效
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        }

        // 销毁掉落物
        Destroy(gameObject);
    }

    /// <summary>
    /// 在Scene视图中显示交互范围
    /// </summary>
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}