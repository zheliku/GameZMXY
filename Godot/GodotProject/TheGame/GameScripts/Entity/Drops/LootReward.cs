using GameConfig.Loot;
using GameConfig.Equipment;

namespace GameLogic.Drops
{
    /// <summary>
    /// 一颗飞行球在生成时确定的不可变奖励。
    /// </summary>
    /// <param name="Kind">奖励类型。</param>
    /// <param name="Amount">生成时确定的灵魂、无双或生命/魔法恢复数量。</param>
    public readonly record struct LootReward(LootKind Kind, int Amount);

    /// <summary>
    /// 飞行球承载的收益类型；颜色由外置动画资源提供。
    /// </summary>
    public enum LootKind
    {
        /// <summary>
        /// 持久灵魂。
        /// </summary>
        Soul,

        /// <summary>
        /// 本次出战的无双积累。
        /// </summary>
        Musou,

        /// <summary>
        /// 本次出战的魔法恢复。
        /// </summary>
        Mana,

        /// <summary>
        /// 本次出战的生命恢复。
        /// </summary>
        Health,
    }

    /// <summary>
    /// 一次地面掉落的定义选择，尚未创建节点或修改档案。
    /// </summary>
    /// <param name="Pickup">互斥恢复品，无掉落时为 None。</param>
    /// <param name="Equipment">独立装备判定，无掉落时为 None。</param>
    public readonly record struct GroundLootSelection(PickupId Pickup, EquipmentId Equipment);
}
