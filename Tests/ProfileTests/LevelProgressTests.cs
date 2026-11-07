using System;
using System.Linq;
using GameLogic.Architecture;
using GameLogic.Level;
using GameLogic.Save;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GameLogic.Profile.Tests
{
    /// <summary>
    /// 关卡开放、重玩、失败、回滚和存档往返使用同一份档案进度。
    /// </summary>
    public sealed class LevelProgressTests
    {
        /// <summary>
        /// 连续命中累计；计数连续两次检查未变化时断连，未推进游戏时钟则保持，离关后清零。
        /// </summary>
        [Fact]
        public void Combo_UsesOfficialStablePollAndSessionClock()
        {
            var scope = new TestGameScope(1, [(1, 0)], 0, 25);
            var system = scope.GetSystem<LevelSystem>();
            const double pollInterval = 20d / 24d;

            system.RecordComboHit();
            Assert.Equal(0, scope.Level.ComboCount.Value);
            system.Prepare(1);
            system.StartSession(1);
            system.RecordComboHit();
            system.RecordComboHit();
            Assert.Equal(2, scope.Level.ComboCount.Value);

            system.AdvanceTime(pollInterval);
            Assert.Equal(2, scope.Level.ComboCount.Value);
            // 暂停时关卡帧入口不向系统提供游戏时间，连击计数不会跨过断连检查点。
            Assert.Equal(2, scope.Level.ComboCount.Value);
            system.AdvanceTime(pollInterval);
            Assert.Equal(0, scope.Level.ComboCount.Value);

            system.RecordComboHit();
            Assert.Equal(1, scope.Level.ComboCount.Value);
            system.EndSession();
            Assert.Equal(0, scope.Level.ComboCount.Value);
            system.Prepare(1);
            system.StartSession(1);
            Assert.Equal(0, scope.Level.ComboCount.Value);
            system.EndSession();
        }

        /// <summary>
        /// 目标出口只接受当前末段的指定生成点，重复事件及普通关卡不能提前通关。
        /// </summary>
        [Fact]
        public void TargetExit_RejectsWrongStageAndRepeatedDefeat()
        {
            var scope = new TestGameScope(1, [(1, 0)], 0, 25);
            var profile = scope.GetSystem<ProfileSystem>();
            for (int id = 1; id <= 3; id++)
            {
                profile.RecordLevelClear(id);
            }

            var system = scope.GetSystem<LevelSystem>();
            system.Prepare(4);
            Assert.False(system.TryOpenBatCaveExit("stage_5_d"));
            system.StartSession(1);
            system.StartBattle();
            Assert.False(system.TryOpenBatCaveExit("stage_5_d"));
            for (int stage = 1; stage < 5; stage++)
            {
                system.ClearBattle();
                system.StartBattle();
            }
            Assert.False(system.TryOpenBatCaveExit("stage_5_a"));
            Assert.True(system.TryOpenBatCaveExit("stage_5_d"));
            Assert.False(system.TryOpenBatCaveExit("stage_5_d"));
            Assert.Equal(ELevelOutcome.Running, scope.Level.Outcome.Value);
            Assert.Null(system.CheckpointWrite);
            system.Complete(ELevelOutcome.Cleared, 100);
            Assert.Equal(4, scope.Profile.HighestClearedLevelId.Value);
        }

        /// <summary>
        /// 新档只开放首关，逐关通关开放下一关，重玩不倒退。
        /// </summary>
        [Fact]
        public void Clear_UnlocksNextLevel_WithoutSkippingOrRegressing()
        {
            var scope = new TestGameScope(1, [(1, 0)], 0, 25);
            var system = scope.GetSystem<ProfileSystem>();
            Assert.Equal(1, system.LatestLevelId);
            Assert.True(system.CanEnterLevel(1));
            Assert.False(system.CanEnterLevel(2));
            Assert.Throws<InvalidOperationException>(() => system.RecordLevelClear(2));
            int lastLevelId = TestTables.Tables.TbLevelConfig.DataList
                .Where(level => level.LevelId != DragonPalaceRules.LevelId).Max(level => level.LevelId);
            for (int id = 1; id <= lastLevelId; id++)
            {
                system.RecordLevelClear(id);
                Assert.Equal(Math.Min(id + 1, lastLevelId), system.LatestLevelId);
                Assert.True(system.CanEnterLevel(id));
            }
            system.RecordLevelClear(1);
            Assert.Equal(lastLevelId, scope.Profile.HighestClearedLevelId.Value);
            Assert.False(system.CanEnterLevel(TestTables.Tables.TbLevelConfig.DataList.Max(level => level.LevelId) + 1));
            Assert.False(system.CanEnterLevel(0));
            Assert.False(system.CanEnterLevel(-1));
        }

        /// <summary>
        /// 失败、进入或放弃不开放下一关，锁定关卡在关卡系统边界也被拒绝。
        /// </summary>
        [Fact]
        public void FailureAndAbandon_DoNotUnlock()
        {
            var scope = new TestGameScope(1, [(1, 0)], 0, 25);
            var level = scope.GetSystem<LevelSystem>();
            Assert.Throws<InvalidOperationException>(() => level.Prepare(2));
            level.Prepare(1);
            level.StartSession(1);
            Assert.Equal(0, scope.Profile.HighestClearedLevelId.Value);
            level.Complete(ELevelOutcome.Defeated, 0);
            Assert.Equal(0, scope.Profile.HighestClearedLevelId.Value);
            level.EndSession();
            level.Prepare(1);
            level.StartSession(1);
            level.EndSession();
            Assert.Equal(0, scope.Profile.HighestClearedLevelId.Value);
        }

        /// <summary>
        /// 进度随正式 DTO 往返并支持原地回滚，不另造进度模型。
        /// </summary>
        [Fact]
        public void SaveRoundTripAndRollback_PreserveProgressAndBindingIdentity()
        {
            var scope = new TestGameScope(1, [(1, 135)], 17, 25);
            var binding = scope.Profile.HighestClearedLevelId;
            scope.GetSystem<ProfileSystem>().RecordLevelClear(1);
            var snapshot = JsonConvert.DeserializeObject<GameData>(JsonConvert.SerializeObject(scope.CaptureSaveData()));
            var restored = TestGameScope.FromSaveData(snapshot, TestTables.Curve, TestTables.Tables.TbEquipmentConfig, 25);
            Assert.Equal(1, restored.Profile.HighestClearedLevelId.Value);
            Assert.Equal(2, restored.GetSystem<ProfileSystem>().LatestLevelId);
            scope.GetSystem<ProfileSystem>().RecordLevelClear(2);
            scope.RestoreCheckpoint(snapshot);
            Assert.Same(binding, scope.Profile.HighestClearedLevelId);
            Assert.Equal(1, binding.Value);
        }

        /// <summary>
        /// 不存在的关卡或缺失必需进度字段不能被当作新档接受。
        /// </summary>
        /// <param name="id">应被拒绝的关卡进度编号。</param>
        [Theory]
        [InlineData(-1)]
        [InlineData(999)]
        public void InvalidProgress_IsRejected(int id)
        {
            var scope = new TestGameScope(1, [(1, 0)], 0, 25);
            var data = scope.CaptureSaveData();
            data.HighestClearedLevelId = id;
            Assert.Throws<ArgumentException>(() => new SaveDataUtility().Validate(data, TestTables.Tables.TbEquipmentConfig,
                25, TestTables.Tables.TbSkillConfig, TestTables.Tables.TbLevelConfig, TestTables.Tables.TbPassiveConfig));
            Assert.Throws<ArgumentException>(() => scope.RestoreCheckpoint(data));
            var json = JObject.FromObject(scope.CaptureSaveData());
            Assert.True(json.Remove(nameof(GameData.HighestClearedLevelId)));
            Assert.Throws<JsonSerializationException>(() => json.ToObject<GameData>());
        }
    }
}
