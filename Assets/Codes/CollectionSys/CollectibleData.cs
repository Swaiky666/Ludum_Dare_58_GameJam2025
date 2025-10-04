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
}