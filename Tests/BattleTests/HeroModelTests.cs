using GameLogic.Battle;
using GameLogic.Battle.Attributes;
using GameLogic.Battle.Damage;
using System;
using GameConfig.Attributes;
using Xunit;

namespace GameLogic.Battle.Tests
{
    /// <summary>属性汇总、生命魔法资源与无双进度的领域规则。</summary>
    public sealed class HeroModelTests
    {
        /// <summary>结算快照冻结来源和属性；后续 Buff 变更不影响已发出的攻击。</summary>
        [Fact]
        public void Combatant_FreezesActorIdentityAndFinalAttributes()
        {
            var scope = new HeroTestScope();
            var attributes = scope.Hero;
            AttributeSourceId source = new("Buff", 1);
            scope.System.SetSource(source, [AttributeModifier.Flat(AttributeType.Power, 15)]);
            CombatantAttributes snapshot = attributes.CaptureCombatAttributes(-42);
            scope.System.SetSource(source, [AttributeModifier.Flat(AttributeType.Power, 99)]);
            Assert.Equal((-42, CombatSide.Hero, 1, 15),
                (snapshot.ActorId, snapshot.Side, snapshot.Level, snapshot.Power));
            Assert.Equal(99, attributes.Power.Value);
        }

        /// <summary>最终值 = (基础 + Σ固定) × (1 + Σ同类百分比) × Π(1 + 独立百分比)。</summary>
        [Fact]
        public void Attributes_AppliesModifierOrder()
        {
            var scope = new HeroTestScope();
            var sheet = scope.Hero;
            scope.System.SetSource(new AttributeSourceId("Equipment", 1), new[]
            {
                AttributeModifier.Flat(AttributeType.Power, 10),
                AttributeModifier.PercentAdd(AttributeType.Power, 0.1f),
            });
            scope.System.SetSource(new AttributeSourceId("Buff", 1), new[]
            {
                AttributeModifier.PercentAdd(AttributeType.Power, 0.1f),
                AttributeModifier.PercentMult(AttributeType.Power, 0.5f),
            });

            // 基础 0 + 固定 10 = 10；同类百分比 0.1+0.1 → ×1.2 = 12；独立 ×1.5 = 18。
            Assert.Equal(18, sheet.Power.Value);
            Assert.Equal(0, sheet.Def.Value);
        }

        /// <summary>按来源整体移除，同一来源再次登记时整体替换。</summary>
        [Fact]
        public void Attributes_RemovesAndReplacesWholeSources()
        {
            var scope = new HeroTestScope();
            var sheet = scope.Hero;
            AttributeSourceId sword = new("Equipment", 7);
            scope.System.SetSource(sword, new[] { AttributeModifier.Flat(AttributeType.Power, 5), AttributeModifier.Flat(AttributeType.Crit, 2) });
            scope.System.SetSource(sword, new[] { AttributeModifier.Flat(AttributeType.Power, 8) });
            Assert.Equal((8, 0), (sheet.Power.Value, sheet.Crit.Value));

            Assert.True(scope.System.RemoveSource(sword));
            Assert.False(scope.System.RemoveSource(sword));
            Assert.Equal(0, sheet.Power.Value);
        }

        /// <summary>最终值下限为 0，整数读取四舍五入（远离零）。</summary>
        [Fact]
        public void Attributes_ClampsAndRounds()
        {
            var scope = new HeroTestScope();
            var sheet = scope.Hero;
            scope.System.SetSource(new AttributeSourceId("Debuff", 1), new[] { AttributeModifier.Flat(AttributeType.Def, -5) });
            Assert.Equal(0, sheet.Def.Value);

            scope.System.SetSource(new AttributeSourceId("Buff", 2), new[] { AttributeModifier.Flat(AttributeType.Mdef, 2.5f) });
            Assert.Equal(3, sheet.Mdef.Value);
        }

        /// <summary>调用方集合被修改不影响已登记修正，无效来源被拒绝。</summary>
        [Fact]
        public void Attributes_CopiesModifiersAndRejectsInvalidSources()
        {
            var scope = new HeroTestScope();
            var sheet = scope.Hero;
            AttributeModifier[] modifiers = { AttributeModifier.Flat(AttributeType.Power, 4) };
            scope.System.SetSource(new AttributeSourceId("Pellet", 1), modifiers);
            modifiers[0] = AttributeModifier.Flat(AttributeType.Power, 100);
            Assert.Equal(4, sheet.Power.Value);
            scope.System.RemoveSource(new AttributeSourceId("Pellet", 1));
            Assert.Equal(0, sheet.Power.Value);
            Assert.Throws<ArgumentException>(() => new AttributeSourceId("", 1));
            Assert.Throws<ArgumentException>(() => scope.System.SetSource(default, modifiers));
        }

        /// <summary>生命归零只报告一次死亡；归零后不再扣血、也不能治疗复活。</summary>
        [Fact]
        public void Vitals_ReportsDeathExactlyOnce()
        {
            var scope = new HeroTestScope();
            var vitals = scope.Hero;
            int notifications = 0;
            vitals.Hp.Register(_ => notifications++);
            scope.System.SetGrowth(BaseAttributes(100, 30), 1, 1, null, null, refill: true);
            Assert.Equal((100, 100, 30, 30), (vitals.Hp.Value, vitals.MaxHp.Value, vitals.Mp.Value, vitals.MaxMp.Value));

            Assert.False(scope.System.Damage(60));
            Assert.False(scope.System.Damage(0));
            Assert.True(scope.System.Damage(60));
            Assert.False(scope.System.Damage(10));
            scope.System.Heal(50);
            Assert.Equal(0, vitals.Hp.Value);
            Assert.Equal(3, notifications);
        }

        /// <summary>上限变化：补满或钳到新上限；魔法不足时拒绝消耗。</summary>
        [Fact]
        public void Vitals_ClampsOnMaximumChangeAndGuardsMp()
        {
            var scope = new HeroTestScope();
            var vitals = scope.Hero;
            scope.System.SetGrowth(BaseAttributes(100, 30), 1, 1, null, null, refill: true);
            scope.System.Damage(30);
            scope.System.SetGrowth(BaseAttributes(50, 10), 1, 1, null, null, refill: false);
            Assert.Equal((50, 10), (vitals.Hp.Value, vitals.Mp.Value));
            scope.System.SetGrowth(BaseAttributes(130, 65), 1, 1, null, null, refill: true);
            Assert.Equal((130, 65), (vitals.Hp.Value, vitals.Mp.Value));

            Assert.False(scope.System.TrySpendMp(66));
            Assert.True(scope.System.TrySpendMp(60));
            scope.System.RestoreMp(100);
            Assert.Equal(65, vitals.Mp.Value);
            scope.System.SetGrowth(BaseAttributes(0, -1), 1, 1, null, null, refill: true);
            Assert.Equal((1, 0), (vitals.MaxHp.Value, vitals.MaxMp.Value));
        }

        /// <summary>无双进度封顶并在蓄满时才能消耗。</summary>
        [Fact]
        public void Musou_CapsAndConsumesWhenFull()
        {
            var scope = new HeroTestScope();
            var gauge = scope.Hero;
            int notifications = 0;
            gauge.Ws.Register(_ => notifications++);
            scope.System.ResetWs(100, 1f, 6f);
            scope.System.AddWs(60);
            Assert.False(scope.System.TryActivateWs());
            scope.System.AddWs(60);
            Assert.Equal(60, gauge.Ws.Value);
            scope.System.AddWs(40);
            Assert.True(gauge.IsWsFull);
            Assert.Equal(100, gauge.Ws.Value);
            scope.System.ClearWs();
            Assert.Equal(0, gauge.Ws.Value);
            Assert.Equal(3, notifications);
        }

        /// <summary>用生成类型的真实读取契约建立资源测试基础值，不增加生产测试 API。</summary>
        private static ActorBaseAttributes BaseAttributes(int hp, int mp)
        {
            var buffer = new Luban.ByteBuf();
            buffer.WriteInt(hp);
            buffer.WriteInt(mp);
            for (int i = 2; i < 16; i++)
                buffer.WriteInt(0);
            return new ActorBaseAttributes(buffer);
        }

        /// <summary>十六种来源修正均立即发布对应属性；相等最终值不重复通知，移除恢复原值。</summary>
        [Theory]
        [InlineData(AttributeType.MaxHp)]
        [InlineData(AttributeType.MaxMp)]
        [InlineData(AttributeType.Power)]
        [InlineData(AttributeType.Def)]
        [InlineData(AttributeType.Mdef)]
        [InlineData(AttributeType.Crit)]
        [InlineData(AttributeType.Miss)]
        [InlineData(AttributeType.Lucky)]
        [InlineData(AttributeType.Toughness)]
        [InlineData(AttributeType.Htarget)]
        [InlineData(AttributeType.CritReduce)]
        [InlineData(AttributeType.Ar)]
        [InlineData(AttributeType.Sp)]
        [InlineData(AttributeType.Vampirism)]
        [InlineData(AttributeType.HpRegen)]
        [InlineData(AttributeType.MpRegen)]
        public void EveryAttribute_NotifiesActualChanges(AttributeType attribute)
        {
            var scope = new HeroTestScope();
            var model = scope.Hero;
            scope.System.Reset(1, 1, BaseAttributes(100, 30));
            var property = model.GetAttribute(attribute);
            int original = property.Value, count = 0;
            property.Register(value => { count++; Assert.Equal(property.Value, value); });
            var source = new AttributeSourceId("Buff", 9);
            scope.System.SetSource(source, [AttributeModifier.Flat(attribute, 7)]);
            Assert.Equal(original + 7, property.Value);
            scope.System.SetSource(source, [AttributeModifier.Flat(attribute, 7)]);
            Assert.Equal(1, count);
            scope.System.RemoveSource(source);
            Assert.Equal(original, property.Value);
            Assert.Equal(2, count);
        }

        /// <summary>重算后钳制资源到新上限，各绑定只传对应的新值。</summary>
        [Fact]
        public void AttributeRecalculation_ClampsResourcesAndPublishesNewValue()
        {
            var scope = new HeroTestScope();
            var model = scope.Hero;
            scope.System.Reset(1, 1, BaseAttributes(100, 30));
            scope.System.Damage(40);
            int observed = 0;
            model.MaxHp.Register(_ =>
            {
                observed++;
                Assert.Equal(50, model.MaxHp.Value);
            });
            scope.System.SetGrowth(BaseAttributes(50, 10), 1, 1, null, null, refill: false);
            Assert.Equal(1, observed);
            Assert.Equal((50, 10, 10, 50), (model.MaxHp.Value, model.MaxMp.Value, model.Mp.Value, model.Hp.Value));
        }

        /// <summary>致命伤害直接更新生命并关闭无双，重复伤害不再次发布生命变化。</summary>
        [Fact]
        public void Death_DirectDamageClearsMusouAndNotifiesHealthOnce()
        {
            var scope = new HeroTestScope();
            scope.System.Reset(1, 1, BaseAttributes(100, 30));
            scope.System.ResetWs(100, 1f, 6f);
            scope.System.AddWs(100);
            Assert.True(scope.System.TryActivateWs());
            int notifications = 0;
            scope.Hero.Hp.Register(value =>
            {
                notifications++;
                Assert.Equal(0, value);
            });
            Assert.True(scope.System.Damage(100));
            Assert.Equal(0, scope.Hero.Ws.Value);
            Assert.False(scope.Hero.WsActive.Value);
            Assert.False(scope.System.Damage(100));
            scope.System.Heal(100);
            scope.System.AddWs(100);
            Assert.False(scope.System.TryActivateWs());
            Assert.Equal(1, notifications);
        }

        /// <summary>无双重置通过正常赋值通知各自的新值，相同重置不会重复通知。</summary>
        [Fact]
        public void MusouReset_PublishesEachNewValueAndSuppressesEqualReset()
        {
            var scope = new HeroTestScope();
            scope.System.ResetWs(100, 1f, 6f);
            scope.System.AddWs(100);
            Assert.True(scope.System.TryActivateWs());
            int notifications = 0;
            scope.Hero.Ws.Register(value => { notifications++; Assert.Equal(0, value); });
            scope.Hero.MaxWs.Register(value => { notifications++; Assert.Equal(200, value); });
            scope.Hero.WsActive.Register(value => { notifications++; Assert.False(value); });
            scope.System.ResetWs(200, 1f, 6f);
            Assert.Equal(3, notifications);
            Assert.Equal((0, 200, false), (scope.Hero.Ws.Value, scope.Hero.MaxWs.Value, scope.Hero.WsActive.Value));
            scope.System.ResetWs(200, 1f, 6f);
            Assert.Equal(3, notifications);
        }

        /// <summary>系统统一提供无双的伤害和运动查询，结束后恢复普通倍率。</summary>
        [Fact]
        public void MusouBonusQueries_FollowActivationAndEnd()
        {
            var scope = new HeroTestScope();
            var config = BattleArchitecture.Instance.GetModel<GameLogic.Config.ConfigurationModel>().Tables.TbBattleConfig.Data;
            Assert.Equal((1f, 1f, 1f, 1f, false), (scope.System.MoveSpeedMultiplier,
                scope.System.JumpSpeedMultiplier, scope.System.GravityMultiplier, scope.System.AttackPowerMultiplier, scope.System.SuperArmor));
            scope.System.ResetWs(100, 1f, 6f);
            scope.System.AddWs(100);
            Assert.True(scope.System.TryActivateWs());
            Assert.Equal((config.WsMoveSpeedMultiplier, config.WsJumpSpeedMultiplier, config.WsGravityMultiplier,
                config.WsAttackMultiplier, true), (scope.System.MoveSpeedMultiplier, scope.System.JumpSpeedMultiplier,
                scope.System.GravityMultiplier, scope.System.AttackPowerMultiplier, scope.System.SuperArmor));
            scope.System.ClearWs();
            Assert.Equal((1f, 1f, 1f, 1f, false), (scope.System.MoveSpeedMultiplier,
                scope.System.JumpSpeedMultiplier, scope.System.GravityMultiplier, scope.System.AttackPowerMultiplier, scope.System.SuperArmor));
        }
    }
}
