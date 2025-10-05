using System;
using System.Collections.Generic;
using UnityEngine;

public class CollectionManager : MonoBehaviour
{
    public static CollectionManager Instance { get; private set; }

    [Header("所有收集品数据")]
    [SerializeField] private List<CollectibleData> collectibleDataList = new List<CollectibleData>();

    private Dictionary<int, CollectibleData> collectibleDict = new Dictionary<int, CollectibleData>();

    public event Action OnCollectionUpdated;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        InitializeCollectibles();
    }

    private void InitializeCollectibles()
    {
        collectibleDict.Clear();

        foreach (var data in collectibleDataList)
        {
            if (data != null)
            {
                collectibleDict[data.id] = data;
                Debug.Log($"Initialized collectible: ID={data.id}, Name={data.itemName}");
            }
        }

        Debug.Log($"Total collectibles initialized: {collectibleDict.Count}");
    }

    public List<CollectibleData> GetAllCollectibles()
    {
        return new List<CollectibleData>(collectibleDict.Values);
    }

    // 外部调用此方法来收集物品
    public void CollectItem(int collectibleId)
    {
        if (collectibleDict.TryGetValue(collectibleId, out CollectibleData item))
        {
            item.Collect();
            OnCollectionUpdated?.Invoke();
        }
        else
        {
            Debug.LogWarning($"Collectible ID {collectibleId} does not exist!");
        }
    }

    public float GetCollectionProgress()
    {
        if (collectibleDict.Count == 0) return 0f;

        int collectedCount = 0;
        foreach (var item in collectibleDict.Values)
        {
            if (item.isCollected)
                collectedCount++;
        }

        return (float)collectedCount / collectibleDict.Count;
    }

    public CollectibleData GetCollectibleById(int id)
    {
        collectibleDict.TryGetValue(id, out CollectibleData data);
        return data;
    }

    // 获取列表中第一个收集品
    public CollectibleData GetFirstCollectible()
    {
        if (collectibleDataList.Count > 0)
        {
            return collectibleDataList[0];
        }
        return null;
    }

    // 检查并收集第一个收集品（如果未收集）
    public void CheckAndCollectFirstItem()
    {
        if (collectibleDataList.Count > 0)
        {
            CollectibleData firstItem = collectibleDataList[0];

            if (firstItem != null && !firstItem.isCollected)
            {
                Debug.Log($"首次进入游戏，自动收集第一个物品: {firstItem.itemName}");
                CollectItem(firstItem.id);
            }
            else if (firstItem != null)
            {
                Debug.Log($"第一个物品 {firstItem.itemName} 已经被收集过了");
            }
        }
    }

    // 重置所有收集状态（可选功能）
    public void ResetAllCollections()
    {
        foreach (var item in collectibleDict.Values)
        {
            item.ResetCollection();
        }
        OnCollectionUpdated?.Invoke();
    }

    // 清除所有记录（包括携带武器）
    public void ClearAllRecords(EquippedWeaponData equippedWeaponData = null)
    {
        Debug.Log("开始清除所有收集记录...");

        // 遍历所有收集品，清除记录
        foreach (var item in collectibleDict.Values)
        {
            item.ResetCollection();
            Debug.Log($"已清除: {item.itemName}");
        }

        // 清除携带武器数据
        if (equippedWeaponData != null)
        {
            equippedWeaponData.UnequipWeapon();
        }

        // 触发更新事件
        OnCollectionUpdated?.Invoke();

        Debug.Log("所有记录已清除！");
    }

    // 收集所有物品
    public void CollectAllItems()
    {
        Debug.Log("开始收集所有物品...");

        foreach (var item in collectibleDict.Values)
        {
            if (item != null)
            {
                item.Collect();
                Debug.Log($"已收集: {item.itemName}");
            }
        }

        // 触发更新事件
        OnCollectionUpdated?.Invoke();

        Debug.Log("所有物品已收集！");
    }
}