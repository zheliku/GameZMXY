using GameLogic.Battle.Damage;
using GameLogic.Battle;
using System;
using Xunit;

namespace GameLogic.Battle.Tests;

/// <summary>验证蓄力、激活、计时耗尽和复位的运行期无双契约。</summary>
public sealed class MusouTests
{
    /// <summary>原版伤害比例四舍五入，暴击伤害可增加蓄力，零威力不产生无穷奖励。</summary>
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
        scope.System.ResetWs(100, 1f, 6f);
        scope.System.RecordHit(damage, power);
        Assert.Equal(expected, scope.Hero.Ws.Value);
    }

    /// <summary>自然衰减不随命中重置，满值冻结相位，暂停与长帧保持一致。</summary>
    [Fact]
    public void NaturalDecay_PreservesFullGaugeAndDoesNotResetOnHit()
    {
        var scope = new HeroTestScope();
        var gauge = scope.Hero;
        scope.System.ResetWs(100, 1f, 6f);
        scope.System.AddWs(20);
        scope.System.AdvanceWs(.6f);
        scope.System.AddWs(2);
        scope.System.AdvanceWs(.4f);
        Assert.Equal(21, gauge.Ws.Value);
        scope.System.AdvanceWs(3);
        Assert.Equal(18, gauge.Ws.Value);
        scope.System.AdvanceWs(0);
        Assert.Equal(18, gauge.Ws.Value);
        scope.System.AddWs(82);
        scope.System.AdvanceWs(60);
        Assert.True(gauge.IsWsFull);
        Assert.Equal(100, gauge.Ws.Value);
        scope.System.ClearWs();
        scope.System.AddWs(8);
        scope.System.AdvanceWs(8);
        Assert.Equal(0, gauge.Ws.Value);
    }

    /// <summary>命中在开启时不积累，黄球仍可补充；超过上限的整颗黄球不增加。</summary>
    [Fact]
    public void Pickup_CanRefillActiveGauge_WithoutOverflow()
    {
        var scope = new HeroTestScope();
        var gauge = scope.Hero;
        scope.System.ResetWs(100, 1f, 6f);
        scope.System.AddWs(100);
        Assert.True(scope.System.TryActivateWs());
        scope.System.AdvanceWs(2);
        int value = gauge.Ws.Value;
        scope.System.AddWs(5);
        Assert.Equal(value, gauge.Ws.Value);
        scope.System.AddWsFromPickup(5);
        Assert.Equal(value + 5, gauge.Ws.Value);
        scope.System.AddWsFromPickup(100);
        Assert.Equal(value + 5, gauge.Ws.Value);
        Assert.True(gauge.WsActive.Value);
    }

    /// <summary>开启后六次/秒扣点与自然扣点叠加，分帧和长帧得到相同结果。</summary>
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
        var whole = wholeScope.Hero;
        var split = splitScope.Hero;
        foreach (var scope in new[] { wholeScope, splitScope })
        {
            scope.System.ResetWs(100, 1f, 6f);
            scope.System.AddWs(100);
            Assert.True(scope.System.TryActivateWs());
        }
        wholeScope.System.AdvanceWs(5);
        for (int i = 0; i < 5 * updatesPerSecond; i++)
            splitScope.System.AdvanceWs(1f / updatesPerSecond);
        Assert.Equal(66, whole.Ws.Value);
        Assert.Equal(whole.Ws.Value, split.Ws.Value);
        wholeScope.System.AdvanceWs(20);
        splitScope.System.AdvanceWs(20);
        Assert.False(whole.WsActive.Value);
        Assert.False(split.WsActive.Value);
    }

    /// <summary>未满不能开启；开启后失去满值提示且命中不续充、不能重开。</summary>
    [Fact]
    public void Activation_RequiresFullGaugeAndLocksGains()
    {
        var scope = new HeroTestScope();
        var gauge = scope.Hero;
        scope.System.ResetWs(100, 1f, 6f);
        scope.System.AddWs(99);
        Assert.False(scope.System.TryActivateWs());
        scope.System.AddWs(1);
        Assert.Equal(100, gauge.Ws.Value);
        Assert.True(scope.System.TryActivateWs());
        Assert.True(gauge.WsActive.Value);
        Assert.False(gauge.IsWsFull);
        Assert.False(scope.System.TryActivateWs());
        scope.System.AdvanceWs(2.5f);
        scope.System.AddWs(40);
        Assert.Equal(83, gauge.Ws.Value);
    }

    /// <summary>观察者能读到完整激活/耗尽快照；不同步长不改变持续时间，清零后可再次蓄力。</summary>
    [Fact]
    public void TimeAndReset_NotifyCommittedStateAndAllowReuse()
    {
        var scope = new HeroTestScope();
        var gauge = scope.Hero;
        scope.System.ResetWs(100, 1f, 6f);
        scope.System.AddWs(100);
        int notices = 0;
        bool observedActive = false;
        gauge.WsActive.Register(_ =>
        {
            notices++;
            observedActive = gauge.WsActive.Value;
        });
        Assert.True(scope.System.TryActivateWs());
        Assert.True(observedActive);
        scope.System.AdvanceWs(6.25f);
        Assert.Equal(57, gauge.Ws.Value);
        scope.System.AdvanceWs(20f);
        Assert.False(observedActive);
        Assert.Equal(0, gauge.Ws.Value);
        Assert.Equal(2, notices);
        scope.System.AddWs(100);
        Assert.True(scope.System.TryActivateWs());
        scope.System.ResetWs(50, 1f, 6f);
        Assert.False(gauge.WsActive.Value);
        Assert.Equal((0, 50), (gauge.Ws.Value, gauge.MaxWs.Value));
        scope.System.AddWs(50);
        Assert.True(scope.System.TryActivateWs());
        scope.System.ClearWs();
        Assert.False(gauge.WsActive.Value);
        Assert.Equal(0, gauge.Ws.Value);
    }
}
