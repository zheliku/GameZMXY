using GameLogic.Architecture;
using GameLogic.Battle;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>
/// 官方英雄连续受击保护：五次触发、相邻一秒断链和两秒游戏时间无敌。
/// </summary>
public sealed class HurtProtectionTests
{
    /// <summary>
    /// 连击计数和保护秒数由各人独占，推进或触发一人的保护不改变另一人的连击相位。
    /// </summary>
    /// <param name="heroId">先触发保护的英雄。</param>
    /// <param name="otherId">保留独立连击的另一英雄。</param>
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    public void TwoHeroes_KeepHitChainsAndProtectionClocksIndependent(int heroId, int otherId)
    {
        var scope = new TestGameScope([(1, 0), (2, 0)], 0, 20);
        HeroSystem system = scope.GetSystem<HeroSystem>();
        HeroState hero = scope.GetModel<HeroModel>().GetState(heroId);
        HeroState other = scope.GetModel<HeroModel>().GetState(otherId);
        system.RecordHurt(otherId);
        system.AdvanceHurtProtection(otherId, .25f);
        for (int hit = 0; hit < 5; hit++)
        {
            system.RecordHurt(heroId);
        }
        Assert.Equal((0, 2d), (hero.HurtHitCount, hero.HurtInvulnerabilitySeconds));
        Assert.Equal((1, .75d, 0d), (other.HurtHitCount, other.HurtChainSeconds, other.HurtInvulnerabilitySeconds));
        system.AdvanceHurtProtection(heroId, 1);
        Assert.Equal((1, .75d, 0d), (other.HurtHitCount, other.HurtChainSeconds, other.HurtInvulnerabilitySeconds));
        system.AdvanceHurtProtection(otherId, .75f);
        Assert.Equal(0, other.HurtHitCount);
        Assert.Equal(1d, hero.HurtInvulnerabilitySeconds);
        for (int hit = 0; hit < 5; hit++)
        {
            system.RecordHurt(otherId);
        }
        system.AdvanceHurtProtection(heroId, 1);
        Assert.Equal(0, hero.HurtInvulnerabilitySeconds);
        Assert.Equal(2d, other.HurtInvulnerabilitySeconds);
    }

    /// <summary>
    /// 悟空与唐僧共用阈值，第五次扣血后保护后续命中，持续时间不依赖物理帧率。
    /// </summary>
    /// <param name="heroId">官方拥有保护的英雄编号。</param>
    /// <param name="frequency">每秒物理推进次数。</param>
    [Theory]
    [InlineData(1, 24)]
    [InlineData(1, 60)]
    [InlineData(1, 120)]
    [InlineData(2, 60)]
    public void FifthHit_ProtectsForTwoSeconds(int heroId, int frequency)
    {
        var scope = new TestGameScope([(heroId, 0)], 0, 25, initializeHero: heroId == 1);
        HeroSystem system = scope.GetSystem<HeroSystem>();
        if (heroId != 1)
        {
            // 唐僧尚无正式成长表；借用基础数值只验证保护规则不依赖悟空身份。
            system.Reset(heroId, 1, TestTables.Tables.TbHeroGrowthConfig.Get(1).Levels[0].BaseAttributes);
        }
        int initial = scope.Hero.Hp.Value;
        for (int hit = 1; hit <= 5; hit++)
        {
            system.Damage(scope.Hero.HeroId, 1);
            system.RecordHurt(scope.Hero.HeroId);
            Assert.Equal(initial - hit, scope.Hero.Hp.Value);
            Assert.Equal(hit == 5 ? 2d : 0d, scope.Hero.HurtInvulnerabilitySeconds);
        }

        for (int frame = 0; frame < frequency * 2 - 1; frame++)
        {
            system.AdvanceHurtProtection(scope.Hero.HeroId, 1f / frequency);
        }
        Assert.True(scope.Hero.HurtInvulnerabilitySeconds > 0);
        system.AdvanceHurtProtection(scope.Hero.HeroId, 1f / frequency);
        Assert.Equal(0, scope.Hero.HurtInvulnerabilitySeconds);
    }

    /// <summary>
    /// 间隔不足一秒保留连击，达到一秒清空；暂停不推进，死亡和重建清理本局保护。
    /// </summary>
    [Fact]
    public void Chain_BreaksAtOneSecondAndClearsWithSession()
    {
        var scope = new TestGameScope([(1, 0)], 0, 25);
        HeroSystem system = scope.GetSystem<HeroSystem>();
        system.RecordHurt(scope.Hero.HeroId);
        system.AdvanceHurtProtection(scope.Hero.HeroId, .99f);
        system.RecordHurt(scope.Hero.HeroId);
        Assert.Equal(2, scope.Hero.HurtHitCount);
        system.AdvanceHurtProtection(scope.Hero.HeroId, 1);
        system.RecordHurt(scope.Hero.HeroId);
        Assert.Equal(1, scope.Hero.HurtHitCount);
        for (int hit = 0; hit < 4; hit++)
        {
            system.RecordHurt(scope.Hero.HeroId);
        }
        system.AdvanceHurtProtection(scope.Hero.HeroId, 0);
        system.AdvanceHurtProtection(scope.Hero.HeroId, float.NaN);
        Assert.Equal(2, scope.Hero.HurtInvulnerabilitySeconds);
        system.RecordHurt(scope.Hero.HeroId);
        Assert.Equal(0, scope.Hero.HurtHitCount);
        system.Damage(scope.Hero.HeroId, int.MaxValue);
        Assert.Equal(0, scope.Hero.HurtInvulnerabilitySeconds);
        system.Reset(1, 1, TestTables.Tables.TbHeroGrowthConfig.Get(1).Levels[0].BaseAttributes);
        system.RecordHurt(scope.Hero.HeroId);
        Assert.Equal(1, scope.Hero.HurtHitCount);
    }
}
