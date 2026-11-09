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
