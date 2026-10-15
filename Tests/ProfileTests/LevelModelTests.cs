using System;
using GameLogic.Level;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>本局数据的时间边界、结局成绩和停止语义，不依赖场景节点。</summary>
public sealed class LevelModelTests
{
    /// <summary>只有开始后累计时间，结局成绩在后续帧和清理期间保持冻结。</summary>
    [Fact]
    public void Result_FreezesRunStatistics_AndStopDoesNotPublishAbandonment()
    {
        var levelModel = Create();
        levelModel.Tick(100);
        levelModel.Begin(new GameData());
        levelModel.Tick(1.25);
        levelModel.RecordDefeat(25);
        levelModel.RecordDefeat(7);
        int ended = 0;
        levelModel.Ended += _ => ended++;
        levelModel.Complete(LevelOutcome.Defeated, 0);
        var result = levelModel.Result;
        levelModel.Tick(100);
        levelModel.Stop();
        Assert.Equal(1.25, result.ElapsedSeconds);
        Assert.Equal(2, result.DefeatedMonsters);
        Assert.Equal(32, result.Experience);
        Assert.Equal(0, result.HealthPercent);
        Assert.Same(result, levelModel.Result);
        Assert.Null(levelModel.EntrySnapshot);
        Assert.False(levelModel.SessionActive);
        Assert.Equal(1, ended);
    }

    /// <summary>未提交的本局保留进关副本供模块回滚，停止只发布一次放弃。</summary>
    [Fact]
    public void Stop_ReleasesUnsubmittedSnapshot_AndPublishesAbandonmentOnce()
    {
        var levelModel = Create();
        var snapshot = new GameData { Soul = 80 };
        levelModel.Begin(snapshot);
        Assert.Same(snapshot, levelModel.EntrySnapshot);
        int ended = 0;
        levelModel.Ended += outcome =>
        {
            Assert.Equal(LevelOutcome.Abandoned, outcome);
            Assert.Null(levelModel.EntrySnapshot);
            Assert.False(levelModel.SessionActive);
            ended++;
        };
        levelModel.Stop();
        levelModel.Stop();
        Assert.Equal(1, ended);
        Assert.Equal(LevelStagePhase.Stopped, levelModel.Sequence.Phase);
        Assert.Null(levelModel.Result);
    }

    /// <summary>重复开始不得替换已经存在的回滚时间边界。</summary>
    [Fact]
    public void Begin_RejectsDuplicateSession_WithoutReplacingSnapshot()
    {
        var levelModel = Create();
        var snapshot = new GameData { Soul = 10 };
        levelModel.Begin(snapshot);
        Assert.Throws<InvalidOperationException>(() => levelModel.Begin(new GameData { Soul = 20 }));
        Assert.Same(snapshot, levelModel.EntrySnapshot);
    }

    /// <summary>使用正式关卡定义创建独立本局数据。</summary>
    /// <returns>尚未开始的本局模型。</returns>
    private static LevelModel Create()
    {
        var levelModel = new LevelModel();
        levelModel.Prepare(TestTables.Tables.TbLevelConfig.Get(1));
        return levelModel;
    }
}
