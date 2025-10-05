using System;
using UnityEngine;

[CreateAssetMenu(fileName = "New Collectible", menuName = "Collection System/Collectible")]
public class CollectibleData : ScriptableObject
{
    [Header("基本信息")]
    public int id;                      // 编号
    public string itemName;             // 名字
    public Sprite icon;                 // 图片

    [Header("描述")]
    [TextArea(3, 5)]
    public string description;          // 描述

    [Header("预制体")]
    public GameObject prefab;           // prefab
    public GameObject dropPrefab;       // 掉落物预制体（宝箱打开后生成的物品）

    [Header("收集状态")]
    public bool isCollected = false;           // 是否收集
    public int collectionCount = 0;            // 收集次数（默认为0）

    [Tooltip("收集时间，如果没有收集则为空")]
    public string collectionTime = "";         // 收集时间（可以不填）

    // 内部使用的DateTime，用于排序和显示
    [NonSerialized]
    private DateTime _collectionDateTime;

    public DateTime CollectionDateTime
    {
        get
        {
            if (_collectionDateTime == default && !string.IsNullOrEmpty(collectionTime))
            {
                DateTime.TryParse(collectionTime, out _collectionDateTime);
            }
            return _collectionDateTime;
        }
        set
        {
            _collectionDateTime = value;
            collectionTime = value.ToString("yyyy/MM/dd HH:mm");
        }
    }

    // 收集物品的方法
    public void Collect()
    {
        if (!isCollected)
        {
            isCollected = true;
        }
        collectionCount++;
        CollectionDateTime = DateTime.Now;
    }

    // 重置收集状态（用于测试或重置功能）
    public void ResetCollection()
    {
        isCollected = false;
        collectionCount = 0;
        collectionTime = "";
        _collectionDateTime = default;
    }
}