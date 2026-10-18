using GameConfig.Equipment;
using GameConfig.Loot;
using Godot;

namespace GameLogic.Equipment;

/// <summary>一种物品的编辑器外观，供背包和地面掉落共同引用，不保存交易或属性数值。</summary>
[Tool, GlobalClass]
public partial class ItemAppearanceResource : Resource
{
    [Export] private EquipmentId m_EquipmentId; // 装备主键；恢复品填 None。
    [Export] private PickupId m_PickupId; // 恢复品主键；装备填 None。
    [Export] private Texture2D m_InventoryIcon; // 背包中的透明图标。
    [Export] private Texture2D m_GroundIcon; // 地面图标，保留原始透明留白。
    [Export] private Vector2 m_GroundScale = new(.7f, .7f); // 地面显示缩放，不改变碰撞体。

    /// <summary>对应的装备主键。</summary>
    public EquipmentId EquipmentId => m_EquipmentId;

    /// <summary>对应的恢复品主键。</summary>
    public PickupId PickupId => m_PickupId;

    /// <summary>背包和穿戴槽共用的图标。</summary>
    public Texture2D InventoryIcon => m_InventoryIcon;

    /// <summary>地面节点使用的图标。</summary>
    public Texture2D GroundIcon => m_GroundIcon;

    /// <summary>地面图标的实际显示缩放。</summary>
    public Vector2 GroundScale => m_GroundScale;
}
