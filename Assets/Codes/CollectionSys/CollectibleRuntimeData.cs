using System;

[Serializable]
public class CollectibleRuntimeData
{
    public CollectibleData data;        // 引用ScriptableObject数据
    public bool isCollected;            // 是否收集
    public int collectionCount;         // 已收集次数
    public DateTime collectionTime;     // 收集时间

    public CollectibleRuntimeData(CollectibleData data)
    {
        this.data = data;
        this.isCollected = false;
        this.collectionCount = 0;
        this.collectionTime = DateTime.MinValue;
    }
}