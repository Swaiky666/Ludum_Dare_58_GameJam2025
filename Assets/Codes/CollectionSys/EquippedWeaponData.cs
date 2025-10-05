using UnityEngine;

[CreateAssetMenu(fileName = "EquippedWeapon", menuName = "Weapon System/Equipped Weapon")]
public class EquippedWeaponData : ScriptableObject
{
    [Header("当前携带的武器")]
    public GameObject weaponPrefab;  // 携带的武器预制体
    public int weaponId = -1;        // 武器ID（-1表示没有装备）
    public string weaponName = "";   // 武器名称

    /// <summary>
    /// 装备武器
    /// </summary>
    public void EquipWeapon(CollectibleData collectible)
    {
        if (collectible != null)
        {
            weaponPrefab = collectible.prefab;
            weaponId = collectible.id;
            weaponName = collectible.itemName;

            Debug.Log($"装备武器: {weaponName} (ID: {weaponId})");
        }
    }

    /// <summary>
    /// 卸下武器（清空）
    /// </summary>
    public void UnequipWeapon()
    {
        weaponPrefab = null;
        weaponId = -1;
        weaponName = "";

        Debug.Log("武器已卸下");
    }

    /// <summary>
    /// 检查是否装备了武器
    /// </summary>
    public bool IsEquipped()
    {
        return weaponPrefab != null;
    }
}