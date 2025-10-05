using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

/// <summary>
/// 武器槽位 - 显示武器图标和名称，支持点击和拖动
/// </summary>
public class WeaponSlot : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("UI References")]
    [SerializeField] private Image weaponIcon;                // 武器图标
    [SerializeField] private TextMeshProUGUI weaponNameText;  // 武器名称文本
    [SerializeField] private GameObject emptySlotHint;        // 空槽位提示（可选）

    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightColor = new Color(1f, 1f, 0.5f, 1f);
    [SerializeField] private Color dropHighlightColor = new Color(0.5f, 1f, 0.5f, 1f); // 可以接收拖放时的高亮色

    [Header("Slot Index")]
    [SerializeField] private int slotIndex = 0; // 0=左手, 1=右手

    private IEquippable currentWeapon;
    private bool isEmpty = true;
    private Canvas canvas;
    private RectTransform rectTransform;

    // 在初始化时记录并固定，永远不变
    private Vector2 fixedPosition;
    private Transform fixedParent;
    private int fixedSiblingIndex;

    private CanvasGroup canvasGroup;

    // 点击事件
    public event Action OnSlotClicked;
    // 武器交换事件（传递两个槽位的索引）
    public event Action<int, int> OnWeaponSwap;
    // 拖动开始事件
    public event Action OnDragStarted;

    void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // ⭐ 关键：在初始化时就记录固定位置，永远不变
        fixedPosition = rectTransform.anchoredPosition;
        fixedParent = transform.parent;
        fixedSiblingIndex = transform.GetSiblingIndex();

        Debug.Log($"<color=cyan>[初始化] 槽位{slotIndex} 固定位置已记录: Position={fixedPosition}, Parent={fixedParent.name}, SiblingIndex={fixedSiblingIndex}</color>");

        // 初始化为空槽位
        ClearSlot();
    }

    /// <summary>
    /// 设置槽位索引
    /// </summary>
    public void SetSlotIndex(int index)
    {
        slotIndex = index;
    }

    /// <summary>
    /// 获取槽位索引
    /// </summary>
    public int GetSlotIndex()
    {
        return slotIndex;
    }

    /// <summary>
    /// 设置武器显示
    /// </summary>
    public void SetWeapon(IEquippable weapon)
    {
        if (weapon == null)
        {
            ClearSlot();
            return;
        }

        currentWeapon = weapon;
        isEmpty = false;

        // 更新图标
        if (weaponIcon != null)
        {
            weaponIcon.enabled = true;
            weaponIcon.sprite = weapon.Icon;
            weaponIcon.color = normalColor;
        }

        // 更新名称
        if (weaponNameText != null)
        {
            weaponNameText.enabled = true;
            weaponNameText.text = weapon.EquipmentName;
        }

        // 隐藏空槽位提示
        if (emptySlotHint != null)
        {
            emptySlotHint.SetActive(false);
        }
    }

    /// <summary>
    /// 清空槽位
    /// </summary>
    public void ClearSlot()
    {
        currentWeapon = null;
        isEmpty = true;

        // 隐藏图标
        if (weaponIcon != null)
        {
            weaponIcon.enabled = false;
        }

        // 隐藏名称
        if (weaponNameText != null)
        {
            weaponNameText.enabled = false;
            weaponNameText.text = "";
        }

        // 显示空槽位提示
        if (emptySlotHint != null)
        {
            emptySlotHint.SetActive(true);
        }
    }

    /// <summary>
    /// 点击事件
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isEmpty) return;

        // 触发点击事件
        OnSlotClicked?.Invoke();

        // 高亮效果
        if (weaponIcon != null)
        {
            StartCoroutine(HighlightEffect());
        }
    }

    /// <summary>
    /// 高亮效果协程
    /// </summary>
    System.Collections.IEnumerator HighlightEffect()
    {
        weaponIcon.color = highlightColor;
        yield return new UnityEngine.WaitForSecondsRealtime(0.2f); // 使用RealTime因为Time.timeScale = 0
        weaponIcon.color = normalColor;
    }

    /// <summary>
    /// 开始拖动 - 空槽位也可以拖动
    /// </summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        // 触发拖动开始事件（通知InventoryUI清空DetailPanel）
        OnDragStarted?.Invoke();

        // 移动到Canvas最顶层（这样拖动时不会被其他UI遮挡）
        transform.SetParent(canvas.transform);
        transform.SetAsLastSibling();

        // 设置透明度，让拖动时槽位半透明
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0.8f;
            canvasGroup.blocksRaycasts = false; // 拖动时不阻挡射线，允许检测下方的槽位
        }

        string weaponName = isEmpty ? "空槽位" : currentWeapon.EquipmentName;
        Debug.Log($"<color=yellow>[拖动开始] {weaponName} (槽位{slotIndex})</color>");
    }

    /// <summary>
    /// 拖动中 - 空槽位也跟随鼠标
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (rectTransform == null) return;

        // 让槽位跟随鼠标位置
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint
        );

        rectTransform.anchoredPosition = localPoint;
    }

    /// <summary>
    /// 结束拖动 - 无论是空槽位还是有武器，无论发生什么，都回到固定位置
    /// </summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        // ⭐ 关键：无论isEmpty状态如何，总是回到固定位置
        transform.SetParent(fixedParent);
        transform.SetSiblingIndex(fixedSiblingIndex);
        rectTransform.anchoredPosition = fixedPosition;

        // 恢复透明度和射线检测
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }

        Debug.Log($"<color=yellow>[拖动结束] 槽位{slotIndex} 已恢复到固定位置: {fixedPosition}</color>");
    }

    /// <summary>
    /// 接收拖放 - 当其他槽位被拖到这个槽位上时触发（空槽位也可以）
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        // 获取被拖动的槽位
        WeaponSlot draggedSlot = eventData.pointerDrag?.GetComponent<WeaponSlot>();

        // 确保拖动的是有效槽位且不是自己
        if (draggedSlot == null || draggedSlot == this) return;

        // 触发武器交换事件（无论是否为空槽位）
        string fromWeapon = draggedSlot.isEmpty ? "空" : draggedSlot.currentWeapon.EquipmentName;
        string toWeapon = isEmpty ? "空" : currentWeapon.EquipmentName;

        Debug.Log($"<color=cyan>[触发交换] 槽位{draggedSlot.GetSlotIndex()}({fromWeapon}) → 槽位{slotIndex}({toWeapon})</color>");

        OnWeaponSwap?.Invoke(draggedSlot.GetSlotIndex(), slotIndex);
    }

    /// <summary>
    /// 获取当前武器
    /// </summary>
    public IEquippable GetWeapon()
    {
        return currentWeapon;
    }

    /// <summary>
    /// 检查槽位是否为空
    /// </summary>
    public bool IsEmpty()
    {
        return isEmpty;
    }
}