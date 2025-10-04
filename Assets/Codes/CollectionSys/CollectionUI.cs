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
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TextMeshProUGUI progressText;

    [Header("筛选按钮")]
    [SerializeField] private Button showAllButton;
    [SerializeField] private Button showCollectedButton;
    [SerializeField] private Button showUnCollectedButton;

    [Header("排序按钮")]
    [SerializeField] private Button sortByIdButton;
    [SerializeField] private Button sortByNameButton;
    [SerializeField] private Button sortByTimeButton;

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

        // 延迟刷新确保CollectionManager已初始化
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
    }

    public void SetFilter(FilterMode mode)
    {
        currentFilter = mode;
        RefreshDisplay();
    }

    public void SetSort(SortMode mode)
    {
        currentSort = mode;
        RefreshDisplay();
    }

    public void RefreshDisplay()
    {
        ClearItems();

        if (CollectionManager.Instance == null)
        {
            Debug.LogWarning("CollectionManager.Instance is null!");
            return;
        }

        List<CollectibleRuntimeData> collectibles = CollectionManager.Instance.GetAllCollectibles();

        Debug.Log($"RefreshDisplay: Found {collectibles.Count} collectibles");

        // 筛选
        collectibles = FilterCollectibles(collectibles);
        Debug.Log($"After filter ({currentFilter}): {collectibles.Count} collectibles");

        // 排序
        collectibles = SortCollectibles(collectibles);

        // 显示
        foreach (var collectible in collectibles)
        {
            if (collectible == null || collectible.data == null)
            {
                Debug.LogWarning("Found null collectible or data!");
                continue;
            }

            GameObject itemObj = Instantiate(itemPrefab, itemContainer);
            CollectibleItemUI itemUI = itemObj.GetComponent<CollectibleItemUI>();
            if (itemUI != null)
            {
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
    }

    private List<CollectibleRuntimeData> FilterCollectibles(List<CollectibleRuntimeData> collectibles)
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

    private List<CollectibleRuntimeData> SortCollectibles(List<CollectibleRuntimeData> collectibles)
    {
        switch (currentSort)
        {
            case SortMode.ById:
                return collectibles.OrderBy(c => c.data.id).ToList();
            case SortMode.ByName:
                return collectibles.OrderBy(c => c.data.itemName).ToList();
            case SortMode.ByTime:
                return collectibles.OrderByDescending(c => c.collectionTime).ToList();
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
}