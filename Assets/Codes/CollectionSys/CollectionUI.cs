using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CollectionUI : MonoBehaviour
{
    [Header("UI组件")]
    [SerializeField] private Transform itemContainer;
    [SerializeField] private GameObject itemPrefab;
    [SerializeField] private UnityEngine.UI.Slider progressSlider;
    [SerializeField] private TextMeshProUGUI progressText;

    [Header("滚动控制")]
    [SerializeField] private ManualScrollController manualScroller;

    [Header("选择高亮Material")]
    [SerializeField] private Material selectionMaterial;  // 选中时使用的Material

    [Header("筛选按钮")]
    [SerializeField] private UnityEngine.UI.Button showAllButton;
    [SerializeField] private UnityEngine.UI.Button showCollectedButton;
    [SerializeField] private UnityEngine.UI.Button showUnCollectedButton;

    [Header("排序按钮")]
    [SerializeField] private UnityEngine.UI.Button sortByIdButton;
    [SerializeField] private UnityEngine.UI.Button sortByNameButton;
    [SerializeField] private UnityEngine.UI.Button sortByTimeButton;

    [Header("导航按钮")]
    [SerializeField] private UnityEngine.UI.Button backToMenuButton;

    [Header("功能按钮")]
    [SerializeField] private UnityEngine.UI.Button clearRecordsButton;
    [SerializeField] private UnityEngine.UI.Button collectAllButton;

    [Header("装备数据")]
    [SerializeField] private EquippedWeaponData equippedWeaponData;

    private List<CollectibleItemUI> currentItems = new List<CollectibleItemUI>();
    private FilterMode currentFilter = FilterMode.All;
    private SortMode currentSort = SortMode.ById;

    public enum FilterMode
    {
        All,
        Collected,
        UnCollected
    }

    public enum SortMode
    {
        ById,
        ByName,
        ByTime
    }

    private void Start()
    {
        SetupButtons();

        if (CollectionManager.Instance != null)
        {
            CollectionManager.Instance.OnCollectionUpdated += RefreshDisplay;
        }

        if (manualScroller == null)
        {
            manualScroller = GetComponentInChildren<ManualScrollController>();
        }

        Invoke(nameof(RefreshDisplay), 0.1f);
    }

    private void OnDestroy()
    {
        if (CollectionManager.Instance != null)
        {
            CollectionManager.Instance.OnCollectionUpdated -= RefreshDisplay;
        }
    }

    private void SetupButtons()
    {
        if (showAllButton != null)
            showAllButton.onClick.AddListener(() => SetFilter(FilterMode.All));

        if (showCollectedButton != null)
            showCollectedButton.onClick.AddListener(() => SetFilter(FilterMode.Collected));

        if (showUnCollectedButton != null)
            showUnCollectedButton.onClick.AddListener(() => SetFilter(FilterMode.UnCollected));

        if (sortByIdButton != null)
            sortByIdButton.onClick.AddListener(() => SetSort(SortMode.ById));

        if (sortByNameButton != null)
            sortByNameButton.onClick.AddListener(() => SetSort(SortMode.ByName));

        if (sortByTimeButton != null)
            sortByTimeButton.onClick.AddListener(() => SetSort(SortMode.ByTime));

        if (backToMenuButton != null)
            backToMenuButton.onClick.AddListener(OnBackToMenu);

        if (clearRecordsButton != null)
            clearRecordsButton.onClick.AddListener(OnClearRecords);

        if (collectAllButton != null)
            collectAllButton.onClick.AddListener(OnCollectAll);
    }

    public void SetFilter(FilterMode mode)
    {
        currentFilter = mode;
        RefreshDisplay();
        ScrollToTop();
    }

    public void SetSort(SortMode mode)
    {
        currentSort = mode;
        RefreshDisplay();
        ScrollToTop();
    }

    public void RefreshDisplay()
    {
        // 清除所有选中状态
        CollectibleItemUI.ClearAllSelection();

        ClearItems();

        if (CollectionManager.Instance == null)
        {
            Debug.LogWarning("CollectionManager.Instance is null!");
            return;
        }

        List<CollectibleData> collectibles = CollectionManager.Instance.GetAllCollectibles();

        Debug.Log($"RefreshDisplay: Found {collectibles.Count} collectibles");

        collectibles = FilterCollectibles(collectibles);
        Debug.Log($"After filter ({currentFilter}): {collectibles.Count} collectibles");

        collectibles = SortCollectibles(collectibles);

        foreach (var collectible in collectibles)
        {
            if (collectible == null)
            {
                Debug.LogWarning("Found null collectible!");
                continue;
            }

            GameObject itemObj = Instantiate(itemPrefab, itemContainer);
            CollectibleItemUI itemUI = itemObj.GetComponent<CollectibleItemUI>();
            if (itemUI != null)
            {
                // ⭐ 设置选择Material
                itemUI.selectionMaterial = selectionMaterial;
                itemUI.equippedWeaponData = equippedWeaponData;
                itemUI.Setup(collectible);
                currentItems.Add(itemUI);
            }
            else
            {
                Debug.LogWarning("ItemPrefab is missing CollectibleItemUI component!");
            }
        }

        Debug.Log($"Displayed {currentItems.Count} items in UI");
        UpdateProgress();

        if (manualScroller != null)
        {
            manualScroller.UpdateContentHeight(currentItems.Count);
            manualScroller.ScrollToTop();
        }
    }

    private List<CollectibleData> FilterCollectibles(List<CollectibleData> collectibles)
    {
        switch (currentFilter)
        {
            case FilterMode.Collected:
                return collectibles.Where(c => c.isCollected).ToList();
            case FilterMode.UnCollected:
                return collectibles.Where(c => !c.isCollected).ToList();
            case FilterMode.All:
            default:
                return collectibles;
        }
    }

    private List<CollectibleData> SortCollectibles(List<CollectibleData> collectibles)
    {
        switch (currentSort)
        {
            case SortMode.ById:
                return collectibles.OrderBy(c => c.id).ToList();
            case SortMode.ByName:
                return collectibles.OrderBy(c => c.itemName).ToList();
            case SortMode.ByTime:
                return collectibles.OrderByDescending(c => c.CollectionDateTime).ToList();
            default:
                return collectibles;
        }
    }

    private void ClearItems()
    {
        foreach (var item in currentItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        currentItems.Clear();
    }

    private void UpdateProgress()
    {
        if (CollectionManager.Instance == null) return;

        float progress = CollectionManager.Instance.GetCollectionProgress();

        if (progressSlider != null)
        {
            progressSlider.value = progress;
        }

        if (progressText != null)
        {
            progressText.text = $"{(progress * 100f):F1}%";
        }
    }

    private void OnBackToMenu()
    {
        if (GameSceneManager.Instance != null)
        {
            GameSceneManager.Instance.LoadMainMenu();
        }
        else
        {
            Debug.LogError("GameSceneManager.Instance is null!");
        }
    }

    private void OnClearRecords()
    {
        if (CollectionManager.Instance != null)
        {
            Debug.Log("清除所有收集记录...");
            CollectionManager.Instance.ClearAllRecords(equippedWeaponData);
            RefreshDisplay();
        }
        else
        {
            Debug.LogError("CollectionManager.Instance is null!");
        }
    }

    private void OnCollectAll()
    {
        if (CollectionManager.Instance != null)
        {
            Debug.Log("收集所有物品...");
            CollectionManager.Instance.CollectAllItems();
            RefreshDisplay();
        }
        else
        {
            Debug.LogError("CollectionManager.Instance is null!");
        }
    }

    public void ScrollToTop()
    {
        if (manualScroller != null)
        {
            manualScroller.ScrollToTop();
        }
    }

    public void ScrollToBottom()
    {
        if (manualScroller != null)
        {
            manualScroller.ScrollToBottom();
        }
    }

    public void LogScrollInfo()
    {
        if (manualScroller != null)
        {
            manualScroller.RecalculateHeight();
            Debug.Log($"Item Count: {currentItems.Count}");
        }
    }

    /// <summary>
    /// 获取当前选中的收集品数据
    /// </summary>
    public CollectibleData GetSelectedCollectibleData()
    {
        CollectibleItemUI selectedItem = CollectibleItemUI.GetCurrentSelectedItem();
        if (selectedItem != null)
        {
            return selectedItem.GetData();
        }
        return null;
    }
}