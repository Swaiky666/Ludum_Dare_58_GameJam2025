using System;
using System.Collections.Generic;
using UnityEngine;

public class CollectionManager : MonoBehaviour
{
    public static CollectionManager Instance { get; private set; }

    [Header("所有收集品数据")]
    [SerializeField] private List<CollectibleData> collectibleDataList = new List<CollectibleData>();

    private Dictionary<int, CollectibleRuntimeData> runtimeDataDict = new Dictionary<int, CollectibleRuntimeData>();

    public event Action OnCollectionUpdated;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        InitializeRuntimeData();
    }

    private void InitializeRuntimeData()
    {
        runtimeDataDict.Clear();

        foreach (var data in collectibleDataList)
        {
            if (data != null)
            {
                runtimeDataDict[data.id] = new CollectibleRuntimeData(data);
                Debug.Log($"Initialized collectible: ID={data.id}, Name={data.itemName}");
            }
        }

        Debug.Log($"Total collectibles initialized: {runtimeDataDict.Count}");
    }

    public List<CollectibleRuntimeData> GetAllCollectibles()
    {
        return new List<CollectibleRuntimeData>(runtimeDataDict.Values);
    }

    // 外部调用此方法来收集物品
    public void CollectItem(int collectibleId)
    {
        if (runtimeDataDict.TryGetValue(collectibleId, out CollectibleRuntimeData item))
        {
            if (!item.isCollected)
            {
                item.isCollected = true;
            }

            item.collectionCount++;
            item.collectionTime = DateTime.Now;

            OnCollectionUpdated?.Invoke();
        }
        else
        {
            Debug.LogWarning($"Collectible ID {collectibleId} does not exist!");
        }
    }

    public float GetCollectionProgress()
    {
        if (runtimeDataDict.Count == 0) return 0f;

        int collectedCount = 0;
        foreach (var item in runtimeDataDict.Values)
        {
            if (item.isCollected)
                collectedCount++;
        }

        return (float)collectedCount / runtimeDataDict.Count;
    }

    public CollectibleRuntimeData GetCollectibleById(int id)
    {
        runtimeDataDict.TryGetValue(id, out CollectibleRuntimeData data);
        return data;
    }
}