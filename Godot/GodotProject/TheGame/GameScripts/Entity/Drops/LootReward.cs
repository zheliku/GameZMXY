using System;
using System.Collections.Generic;
using GameConfig.Loot;
using GameConfig.Monster;
using GameConfig.Equipment;
using Godot;

namespace GameLogic.Drops;

/// <summary>一颗球的不可变奖励及纯掉落规则；不创建节点或修改档案。</summary>
/// <param name="Kind">奖励类型。</param>
/// <param name="Amount">灵魂或无双数量；魔法在接触时按当前上限计算。</param>
public readonly record struct LootReward(LootKind Kind, int Amount);


/// <summary>飞行球承载的收益类型；颜色由外置动画资源提供。</summary>
public enum LootKind
{
    /// <summary>持久灵魂。</summary>
    Soul,

    /// <summary>本次出战的无双积累。</summary>
    Musou,

    /// <summary>本次出战的魔法恢复。</summary>
    Mana,
}

/// <summary>一次地面掉落的定义选择，尚未创建节点或修改档案。</summary>
/// <param name="Pickup">互斥恢复品，无掉落时为 None。</param>
/// <param name="Equipment">独立装备判定，无掉落时为 None。</param>
public readonly record struct GroundLootSelection(PickupId Pickup, EquipmentId Equipment);
