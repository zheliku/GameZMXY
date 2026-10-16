using System;
using GameLogic.Level;
using GameLogic.Save;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>通过正式关卡系统验证计时、结算与回滚，只隔离存档磁盘写入。</summary>
public sealed class LevelModelTests
{
    /// <summary>只有开始后累计时间，结局成绩在后续帧和清理期间保持冻结。</summary>
    [Fact]
    public void Result_FreezesRunStatistics_AndStopDoesNotPublishAbandonment()
    {
        var scope = new TestGameScope(1, [(1, 0)], 80, 25);
        var system = scope.GetSystem<LevelSystem>();
        system.Prepare(1);
        var levelModel = scope.Level;
        system.AdvanceTime(100);
        system.StartSession(1);
        system.AdvanceTime(1.25);
        system.AdvanceTime(double.NaN);
        system.AdvanceTime(double.PositiveInfinity);
        system.AdvanceTime(-1);
        system.RecordDefeat(25);
        system.RecordDefeat(7);
        int ended = 0;
        levelModel.Outcome.Register(_ => ended++);
        system.Complete(LevelOutcome.Defeated, 0);
        var result = levelModel.Result;
        system.Complete(LevelOutcome.Cleared, 100);
        system.AdvanceTime(100);
        system.EndSession();
        Assert.Equal(1.25, result.ElapsedSeconds);
        Assert.Equal(2, result.DefeatedMonsters);
        Assert.Equal(32, result.Experience);
        Assert.Equal(0, result.HealthPercent);
        Assert.Same(result, levelModel.Result);
        Assert.Null(levelModel.EntrySnapshot);
        Assert.False(levelModel.SessionActive);
        Assert.Equal(1, ended);
        Assert.Equal(levelModel.Result == null ? 0 : 1, scope.GetSystem<SaveSystem>().CheckpointCount);
    }

    /// <summary>未提交的本局保留进关副本供模块回滚，停止只发布一次放弃。</summary>
    [Fact]
    public void Stop_ReleasesUnsubmittedSnapshot_AndPublishesAbandonmentOnce()
    {
        var scope = new TestGameScope(1, [(1, 0)], 80, 25);
        var system = scope.GetSystem<LevelSystem>();
        system.Prepare(1);
        var levelModel = scope.Level;
        system.StartSession(1);
        Assert.Equal(80, levelModel.EntrySnapshot.Soul);
        scope.GetSystem<ProfileSystem>().AddSoul(20);
        int ended = 0;
        levelModel.Outcome.Register(outcome =>
        {
            Assert.Equal(LevelOutcome.Abandoned, outcome);
            Assert.Null(levelModel.EntrySnapshot);
            Assert.False(levelModel.SessionActive);
            ended++;
        });
        system.EndSession();
        system.EndSession();
        Assert.Equal(1, ended);
        Assert.Equal(levelModel.Result == null ? 0 : 1, scope.GetSystem<SaveSystem>().CheckpointCount);
        Assert.Equal(LevelStagePhase.Stopped, levelModel.Sequence.Phase);
        Assert.Null(levelModel.Result);
        Assert.Equal(80, scope.Profile.Soul.Value);
    }

    /// <summary>重复开始不得替换已经存在的回滚时间边界。</summary>
    [Fact]
    public void Begin_RejectsDuplicateSession_WithoutReplacingSnapshot()
    {
        var scope = new TestGameScope(1, [(1, 0)], 80, 25);
        var system = scope.GetSystem<LevelSystem>();
        system.Prepare(1);
        var levelModel = scope.Level;
        system.StartSession(1);
        var snapshot = levelModel.EntrySnapshot;
        Assert.Throws<InvalidOperationException>(() => system.StartSession(1));
        Assert.Same(snapshot, levelModel.EntrySnapshot);
    }
}
