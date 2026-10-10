using System;
using GameLogic.Battle.Stats;
using Xunit;

namespace GameLogic.Battle.Tests;

/// <summary>验证蓄力、激活、计时耗尽和复位的运行期无双契约。</summary>
public sealed class MusouTests
{
	/// <summary>未满不能开启；开启后失去满值提示且不能续充、重开或直接消费。</summary>
	[Fact]
	public void Activation_RequiresFullGaugeAndLocksGains()
	{
		MusouGauge gauge = new();
		gauge.Reset(100);
		gauge.Add(99);
		Assert.False(gauge.TryActivate(12.5f));
		gauge.Add(int.MaxValue);
		Assert.Equal(100, gauge.Value);
		Assert.False(gauge.TryActivate(float.NaN));
		Assert.False(gauge.TryActivate(0f));
		Assert.True(gauge.TryActivate(12.5f));
		Assert.True(gauge.IsActive);
		Assert.False(gauge.IsFull);
		Assert.False(gauge.TryActivate(12.5f));
		Assert.False(gauge.TryConsume());
		gauge.Advance(2.5f);
		gauge.Add(40);
		Assert.Equal(80, gauge.Value);
		Assert.Equal(10f, gauge.RemainingSeconds);
	}

	/// <summary>观察者能读到完整激活/耗尽快照；不同步长不改变持续时间，清零后可再次蓄力。</summary>
	[Fact]
	public void TimeAndReset_NotifyCommittedStateAndAllowReuse()
	{
		MusouGauge gauge = new();
		gauge.Reset(100);
		gauge.Add(100);
		int notices = 0;
		bool observedActive = false;
		gauge.Changed += () => { notices++; observedActive = gauge.IsActive; };
		Assert.True(gauge.TryActivate(12.5f));
		Assert.True(observedActive);
		gauge.Advance(6.25f);
		Assert.Equal(50, gauge.Value);
		gauge.Advance(20f);
		Assert.False(observedActive);
		Assert.Equal(0, gauge.Value);
		Assert.Equal(3, notices);
		gauge.Add(100);
		Assert.True(gauge.TryActivate(12.5f));
		gauge.Reset(50);
		Assert.False(gauge.IsActive);
		Assert.Equal((0, 50, 0f), (gauge.Value, gauge.Max, gauge.RemainingSeconds));
		gauge.Add(50);
		Assert.True(gauge.TryActivate(12.5f));
		gauge.Clear();
		Assert.False(gauge.IsActive);
		Assert.Equal(0, gauge.Value);
	}
}
