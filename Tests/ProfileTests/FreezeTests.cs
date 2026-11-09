using System;
using System.Collections.Generic;
using GameConfig.Battle;
using GameLogic.Architecture;
using GameLogic.Battle;
using Xunit;
using static GameLogic.Battle.HeroState;

namespace GameLogic.Profile.Tests;

/// <summary>
/// 冰冻控制状态的秒制、刷新、暂停和本局生命周期检查。
/// </summary>
public sealed class FreezeTests
{
    /// <summary>
    /// 两人的冻结和中毒独立计时；死亡或 Reset 只清本人效果和保护，死亡同时清本人无双。
    /// </summary>
    /// <param name="heroId">死亡或重置的英雄。</param>
    /// <param name="otherId">效果继续保留的另一英雄。</param>
    /// <param name="reset">是否用重置替代死亡触发个人清理。</param>
    [Theory]
    [InlineData(1, 2, false)]
    [InlineData(2, 1, false)]
    [InlineData(1, 2, true)]
    [InlineData(2, 1, true)]
    public void TwoHeroes_AdvanceAndClearOnlyPersonalEffects(int heroId, int otherId, bool reset)
    {
        var scope = new TestGameScope([(1, 0), (2, 0)], 0, 20);
        HeroSystem heroes = scope.GetSystem<HeroSystem>();
        HeroStatusSystem effects = scope.GetSystem<HeroStatusSystem>();
        HeroState hero = scope.GetModel<HeroModel>().GetState(heroId);
        HeroState other = scope.GetModel<HeroModel>().GetState(otherId);
        AttackConfig poison = TestTables.Tables.TbAttackConfig.Get(TestTables.ConfigIds.GetAttackId(GameConfig.Entity.EntityId.LionCamelKing, 3, 2));
        effects.ApplyFreeze(heroId, 2);
        effects.ApplyFreeze(otherId, 5);
        effects.ApplyPoison(heroId, poison);
        effects.ApplyPoison(otherId, poison);
        Assert.NotSame(hero.GetStatusEffect(StatusEffectKind.Poison), other.GetStatusEffect(StatusEffectKind.Poison));
        effects.AdvanceFreeze(heroId, 1);
        Assert.Equal(0, effects.AdvancePoison(heroId, .75f));
        Assert.Equal(0, effects.AdvancePoison(otherId, .25f));
        Assert.Equal(20, effects.AdvancePoison(heroId, .25f));
        Assert.Equal(1, hero.GetStatusEffect(StatusEffectKind.Freeze).RemainingSeconds);
        Assert.Equal(5, other.GetStatusEffect(StatusEffectKind.Freeze).RemainingSeconds);
        Assert.Equal(10, other.GetStatusEffect(StatusEffectKind.Poison).RemainingTicks);
        Assert.Equal(.75, other.GetStatusEffect(StatusEffectKind.Poison).TickLeftSeconds);
        heroes.AddWs(heroId, 20);
        heroes.AddWs(otherId, 30);
        for (int hit = 0; hit < 5; hit++)
        {
            heroes.RecordHurt(heroId);
            heroes.RecordHurt(otherId);
        }
        int otherHp = other.Hp.Value;

        if (reset)
        {
            heroes.Reset(heroId, 1, TestTables.Tables.TbHeroGrowthConfig.Get(heroId).Levels[0].BaseAttributes);
        }
        else
        {
            Assert.True(heroes.Damage(heroId, int.MaxValue));
            Assert.Equal(0, hero.Ws.Value);
            Assert.False(hero.WsActive.Value);
        }

        Assert.False(effects.IsFrozen(heroId) || effects.IsPoisoned(heroId));
        Assert.Equal(0, hero.HurtInvulnerabilitySeconds);
        Assert.True(effects.IsFrozen(otherId) && effects.IsPoisoned(otherId));
        Assert.Equal((otherHp, 30, 2d), (other.Hp.Value, other.Ws.Value, other.HurtInvulnerabilitySeconds));
        Assert.Equal(5, other.GetStatusEffect(StatusEffectKind.Freeze).RemainingSeconds);
        Assert.Equal(20, effects.AdvancePoison(otherId, .75f));
        Assert.Equal(9, other.GetStatusEffect(StatusEffectKind.Poison).RemainingTicks);
        effects.AdvanceFreeze(otherId, 5);
        Assert.False(effects.IsFrozen(otherId));
    }

    /// <summary>
    /// 两档官方持续时间在不同物理频率下按秒结束，不受渲染帧数影响。
    /// </summary>
    /// <param name="frequency">每秒游戏步数。</param>
    /// <param name="seconds">原版普通或高等级龙王的冰冻时长。</param>
    [Theory]
    [InlineData(24, 2)]
    [InlineData(60, 2)]
    [InlineData(120, 2)]
    [InlineData(144, 5)]
    public void Freeze_ExpiresInGameSeconds(int frequency, int seconds)
    {
        var scope = new TestGameScope([(1, 0)], 0, 25);
        HeroStatusSystem system = scope.GetSystem<HeroStatusSystem>();
        system.ApplyFreeze(scope.Hero.HeroId, seconds);
        for (int step = 0; step < seconds * frequency - 1; step++)
        {
            system.AdvanceFreeze(scope.Hero.HeroId, 1f / frequency);
        }

        Assert.True(system.IsFrozen(scope.Hero.HeroId));
        system.AdvanceFreeze(scope.Hero.HeroId, 1f / frequency);
        Assert.False(system.IsFrozen(scope.Hero.HeroId));
    }

    /// <summary>
    /// 冻结和中毒可并存，离关重建清空两者，其他作用域不受影响。
    /// </summary>
    [Fact]
    public void Effects_CoexistAndClearWithoutCrossingScopes()
    {
        var scope = new TestGameScope([(1, 0)], 0, 25);
        var otherScope = new TestGameScope([(1, 0)], 0, 25);
        HeroStatusSystem system = scope.GetSystem<HeroStatusSystem>();
        HeroStatusSystem otherSystem = otherScope.GetSystem<HeroStatusSystem>();
        AttackConfig poison = TestTables.Tables.TbAttackConfig.Get(TestTables.ConfigIds.GetAttackId(GameConfig.Entity.EntityId.LionCamelKing, 3, 2));

        // 冻结限制动作但不停止中毒的游戏时钟，各作用域独立推进。
        system.ApplyFreeze(scope.Hero.HeroId, 2);
        system.ApplyPoison(scope.Hero.HeroId, poison);
        otherSystem.ApplyFreeze(otherScope.Hero.HeroId, 5);
        system.AdvanceFreeze(scope.Hero.HeroId, 1);
        Assert.Equal(20, system.AdvancePoison(scope.Hero.HeroId, 1));
        Assert.True(system.IsFrozen(scope.Hero.HeroId) && system.IsPoisoned(scope.Hero.HeroId));
        Assert.False(otherSystem.IsPoisoned(otherScope.Hero.HeroId));

        // 走正式系统的结束与重新初始化路径，旧效果不能进入新一局。
        scope.RestartHero();
        Assert.False(system.IsFrozen(scope.Hero.HeroId) || system.IsPoisoned(scope.Hero.HeroId));
        Assert.Equal(0, system.AdvancePoison(scope.Hero.HeroId, 100));
        Assert.True(otherSystem.IsFrozen(otherScope.Hero.HeroId));
        Assert.Equal(5f, otherScope.Hero.StatusEffects.GetValueOrDefault(StatusEffectKind.Freeze).RemainingSeconds);
        system.ApplyPoison(scope.Hero.HeroId, poison);
        Assert.Equal(20, system.AdvancePoison(scope.Hero.HeroId, 1));
    }

    /// <summary>
    /// 暂停与非法步长不推进，刷新不缩短控制；死亡与重建清理旧状态。
    /// </summary>
    [Fact]
    public void Freeze_RefreshesPausesAndClearsWithSession()
    {
        var scope = new TestGameScope([(1, 0)], 0, 25);
        HeroStatusSystem system = scope.GetSystem<HeroStatusSystem>();
        HeroSystem heroSystem = scope.GetSystem<HeroSystem>();
        system.ApplyFreeze(scope.Hero.HeroId, 5);
        scope.Hero.Ws.Value = scope.Hero.MaxWs.Value;
        Assert.False(heroSystem.TryActivateWs(scope.Hero.HeroId));
        Assert.False(scope.Hero.WsActive.Value);
        system.AdvanceFreeze(scope.Hero.HeroId, 1);
        system.ApplyFreeze(scope.Hero.HeroId, 2);
        system.AdvanceFreeze(scope.Hero.HeroId, 0);
        system.AdvanceFreeze(scope.Hero.HeroId, float.NaN);
        Assert.True(system.IsFrozen(scope.Hero.HeroId));
        Assert.Throws<ArgumentOutOfRangeException>(() => system.ApplyFreeze(scope.Hero.HeroId, float.PositiveInfinity));
        heroSystem.Damage(scope.Hero.HeroId, int.MaxValue);
        Assert.False(system.IsFrozen(scope.Hero.HeroId));
        system.ApplyFreeze(scope.Hero.HeroId, 2);
        Assert.False(system.IsFrozen(scope.Hero.HeroId));
        heroSystem.Reset(1, 1, TestTables.Tables.TbHeroGrowthConfig.Get(1).Levels[0].BaseAttributes);
        system.ApplyFreeze(scope.Hero.HeroId, 2);
        heroSystem.Reset(1, 1, TestTables.Tables.TbHeroGrowthConfig.Get(1).Levels[0].BaseAttributes);
        Assert.False(system.IsFrozen(scope.Hero.HeroId));
    }
}
