using GameLogic.Architecture;
using GameLogic.Equipment;
using GameLogic.Profile;
using GameLogic.Config;
using GameLogic.Battle.Attributes;
using GameLogic.Save.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Hero;
using GameLogic.Save;
using Newtonsoft.Json;
using Xunit;

namespace GameLogic.Profile.Tests
{
    /// <summary>档案领域规则：成长派生、钱包、出战装配、存档格式与映射。</summary>
    public sealed class ProfileTests
    {
        /// <summary>累计经验是唯一事实，升级与跨级都从曲线派生，并只通知一次。</summary>
        [Fact]
        public void Progression_DerivesLevelAndNotifiesOncePerChange()
        {
            ProfileModel progression = new(1, [(1, 0)], TestTables.Curve, 25);
            TestTables.Initialize(progression);
            int notifications = 0;
            progression.Progress.Register(_ => notifications++);

            // 135 经验恰好升到 2 级；再加 145 + 1 跨到 3 级并保留 1 点余量。
            Assert.Equal(1, TestTables.ForProfile(progression).AddExperience(135));
            Assert.Equal((2, 0, 145), (progression.Level, progression.Experience, progression.MaxExperience));
            Assert.Equal(1, TestTables.ForProfile(progression).AddExperience(146));
            Assert.Equal((3, 1, 155), (progression.Level, progression.Experience, progression.MaxExperience));
            Assert.Equal(0, TestTables.ForProfile(progression).AddExperience(0));
            Assert.Equal(2, notifications);

            // 回滚恢复派生值并通知；相同值不通知。
            progression.RestoreExperience(progression.ActiveHeroId, 0);
            progression.RestoreExperience(progression.ActiveHeroId, 0);
            Assert.Equal((1, 0, 135), (progression.Level, progression.Experience, progression.MaxExperience));
            Assert.Equal(3, notifications);
            Assert.Throws<ArgumentOutOfRangeException>(() => TestTables.ForProfile(progression).AddExperience(-1));
        }

        /// <summary>满级后不再增长，也不发通知。</summary>
        [Fact]
        public void Progression_StopsAtMaxLevel()
        {
            ProfileModel progression = new(1, [(1, int.MaxValue)], TestTables.Curve, 25);
            TestTables.Initialize(progression);
            int notifications = 0;
            progression.Progress.Register(_ => notifications++);
            Assert.Equal(TestTables.Curve.MaxLevel, progression.Level);
            Assert.Equal(0, TestTables.ForProfile(progression).AddExperience(1000));
            Assert.Equal(0, notifications);
            Assert.Equal((55, 0, 0), (progression.Level, progression.Experience, progression.MaxExperience));
        }

        /// <summary>钱包不出现负数，不足时拒绝扣除。</summary>
        [Fact]
        public void Wallet_RejectsOverspendAndNegativeAmounts()
        {
            ProfileModel wallet = new(1, [(1, 0)], TestTables.Curve, 25, -5);
            Assert.Equal(0, wallet.Soul.Value);
            TestTables.ForProfile(wallet).AddSoul(30);
            Assert.False(TestTables.ForProfile(wallet).TrySpendSoul(31));
            Assert.True(TestTables.ForProfile(wallet).TrySpendSoul(30));
            Assert.Equal(0, wallet.Soul.Value);
            TestTables.ForProfile(wallet).AddSoul(int.MaxValue);
            TestTables.ForProfile(wallet).AddSoul(10);
            Assert.Equal(int.MaxValue, wallet.Soul.Value);
            Assert.Throws<ArgumentOutOfRangeException>(() => TestTables.ForProfile(wallet).AddSoul(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => TestTables.ForProfile(wallet).TrySpendSoul(-1));
        }

        /// <summary>零额、扣款失败、封顶和回滚都保持有效余额。</summary>
        [Fact]
        public void Wallet_PreservesBalanceAcrossNoOpsAndRestore()
        {
            ProfileModel wallet = new(1, [(1, 0)], TestTables.Curve, 25, 30);
            TestTables.ForProfile(wallet).AddSoul(0);
            Assert.True(TestTables.ForProfile(wallet).TrySpendSoul(0));
            Assert.False(TestTables.ForProfile(wallet).TrySpendSoul(31));
            Assert.Equal(30, wallet.Soul.Value);
            Assert.True(TestTables.ForProfile(wallet).TrySpendSoul(10));
            Assert.Equal(20, wallet.Soul.Value);
            wallet.RestoreSoul(20);
            Assert.Equal(20, wallet.Soul.Value);
            TestTables.ForProfile(wallet).AddSoul(int.MaxValue);
            TestTables.ForProfile(wallet).AddSoul(1);
            Assert.Equal(int.MaxValue, wallet.Soul.Value);
            wallet.RestoreSoul(-1);
            Assert.Equal(0, wallet.Soul.Value);
        }

        /// <summary>档案拒绝重复英雄与未拥有的出战英雄。</summary>
        [Fact]
        public void Profile_RejectsInvalidHeroSets()
        {
            ProfileModel hero = new(1, [(1, 0)], TestTables.Curve, 25);
            TestTables.Initialize(hero);
            Assert.Throws<ArgumentException>(() => new TestGameScope(1, new[] { (1, 0), (1, 0) }, 0, 25));
            Assert.Throws<ArgumentException>(() => new TestGameScope(2, new[] { (1, 0) }, 0, 25));
            Assert.Equal(1, new TestGameScope(1, new[] { (1, 0) }, 0, 25).Profile.ActiveHeroId);
        }

        /// <summary>钱包只在余额实际提交后通知，失败消费、零增量和封顶不重复通知。</summary>
        [Fact]
        public void Wallet_NotifiesOnlyCommittedBalanceChanges()
        {
            ProfileModel wallet = new(1, [(1, 0)], TestTables.Curve, 25, 10);
            int notifications = 0;
            int observed = -1;
            wallet.Soul.Register(_ =>
            {
                notifications++;
                observed = wallet.Soul.Value;
            });
            TestTables.ForProfile(wallet).AddSoul(0);
            Assert.False(TestTables.ForProfile(wallet).TrySpendSoul(11));
            Assert.Equal(0, notifications);
            TestTables.ForProfile(wallet).AddSoul(5);
            Assert.Equal((1, 15), (notifications, observed));
            Assert.True(TestTables.ForProfile(wallet).TrySpendSoul(3));
            Assert.Equal((2, 12), (notifications, observed));
            wallet.RestoreSoul(12);
            Assert.Equal(2, notifications);
            wallet.RestoreSoul(int.MaxValue);
            TestTables.ForProfile(wallet).AddSoul(1);
            Assert.Equal((3, int.MaxValue), (notifications, observed));
        }

        /// <summary>成长改变自动重算绑定属性，局内 Buff 来源保持，升级补满资源。</summary>
        [Fact]
        public void HeroAttributes_FollowsProgressionAndKeepsBuff()
        {
            var scope = new TestGameScope(1, [(1, 0)], 0, 25);
            var model = scope.Hero;
            scope.GetSystem<GameLogic.Battle.HeroSystem>().SetSource(new AttributeSourceId("Buff", 1),
                [AttributeModifier.PercentMult(GameConfig.Attributes.AttributeType.Power, 1)]);
            Assert.Equal(20, model.Power.Value);
            scope.GetSystem<GameLogic.Battle.HeroSystem>().Damage(20);
            scope.GetSystem<ProfileSystem>().AddExperience(140);
            Assert.Equal(30, model.Power.Value);
            Assert.Equal(130, model.MaxHp.Value);
            Assert.Equal(model.MaxHp.Value, model.Hp.Value);
        }

        /// <summary>成长表与官方 Role1.upGrade 公式逐级一致（防止迁移脚本或手工改表引入偏差）。</summary>
        [Fact]
        public void GrowthTable_MatchesOfficialWukongGrowth()
        {
            for (int level = 1; level <= TestTables.Curve.MaxLevel; level++)
            {
                HeroGrowthConfig row = TestTables.Tables.TbHeroGrowthConfig.Get(1, level);
                Assert.NotNull(row);
                int g = level - 1;
                int defense = level < 15 ? 2 + 2 * g : 28 + (level - 14) * 5;
                Assert.Equal((80 + 50 * g, 50 + 20 * g, 10 + 5 * g, defense, 0),
                    (row.BaseAttributes.MaxHp, row.BaseAttributes.MaxMp, row.BaseAttributes.Power, row.BaseAttributes.Def, row.BaseAttributes.Mdef));
            }
        }

        /// <summary>怪物基础值符合官方数据，同一种实体只配置一条基础数值。</summary>
        [Fact]
        public void MonsterTable_MatchesOfficialStatsAndHasOneConfigPerEntity()
        {
            (int Id, int Hp, int Defense, int Xp)[] values = [(1, 50, 0, 5), (2, 100, 1, 6), (3, 300, 5, 7)];
            foreach (var expected in values)
            {
                var monster = TestTables.Tables.TbMonsterConfig.Get(expected.Id);
                Assert.Equal((expected.Hp, expected.Defense, expected.Xp),
                    (monster.BaseAttributes.MaxHp, monster.BaseAttributes.Def, monster.AddExp));
            }
            Assert.All(TestTables.Tables.TbMonsterConfig.DataList.GroupBy(x => x.EntityId), group => Assert.Single(group));
            Assert.Null(TestTables.Tables.TbMonsterConfig.GetOrDefault(4));
        }

        /// <summary>真实配置通过启动校验。</summary>
        [Fact]
        public void ConfigurationModel_AcceptsShippedTables()
        {
            ExperienceCurve curve = GameLogic.Config.ConfigurationSystem.ValidateTables(TestTables.Tables);
            Assert.Equal(55, curve.MaxLevel);
        }

        /// <summary>缺少版本或进度字段的 JSON 不得被初始值误判为当前格式。</summary>
        [Fact]
        public void Archive_RejectsMissingRequiredFields()
        {
            Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<GameData>("{\"UnitId\":7}"));
            Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<GameData>("{\"SaveVersion\":2,\"UnitId\":7}"));
        }

        /// <summary>框架创建的当前格式空槽可以序列化回读，仍未建立角色。</summary>
        [Fact]
        public void Archive_RoundTripsUnclaimedCurrentSlot()
        {
            GameData data = JsonConvert.DeserializeObject<GameData>(JsonConvert.SerializeObject(new GameData { UnitId = 7 }));
            Assert.Equal(GameData.CurrentVersion, data.SaveVersion);
            Assert.Equal(0, data.ActiveHeroId);
            Assert.Empty(data.Heroes);
            Assert.Equal(0, data.Soul);
            Assert.Equal(7, data.UnitId);
        }

        /// <summary>仅接受当前格式的完整档案；不转换其他版本。</summary>
        [Fact]
        public void Mapper_RejectsOtherVersionsAndEmptyHeroes()
        {
            foreach (int version in Enumerable.Range(0, GameData.CurrentVersion).Append(GameData.CurrentVersion + 1))
                Assert.Throws<NotSupportedException>(() => TestGameScope.FromSaveData(new GameData { SaveVersion = version, ActiveHeroId = 1, Heroes = [new HeroSaveData { HeroId = 1 }] }, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25));
            Assert.Throws<ArgumentException>(() => TestGameScope.FromSaveData(new GameData(), TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25));
        }

        /// <summary>档案 → 快照 → JSON → 快照 → 档案往返不丢状态，快照与运行时不共享引用。</summary>
        [Fact]
        public void Mapper_RoundTripsThroughJson()
        {
            TestGameScope playerModel = new(1, new[] { (1, 472) }, 42, 25);
            GameData snapshot = playerModel.CaptureSaveData();
            playerModel.GetSystem<ProfileSystem>().AddSoul(100);
            Assert.Equal(42, snapshot.Soul);

            string json = JsonConvert.SerializeObject(snapshot);
            GameData loaded = JsonConvert.DeserializeObject<GameData>(json);
            TestGameScope restored = TestGameScope.FromSaveData(loaded, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25);
            Assert.Equal(42, restored.Profile.Soul.Value);
            Assert.Equal((4, 37), (restored.Profile.Level, restored.Profile.Experience));
            Assert.DoesNotContain("\"Player\"", json);
            Assert.DoesNotContain("\"Profile\"", json);
            Assert.Contains("\"TotalExperience\"", json);
        }

        /// <summary>回滚把档案恢复到进关快照，保留原对象并通知订阅者。</summary>
        [Fact]
        public void Mapper_RollsBackToSnapshot()
        {
            TestGameScope playerModel = new(1, new[] { (1, 0) }, 5, 25);
            GameData snapshot = playerModel.CaptureSaveData();
            ProfileModel progression = playerModel.Profile;
            List<int> levels = new();
            progression.Progress.Register(_ => levels.Add(progression.Level));

            TestTables.ForProfile(progression).AddExperience(400);
            playerModel.GetSystem<ProfileSystem>().AddSoul(50);
            playerModel.RestoreCheckpoint(snapshot);

            Assert.Same(progression, playerModel.Profile);
            Assert.Equal((1, 5), (progression.Level, playerModel.Profile.Soul.Value));
            Assert.Equal(new[] { 3, 1 }, levels);
        }
    }
}
