using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Enemy;
using GameConfig.Hero;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Attributes;
using GameLogic.Config;
using GameLogic.Equipment;
using GameLogic.Profile;
using GameLogic.Save;
using GameLogic.Save.Data;
using Newtonsoft.Json;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>
/// 档案领域规则：成长派生、钱包、出战装配、存档格式与映射。
/// </summary>
public sealed class ProfileTests
{
    /// <summary>
    /// 累计经验是唯一事实，升级与跨级都从曲线派生，并只通知一次。
    /// </summary>
    [Fact]
    public void Progression_DerivesLevelAndNotifiesOncePerChange()
    {
        ProfileModel progression = new([(1, 0)], TestTables.Curve, 25);
        TestTables.Initialize(progression);
        int notifications = 0;
        progression.GetProgress(progression.HeroOf(PlayerSlot.P1)).Register(_ => notifications++);

        // 135 经验恰好升到 2 级；再加 145 + 1 跨到 3 级并保留 1 点余量。
        Assert.Equal(1, TestTables.ForProfile(progression).AddExperience(1, 135));
        Assert.Equal((2, 0, 145), (progression.GetProgress(progression.HeroOf(PlayerSlot.P1)).Value.Level, progression.GetProgress(progression.HeroOf(PlayerSlot.P1)).Value.Experience, progression.GetProgress(progression.HeroOf(PlayerSlot.P1)).Value.MaxExperience));
        Assert.Equal(1, TestTables.ForProfile(progression).AddExperience(1, 146));
        Assert.Equal((3, 1, 155), (progression.GetProgress(progression.HeroOf(PlayerSlot.P1)).Value.Level, progression.GetProgress(progression.HeroOf(PlayerSlot.P1)).Value.Experience, progression.GetProgress(progression.HeroOf(PlayerSlot.P1)).Value.MaxExperience));
        Assert.Equal(0, TestTables.ForProfile(progression).AddExperience(1, 0));
        Assert.Equal(2, notifications);

        // 回滚恢复派生值并通知；相同值不通知。
        progression.RestoreExperience(progression.HeroOf(PlayerSlot.P1), 0);
        progression.RestoreExperience(progression.HeroOf(PlayerSlot.P1), 0);
        Assert.Equal((1, 0, 135), (progression.GetProgress(progression.HeroOf(PlayerSlot.P1)).Value.Level, progression.GetProgress(progression.HeroOf(PlayerSlot.P1)).Value.Experience, progression.GetProgress(progression.HeroOf(PlayerSlot.P1)).Value.MaxExperience));
        Assert.Equal(3, notifications);
        Assert.Throws<ArgumentOutOfRangeException>(() => TestTables.ForProfile(progression).AddExperience(1, -1));
    }

    /// <summary>
    /// 满级后不再增长，也不发通知。
    /// </summary>
    [Fact]
    public void Progression_StopsAtMaxLevel()
    {
        ProfileModel progression = new([(1, int.MaxValue)], TestTables.Curve, 25);
        TestTables.Initialize(progression);
        int notifications = 0;
        progression.GetProgress(progression.HeroOf(PlayerSlot.P1)).Register(_ => notifications++);
        Assert.Equal(TestTables.Curve.MaxLevel, progression.GetProgress(progression.HeroOf(PlayerSlot.P1)).Value.Level);
        Assert.Equal(0, TestTables.ForProfile(progression).AddExperience(1, 1000));
        Assert.Equal(0, notifications);
        Assert.Equal((55, 0, 0), (progression.GetProgress(progression.HeroOf(PlayerSlot.P1)).Value.Level, progression.GetProgress(progression.HeroOf(PlayerSlot.P1)).Value.Experience, progression.GetProgress(progression.HeroOf(PlayerSlot.P1)).Value.MaxExperience));
    }

    /// <summary>
    /// 钱包不出现负数，不足时拒绝扣除。
    /// </summary>
    [Fact]
    public void Wallet_RejectsOverspendAndNegativeAmounts()
    {
        ProfileModel wallet = new([(1, 0)], TestTables.Curve, 25, -5);
        Assert.Equal(0, wallet.GetSoul(wallet.HeroOf(PlayerSlot.P1)).Value);
        TestTables.ForProfile(wallet).AddSoul(1, 30);
        Assert.False(TestTables.ForProfile(wallet).TrySpendSoul(1, 31));
        Assert.True(TestTables.ForProfile(wallet).TrySpendSoul(1, 30));
        Assert.Equal(0, wallet.GetSoul(wallet.HeroOf(PlayerSlot.P1)).Value);
        TestTables.ForProfile(wallet).AddSoul(1, int.MaxValue);
        TestTables.ForProfile(wallet).AddSoul(1, 10);
        Assert.Equal(int.MaxValue, wallet.GetSoul(wallet.HeroOf(PlayerSlot.P1)).Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => TestTables.ForProfile(wallet).AddSoul(1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => TestTables.ForProfile(wallet).TrySpendSoul(1, -1));
    }

    /// <summary>
    /// 零额、扣款失败、封顶和回滚都保持有效余额。
    /// </summary>
    [Fact]
    public void Wallet_PreservesBalanceAcrossNoOpsAndRestore()
    {
        ProfileModel wallet = new([(1, 0)], TestTables.Curve, 25, 30);
        TestTables.ForProfile(wallet).AddSoul(1, 0);
        Assert.True(TestTables.ForProfile(wallet).TrySpendSoul(1, 0));
        Assert.False(TestTables.ForProfile(wallet).TrySpendSoul(1, 31));
        Assert.Equal(30, wallet.GetSoul(wallet.HeroOf(PlayerSlot.P1)).Value);
        Assert.True(TestTables.ForProfile(wallet).TrySpendSoul(1, 10));
        Assert.Equal(20, wallet.GetSoul(wallet.HeroOf(PlayerSlot.P1)).Value);
        wallet.RestoreSoul(1, 20);
        Assert.Equal(20, wallet.GetSoul(wallet.HeroOf(PlayerSlot.P1)).Value);
        TestTables.ForProfile(wallet).AddSoul(1, int.MaxValue);
        TestTables.ForProfile(wallet).AddSoul(1, 1);
        Assert.Equal(int.MaxValue, wallet.GetSoul(wallet.HeroOf(PlayerSlot.P1)).Value);
        wallet.RestoreSoul(1, -1);
        Assert.Equal(0, wallet.GetSoul(wallet.HeroOf(PlayerSlot.P1)).Value);
    }

    /// <summary>
    /// 档案拒绝重复英雄，且不为未参战英雄分配席位。
    /// </summary>
    [Fact]
    public void Profile_RejectsInvalidHeroSets()
    {
        ProfileModel hero = new([(1, 0)], TestTables.Curve, 25);
        TestTables.Initialize(hero);
        Assert.Throws<ArgumentException>(() => new TestGameScope(new[] { (1, 0), (1, 0) }, 0, 25));
        Assert.Throws<ArgumentOutOfRangeException>(() => hero.SlotOf(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => hero.HeroOf(PlayerSlot.P2));
        Assert.Throws<ArgumentOutOfRangeException>(() => hero.HeroOf((PlayerSlot)0));
        Assert.Throws<ArgumentOutOfRangeException>(() => hero.HeroOf((PlayerSlot)3));
        Assert.Throws<ArgumentException>(() => new ProfileModel([], TestTables.Curve, 25));
        Assert.Throws<ArgumentException>(() => new ProfileModel([(1, 0), (2, 0), (3, 0)], TestTables.Curve, 25));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProfileModel([(0, 0)], TestTables.Curve, 25));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProfileModel([(-1, 0)], TestTables.Curve, 25));
        Assert.Equal(1, new TestGameScope(new[] { (1, 0) }, 0, 25).Profile.HeroOf(PlayerSlot.P1));
    }

    /// <summary>
    /// 阵容的输入顺序决定席位，反向英雄编号经过存档往返仍保持原席位。
    /// </summary>
    [Fact]
    public void Mapper_PreservesRosterOrderAndSeatsThroughJson()
    {
        var scope = new TestGameScope([(2, 472), (1, 135)], 42, 25);
        GameData snapshot = scope.CaptureSaveData();
        GameData loaded = JsonConvert.DeserializeObject<GameData>(JsonConvert.SerializeObject(snapshot));
        var restored = TestGameScope.FromSaveData(loaded, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25);

        // 使用非编号升序阵容，证明席位不会在构造、捕获或恢复时重新排序。
        Assert.Equal(new[] { 2, 1 }, scope.Profile.HeroIds);
        Assert.Equal(new[] { 2, 1 }, loaded.Heroes.Select(hero => hero.HeroId));
        Assert.Equal(new[] { 2, 1 }, restored.Profile.HeroIds);
        Assert.Equal(2, restored.Profile.HeroOf(PlayerSlot.P1));
        Assert.Equal(1, restored.Profile.HeroOf(PlayerSlot.P2));
        Assert.Equal(PlayerSlot.P1, restored.Profile.SlotOf(2));
        Assert.Equal(PlayerSlot.P2, restored.Profile.SlotOf(1));
        Assert.Equal(472, restored.Profile.GetProgress(2).Value.TotalExperience);
        Assert.Equal(135, restored.Profile.GetProgress(1).Value.TotalExperience);
        Assert.Equal(GameData.CurrentVersion, loaded.SaveVersion);
    }

    /// <summary>
    /// 存档信封拒绝人数越界与重复英雄，不能建立含歧义席位的档案。
    /// </summary>
    [Fact]
    public void Mapper_RejectsInvalidRosterEnvelopes()
    {
        HeroSaveData first = new() { HeroId = 1 };
        HeroSaveData second = new() { HeroId = 2 };
        HeroSaveData third = new() { HeroId = 3 };
        foreach (List<HeroSaveData> heroes in new List<HeroSaveData>[] { [], [first, first], [first, second, third] })
        {
            Assert.Throws<ArgumentException>(() => SaveDataUtility.ValidateEnvelope(new GameData { Heroes = heroes }));
        }
    }

    /// <summary>
    /// 钱包只在余额实际提交后通知，失败消费、零增量和封顶不重复通知。
    /// </summary>
    [Fact]
    public void Wallet_NotifiesOnlyCommittedBalanceChanges()
    {
        ProfileModel wallet = new([(1, 0)], TestTables.Curve, 25, 10);
        int notifications = 0;
        int observed = -1;
        wallet.GetSoul(wallet.HeroOf(PlayerSlot.P1)).Register(_ =>
        {
            notifications++;
            observed = wallet.GetSoul(wallet.HeroOf(PlayerSlot.P1)).Value;
        });
        TestTables.ForProfile(wallet).AddSoul(1, 0);
        Assert.False(TestTables.ForProfile(wallet).TrySpendSoul(1, 11));
        Assert.Equal(0, notifications);
        TestTables.ForProfile(wallet).AddSoul(1, 5);
        Assert.Equal((1, 15), (notifications, observed));
        Assert.True(TestTables.ForProfile(wallet).TrySpendSoul(1, 3));
        Assert.Equal((2, 12), (notifications, observed));
        wallet.RestoreSoul(1, 12);
        Assert.Equal(2, notifications);
        wallet.RestoreSoul(1, int.MaxValue);
        TestTables.ForProfile(wallet).AddSoul(1, 1);
        Assert.Equal((3, int.MaxValue), (notifications, observed));
    }

    /// <summary>
    /// 成长改变自动重算绑定属性，局内 Buff 来源保持，升级补满资源。
    /// </summary>
    [Fact]
    public void HeroAttributes_FollowsProgressionAndKeepsBuff()
    {
        var scope = new TestGameScope([(1, 0)], 0, 25);
        HeroModel model = scope.Hero;
        scope.GetSystem<GameLogic.Battle.HeroSystem>().SetSource(new AttributeSourceId("Buff", 1),
            [AttributeModifier.PercentMult(GameConfig.Attributes.AttributeType.Power, 1)]);
        Assert.Equal(20, model.Power.Value);
        scope.GetSystem<GameLogic.Battle.HeroSystem>().Damage(20);
        scope.GetSystem<ProfileSystem>().AddExperience(1, 140);
        Assert.Equal(30, model.Power.Value);
        Assert.Equal(130, model.MaxHp.Value);
        Assert.Equal(model.MaxHp.Value, model.Hp.Value);
    }

    /// <summary>
    /// 成长表与官方 Role1.upGrade 公式逐级一致（防止迁移脚本或手工改表引入偏差）。
    /// </summary>
    [Fact]
    public void GrowthTable_MatchesOfficialWukongGrowth()
    {
        for (int level = 1; level <= TestTables.Curve.MaxLevel; level++)
        {
            HeroGrowthLevel row = TestTables.Tables.TbHeroGrowthConfig.Get(1).Levels[level - 1];
            Assert.NotNull(row);
            int g = level - 1;
            int defense = level < 15 ? 2 + 2 * g : 28 + (level - 14) * 5;
            Assert.Equal((80 + 50 * g, 50 + 20 * g, 10 + 5 * g, defense, 0),
                (row.BaseAttributes.MaxHp, row.BaseAttributes.MaxMp, row.BaseAttributes.Power, row.BaseAttributes.Def, row.BaseAttributes.Mdef));
        }
    }

    /// <summary>
    /// 怪物基础值符合官方数据，同一种实体只配置一条基础数值。
    /// </summary>
    [Fact]
    public void EnemyTable_MatchesOfficialStatsAndHasOneConfigPerEntity()
    {
        (int Id, int Hp, int Defense, int Xp)[] values = [(1, 50, 0, 5), (2, 100, 1, 6), (3, 300, 5, 7),
            (4, 1500, 5, 25), (6, 3000, 6, 35), (7, 600, 8, 22), (8, 500, 7, 18),
            (9, 10000, 20, 130), (10, 24000, 80, 225), (11, 6000, 18, 100), (13, 700, 9, 26), (14, 1000, 25, 48),
            (15, 800, 10, 30), (16, 1200, 50, 57), (17, 1600, 70, 75), (18, 999999, 25, 500),
            (19, 48000, 90, 300), (20, 1400, 30, 65), (21, 6000, 25, 0),
            // BaseMonster.__added覆盖兽形、人形的构造血量；其余值来自各自官方构造。
            (22, 20000, 100, 48), (23, 40000, 100, 100), (24, 200000, 0, 100), (25, 5000, 120, 45), (26,12000,50,150)];
        foreach ((int Id, int Hp, int Defense, int Xp) expected in values)
        {
            EnemyConfig monster = TestTables.Tables.TbEnemyConfig.DataList.Single(config => config.LegacyId == expected.Id);
            Assert.Equal((expected.Hp, expected.Defense, expected.Xp),
                (monster.BaseAttributes.MaxHp, monster.BaseAttributes.Def, monster.AddExp));
        }
        Assert.All(TestTables.Tables.TbEnemyConfig.DataList.GroupBy(x => x.EntityId), group => Assert.Single(group));
        Assert.Equal(values.Length, TestTables.Tables.TbEnemyConfig.DataList.Count);
    }

    /// <summary>
    /// 真实配置通过启动校验。
    /// </summary>
    [Fact]
    public void ConfigurationModel_AcceptsShippedTables()
    {
        IArchitecture scope = TestTables.CreateConfigurationScope(TestTables.Tables);
        Assert.True(scope.GetSystem<GameLogic.Config.ConfigurationSystem>().Initialized);
        Assert.Equal(55, scope.GetUtility<ExperienceCurveUtility>().MaxLevel);
    }

    /// <summary>
    /// 缺少版本或进度字段的 JSON 不得被初始值误判为当前格式。
    /// </summary>
    [Fact]
    public void Archive_RejectsMissingRequiredFields()
    {
        Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<GameData>("{\"UnitId\":7}"));
        Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<GameData>("{\"SaveVersion\":2,\"UnitId\":7}"));
    }

    /// <summary>
    /// 当前格式的英雄记录必须明确保存身份与累计经验，缺字段不能被零值掩盖。
    /// </summary>
    /// <param name="field">要模拟丢失的字段。</param>
    [Theory]
    [InlineData("HeroId")]
    [InlineData("TotalExperience")]
    [InlineData("Soul")]
    [InlineData("BagInstanceIds")]
    public void Archive_RejectsIncompleteHeroRecord(string field)
    {
        var data = Newtonsoft.Json.Linq.JObject.FromObject(new GameData
        {
            Heroes = [new GameLogic.Save.Data.HeroSaveData { HeroId = 1, TotalExperience = 0 }]
        });
        var hero = (Newtonsoft.Json.Linq.JObject)data["Heroes"][0];
        hero.Remove(field);
        Assert.Throws<JsonSerializationException>(() => data.ToObject<GameData>());
    }

    /// <summary>
    /// 框架创建的当前格式空槽可以序列化回读，仍未建立角色。
    /// </summary>
    [Fact]
    public void Archive_RoundTripsUnclaimedCurrentSlot()
    {
        GameData data = JsonConvert.DeserializeObject<GameData>(JsonConvert.SerializeObject(new GameData { UnitId = 7 }));
        Assert.Equal(GameData.CurrentVersion, data.SaveVersion);
        Assert.Empty(data.Heroes);
        Assert.Empty(data.EquipmentItems);
        Assert.DoesNotContain("\"Soul\"", JsonConvert.SerializeObject(data));
        Assert.Equal(7, data.UnitId);
    }

    /// <summary>
    /// 仅接受当前格式的完整档案；不转换其他版本。
    /// </summary>
    [Fact]
    public void Mapper_RejectsOtherVersionsAndEmptyHeroes()
    {
        foreach (int version in Enumerable.Range(0, GameData.CurrentVersion).Append(GameData.CurrentVersion + 1))
        {
            Assert.Throws<NotSupportedException>(() => TestGameScope.FromSaveData(new GameData { SaveVersion = version, Heroes = [new HeroSaveData { HeroId = 1 }] }, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25));
        }

        Assert.Throws<ArgumentException>(() => TestGameScope.FromSaveData(new GameData(), TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25));
    }

    /// <summary>
    /// 档案 → 快照 → JSON → 快照 → 档案往返不丢状态，快照与运行时不共享引用。
    /// </summary>
    [Fact]
    public void Mapper_RoundTripsThroughJson()
    {
        TestGameScope playerModel = new(new[] { (1, 472) }, 42, 25);
        GameData snapshotData = playerModel.CaptureSaveData();
        playerModel.GetSystem<ProfileSystem>().AddSoul(1, 100);
        Assert.Equal(42, snapshotData.Heroes[0].Soul);

        string json = JsonConvert.SerializeObject(snapshotData);
        GameData loadedData = JsonConvert.DeserializeObject<GameData>(json);
        var restored = TestGameScope.FromSaveData(loadedData, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25);
        Assert.Equal(42, restored.Profile.GetSoul(restored.Profile.HeroOf(PlayerSlot.P1)).Value);
        Assert.Equal((4, 37), (restored.Profile.GetProgress(restored.Profile.HeroOf(PlayerSlot.P1)).Value.Level, restored.Profile.GetProgress(restored.Profile.HeroOf(PlayerSlot.P1)).Value.Experience));
        Assert.DoesNotContain("\"Player\"", json);
        Assert.DoesNotContain("\"Profile\"", json);
        Assert.Contains("\"TotalExperience\"", json);
    }

    /// <summary>
    /// 回滚把档案恢复到进关快照，保留原对象并通知订阅者。
    /// </summary>
    [Fact]
    public void Mapper_RollsBackToSnapshot()
    {
        TestGameScope playerModel = new(new[] { (1, 0) }, 5, 25);
        GameData data = playerModel.CaptureSaveData();
        ProfileModel progression = playerModel.Profile;
        List<int> levels = new();
        progression.GetProgress(progression.HeroOf(PlayerSlot.P1)).Register(_ => levels.Add(progression.GetProgress(progression.HeroOf(PlayerSlot.P1)).Value.Level));

        TestTables.ForProfile(progression).AddExperience(1, 400);
        playerModel.GetSystem<ProfileSystem>().AddSoul(1, 50);
        playerModel.RestoreCheckpoint(data);

        Assert.Same(progression, playerModel.Profile);
        Assert.Equal((1, 5), (progression.GetProgress(progression.HeroOf(PlayerSlot.P1)).Value.Level, playerModel.Profile.GetSoul(playerModel.Profile.HeroOf(PlayerSlot.P1)).Value));
        Assert.Equal(new[] { 3, 1 }, levels);
    }
}
