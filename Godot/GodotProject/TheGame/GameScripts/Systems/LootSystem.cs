using System;
using System.Collections.Generic;
using GameConfig;
using GameConfig.Battle;
using GameConfig.Enemy;
using GameConfig.Equipment;
using GameConfig.Loot;
using GameLogic.Architecture;
using GameLogic.Config;
using Godot;

namespace GameLogic.Drops;

/// <summary>
/// 掉落规则系统；查询共享定义、抽取奖励，不创建节点或修改档案。
/// </summary>
public sealed class LootSystem : AbstractSystem
{
    /// <summary>
    /// 绑定所属架构。
    /// </summary>
    /// <param name="architecture">所属游戏入口。</param>
    public LootSystem(IArchitecture architecture) : base(architecture) { }

    /// <summary>
    /// 恢复品互斥抽取；装备另外独立判定，按原版 round 索引对应的权重选一件。
    /// </summary>
    /// <param name="equipmentChance">怪物装备概率。</param>
    /// <param name="equipmentPoolId">怪物的装备池主键。</param>
    /// <param name="random">返回 [0,1] 的采样器，允许 Godot 的闭区间端点。</param>
    /// <returns>至多一件恢复品和一件装备的定义编号。</returns>
    public GroundLootSelection RollGround(float equipmentChance, EquipmentDropPoolId equipmentPoolId, Func<float> random)
    {
        Tables tables = this.GetModel<ConfigurationModel>().Tables;
        IReadOnlyList<PickupConfig> pickups = tables.TbPickupConfig.DataList;
        EquipmentDropPoolConfig pool = tables.TbEquipmentDropPoolConfig.GetOrDefault(equipmentPoolId);
        float medicine = random();
        PickupId pickup = PickupId.None;
        float threshold = 0;
        foreach (PickupConfig entry in pickups)
        {
            threshold += entry.DropChance;
            if (medicine < threshold)
            {
                pickup = entry.Id;
                break;
            }
        }

        EquipmentId equipment = EquipmentId.None;
        if (pool != null && random() < equipmentChance)
        {
            int total = 0;
            foreach (EquipmentDropEntry entry in pool.Entries)
            {
                total = checked(total + entry.Weight);
            }

            int sample = Math.Min(total - 1, (int)(random() * total));
            foreach (EquipmentDropEntry entry in pool.Entries)
            {
                sample -= entry.Weight;
                if (sample < 0)
                {
                    equipment = entry.EquipmentId;
                    break;
                }
            }
        }

        return new GroundLootSelection(pickup, equipment);
    }

    /// <summary>
    /// 按原版规则生成灵魂、魔法、无双与生命球；经验独立在击败时结算。
    /// </summary>
    /// <param name="monster">怪物基础掉落配置。</param>
    /// <param name="soulPerOrb">本次敌人实例确定的每球灵魂值。</param>
    /// <param name="maximumHp">生成时英雄的生命上限。</param>
    /// <param name="maximumMp">生成时英雄的魔法上限。</param>
    /// <param name="random">返回 [0,1) 随机值的函数。</param>
    /// <returns>每颗球的类型和奖励；恢复量按生成时角色上限确定。</returns>
    public List<LootReward> Roll(EnemyConfig monster, int soulPerOrb, int maximumHp, int maximumMp, Func<float> random)
    {
        BattleConfig drops = this.GetModel<ConfigurationModel>().Tables.TbBattleConfig.Data;
        var rewards = new List<LootReward>();
        int souls = Count(monster.SoulCount, random());
        for (int i = 0; i < souls; i++)
        {
            rewards.Add(new(LootKind.Soul, soulPerOrb));
        }

        // 原版蓝球 12%：1、2、3 颗各 4%，在条件数量区间内均匀采样。
        if (random() < monster.ManaDropChance)
        {
            int count = Count(monster.ManaCount, random());
            int amount = ManaAmount(maximumMp);
            for (int i = 0; i < count; i++)
            {
                rewards.Add(new(LootKind.Mana, amount));
            }
        }

        // 黄球是无双，每颗 5 点；独立于蓝球采样，不再额外生成经验球。
        if (random() < monster.MusouDropChance)
        {
            int count = Count(monster.MusouCount, random());
            for (int i = 0; i < count; i++)
            {
                rewards.Add(new(LootKind.Musou, drops.MusouPerOrb));
            }
        }

        // 生命球与蓝、黄球分别独立采样；单颗恢复量在掉落时固定。
        if (random() < drops.HealthDropChance)
        {
            int count = Count(drops.HealthCount, random());
            int amount = HealthAmount(maximumHp);
            for (int i = 0; i < count; i++)
            {
                rewards.Add(new(LootKind.Health, amount));
            }
        }

        return rewards;
    }

    /// <summary>
    /// 按生成时的魔法上限计算每颗蓝球恢复量。
    /// </summary>
    /// <param name="maximum">当前魔法上限。</param>
    /// <returns>向下取整后的回蓝量。</returns>
    public int ManaAmount(int maximum)
    {
        BattleConfig drops = this.GetModel<ConfigurationModel>().Tables.TbBattleConfig.Data;
        return Math.Max(drops.ManaMinimum, (int)(maximum * drops.ManaMaxFraction));
    }

    /// <summary>
    /// 按生成时的生命上限计算每颗红球恢复量。
    /// </summary>
    /// <param name="maximum">掉落时的生命上限。</param>
    /// <returns>向下取整并满足最低值的回血量。</returns>
    public int HealthAmount(int maximum)
    {
        BattleConfig drops = this.GetModel<ConfigurationModel>().Tables.TbBattleConfig.Data;
        return Math.Max(drops.HealthMinimum, (int)(maximum * drops.HealthMaxFraction));
    }

    /// <summary>
    /// 在包含两端的整数区间中均匀采样。
    /// </summary>
    /// <param name="range">已通过启动校验的区间。</param>
    /// <param name="random">[0,1) 随机值。</param>
    /// <returns>区间中的整数。</returns>
    private static int Count(Vector2I range, float random) => Math.Min(range.Y, range.X + (int)(random * (range.Y - range.X + 1)));
}
