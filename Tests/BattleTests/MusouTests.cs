using GameLogic.Battle.Damage;
using GameLogic.Battle.Resources;
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
    public void HitGain_UsesOfficialDamageRatio(int damage, int power, int expected) =>
        Assert.Equal(expected, new CombatSystem(BattleArchitecture.Instance).HitGain(damage, power));

    /// <summary>自然衰减不随命中重置，满值冻结相位，暂停与长帧保持一致。</summary>
    [Fact]
    public void NaturalDecay_PreservesFullGaugeAndDoesNotResetOnHit()
    {
        MusouGauge gauge = new();
        gauge.Reset(100, 1f, 6f);
        gauge.Add(20);
        gauge.Advance(.6f);
        gauge.Add(2);
        gauge.Advance(.4f);
        Assert.Equal(21, gauge.Value);
        gauge.Advance(3);
        Assert.Equal(18, gauge.Value);
        gauge.Advance(0);
        Assert.Equal(18, gauge.Value);
        gauge.Add(82);
        gauge.Advance(60);
        Assert.True(gauge.IsFull);
        Assert.Equal(100, gauge.Value);
        gauge.Clear();
        gauge.Add(8);
        gauge.Advance(8);
        Assert.Equal(0, gauge.Value);
    }

    /// <summary>命中在开启时不积累，黄球仍可补充；超过上限的整颗黄球不增加。</summary>
    [Fact]
    public void Pickup_CanRefillActiveGauge_WithoutOverflow()
    {
        MusouGauge gauge = new();
        gauge.Reset(100, 1f, 6f);
        gauge.Add(100);
        Assert.True(gauge.TryActivate());
        gauge.Advance(2);
        int value = gauge.Value;
        gauge.Add(5);
        Assert.Equal(value, gauge.Value);
        gauge.AddFromPickup(5);
        Assert.Equal(value + 5, gauge.Value);
        gauge.AddFromPickup(100);
        Assert.Equal(value + 5, gauge.Value);
        Assert.True(gauge.IsActive);
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
        MusouGauge whole = new(), split = new();
        foreach (var gauge in new[] { whole, split })
        {
            gauge.Reset(100, 1f, 6f);
            gauge.Add(100);
            Assert.True(gauge.TryActivate());
        }
        whole.Advance(5);
        for (int i = 0; i < 5 * updatesPerSecond; i++)
            split.Advance(1f / updatesPerSecond);
        Assert.Equal(66, whole.Value);
        Assert.Equal(whole.Value, split.Value);
        whole.Advance(20);
        split.Advance(20);
        Assert.False(whole.IsActive);
        Assert.False(split.IsActive);
    }
    /// <summary>未满不能开启；开启后失去满值提示且命中不续充、不能重开。</summary>
    [Fact]
    public void Activation_RequiresFullGaugeAndLocksGains()
    {
        MusouGauge gauge = new();
        gauge.Reset(100, 1f, 6f);
        gauge.Add(99);
        Assert.False(gauge.TryActivate());
        gauge.Add(1);
        Assert.Equal(100, gauge.Value);
        Assert.True(gauge.TryActivate());
        Assert.True(gauge.IsActive);
        Assert.False(gauge.IsFull);
        Assert.False(gauge.TryActivate());
        gauge.Advance(2.5f);
        gauge.Add(40);
        Assert.Equal(83, gauge.Value);
    }

    /// <summary>观察者能读到完整激活/耗尽快照；不同步长不改变持续时间，清零后可再次蓄力。</summary>
    [Fact]
    public void TimeAndReset_NotifyCommittedStateAndAllowReuse()
    {
        MusouGauge gauge = new();
        gauge.Reset(100, 1f, 6f);
        gauge.Add(100);
        int notices = 0;
        bool observedActive = false;
        gauge.Changed += () =>
        {
            notices++;
            observedActive = gauge.IsActive;
        };
        Assert.True(gauge.TryActivate());
        Assert.True(observedActive);
        gauge.Advance(6.25f);
        Assert.Equal(57, gauge.Value);
        gauge.Advance(20f);
        Assert.False(observedActive);
        Assert.Equal(0, gauge.Value);
        Assert.Equal(3, notices);
        gauge.Add(100);
        Assert.True(gauge.TryActivate());
        gauge.Reset(50, 1f, 6f);
        Assert.False(gauge.IsActive);
        Assert.Equal((0, 50), (gauge.Value, gauge.Max));
        gauge.Add(50);
        Assert.True(gauge.TryActivate());
        gauge.Clear();
        Assert.False(gauge.IsActive);
        Assert.Equal(0, gauge.Value);
    }
}
