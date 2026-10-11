using GameLogic.Battle.Resources;
using GameLogic.Battle.Attributes;
using System;
using GameConfig.Stat;
using Xunit;

namespace GameLogic.Battle.Tests
{
	/// <summary>属性汇总、生命魔法资源与无双进度的领域规则。</summary>
	public sealed class ActorAttributesTests
	{
		/// <summary>最终值 = (基础 + Σ固定) × (1 + Σ同类百分比) × Π(1 + 独立百分比)。</summary>
		[Fact]
		public void Attributes_AppliesModifierOrder()
		{
			ActorAttributes sheet = new();
			sheet.SetSource(new AttributeSourceId("Equipment", 1), new[]
			{
				AttributeModifier.Flat(StatType.Power, 10),
				AttributeModifier.PercentAdd(StatType.Power, 0.1f),
			});
			sheet.SetSource(new AttributeSourceId("Buff", 1), new[]
			{
				AttributeModifier.PercentAdd(StatType.Power, 0.1f),
				AttributeModifier.PercentMult(StatType.Power, 0.5f),
			});

			// 基础 0 + 固定 10 = 10；同类百分比 0.1+0.1 → ×1.2 = 12；独立 ×1.5 = 18。
			Assert.Equal(18f, sheet.Get(StatType.Power), 3);
			Assert.Equal(0f, sheet.Get(StatType.Def));
		}

		/// <summary>按来源整体移除，同一来源再次登记时整体替换。</summary>
		[Fact]
		public void Attributes_RemovesAndReplacesWholeSources()
		{
			ActorAttributes sheet = new();
			AttributeSourceId sword = new("Equipment", 7);
			sheet.SetSource(sword, new[] { AttributeModifier.Flat(StatType.Power, 5), AttributeModifier.Flat(StatType.Crit, 2) });
			sheet.SetSource(sword, new[] { AttributeModifier.Flat(StatType.Power, 8) });
			Assert.Equal((8, 0), (sheet.GetInt(StatType.Power), sheet.GetInt(StatType.Crit)));

			Assert.True(sheet.RemoveSource(sword));
			Assert.False(sheet.RemoveSource(sword));
			Assert.Equal(0, sheet.GetInt(StatType.Power));
		}

		/// <summary>最终值下限为 0，整数读取四舍五入（远离零）。</summary>
		[Fact]
		public void Attributes_ClampsAndRounds()
		{
			ActorAttributes sheet = new();
			sheet.SetSource(new AttributeSourceId("Debuff", 1), new[] { AttributeModifier.Flat(StatType.Def, -5) });
			Assert.Equal(0, sheet.GetInt(StatType.Def));

			sheet.SetSource(new AttributeSourceId("Buff", 2), new[] { AttributeModifier.Flat(StatType.Mdef, 2.5f) });
			Assert.Equal(3, sheet.GetInt(StatType.Mdef));
		}

		/// <summary>调用方集合被修改不影响已登记修正，无效来源被拒绝。</summary>
		[Fact]
		public void Attributes_CopiesModifiersAndRejectsInvalidSources()
		{
			ActorAttributes sheet = new();
			AttributeModifier[] modifiers = { AttributeModifier.Flat(StatType.Power, 4) };
			sheet.SetSource(new AttributeSourceId("Pellet", 1), modifiers);
			modifiers[0] = AttributeModifier.Flat(StatType.Power, 100);
			Assert.Equal(4, sheet.GetInt(StatType.Power));
			sheet.RemoveSource(new AttributeSourceId("Pellet", 1));
			Assert.Equal(0, sheet.GetInt(StatType.Power));
			Assert.Throws<ArgumentException>(() => new AttributeSourceId("", 1));
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
