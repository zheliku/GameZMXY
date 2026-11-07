using System;
using GameConfig.Loot;
using GameLogic.Battle;
using GameLogic.Drops;
using GameLogic.Level;
using Xunit;

namespace GameLogic.Profile.Tests
{
    /// <summary>
    /// 关卡系统直接验证拾取效果和会话资格，场景不再拥有这部分规则。
    /// </summary>
    public sealed class LevelRewardTests
    {
        /// <summary>
        /// 三种恢复品按真实配置恢复并钳制上限，满值仍可拾取。
        /// </summary>
        /// <param name="id">恢复品定义。</param>
        [Theory]
        [InlineData(EPickupId.SmallPeach)]
        [InlineData(EPickupId.LargePeach)]
        [InlineData(EPickupId.ManaBottle)]
        public void Pickup_UsesConfiguredEffectAndConsumesAtMaximum(EPickupId id)
        {
            var scope = Ready();
            var system = scope.GetSystem<LevelSystem>();
            var pickup = TestTables.Tables.TbPickupConfig.Get(id);
            scope.Hero.Hp.Value = 1;
            scope.Hero.Mp.Value = 0;
            Assert.True(system.TryCollectPickup(id));
            Assert.Equal(pickup.Effect == EPickupEffect.Health
                ? Math.Min(scope.Hero.MaxHp.Value, 1 + pickup.RestoreAmount) : 1, scope.Hero.Hp.Value);
            Assert.Equal(pickup.Effect == EPickupEffect.Mana
                ? Math.Min(scope.Hero.MaxMp.Value, pickup.RestoreAmount) : 0, scope.Hero.Mp.Value);
            scope.GetSystem<HeroSystem>().Heal(int.MaxValue);
            scope.GetSystem<HeroSystem>().RestoreMp(int.MaxValue);
            Assert.True(system.TryCollectPickup(id));
            Assert.False(system.TryCollectPickup(EPickupId.None));
            Assert.False(system.TryCollectPickup((EPickupId)999));
        }

        /// <summary>
        /// 球体沿用生成时的恢复量，飞行途中升级不重新计算；不会串改其他奖励。
        /// </summary>
        [Fact]
        public void Orb_PreservesSpawnRewardAcrossLevelUpAndClampsRecovery()
        {
            var scope = Ready();
            var system = scope.GetSystem<LevelSystem>();
            scope.Hero.Mp.Value = 0;
            system.CollectLoot(new(ELootKind.Soul, 11));
            system.CollectLoot(new(ELootKind.Musou, 5));
            Assert.Equal(11, scope.Profile.Soul.Value);
            Assert.Equal(5, scope.Hero.Ws.Value);
            var loot = scope.GetSystem<LootSystem>().Roll(TestTables.Tables.TbEnemyConfig.DataList[0], 0,
                scope.Hero.MaxHp.Value, scope.Hero.MaxMp.Value, () => 0f);
            var health = loot.Find(x => x.Kind == ELootKind.Health);
            var mana = loot.Find(x => x.Kind == ELootKind.Mana);
            scope.GetSystem<ProfileSystem>().AddExperience(int.MaxValue);
            Assert.True(scope.GetSystem<LootSystem>().HealthAmount(scope.Hero.MaxHp.Value) > health.Amount);
            Assert.True(scope.GetSystem<LootSystem>().ManaAmount(scope.Hero.MaxMp.Value) > mana.Amount);
            scope.Hero.Hp.Value = 1;
            scope.Hero.Mp.Value = 0;
            system.CollectLoot(health);
            system.CollectLoot(mana);
            Assert.Equal(1 + health.Amount, scope.Hero.Hp.Value);
            Assert.Equal(mana.Amount, scope.Hero.Mp.Value);
            scope.Hero.Hp.Value = scope.Hero.MaxHp.Value - 1;
            system.CollectLoot(health);
            Assert.Equal(scope.Hero.MaxHp.Value, scope.Hero.Hp.Value);
            system.CollectLoot(health);
            Assert.Equal(scope.Hero.MaxHp.Value, scope.Hero.Hp.Value);
            Assert.Equal(11, scope.Profile.Soul.Value);
            Assert.Equal(5, scope.Hero.Ws.Value);
            Assert.Throws<ArgumentOutOfRangeException>(() => system.CollectLoot(new((ELootKind)999, 1)));
        }

        /// <summary>
        /// 未开始、已结束、已停止或死亡时，恢复品与所有球体都不得修改数据。
        /// </summary>
        /// <param name="state">0 未开始、1 结局、2 停止、3 已死亡但尚未发布结局。</param>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void InactiveRun_RejectsEveryRecoveryAndReward(int state)
        {
            var scope = state == 0 ? new TestGameScope(1, [(1, 0)], 0, 25) : Ready();
            var system = scope.GetSystem<LevelSystem>();
            if (state == 1)
            {
                system.Complete(ELevelOutcome.Defeated, 0);
            }
            else if (state == 2)
            {
                system.EndSession();
            }
            else if (state == 3)
            {
                scope.GetSystem<HeroSystem>().Damage(int.MaxValue);
            }

            int hp = scope.Hero.Hp.Value;
            scope.Hero.Mp.Value = 0;
            Assert.False(system.TryCollectPickup(EPickupId.LargePeach));
            Assert.False(system.TryCollectPickup(EPickupId.ManaBottle));
            foreach (var kind in Enum.GetValues<ELootKind>())
            {
                system.CollectLoot(new(kind, 5));
            }

            Assert.Equal(hp, scope.Hero.Hp.Value);
            Assert.Equal(0, scope.Hero.Mp.Value);
            Assert.Equal(0, scope.Hero.Ws.Value);
            Assert.Equal(0, scope.Profile.Soul.Value);
        }

        /// <summary>
        /// 灵魂属于关卡事务，放弃回滚；不把未领取奖励保存到档案。
        /// </summary>
        [Fact]
        public void Abandon_RollsBackCollectedSoul()
        {
            var scope = Ready();
            var system = scope.GetSystem<LevelSystem>();
            system.CollectLoot(new(ELootKind.Soul, 17));
            Assert.Equal(17, scope.Profile.Soul.Value);
            system.EndSession();
            Assert.Equal(0, scope.Profile.Soul.Value);
        }

        /// <summary>
        /// 建立真实系统的已开始关卡。
        /// </summary>
        /// <returns>独立测试作用域。</returns>
        private static TestGameScope Ready()
        {
            var scope = new TestGameScope(1, [(1, 0)], 0, 25);
            scope.GetSystem<LevelSystem>().Prepare(1);
            scope.GetSystem<LevelSystem>().StartSession(1);
            return scope;
        }
    }
}
