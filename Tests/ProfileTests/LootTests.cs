using System.Linq;
using GameLogic.Drops;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>真实配置驱动的官方灵魂、蓝球与无双球掉落边界回归。</summary>
public sealed class LootTests
{
    /// <summary>直接核对游戏使用的导表产物，没有在代码中复制另一套正式配置。</summary>
    [Fact]
    public void OfficialMusouTable_UsesDamageRatioAndTimedDecay()
    {
        var battle = TestTables.Tables.TbBattleConfig.Data;
        Assert.Equal(100, battle.WsMax);
        Assert.Equal(2, battle.WsGainPerDamageRatio);
        Assert.Equal(1, battle.WsDecayPerSecond);
        Assert.Equal(6, battle.WsDrainPerSecond);
        Assert.Equal(1.5f, battle.WsAttackMultiplier);
        Assert.Equal(1.5f, battle.WsMoveSpeedMultiplier);
        Assert.Equal(1.5f, battle.WsJumpSpeedMultiplier);
        Assert.Equal(2.5f, battle.WsGravityMultiplier);
        Assert.Equal(1, battle.WsIntroPauseDuration);
        Assert.Equal(5, TestTables.Tables.TbDropConfig.Data.MusouPerOrb);
    }
    /// <summary>灵魂数量落在官方区间；奖励只含三种球，数量与经验规模无关。</summary>
    [Fact]
    public void Rewards_RespectOfficialRanges_AndHaveBoundedCount()
    {
        var drop = TestTables.Tables.TbDropConfig.Data;
        foreach (var monster in TestTables.Tables.TbMonsterConfig.DataList)
            foreach (float random in new[] { 0f, .34f, .67f, .99999f, 1f })
            {
                var loot = new LootSystem(TestTables.Architecture).Roll(monster, () => random);
                Assert.InRange(loot.Count, 2, 10);
                Assert.All(loot.Where(x => x.Kind == LootKind.Musou), x => Assert.Equal(5, x.Amount));
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
        var loot = new LootSystem(TestTables.Architecture).Roll(monster, Random);
        Assert.Equal(expected, loot.Count(x => x.Kind == LootKind.Mana));
    }

    /// <summary>黄球独立采用 12% 掉落概率，每颗加 5 无双，蓝球未触发也可触发黄球。</summary>
    [Theory]
    [InlineData(.11999f, 0f, 1)]
    [InlineData(.11999f, .99999f, 3)]
    [InlineData(.12f, 0f, 0)]
    public void Musou_HasIndependentChanceAndFivePoints(float chance, float count, int expected)
    {
        int calls = 0;
        float Random() => ++calls switch { 1 => 0f, 2 => .99f, 3 => chance, _ => count };
        var loot = new LootSystem(TestTables.Architecture).Roll(TestTables.Tables.TbMonsterConfig.DataList[0], Random);
        Assert.DoesNotContain(loot, x => x.Kind == LootKind.Mana);
        Assert.Equal(expected, loot.Count(x => x.Kind == LootKind.Musou));
        Assert.All(loot.Where(x => x.Kind == LootKind.Musou), x => Assert.Equal(5, x.Amount));
    }

    /// <summary>蓝球最低恢复 10，超过最低量后按魔法上限 5% 向下取整。</summary>
    [Theory]
    [InlineData(50, 10)]
    [InlineData(1000, 50)]
    [InlineData(1039, 51)]
    public void Mana_UsesCurrentMaximum(int maximum, int expected) =>
        Assert.Equal(expected, new LootSystem(TestTables.Architecture).ManaAmount(maximum));
}
