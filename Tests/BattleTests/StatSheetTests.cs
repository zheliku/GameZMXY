using System;
using GameConfig.Stat;
using GameLogic.Battle.Stats;
using Xunit;

namespace GameLogic.Battle.Tests
{
	/// <summary>属性汇总、生命魔法资源与无双进度的领域规则。</summary>
	public sealed class StatSheetTests
	{
		/// <summary>最终值 = (基础 + Σ固定) × (1 + Σ同类百分比) × Π(1 + 独立百分比)。</summary>
		[Fact]
		public void Sheet_AppliesModifierOrder()
		{
			StatSheet sheet = new();
			sheet.SetSource(new StatSource("Equipment", 1), new[]
			{
				StatModifier.Flat(StatType.Power, 10),
				StatModifier.PercentAdd(StatType.Power, 0.1f),
			});
			sheet.SetSource(new StatSource("Buff", 1), new[]
			{
				StatModifier.PercentAdd(StatType.Power, 0.1f),
				StatModifier.PercentMult(StatType.Power, 0.5f),
			});

			// 基础 0 + 固定 10 = 10；同类百分比 0.1+0.1 → ×1.2 = 12；独立 ×1.5 = 18。
			Assert.Equal(18f, sheet.Get(StatType.Power), 3);
			Assert.Equal(0f, sheet.Get(StatType.Def));
		}

		/// <summary>按来源整体移除，同一来源再次登记时整体替换。</summary>
		[Fact]
		public void Sheet_RemovesAndReplacesWholeSources()
		{
			StatSheet sheet = new();
			StatSource sword = new("Equipment", 7);
			sheet.SetSource(sword, new[] { StatModifier.Flat(StatType.Power, 5), StatModifier.Flat(StatType.Crit, 2) });
			sheet.SetSource(sword, new[] { StatModifier.Flat(StatType.Power, 8) });
			Assert.Equal((8, 0), (sheet.GetInt(StatType.Power), sheet.GetInt(StatType.Crit)));

			Assert.True(sheet.RemoveSource(sword));
			Assert.False(sheet.RemoveSource(sword));
			Assert.Equal(0, sheet.GetInt(StatType.Power));
		}

		/// <summary>最终值下限为 0，整数读取四舍五入（远离零）。</summary>
		[Fact]
		public void Sheet_ClampsAndRounds()
		{
			StatSheet sheet = new();
			sheet.SetSource(new StatSource("Debuff", 1), new[] { StatModifier.Flat(StatType.Def, -5) });
			Assert.Equal(0, sheet.GetInt(StatType.Def));

			sheet.SetSource(new StatSource("Buff", 2), new[] { StatModifier.Flat(StatType.Mdef, 2.5f) });
			Assert.Equal(3, sheet.GetInt(StatType.Mdef));
		}

		/// <summary>每次登记、移除都通知；调用方的集合在登记后被修改不影响已登记修正。</summary>
		[Fact]
		public void Sheet_NotifiesAndCopiesModifiers()
		{
			StatSheet sheet = new();
			int notifications = 0;
			sheet.Changed += () => notifications++;
			StatModifier[] modifiers = { StatModifier.Flat(StatType.Power, 4) };
			sheet.SetSource(new StatSource("Pellet", 1), modifiers);
			modifiers[0] = StatModifier.Flat(StatType.Power, 100);
			Assert.Equal(4, sheet.GetInt(StatType.Power));
			sheet.RemoveSource(new StatSource("Pellet", 1));
			Assert.Equal(2, notifications);
			Assert.Throws<ArgumentException>(() => new StatSource("", 1));
			Assert.Throws<ArgumentException>(() => sheet.SetSource(default, modifiers));
		}

		/// <summary>生命归零只报告一次死亡；归零后不再扣血、也不能治疗复活。</summary>
		[Fact]
		public void Vitals_ReportsDeathExactlyOnce()
		{
			Vitals vitals = new();
			int notifications = 0;
			vitals.Changed += () => notifications++;
			vitals.SetMaximums(100, 30, refill: true);
			Assert.Equal((100, 100, 30, 30), (vitals.Hp, vitals.MaxHp, vitals.Mp, vitals.MaxMp));

			Assert.False(vitals.Damage(60));
			Assert.False(vitals.Damage(0));
			Assert.True(vitals.Damage(60));
			Assert.False(vitals.Damage(10));
			vitals.Heal(50);
			Assert.Equal(0, vitals.Hp);
			Assert.Equal(3, notifications);
		}

		/// <summary>上限变化：补满或钳到新上限；魔法不足时拒绝消耗。</summary>
		[Fact]
		public void Vitals_ClampsOnMaximumChangeAndGuardsMp()
		{
			Vitals vitals = new();
			vitals.SetMaximums(100, 30, refill: true);
			vitals.Damage(30);
			vitals.SetMaximums(50, 10, refill: false);
			Assert.Equal((50, 10), (vitals.Hp, vitals.Mp));
			vitals.SetMaximums(130, 65, refill: true);
			Assert.Equal((130, 65), (vitals.Hp, vitals.Mp));

			Assert.False(vitals.TrySpendMp(66));
			Assert.True(vitals.TrySpendMp(60));
			vitals.RestoreMp(100);
			Assert.Equal(65, vitals.Mp);
			vitals.SetMaximums(0, -1, refill: true);
			Assert.Equal((1, 0), (vitals.MaxHp, vitals.MaxMp));
		}

		/// <summary>无双进度封顶并在蓄满时才能消耗。</summary>
		[Fact]
		public void Musou_CapsAndConsumesWhenFull()
		{
			MusouGauge gauge = new();
			int notifications = 0;
			gauge.Changed += () => notifications++;
			gauge.Reset(100);
			gauge.Add(60);
			Assert.False(gauge.TryConsume());
			gauge.Add(60);
			gauge.Add(5);
			Assert.True(gauge.IsFull);
			Assert.Equal(100, gauge.Value);
			Assert.True(gauge.TryConsume());
			Assert.Equal(0, gauge.Value);
			Assert.Equal(4, notifications);
		}
	}
}
