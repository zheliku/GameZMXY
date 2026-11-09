using GameConfig.Attributes;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Attributes;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>
/// 装备恢复按官方25/24秒节拍推进，并保留负恢复、上限和本局生命周期。
/// </summary>
public sealed class RecoveryTests
{
    /// <summary>
    /// 同一恢复来源键分别拥有数值和25/24秒节拍，推进或更换一人的来源不消耗另一人的相位。
    /// </summary>
    /// <param name="heroId">先结算恢复的英雄。</param>
    /// <param name="otherId">暂时保留恢复相位的另一英雄。</param>
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    public void TwoHeroes_KeepRecoveryValuesAndPhasesIndependent(int heroId, int otherId)
    {
        var scope = new TestGameScope([(1, 0), (2, 0)], 0, 20);
        HeroSystem system = scope.GetSystem<HeroSystem>();
        HeroState hero = scope.GetModel<HeroModel>().GetState(heroId);
        HeroState other = scope.GetModel<HeroModel>().GetState(otherId);
        var source = new AttributeSourceId("Recovery", 1);
        system.SetSource(heroId, source, [AttributeModifier.Flat(AttributeType.HpRegen, 2), AttributeModifier.Flat(AttributeType.MpRegen, 3)]);
        system.SetSource(otherId, source, [AttributeModifier.Flat(AttributeType.HpRegen, 5), AttributeModifier.Flat(AttributeType.MpRegen, 7)]);
        system.Damage(heroId, 20);
        system.Damage(otherId, 20);
        Assert.True(system.TrySpendMp(heroId, 20));
        Assert.True(system.TrySpendMp(otherId, 20));
        int hp = hero.Hp.Value, mp = hero.Mp.Value, otherHp = other.Hp.Value, otherMp = other.Mp.Value;
        system.AdvanceRecovery(heroId, 1);
        system.AdvanceRecovery(otherId, .5f);
        Assert.Equal(0, system.AdvanceRecovery(heroId, 1f / 24));
        Assert.Equal((hp + 2, mp + 3), (hero.Hp.Value, hero.Mp.Value));
        Assert.Equal((otherHp, otherMp, .5d), (other.Hp.Value, other.Mp.Value, other.RecoveryProgress));
        Assert.True(system.RemoveSource(heroId, source));
        Assert.Equal(0, system.AdvanceRecovery(otherId, 13f / 24));
        Assert.Equal((otherHp + 5, otherMp + 7), (other.Hp.Value, other.Mp.Value));
        Assert.Equal((hp + 2, mp + 3), (hero.Hp.Value, hero.Mp.Value));
    }

    /// <summary>
    /// 实际推进25秒始终结算24次，不随引擎频率改变恢复总量。
    /// </summary>
    /// <param name="fps">每秒游戏步数。</param>
    [Theory]
    [InlineData(24)]
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(144)]
    public void Recovery_UsesOfficialSecondsAtEveryFrequency(int fps)
    {
        var scope = new TestGameScope([(1, 0)], 0, 25);
        HeroSystem system = scope.GetSystem<HeroSystem>();
        system.SetSource(scope.Hero.HeroId, new AttributeSourceId("Recovery", 1),
            [AttributeModifier.Flat(AttributeType.HpRegen, 2), AttributeModifier.Flat(AttributeType.MpRegen, 1)]);
        system.Damage(scope.Hero.HeroId, 60);
        system.TrySpendMp(scope.Hero.HeroId, 40);
        for (int index = 0; index < 25 * fps; index++)
        {
            Assert.Equal(0, system.AdvanceRecovery(scope.Hero.HeroId, 1f / fps));
        }

        Assert.Equal(68, scope.Hero.Hp.Value);
        Assert.Equal(34, scope.Hero.Mp.Value);
    }

    /// <summary>
    /// 穿卸只修改后续恢复值，零秒暂停不推进；负恢复不被属性汇总抹掉。
    /// </summary>
    [Fact]
    public void Recovery_KeepsPhaseAcrossEquipmentChangesAndPreservesNegativeCost()
    {
        var scope = new TestGameScope([(1, 0)], 0, 25);
        HeroSystem system = scope.GetSystem<HeroSystem>();
        var source = new AttributeSourceId("Recovery", 1);
        system.AdvanceRecovery(scope.Hero.HeroId, 1f);
        system.SetSource(scope.Hero.HeroId, source, [AttributeModifier.Flat(AttributeType.HpRegen, -2), AttributeModifier.Flat(AttributeType.MpRegen, -3)]);
        Assert.Equal(-2, scope.Hero.HpRegen.Value);
        for (int index = 0; index < 100; index++)
        {
            Assert.Equal(0, system.AdvanceRecovery(scope.Hero.HeroId, 0));
        }

        Assert.Equal(2, system.AdvanceRecovery(scope.Hero.HeroId, 1f / 24));
        Assert.Equal(47, scope.Hero.Mp.Value);
        system.Damage(scope.Hero.HeroId, 2);
        system.RemoveSource(scope.Hero.HeroId, source);
        Assert.Equal(0, system.AdvanceRecovery(scope.Hero.HeroId, 25f / 24));
        Assert.Equal(78, scope.Hero.Hp.Value);
    }

    /// <summary>
    /// 长步钳制资源上限；死亡不恢复，重建本局清除旧节拍。
    /// </summary>
    [Fact]
    public void Recovery_ClampsResourcesAndResetsOnlyWithSession()
    {
        var scope = new TestGameScope([(1, 0)], 0, 25);
        HeroSystem system = scope.GetSystem<HeroSystem>();
        system.SetSource(scope.Hero.HeroId, new AttributeSourceId("Recovery", 1),
            [AttributeModifier.Flat(AttributeType.HpRegen, 100), AttributeModifier.Flat(AttributeType.MpRegen, 100)]);
        system.AdvanceRecovery(scope.Hero.HeroId, 100);
        Assert.Equal((80, 50), (scope.Hero.Hp.Value, scope.Hero.Mp.Value));
        system.Damage(scope.Hero.HeroId, int.MaxValue);
        Assert.Equal(0, system.AdvanceRecovery(scope.Hero.HeroId, 100));
        Assert.Equal(0, scope.Hero.Hp.Value);
        system.Reset(1, 1, TestTables.Tables.TbHeroGrowthConfig.Get(1).Levels[0].BaseAttributes);
        Assert.Equal(0, scope.Hero.RecoveryProgress);
        Assert.Equal(0, system.AdvanceRecovery(scope.Hero.HeroId, float.NaN));
    }
}
