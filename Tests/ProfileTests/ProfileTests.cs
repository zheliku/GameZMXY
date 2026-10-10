using System;
using System.Collections.Generic;
using GameConfig.Hero;
using GameLogic.Battle.Stats;
using GameLogic.Save;
using Newtonsoft.Json;
using Xunit;

namespace GameLogic.Profile.Tests
{
	/// <summary>档案领域规则：成长派生、钱包、出战装配、存档迁移与映射。</summary>
	public sealed class ProfileTests
	{
		/// <summary>累计经验是唯一事实，升级与跨级都从曲线派生，并只通知一次。</summary>
		[Fact]
		public void Progression_DerivesLevelAndNotifiesOncePerChange()
		{
			HeroProgression progression = new(TestTables.Curve, 0);
			int notifications = 0;
			progression.Changed += () => notifications++;

			// 140 经验恰好升到 2 级；再加 160 + 1 跨到 3 级并保留 1 点余量。
			Assert.Equal(1, progression.AddExperience(140));
			Assert.Equal((2, 0, 160), (progression.Level, progression.Experience, progression.MaxExperience));
			Assert.Equal(1, progression.AddExperience(161));
			Assert.Equal((3, 1, 180), (progression.Level, progression.Experience, progression.MaxExperience));
			Assert.Equal(0, progression.AddExperience(0));
			Assert.Equal(2, notifications);

			// 回滚恢复派生值并通知；相同值不通知。
			progression.Restore(0);
			progression.Restore(0);
			Assert.Equal((1, 0, 140), (progression.Level, progression.Experience, progression.MaxExperience));
			Assert.Equal(3, notifications);
			Assert.Throws<ArgumentOutOfRangeException>(() => progression.AddExperience(-1));
		}

		/// <summary>满级后不再增长，也不发通知。</summary>
		[Fact]
		public void Progression_StopsAtMaxLevel()
		{
			HeroProgression progression = new(TestTables.Curve, int.MaxValue);
			int notifications = 0;
			progression.Changed += () => notifications++;
			Assert.True(progression.IsMaxLevel);
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

		/// <summary>档案拒绝重复英雄与未拥有的出战英雄。</summary>
		[Fact]
		public void Profile_RejectsInvalidHeroSets()
		{
			HeroRecord hero = new(1, new HeroProgression(TestTables.Curve, 0));
			Assert.Throws<ArgumentException>(() => new PlayerProfile(1, new[] { hero, hero }, new Wallet(0)));
			Assert.Throws<ArgumentException>(() => new PlayerProfile(2, new[] { hero }, new Wallet(0)));
			Assert.Equal(1, new PlayerProfile(1, new[] { hero }, new Wallet(0)).ActiveHero.HeroId);
		}

		/// <summary>出战装配取成长表当前等级的属性；数值与重构前公式 Base+(L-1)*Grow 一致。</summary>
		[Fact]
		public void StatBuilder_UsesGrowthRowOfCurrentLevel()
		{
			HeroStatBuilder builder = new(TestTables.Tables.TbHeroGrowthConfig);
			HeroRecord hero = new(1, new HeroProgression(TestTables.Curve, 0));
			HeroLoadout level1 = builder.Build(hero);
			Assert.Equal((1, 80, 50, 8), (level1.Level, level1.GrowthStats.MaxHp, level1.GrowthStats.MaxMp, level1.GrowthStats.Power));

			// 升到 2 级：生命 80+50、魔法 50+15、攻击 8+4、物防 10+1。
			hero.Progression.AddExperience(140);
			HeroLoadout level2 = builder.Build(hero);
			Assert.Equal((2, 130, 65, 12, 11), (level2.Level, level2.GrowthStats.MaxHp, level2.GrowthStats.MaxMp,
				level2.GrowthStats.Power, level2.GrowthStats.Def));

			// 装配写入属性汇总：基础值替换，Buff 等其他来源不受影响。
			StatSheet stats = new();
			StatSource buff = new("Buff", 1);
			stats.SetSource(buff, new[] { StatModifier.Flat(GameConfig.Stat.StatType.Power, 3) });
			StatSource[] sources = level1.ApplyTo(stats, null);
			Assert.Equal(11, stats.GetInt(GameConfig.Stat.StatType.Power));
			level2.ApplyTo(stats, sources);
			Assert.Equal(15, stats.GetInt(GameConfig.Stat.StatType.Power));
			Assert.Equal(130, stats.GetInt(GameConfig.Stat.StatType.MaxHp));
		}

		/// <summary>装配隔离输入集合；卸下装备只移除旧的持久来源，保留 Buff。</summary>
		[Fact]
		public void Loadout_SnapshotsModifiersAndReplacesOnlyPersistentSources()
		{
			var growth = TestTables.Tables.TbHeroGrowthConfig.Get(1, 1).Stats;
			StatSource equipment = new("Equipment", 1);
			StatSource buff = new("Buff", 1);
			var modifiers = new List<StatModifier> { StatModifier.Flat(GameConfig.Stat.StatType.Power, 5) };
			var entries = new List<KeyValuePair<StatSource, IReadOnlyList<StatModifier>>> { new(equipment, modifiers) };
			HeroLoadout equipped = new(1, 1, growth, entries);

			// 同时修改外层与内层输入，已构建装配仍按原快照生效。
			modifiers[0] = StatModifier.Flat(GameConfig.Stat.StatType.Power, 500);
			entries.Clear();
			StatSheet stats = new();
			stats.SetSource(buff, [StatModifier.Flat(GameConfig.Stat.StatType.Power, 3)]);
			StatSource[] sources = equipped.ApplyTo(stats, null);
			Assert.Equal(new[] { equipment }, sources);
			Assert.Equal(growth.Power + 8, stats.GetInt(GameConfig.Stat.StatType.Power));

			// 空装配等价于卸下全部持久来源，不改变本局 Buff。
			HeroLoadout unequipped = new(1, 1, growth, []);
			Assert.Empty(unequipped.ApplyTo(stats, sources));
			Assert.Equal(growth.Power + 3, stats.GetInt(GameConfig.Stat.StatType.Power));
		}

		/// <summary>成长表与重构前公式逐级一致（防止迁移脚本或手工改表引入偏差）。</summary>
		[Fact]
		public void GrowthTable_MatchesLegacyLinearFormulaForWukong()
		{
			for (int level = 1; level <= TestTables.Curve.MaxLevel; level++)
			{
				HeroGrowthConfig row = TestTables.Tables.TbHeroGrowthConfig.Get(1, level);
				Assert.NotNull(row);
				int g = level - 1;
				Assert.Equal((80 + 50 * g, 50 + 15 * g, 8 + 4 * g, 10 + g, 10 + g),
					(row.Stats.MaxHp, row.Stats.MaxMp, row.Stats.Power, row.Stats.Def, row.Stats.Mdef));
			}
		}

		/// <summary>真实配置通过启动校验。</summary>
		[Fact]
		public void ConfigValidator_AcceptsShippedTables()
		{
			ExperienceCurve curve = GameLogic.Config.ConfigValidator.ValidateAll(TestTables.Tables);
			Assert.Equal(55, curve.MaxLevel);
		}

		/// <summary>旧格式存档（无 SaveVersion 字段）按"等级不倒退、累计经验不丢失"迁移为版本 1。</summary>
		[Fact]
		public void Migrator_UpgradesVersion0PlayerData()
		{
			// 重构前存档的真实 JSON 形状：Score + Player{Level, TotalExperience, Gold}，没有 SaveVersion。
			const string legacyJson = "{\"Score\":0,\"Player\":{\"Level\":3,\"TotalExperience\":90,\"Gold\":12},\"UnitId\":7}";
			GameData data = JsonConvert.DeserializeObject<GameData>(legacyJson);
			Assert.Equal(0, data.SaveVersion);

			(ProfileSaveData profile, SaveOrigin origin) = SaveMigrator.Migrate(data, TestTables.Curve, 1, 999);
			Assert.Equal(SaveOrigin.Migrated, origin);
			Assert.Equal(1, profile.ActiveHeroId);
			Assert.Equal(12, profile.Gold);
			Assert.Single(profile.Heroes);
			Assert.Equal(300, profile.Heroes[0].TotalExperience); // 3 级门槛 140+160=300，旧经验 90 低于门槛被抬到门槛。
		}

		/// <summary>框架首次启动创建的空档按建档规则新建。</summary>
		[Fact]
		public void Migrator_CreatesProfileForEmptyArchive()
		{
			GameData data = JsonConvert.DeserializeObject<GameData>("{\"UnitId\":7}");
			(ProfileSaveData profile, SaveOrigin origin) = SaveMigrator.Migrate(data, TestTables.Curve, 1, 25);
			Assert.Equal(SaveOrigin.Created, origin);
			Assert.Equal(25, profile.Gold);
			Assert.Equal(0, profile.Heroes[0].TotalExperience);
		}

		/// <summary>当前版本原样读取；版本更高或数据缺失时拒绝，避免降级覆盖。</summary>
		[Fact]
		public void Migrator_RejectsUnknownOrBrokenVersions()
		{
			GameData current = new() { SaveVersion = 1, Profile = new ProfileSaveData { ActiveHeroId = 1 } };
			Assert.Equal(SaveOrigin.Current, SaveMigrator.Migrate(current, TestTables.Curve, 1, 0).Origin);
			Assert.Throws<NotSupportedException>(() => SaveMigrator.Migrate(new GameData { SaveVersion = 1 }, TestTables.Curve, 1, 0));
			Assert.Throws<NotSupportedException>(() => SaveMigrator.Migrate(new GameData { SaveVersion = 2 }, TestTables.Curve, 1, 0));
		}

		/// <summary>档案 → 快照 → JSON → 快照 → 档案往返不丢状态，快照与运行时不共享引用。</summary>
		[Fact]
		public void Mapper_RoundTripsThroughJson()
		{
			PlayerProfile profile = new(1, new[] { new HeroRecord(1, new HeroProgression(TestTables.Curve, 517)) }, new Wallet(42));
			ProfileSaveData snapshot = ProfileMapper.Capture(profile);
			profile.Wallet.AddGold(100);
			Assert.Equal(42, snapshot.Gold);

			string json = JsonConvert.SerializeObject(new GameData { SaveVersion = SaveMigrator.CurrentVersion, Profile = snapshot });
			GameData loaded = JsonConvert.DeserializeObject<GameData>(json);
			PlayerProfile restored = ProfileMapper.Restore(SaveMigrator.Migrate(loaded, TestTables.Curve, 1, 0).Profile, TestTables.Curve);
			Assert.Equal(42, restored.Wallet.Gold);
			Assert.Equal((4, 37), (restored.ActiveHero.Progression.Level, restored.ActiveHero.Progression.Experience));
			Assert.Null(loaded.Player);
		}

		/// <summary>回滚把档案恢复到进关快照，保留原对象并通知订阅者。</summary>
		[Fact]
		public void Mapper_RollsBackToSnapshot()
		{
			PlayerProfile profile = new(1, new[] { new HeroRecord(1, new HeroProgression(TestTables.Curve, 0)) }, new Wallet(5));
			ProfileSaveData snapshot = ProfileMapper.Capture(profile);
			HeroProgression progression = profile.ActiveHero.Progression;
			List<int> levels = new();
			progression.Changed += () => levels.Add(progression.Level);

			progression.AddExperience(400);
			profile.Wallet.AddGold(50);
			ProfileMapper.RollBack(profile, snapshot);

			Assert.Same(progression, profile.ActiveHero.Progression);
			Assert.Equal((1, 5), (progression.Level, profile.Wallet.Gold));
			Assert.Equal(new[] { 3, 1 }, levels);
		}
	}
}
