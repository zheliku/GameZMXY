using System;
using GameLogic.Architecture;
using GameLogic.Level;
using GameLogic.Save;
using Newtonsoft.Json;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>关卡开放、重玩、失败、回滚和存档往返使用同一份档案进度。</summary>
public sealed class LevelProgressTests
{
    /// <summary>新档只开放首关，逐关通关开放下一关，重玩不倒退。</summary>
    [Fact]
    public void Clear_UnlocksNextLevel_WithoutSkippingOrRegressing()
    {
        var scope = new TestGameScope(1, [(1, 0)], 0, 25);
        var system = scope.GetSystem<ProfileSystem>();
        Assert.Equal(1, system.LatestLevelId);
        Assert.True(system.CanEnterLevel(1));
        Assert.False(system.CanEnterLevel(2));
        Assert.Throws<InvalidOperationException>(() => system.RecordLevelClear(2));
        for (int id = 1; id <= 3; id++)
        {
            system.RecordLevelClear(id);
            Assert.Equal(Math.Min(id + 1, 3), system.LatestLevelId);
            Assert.True(system.CanEnterLevel(id));
        }
        system.RecordLevelClear(1);
        Assert.Equal(3, scope.Profile.HighestClearedLevelId.Value);
        Assert.False(system.CanEnterLevel(4));
        Assert.False(system.CanEnterLevel(0));
        Assert.False(system.CanEnterLevel(-1));
    }

    /// <summary>失败、进入或放弃不开放下一关，锁定关卡在关卡系统边界也被拒绝。</summary>
    [Fact]
    public void FailureAndAbandon_DoNotUnlock()
    {
        var scope = new TestGameScope(1, [(1, 0)], 0, 25);
        var level = scope.GetSystem<LevelSystem>();
        Assert.Throws<InvalidOperationException>(() => level.Prepare(2));
        level.Prepare(1);
        level.StartSession(1);
        Assert.Equal(0, scope.Profile.HighestClearedLevelId.Value);
        level.Complete(LevelOutcome.Defeated, 0);
        Assert.Equal(0, scope.Profile.HighestClearedLevelId.Value);
        level.EndSession();
        level.Prepare(1);
        level.StartSession(1);
        level.EndSession();
        Assert.Equal(0, scope.Profile.HighestClearedLevelId.Value);
    }

    /// <summary>进度随正式 DTO 往返并支持原地回滚，不另造进度模型。</summary>
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

    /// <summary>不存在的关卡或缺失必需进度字段不能被当作新档接受。</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void InvalidProgress_IsRejected(int id)
    {
        var scope = new TestGameScope(1, [(1, 0)], 0, 25);
        var data = scope.CaptureSaveData();
        data.HighestClearedLevelId = id;
        Assert.Throws<ArgumentException>(() => new SaveDataUtility().Validate(data, TestTables.Tables.TbEquipmentConfig,
            25, TestTables.Tables.TbSkillConfig, TestTables.Tables.TbLevelConfig));
        Assert.Throws<ArgumentException>(() => scope.RestoreCheckpoint(data));
        Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<GameData>(
            JsonConvert.SerializeObject(scope.CaptureSaveData()).Replace("\"HighestClearedLevelId\":0,", "")));
    }
}
