using GameLogic.Battle.Resources;
using GameLogic.Battle.Attributes;
using GameLogic.Battle.Damage;
using System;
using GameConfig.Attributes;
using Xunit;

namespace GameLogic.Battle.Tests
{
    /// <summary>属性汇总、生命魔法资源与无双进度的领域规则。</summary>
    public sealed class ActorAttributesTests
    {
        /// <summary>结算快照冻结来源和属性；后续 Buff 变更不影响已发出的攻击。</summary>
        [Fact]
        public void Combatant_FreezesActorIdentityAndFinalAttributes()
        {
            ActorAttributes attributes = new();
            AttributeSourceId source = new("Buff", 1);
            attributes.SetSource(source, [AttributeModifier.Flat(AttributeType.Power, 15)]);
            CombatantAttributes snapshot = CombatantAttributes.From(CombatSide.Hero, 2, attributes, -42);
            attributes.SetSource(source, [AttributeModifier.Flat(AttributeType.Power, 99)]);
            Assert.Equal((-42, CombatSide.Hero, 2, 15),
                (snapshot.ActorId, snapshot.Side, snapshot.Level, snapshot.Power));
            Assert.Equal(99, attributes.GetInt(AttributeType.Power));
        }

        /// <summary>最终值 = (基础 + Σ固定) × (1 + Σ同类百分比) × Π(1 + 独立百分比)。</summary>
        [Fact]
        public void Attributes_AppliesModifierOrder()
        {
            ActorAttributes sheet = new();
            sheet.SetSource(new AttributeSourceId("Equipment", 1), new[]
            {
                AttributeModifier.Flat(AttributeType.Power, 10),
                AttributeModifier.PercentAdd(AttributeType.Power, 0.1f),
            });
            sheet.SetSource(new AttributeSourceId("Buff", 1), new[]
            {
                AttributeModifier.PercentAdd(AttributeType.Power, 0.1f),
                AttributeModifier.PercentMult(AttributeType.Power, 0.5f),
            });

            // 基础 0 + 固定 10 = 10；同类百分比 0.1+0.1 → ×1.2 = 12；独立 ×1.5 = 18。
            Assert.Equal(18f, sheet.Get(AttributeType.Power), 3);
            Assert.Equal(0f, sheet.Get(AttributeType.Def));
        }

        /// <summary>按来源整体移除，同一来源再次登记时整体替换。</summary>
        [Fact]
        public void Attributes_RemovesAndReplacesWholeSources()
        {
            ActorAttributes sheet = new();
            AttributeSourceId sword = new("Equipment", 7);
            sheet.SetSource(sword, new[] { AttributeModifier.Flat(AttributeType.Power, 5), AttributeModifier.Flat(AttributeType.Crit, 2) });
            sheet.SetSource(sword, new[] { AttributeModifier.Flat(AttributeType.Power, 8) });
            Assert.Equal((8, 0), (sheet.GetInt(AttributeType.Power), sheet.GetInt(AttributeType.Crit)));

            Assert.True(sheet.RemoveSource(sword));
            Assert.False(sheet.RemoveSource(sword));
            Assert.Equal(0, sheet.GetInt(AttributeType.Power));
        }

        /// <summary>最终值下限为 0，整数读取四舍五入（远离零）。</summary>
        [Fact]
        public void Attributes_ClampsAndRounds()
        {
            ActorAttributes sheet = new();
            sheet.SetSource(new AttributeSourceId("Debuff", 1), new[] { AttributeModifier.Flat(AttributeType.Def, -5) });
            Assert.Equal(0, sheet.GetInt(AttributeType.Def));

            sheet.SetSource(new AttributeSourceId("Buff", 2), new[] { AttributeModifier.Flat(AttributeType.Mdef, 2.5f) });
            Assert.Equal(3, sheet.GetInt(AttributeType.Mdef));
        }

        /// <summary>调用方集合被修改不影响已登记修正，无效来源被拒绝。</summary>
        [Fact]
        public void Attributes_CopiesModifiersAndRejectsInvalidSources()
        {
            ActorAttributes sheet = new();
            AttributeModifier[] modifiers = { AttributeModifier.Flat(AttributeType.Power, 4) };
            sheet.SetSource(new AttributeSourceId("Pellet", 1), modifiers);
            modifiers[0] = AttributeModifier.Flat(AttributeType.Power, 100);
            Assert.Equal(4, sheet.GetInt(AttributeType.Power));
            sheet.RemoveSource(new AttributeSourceId("Pellet", 1));
            Assert.Equal(0, sheet.GetInt(AttributeType.Power));
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
            gauge.Reset(100, 1f, 6f);
            gauge.Add(60);
            Assert.False(gauge.TryActivate());
            gauge.Add(60);
            Assert.Equal(60, gauge.Value);
            gauge.Add(40);
            Assert.True(gauge.IsFull);
            Assert.Equal(100, gauge.Value);
            gauge.Clear();
            Assert.Equal(0, gauge.Value);
            Assert.Equal(4, notifications);
        }
    }
}
