using System;
using GameConfig.Equipment;
using GameConfig.Loot;
using Godot;

namespace GameLogic.Equipment;

/// <summary>场景引用的共享物品外观集合；一条物品外观同时供背包、穿戴和掉落消费。</summary>
[Tool, GlobalClass]
public partial class ItemAppearancesResource : Resource
{
    [Export] private Godot.Collections.Array<ItemAppearanceResource> m_Items = new(); // 在检查器编辑的物品外观条目。

    /// <summary>查找实际装备对应的共享外观。</summary>
    /// <param name="id">稳定的装备主键。</param>
    /// <returns>该物品的外观资源。</returns>
    /// <exception cref="InvalidOperationException">未装配或重复装配指定物品。</exception>
    public ItemAppearanceResource Get(EquipmentId id) => Find(id, PickupId.None);

    /// <summary>查找恢复品对应的共享外观。</summary>
    /// <param name="id">稳定的恢复品主键。</param>
    /// <returns>该物品的外观资源。</returns>
    /// <exception cref="InvalidOperationException">未装配或重复装配指定物品。</exception>
    public ItemAppearanceResource Get(PickupId id) => Find(EquipmentId.None, id);

    /// <summary>验证物品身份和图像，拒绝重复或缺失的场景装配。</summary>
    /// <param name="equipmentId">装备身份；恢复品时为 None。</param>
    /// <param name="pickupId">恢复品身份；装备时为 None。</param>
    /// <returns>唯一的外观条目。</returns>
    /// <exception cref="InvalidOperationException">身份互斥或条目图像不成立。</exception>
    private ItemAppearanceResource Find(EquipmentId equipmentId, PickupId pickupId)
    {
        if ((equipmentId == EquipmentId.None) == (pickupId == PickupId.None))
            throw new InvalidOperationException("物品外观必须指定一种有效物品身份。");
        ItemAppearanceResource found = null;
        foreach (var item in m_Items)
        {
            if (item == null || item.EquipmentId != equipmentId || item.PickupId != pickupId)
                continue;
            if (found != null)
                throw new InvalidOperationException($"物品外观重复：{equipmentId}/{pickupId}");
            found = item;
        }
        if (found == null || found.InventoryIcon == null || found.GroundIcon == null ||
            found.GroundScale.X <= 0 || found.GroundScale.Y <= 0)
            throw new InvalidOperationException($"物品外观缺失或无效：{equipmentId}/{pickupId}");
        return found;
    }
}
