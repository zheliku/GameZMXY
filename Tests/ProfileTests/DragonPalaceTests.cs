using System;
using System.Linq;
using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Level;
using GameLogic.Save;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GameLogic.Profile.Tests
{
    /// <summary>
    /// 隐藏龙宫的首次奖励、事务和主线隔离使用正式模型与存档映射。
    /// </summary>
    public sealed class DragonPalaceTests
    {
        /// <summary>
        /// 隐藏关只跟随入口关的开放资格，通关不会成为地图最新主线。
        /// </summary>
        [Fact]
        public void HiddenRoom_DoesNotAdvanceMainline()
        {
            var scope = new TestGameScope(1, [(1, 0)], 0, 25);
            var profile = scope.GetSystem<ProfileSystem>();
            Assert.False(profile.CanEnterLevel(11));
            profile.RecordLevelClear(1);
            Assert.True(profile.CanEnterLevel(11));
            profile.RecordLevelClear(11);
            Assert.Equal(1, scope.Profile.HighestClearedLevelId.Value);
            Assert.Equal(2, profile.LatestLevelId);
            var data = scope.CaptureSaveData();
            data.HighestClearedLevelId = 11;
            Assert.Throws<ArgumentException>(() => SaveDataUtility.ValidateLevelProgress(data,
                TestTables.Tables.TbLevelConfig));
        }

        /// <summary>
        /// 旗幡资格只领取一次，当前DTO完整往返；放弃会原地恢复进关资格。
        /// </summary>
        [Fact]
        public void BannerEligibility_RoundTripsAndRollsBack()
        {
            var scope = new TestGameScope(1, [(1, 0)], 0, 25);
            var system = scope.GetSystem<ProfileSystem>();
            var checkpoint = scope.CaptureSaveData();
            Assert.True(system.TryClaimEastSeaBanner());
            Assert.False(system.TryClaimEastSeaBanner());
            var captured = JsonConvert.DeserializeObject<GameData>(JsonConvert.SerializeObject(scope.CaptureSaveData()));
            Assert.False(captured.EastSeaBannerAvailable);
            var restored = TestGameScope.FromSaveData(captured, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25);
            Assert.False(restored.GetSystem<ProfileSystem>().TryClaimEastSeaBanner());
            scope.RestoreCheckpoint(checkpoint);
            Assert.True(system.TryClaimEastSeaBanner());
            var incomplete = JObject.FromObject(captured);
            incomplete.Remove(nameof(GameData.EastSeaBannerAvailable));
            Assert.Throws<JsonSerializationException>(() => incomplete.ToObject<GameData>());
        }

        /// <summary>
        /// 转场保留水帘洞已领取收益，错误入口和重复提交拒绝，不产生通关记录。
        /// </summary>
        [Fact]
        public void Transfer_CommitsCollectedProgressWithoutClearingSource()
        {
            var scope = new TestGameScope(1, [(1, 0)], 0, 25);
            var profile = scope.GetSystem<ProfileSystem>();
            profile.RecordLevelClear(1);
            var level = scope.GetSystem<LevelSystem>();
            level.Prepare(2);
            level.StartSession(1);
            profile.AddSoul(21);
            Assert.False(level.TransferToDragonPalace(3));
            Assert.True(level.TransferToDragonPalace(11));
            Assert.False(level.TransferToDragonPalace(11));
            Assert.Equal(LevelOutcome.Transferred, scope.Level.Outcome.Value);
            level.EndSession();
            Assert.Equal(21, scope.Profile.Soul.Value);
            Assert.Equal(1, scope.Profile.HighestClearedLevelId.Value);
            level.Prepare(11);
            level.StartSession(1);
            Assert.True(profile.TryClaimEastSeaBanner());
            level.EndSession();
            Assert.True(scope.Profile.EastSeaBannerAvailable);
            Assert.Equal(21, scope.Profile.Soul.Value);
        }

        /// <summary>
        /// 龙王保留四条基础攻击和四条专属高难度数值，不给公共配置增加等级变体索引。
        /// </summary>
        [Fact]
        public void DragonTiers_KeepOneIdentityAndDistinctCombatData()
        {
            var scope = new TestGameScope(1, [(1, 0)], 0, 25);
            var config = scope.GetModel<ConfigurationModel>();
            var dragon = TestTables.Tables.TbEnemyConfig.Get((int)GameConfig.Entity.EntityId.DragonKing);
            var lowAttacks = config.GetAttacks(dragon.EntityId);
            var highAttacks = Enumerable.Range(1, 4)
                .Select(action => TestTables.Tables.TbAttackConfig.Get(TestTables.ConfigIds.GetAttackId(dragon.EntityId, action, 2))).ToArray();
            Assert.Equal(new[] { 90f, 17f, 20f, 150f }, lowAttacks.Select(item => item.FlatPower.X));
            Assert.Equal(new[] { 200f, 35f, 100f, 300f }, highAttacks.Select(item => item.FlatPower.X));
            Assert.Same(lowAttacks, config.GetAttacks(dragon.EntityId));
            Assert.Equal(12000, dragon.BaseAttributes.MaxHp);
        }
    }
}
