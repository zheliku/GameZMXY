using System;
using GameConfig.Battle;
using GameFramework;
using GameLogic.Battle;
using GameLogic.Battle.Attacks;
using GameLogic.Battle.Damage;
using Godot;
using Xunit;

namespace GameLogic.Battle.Tests;

/// <summary>
/// 验证蓄力、激活、计时耗尽和复位的运行期无双契约。
/// </summary>
public sealed class MusouTests
{
    /// <summary>
    /// 同一系统下两人的蓄积、满值、开启和两种扣点相位互不推进，交换英雄方向仍成立。
    /// </summary>
    /// <param name="heroId">本次主要推进的英雄编号。</param>
    /// <param name="otherId">应保持独立相位的另一英雄编号。</param>
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    public void TwoHeroes_KeepGainActivationAndDrainIndependent(int heroId, int otherId)
    {
        var scope = new HeroTestScope(twoHeroes: true);
        HeroModel heroes = scope.GetModel<HeroModel>();
        HeroState hero = heroes.GetState(heroId);
        HeroState other = heroes.GetState(otherId);
        scope.System.ResetWs(heroId, 100, 1, 6);
        scope.System.ResetWs(otherId, 100, 1, 6);

        // 各自接收命中收益；推进一人的小数相位不能影响另一人的下一次整点扣减。
        scope.System.RecordHit(heroId, new DamageResult(1, false, false, DamageKind.Physics, 20));
        Assert.Equal(0, other.Ws.Value);
        scope.System.RecordHit(otherId, new DamageResult(1, false, false, DamageKind.Physics, 10));
        scope.System.AdvanceWs(heroId, .6f);
        scope.System.AdvanceWs(otherId, .25f);
        scope.System.RecordHit(heroId, new DamageResult(1, false, false, DamageKind.Physics, 2));
        scope.System.AdvanceWs(heroId, .4f);
        Assert.Equal((21, 10), (hero.Ws.Value, other.Ws.Value));
        Assert.Equal(.25, other.WsDecayProgress);
        Assert.False(other.WsActive.Value);

        // 仅本人的满值可以开启；另一人保持原进度、相位和激活状态。
        scope.System.AddWs(heroId, 79);
        Assert.False(scope.System.TryActivateWs(otherId));
        Assert.True(scope.System.TryActivateWs(heroId));
        scope.System.AdvanceWs(heroId, 2);
        Assert.Equal((87, 10), (hero.Ws.Value, other.Ws.Value));
        Assert.True(hero.WsActive.Value);
        Assert.False(other.WsActive.Value);
        Assert.Equal(.25, other.WsDecayProgress);
        Assert.Equal(0, other.WsDrainProgress);
        scope.System.AdvanceWs(otherId, .75f);
        Assert.Equal((87, 9), (hero.Ws.Value, other.Ws.Value));
        scope.System.AddWsFromPickup(heroId, 5);
        Assert.Equal((92, 9), (hero.Ws.Value, other.Ws.Value));

        // 两人同时开启时仍各自累计不足一点的消耗，后开启者不能重置先开启者的时钟。
        scope.System.AdvanceWs(heroId, .125f);
        Assert.Equal(.75, hero.WsDrainProgress);
        scope.System.AddWs(otherId, 91);
        Assert.True(scope.System.TryActivateWs(otherId));
        Assert.Equal(.75, hero.WsDrainProgress);
        scope.System.AdvanceWs(otherId, .25f);
        Assert.Equal((92, 99), (hero.Ws.Value, other.Ws.Value));
        Assert.Equal((.75, .5), (hero.WsDrainProgress, other.WsDrainProgress));
        scope.System.AdvanceWs(heroId, .125f);
        Assert.Equal((90, 99), (hero.Ws.Value, other.Ws.Value));
        Assert.Equal(.5, other.WsDrainProgress);
        scope.System.AdvanceWs(heroId, 20);
        Assert.Equal(0, hero.Ws.Value);
        Assert.False(hero.WsActive.Value);
        Assert.Equal(99, other.Ws.Value);
        Assert.True(other.WsActive.Value);
    }

    /// <summary>
    /// 原版伤害比例四舍五入，暴击伤害可增加蓄力，零威力不产生无穷奖励。
    /// </summary>
    /// <param name="damage">已经结算的实际伤害值。</param>
    /// <param name="power">本次攻击使用的威力值。</param>
    /// <param name="expected">本次参数组合对应的预期结果。</param>
    [Theory]
    [InlineData(10, 10, 2)]
    [InlineData(20, 10, 4)]
    [InlineData(2, 10, 0)]
    [InlineData(3, 10, 1)]
    [InlineData(10, 0, 0)]
    [InlineData(0, 10, 0)]
    public void HitGain_UsesOfficialDamageRatio(int damage, int power, int expected)
    {
        var scope = new HeroTestScope();
        scope.System.ResetWs(scope.Hero.HeroId, 100, 1f, 6f);
        var attacker = new CombatantAttributes(CombatSide.Hero, 1, power, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var attack = AttackData.Create(1001, attacker, damage, DamageKind.Physics, Vector2.Zero, 1, 0);
        try
        {
            var defender = new CombatantAttributes(CombatSide.Enemy, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            DamageResult result = new CombatSystem(scope).Calculate(attack, defender, 1, 1);
            scope.System.RecordHit(scope.Hero.HeroId, result);
            Assert.Equal(expected, scope.Hero.Ws.Value);
        }
        finally
        {
            ReferencePool.Release(attack);
        }
    }

    /// <summary>
    /// 自然衰减不随命中重置，满值冻结相位，暂停与长帧保持一致。
    /// </summary>
    [Fact]
    public void NaturalDecay_PreservesFullGaugeAndDoesNotResetOnHit()
    {
        var scope = new HeroTestScope();
        HeroState gauge = scope.Hero;
        scope.System.ResetWs(scope.Hero.HeroId, 100, 1f, 6f);
        scope.System.AddWs(scope.Hero.HeroId, 20);
        scope.System.AdvanceWs(scope.Hero.HeroId, .6f);
        scope.System.AddWs(scope.Hero.HeroId, 2);
        scope.System.AdvanceWs(scope.Hero.HeroId, .4f);
        Assert.Equal(21, gauge.Ws.Value);
        scope.System.AdvanceWs(scope.Hero.HeroId, 3);
        Assert.Equal(18, gauge.Ws.Value);
        scope.System.AdvanceWs(scope.Hero.HeroId, 0);
        Assert.Equal(18, gauge.Ws.Value);
        scope.System.AddWs(scope.Hero.HeroId, 82);
        scope.System.AdvanceWs(scope.Hero.HeroId, 60);
        Assert.True(gauge.IsWsFull);
        Assert.Equal(100, gauge.Ws.Value);
        scope.System.ClearWs(scope.Hero.HeroId);
        scope.System.AddWs(scope.Hero.HeroId, 8);
        scope.System.AdvanceWs(scope.Hero.HeroId, 8);
        Assert.Equal(0, gauge.Ws.Value);
    }

    /// <summary>
    /// 命中在开启时不积累，黄球仍可补充；超过上限的整颗黄球不增加。
    /// </summary>
    [Fact]
    public void Pickup_CanRefillActiveGauge_WithoutOverflow()
    {
        var scope = new HeroTestScope();
        HeroState gauge = scope.Hero;
        scope.System.ResetWs(scope.Hero.HeroId, 100, 1f, 6f);
        scope.System.AddWs(scope.Hero.HeroId, 100);
        Assert.True(scope.System.TryActivateWs(scope.Hero.HeroId));
        scope.System.AdvanceWs(scope.Hero.HeroId, 2);
        int value = gauge.Ws.Value;
        scope.System.AddWs(scope.Hero.HeroId, 5);
        Assert.Equal(value, gauge.Ws.Value);
        scope.System.AddWsFromPickup(scope.Hero.HeroId, 5);
        Assert.Equal(value + 5, gauge.Ws.Value);
        scope.System.AddWsFromPickup(scope.Hero.HeroId, 100);
        Assert.Equal(value + 5, gauge.Ws.Value);
        Assert.True(gauge.WsActive.Value);
    }

    /// <summary>
    /// 开启后六次/秒扣点与自然扣点叠加，分帧和长帧得到相同结果。
    /// </summary>
    /// <param name="updatesPerSecond">每秒推进次数，用于验证秒制无双扣减。</param>
    [Theory]
    [InlineData(24)]
    [InlineData(60)]
    [InlineData(120)]
    [InlineData(144)]
    [InlineData(240)]
    public void ActiveDrain_IsIndependentOfFrameSize(int updatesPerSecond)
    {
        var wholeScope = new HeroTestScope();
        var splitScope = new HeroTestScope();
        HeroState whole = wholeScope.Hero;
        HeroState split = splitScope.Hero;
        foreach (HeroTestScope scope in new[] { wholeScope, splitScope })
        {
            scope.System.ResetWs(scope.Hero.HeroId, 100, 1f, 6f);
            scope.System.AddWs(scope.Hero.HeroId, 100);
            Assert.True(scope.System.TryActivateWs(scope.Hero.HeroId));
        }
        wholeScope.System.AdvanceWs(wholeScope.Hero.HeroId, 5);
        for (int i = 0; i < 5 * updatesPerSecond; i++)
        {
            splitScope.System.AdvanceWs(splitScope.Hero.HeroId, 1f / updatesPerSecond);
        }

        Assert.Equal(66, whole.Ws.Value);
        Assert.Equal(whole.Ws.Value, split.Ws.Value);
        wholeScope.System.AdvanceWs(wholeScope.Hero.HeroId, 20);
        splitScope.System.AdvanceWs(splitScope.Hero.HeroId, 20);
        Assert.False(whole.WsActive.Value);
        Assert.False(split.WsActive.Value);
    }

    /// <summary>
    /// 未满不能开启；开启后失去满值提示且命中不续充、不能重开。
    /// </summary>
    [Fact]
    public void Activation_RequiresFullGaugeAndLocksGains()
    {
        var scope = new HeroTestScope();
        HeroState gauge = scope.Hero;
        scope.System.ResetWs(scope.Hero.HeroId, 100, 1f, 6f);
        scope.System.AddWs(scope.Hero.HeroId, 99);
        Assert.False(scope.System.TryActivateWs(scope.Hero.HeroId));
        scope.System.AddWs(scope.Hero.HeroId, 1);
        Assert.Equal(100, gauge.Ws.Value);
        Assert.True(scope.System.TryActivateWs(scope.Hero.HeroId));
        Assert.True(gauge.WsActive.Value);
        Assert.False(gauge.IsWsFull);
        Assert.False(scope.System.TryActivateWs(scope.Hero.HeroId));
        scope.System.AdvanceWs(scope.Hero.HeroId, 2.5f);
        scope.System.AddWs(scope.Hero.HeroId, 40);
        Assert.Equal(83, gauge.Ws.Value);
    }

    /// <summary>
    /// 观察者能读到完整激活/耗尽快照；不同步长不改变持续时间，清零后可再次蓄力。
    /// </summary>
    [Fact]
    public void TimeAndReset_NotifyCommittedStateAndAllowReuse()
    {
        var scope = new HeroTestScope();
        HeroState gauge = scope.Hero;
        scope.System.ResetWs(scope.Hero.HeroId, 100, 1f, 6f);
        scope.System.AddWs(scope.Hero.HeroId, 100);
        int notices = 0;
        bool observedActive = false;
        gauge.WsActive.Register(_ =>
        {
            notices++;
            observedActive = gauge.WsActive.Value;
        });
        Assert.True(scope.System.TryActivateWs(scope.Hero.HeroId));
        Assert.True(observedActive);
        scope.System.AdvanceWs(scope.Hero.HeroId, 6.25f);
        Assert.Equal(57, gauge.Ws.Value);
        scope.System.AdvanceWs(scope.Hero.HeroId, 20f);
        Assert.False(observedActive);
        Assert.Equal(0, gauge.Ws.Value);
        Assert.Equal(2, notices);
        scope.System.AddWs(scope.Hero.HeroId, 100);
        Assert.True(scope.System.TryActivateWs(scope.Hero.HeroId));
        scope.System.ResetWs(scope.Hero.HeroId, 50, 1f, 6f);
        Assert.False(gauge.WsActive.Value);
        Assert.Equal((0, 50), (gauge.Ws.Value, gauge.MaxWs.Value));
        scope.System.AddWs(scope.Hero.HeroId, 50);
        Assert.True(scope.System.TryActivateWs(scope.Hero.HeroId));
        scope.System.ClearWs(scope.Hero.HeroId);
        Assert.False(gauge.WsActive.Value);
        Assert.Equal(0, gauge.Ws.Value);
    }
}
