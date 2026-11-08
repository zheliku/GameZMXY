using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig;
using GameConfig.Enemy;
using GameConfig.Entity;
using GameConfig.Equipment;
using GameConfig.Level;
using GameConfig.Loot;
using GameLogic.Equipment;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>
/// 同版官方七星洞出生流与守关奖励的源数值核对。
/// </summary>
public sealed class QixingConfigurationTests
{
    /// <summary>
    /// 每条出生流保留自己的首次等待和间隔，不能按行位置手工汇总。
    /// </summary>
    [Fact]
    public void SpawnStreams_KeepOfficialCountsAndSeconds()
    {
        List<LevelStage> stages = TestTables.Tables.TbLevelConfig.Get(5).Stages;
        LevelSpawnRecipe[] recipes = stages.SelectMany(x => x.Recipes).ToArray();
        Assert.Equal(new[] { 12, 13, 14, 17, 1 }, stages.Select(s => s.Recipes.Sum(r => r.Count)));
        Assert.Equal(new float[] { 4, 4, 12, 12, 4, 4, 12, 12, 12, 4, 12, 12, 12, 4, 4, 4, 12, 17, 17, 4 },
            recipes.Select(r => r.Delay));
        Assert.All(recipes, r => Assert.Equal(2, r.Interval));
        Assert.All(stages, s => Assert.Equal(0, s.SpawnDelay));
    }

    /// <summary>
    /// 狮驼王掉落自己的骨链，随机属性端点与造一直接概率保持一致。
    /// </summary>
    [Fact]
    public void LionLoot_UsesOfficialNecklaceInsteadOfPreviousBossPool()
    {
        Tables tables = TestTables.Tables;
        EnemyConfig boss = tables.TbEnemyConfig.DataList.Single(x => x.EntityId == EntityId.LionCamelKing);
        Assert.Equal(96, boss.MoveSpeed);
        Assert.Equal(.3f, boss.EquipmentDropChance);
        Assert.Equal(EquipmentDropPoolId.LionCamelKing, boss.EquipmentDropPoolId);
        Assert.Equal(EquipmentId.HeavenlyFiendNecklace,
            Assert.Single(tables.TbEquipmentDropPoolConfig.Get(boss.EquipmentDropPoolId).Entries).EquipmentId);
        var scope = new TestGameScope([(1, 0)], 0, 25);
        EquipmentSystem system = scope.GetSystem<EquipmentSystem>();
        EquipmentData low = system.Roll(Guid.NewGuid(), EquipmentId.HeavenlyFiendNecklace, () => 0);
        EquipmentData high = system.Roll(Guid.NewGuid(), EquipmentId.HeavenlyFiendNecklace, () => 1);
        Assert.Equal((30, 10, .03f, .03f), (low.Attributes.Power, low.Attributes.Def, low.Attributes.Crit, low.Attributes.Miss));
        Assert.Equal((40, 12, .03f, .03f), (high.Attributes.Power, high.Attributes.Def, high.Attributes.Crit, high.Attributes.Miss));
        Assert.Equal(80, tables.TbEquipmentConfig.Get(low.EquipmentId).SellPrice);
    }
}
