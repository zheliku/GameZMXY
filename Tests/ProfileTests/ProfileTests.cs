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
			HeroProgression progression = new(1, TestTables.Curve, 0);
			int notifications = 0;
			progression.Changed += () => notifications++;

			// 135 经验恰好升到 2 级；再加 145 + 1 跨到 3 级并保留 1 点余量。
			Assert.Equal(1, progression.AddExperience(135));
			Assert.Equal((2, 0, 145), (progression.Level, progression.Experience, progression.MaxExperience));
			Assert.Equal(1, progression.AddExperience(146));
			Assert.Equal((3, 1, 155), (progression.Level, progression.Experience, progression.MaxExperience));
			Assert.Equal(0, progression.AddExperience(0));
			Assert.Equal(2, notifications);

			// 回滚恢复派生值并通知；相同值不通知。
			progression.Restore(0);
			progression.Restore(0);
			Assert.Equal((1, 0, 135), (progression.Level, progression.Experience, progression.MaxExperience));
			Assert.Equal(3, notifications);
			Assert.Throws<ArgumentOutOfRangeException>(() => progression.AddExperience(-1));
		}

		/// <summary>满级后不再增长，也不发通知。</summary>
		[Fact]
		public void Progression_StopsAtMaxLevel()
		{
			HeroProgression progression = new(1, TestTables.Curve, int.MaxValue);
			int notifications = 0;
			progression.Changed += () => notifications++;
			Assert.Equal(TestTables.Curve.MaxLevel, progression.Level);
			Assert.Equal(0, progression.AddExperience(1000));
			Assert.Equal(0, notifications);
			Assert.Equal((55, 0, 0), (progression.Level, progression.Experience, progression.MaxExperience));
		}

		/// <summary>钱包不出现负数，不足时拒绝扣除。</summary>
		[Fact]
		public void Wallet_RejectsOverspendAndNegativeAmounts()
		{
			Wallet wallet = new(-5);
			Assert.Equal(0, wallet.Gold);
			wallet.AddGold(30);
			Assert.False(wallet.TrySpendGold(31));
			Assert.True(wallet.TrySpendGold(30));
			Assert.Equal(0, wallet.Gold);
			wallet.AddGold(int.MaxValue);
			wallet.AddGold(10);
			Assert.Equal(int.MaxValue, wallet.Gold);
			Assert.Throws<ArgumentOutOfRangeException>(() => wallet.AddGold(-1));
			Assert.Throws<ArgumentOutOfRangeException>(() => wallet.TrySpendGold(-1));
		}

        /// <summary>零额、扣款失败、封顶和回滚都保持有效余额。</summary>
        [Fact]
        public void Wallet_PreservesBalanceAcrossNoOpsAndRestore()
        {
            Wallet wallet = new(30);
            wallet.AddGold(0);
            Assert.True(wallet.TrySpendGold(0));
            Assert.False(wallet.TrySpendGold(31));
            Assert.Equal(30, wallet.Gold);
            Assert.True(wallet.TrySpendGold(10));
            Assert.Equal(20, wallet.Gold);
            wallet.Restore(20);
            Assert.Equal(20, wallet.Gold);
            wallet.AddGold(int.MaxValue);
            wallet.AddGold(1);
            Assert.Equal(int.MaxValue, wallet.Gold);
            wallet.Restore(-1);
            Assert.Equal(0, wallet.Gold);
        }

		/// <summary>档案拒绝重复英雄与未拥有的出战英雄。</summary>
		[Fact]
		public void Profile_RejectsInvalidHeroSets()
		{
			HeroProgression hero = new(1, TestTables.Curve, 0);
			Assert.Throws<ArgumentException>(() => new PlayerProfile(1, new[] { hero, hero }, new Wallet(0)));
			Assert.Throws<ArgumentException>(() => new PlayerProfile(2, new[] { hero }, new Wallet(0)));
			Assert.Equal(1, new PlayerProfile(1, new[] { hero }, new Wallet(0)).ActiveHero.HeroId);
		}

        /// <summary>战斗快照冻结当前等级，升级时只替换基础值并保留本局修正。</summary>
        [Fact]
        public void BattleSnapshot_FreezesGrowthAndPreservesRuntimeModifiers()
        {
            HeroProgression hero = new(1, TestTables.Curve, 0);
            HeroLoadout first = HeroLoadout.Create(hero.HeroId, hero.Level, TestTables.Tables.TbHeroGrowthConfig);
            hero.AddExperience(135);
            HeroLoadout second = HeroLoadout.Create(hero.HeroId, hero.Level, TestTables.Tables.TbHeroGrowthConfig);
            Assert.Equal((1, 80, 50, 10), (first.Level, first.BaseAttributes.MaxHp, first.BaseAttributes.MaxMp, first.BaseAttributes.Power));
            Assert.Equal((2, 130, 70, 15, 4), (second.Level, second.BaseAttributes.MaxHp, second.BaseAttributes.MaxMp,
                second.BaseAttributes.Power, second.BaseAttributes.Def));

            ActorAttributes attributes = new();
            attributes.SetSource(new AttributeSourceId("Musou", 0),
                [AttributeModifier.PercentMult(GameConfig.Stat.StatType.Power, 1)]);
            attributes.SetBase(first.BaseAttributes);
            Assert.Equal(20, attributes.GetInt(GameConfig.Stat.StatType.Power));
            attributes.SetBase(second.BaseAttributes);
            Assert.Equal(30, attributes.GetInt(GameConfig.Stat.StatType.Power));
            Assert.Equal(130, attributes.GetInt(GameConfig.Stat.StatType.MaxHp));
        }

        /// <summary>缺少成长行时明确拒绝，避免默认数值进入实体。</summary>
        [Fact]
        public void BattleSnapshot_RejectsMissingGrowth()
        {
            Assert.Throws<InvalidOperationException>(() => HeroLoadout.Create(999, 1, TestTables.Tables.TbHeroGrowthConfig));
            Assert.Throws<ArgumentNullException>(() => new HeroLoadout(1, 1, null));
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
				Assert.Equal((80 + 50 * g, 50 + 20 * g, 10 + 5 * g, defense, defense),
					(row.Stats.MaxHp, row.Stats.MaxMp, row.Stats.Power, row.Stats.Def, row.Stats.Mdef));
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
					(monster.Stats.MaxHp, monster.Stats.Def, monster.AddExp));
			}
			Assert.All(TestTables.Tables.TbMonsterConfig.DataList.GroupBy(x => x.EntityId), group => Assert.Single(group));
			Assert.Null(TestTables.Tables.TbMonsterConfig.GetOrDefault(4));
		}

		/// <summary>真实配置通过启动校验。</summary>
		[Fact]
		public void ConfigHelper_AcceptsShippedTables()
		{
			ExperienceCurve curve = GameLogic.Config.ConfigHelper.ValidateAll(TestTables.Tables);
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
            Assert.Equal(0, data.Gold);
            Assert.Equal(7, data.UnitId);
        }

        /// <summary>仅接受当前格式的完整档案；不转换其他版本。</summary>
        [Fact]
        public void Mapper_RejectsOtherVersionsAndEmptyHeroes()
        {

            foreach (int version in new[] { 0, 1, 2, GameData.CurrentVersion + 1 })
                Assert.Throws<NotSupportedException>(() => ProfileSaveHelper.RestoreProfile(new GameData { SaveVersion = version, ActiveHeroId = 1, Heroes = [new HeroSaveData { HeroId = 1 }] }, TestTables.Curve));
            Assert.Throws<ArgumentException>(() => ProfileSaveHelper.RestoreProfile(new GameData(), TestTables.Curve));
        }

		/// <summary>档案 → 快照 → JSON → 快照 → 档案往返不丢状态，快照与运行时不共享引用。</summary>
		[Fact]
		public void Mapper_RoundTripsThroughJson()
		{
			PlayerProfile profile = new(1, new[] { new HeroProgression(1, TestTables.Curve, 472) }, new Wallet(42));
			GameData snapshot = ProfileSaveHelper.CaptureSaveData(profile);
			profile.Wallet.AddGold(100);
			Assert.Equal(42, snapshot.Gold);

			string json = JsonConvert.SerializeObject(snapshot);
			GameData loaded = JsonConvert.DeserializeObject<GameData>(json);
			PlayerProfile restored = ProfileSaveHelper.RestoreProfile(loaded, TestTables.Curve);
			Assert.Equal(42, restored.Wallet.Gold);
			Assert.Equal((4, 37), (restored.ActiveHero.Level, restored.ActiveHero.Experience));
			Assert.DoesNotContain("\"Player\"", json);
            Assert.DoesNotContain("\"Profile\"", json);
            Assert.Contains("\"TotalExperience\"", json);
		}

		/// <summary>回滚把档案恢复到进关快照，保留原对象并通知订阅者。</summary>
		[Fact]
		public void Mapper_RollsBackToSnapshot()
		{
			PlayerProfile profile = new(1, new[] { new HeroProgression(1, TestTables.Curve, 0) }, new Wallet(5));
			GameData snapshot = ProfileSaveHelper.CaptureSaveData(profile);
			HeroProgression progression = profile.ActiveHero;
			List<int> levels = new();
			progression.Changed += () => levels.Add(progression.Level);

			progression.AddExperience(400);
			profile.Wallet.AddGold(50);
			ProfileSaveHelper.RestoreCheckpoint(profile, snapshot);

			Assert.Same(progression, profile.ActiveHero);
			Assert.Equal((1, 5), (progression.Level, profile.Wallet.Gold));
			Assert.Equal(new[] { 3, 1 }, levels);
		}
	}
}
