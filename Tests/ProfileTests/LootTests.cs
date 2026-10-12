using System.Linq;
using GameLogic.Drops;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>真实配置驱动的掉落边界与经验守恒回归。</summary>
public sealed class LootTests
{
    /// <summary>所有怪物的经验随机拆分均保持配方总额，灵魂落在官方区间。</summary>
    [Fact]
    public void Rewards_ConserveExperience_AndRespectSoulRange()
    {
        var drop = TestTables.Tables.TbDropConfig.Data;
        foreach (var monster in TestTables.Tables.TbMonsterConfig.DataList)
        foreach (int xp in new[] { 0, 1, monster.AddExp, 25 })
        foreach (float random in new[] { 0f, .34f, .67f, .99999f, 1f })
        {
            var loot = LootHelper.Roll(monster, xp, drop, () => random);
            Assert.Equal(xp, loot.Where(x => x.Kind == LootKind.Experience).Sum(x => x.Amount));
            Assert.All(loot.Where(x => x.Kind == LootKind.Experience), x => Assert.True(x.Amount > 0));
            Assert.InRange(loot.Count(x => x.Kind == LootKind.Soul), monster.SoulCount.X, monster.SoulCount.Y);
            Assert.All(loot.Where(x => x.Kind == LootKind.Soul), x => Assert.Equal(monster.SoulPerOrb, x.Amount));
        }
    }

    /// <summary>12% 概率边界不多发蓝球，触发时数量可覆盖区间两端。</summary>
    [Theory]
    [InlineData(.11999f, 1f, 3)]
    [InlineData(.12f, 0f, 0)]
    [InlineData(0f, 0f, 1)]
    public void Mana_UsesIndependentChance(float chance, float count, int expected)
    {
        int calls = 0;
        float Random() => ++calls == 2 ? chance : count;
        var monster = TestTables.Tables.TbMonsterConfig.DataList[0];
        var loot = LootHelper.Roll(monster, 0, TestTables.Tables.TbDropConfig.Data, Random);
        Assert.Equal(expected, loot.Count(x => x.Kind == LootKind.Mana));
    }

    /// <summary>蓝球最低恢复 10，超过最低量后按魔法上限 5% 向下取整。</summary>
    [Theory]
    [InlineData(50, 10)]
    [InlineData(1000, 50)]
    [InlineData(1039, 51)]
    public void Mana_UsesCurrentMaximum(int maximum, int expected) =>
        Assert.Equal(expected, LootHelper.ManaAmount(maximum, TestTables.Tables.TbDropConfig.Data));
}
