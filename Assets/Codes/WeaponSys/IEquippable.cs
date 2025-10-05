using UnityEngine;

/// <summary>
/// 可装备物品接口
/// </summary>
public interface IEquippable
{
    string EquipmentName { get; }
    Sprite Icon { get; }
    float Cooldown { get; }
    float CurrentCooldown { get; }

    void Use(Vector3 direction, Vector3 origin);
    void UpdateCooldown(float deltaTime);
    bool CanUse();
    void OnEquip();
    void OnUnequip();
}