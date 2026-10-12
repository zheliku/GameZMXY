using System;
using System.Collections.Generic;
using GameConfig.Loot;
using GameConfig.Monster;
using Godot;

namespace GameLogic.Drops;

/// <summary>生成一次击败的掉落清单；外部提供随机数，规则不创建节点或修改档案。</summary>
public static class LootHelper
{
    /// <summary>按原版规则生成灵魂、魔法与无双球；经验独立在击败时结算。</summary>
    /// <param name="monster">怪物基础掉落配置。</param>
    /// <param name="drops">共用掉落配置。</param>
    /// <param name="random">返回 [0,1) 随机值的函数。</param>
    /// <returns>每颗球的类型和奖励；魔法在拾取时按角色上限计算。</returns>
    public static List<LootReward> Roll(MonsterConfig monster, DropConfig drops, Func<float> random)
    {
        var rewards = new List<LootReward>();
        int souls = Count(monster.SoulCount, random());
        for (int i = 0; i < souls; i++) rewards.Add(new(LootKind.Soul, monster.SoulPerOrb));

        // 原版蓝球 12%：1、2、3 颗各 4%，在条件数量区间内均匀采样。
        if (random() < monster.ManaDropChance)
        {
            int count = Count(monster.ManaCount, random());
            for (int i = 0; i < count; i++) rewards.Add(new(LootKind.Mana, 0));
        }
        // 黄球是无双，每颗 5 点；独立于蓝球采样，不再额外生成经验球。
        if (random() < monster.MusouDropChance)
        {
            int count = Count(monster.MusouCount, random());
            for (int i = 0; i < count; i++) rewards.Add(new(LootKind.Musou, drops.MusouPerOrb));
        }
        return rewards;
    }

    /// <summary>计算每颗蓝球实际回蓝量，拾取时读取当前等级的魔法上限。</summary>
    /// <param name="maximum">当前魔法上限。</param>
    /// <param name="drops">最低值和上限比例配置。</param>
    /// <returns>向下取整后的回蓝量。</returns>
    public static int ManaAmount(int maximum, DropConfig drops) =>
        Math.Max(drops.ManaMinimum, (int)(maximum * drops.ManaMaxFraction));

    /// <summary>在包含两端的整数区间中均匀采样。</summary>
    /// <param name="range">已通过启动校验的区间。</param>
    /// <param name="random">[0,1) 随机值。</param>
    /// <returns>区间中的整数。</returns>
    private static int Count(Vector2I range, float random) =>
        Math.Min(range.Y, range.X + (int)(random * (range.Y - range.X + 1)));
}

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

/// <summary>一颗球的不可变奖励；魔法值在实际接触时按当前上限计算。</summary>
/// <param name="Kind">奖励类型。</param>
/// <param name="Amount">灵魂或无双数量。</param>
public readonly record struct LootReward(LootKind Kind, int Amount);
