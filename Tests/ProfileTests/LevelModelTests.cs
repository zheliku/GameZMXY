using System;
using GameLogic.Level;
using GameLogic.Save;
using Xunit;

namespace GameLogic.Profile.Tests;

/// <summary>
/// 通过正式关卡系统验证计时、结算与回滚，只隔离存档磁盘写入。
/// </summary>
public sealed class LevelModelTests
{
    /// <summary>
    /// 只有开始后累计时间，结局成绩在后续帧和清理期间保持冻结。
    /// </summary>
    [Fact]
    public void Result_FreezesRunStatistics_AndStopDoesNotPublishAbandonment()
    {
        var scope = new TestGameScope([(1, 0)], 80, 25);
        LevelSystem system = scope.GetSystem<LevelSystem>();
        system.Prepare(1);
        LevelModel levelModel = scope.Level;
        system.AdvanceTime(100);
        system.StartSession([1]);
        system.AdvanceTime(1.25);
        system.AdvanceTime(double.NaN);
        system.AdvanceTime(double.PositiveInfinity);
        system.AdvanceTime(-1);
        system.RecordDefeat(1, 25);
        system.RecordDefeat(1, 7);
        int ended = 0;
        levelModel.Outcome.Register(_ => ended++);
        system.Complete(LevelOutcome.Defeated, 0);
        LevelResult result = levelModel.Result;
        system.Complete(LevelOutcome.Cleared, 100);
        system.AdvanceTime(100);
        system.EndSession();
        Assert.Equal(1.25, result.ElapsedSeconds);
        Assert.Equal(2, result.DefeatedEnemies);
        Assert.Equal(32, result.Experience);
        Assert.Equal(0, result.HealthPercent);
        Assert.Same(result, levelModel.Result);
        Assert.Null(levelModel.EntrySnapshot);
        Assert.False(levelModel.SessionActive);
        Assert.Equal(1, ended);
        Assert.Equal(levelModel.Result == null ? 0 : 1, scope.GetSystem<SaveSystem>().CheckpointCount);
    }

    /// <summary>
    /// 无英雄归属的击败只增加计数，不改变任一英雄经验或已发经验统计。
    /// </summary>
    [Fact]
    public void UnownedDefeat_OnlyCountsEnemy()
    {
        var scope = new TestGameScope([(1, 0), (2, 0)], 0, 25);
        LevelSystem system = scope.GetSystem<LevelSystem>();
        system.Prepare(1);
        system.StartSession([1, 2]);
        system.RecordDefeat(2, 25);
        HeroProgressionState first = scope.Profile.GetProgress(1).Value;
        HeroProgressionState second = scope.Profile.GetProgress(2).Value;

        // 经验足以升级也不能冒认 P1 或覆盖已有的已发经验统计。
        Assert.False(system.RecordDefeat(0, 135));
        Assert.Equal(2, scope.Level.DefeatedEnemyCount);
        Assert.Equal(25, scope.Level.AwardedExperience);
        Assert.Equal(first, scope.Profile.GetProgress(1).Value);
        Assert.Equal(second, scope.Profile.GetProgress(2).Value);
    }

    /// <summary>
    /// 经验只发给显式归属英雄，另一人的成长保持原样。
    /// </summary>
    /// <param name="heroId">本次击败收益所属英雄。</param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Defeat_AwardsOnlyRequestedHero(int heroId)
    {
        var scope = new TestGameScope([(1, 0), (2, 0)], 0, 25);
        LevelSystem system = scope.GetSystem<LevelSystem>();
        system.Prepare(1);
        system.StartSession([1, 2]);
        HeroProgressionState other = scope.Profile.GetProgress(3 - heroId).Value;

        // 用真实成长门槛验证升级返回值与个人经验，不复制经验公式。
        Assert.True(system.RecordDefeat(heroId, 135));
        Assert.Equal(1, scope.Level.DefeatedEnemyCount);
        Assert.Equal(135, scope.Level.AwardedExperience);
        Assert.Equal(135, scope.Profile.GetProgress(heroId).Value.TotalExperience);
        Assert.Equal(2, scope.Profile.GetProgress(heroId).Value.Level);
        Assert.Equal(other, scope.Profile.GetProgress(3 - heroId).Value);
    }

    /// <summary>
    /// 未提交的本局保留进关副本供模块回滚，停止只发布一次放弃。
    /// </summary>
    [Fact]
    public void Stop_ReleasesUnsubmittedSnapshot_AndPublishesAbandonmentOnce()
    {
        var scope = new TestGameScope([(1, 0)], 80, 25);
        LevelSystem system = scope.GetSystem<LevelSystem>();
        system.Prepare(1);
        LevelModel levelModel = scope.Level;
        system.StartSession([1]);
        Assert.Equal(80, levelModel.EntrySnapshot.Heroes[0].Soul);
        scope.GetSystem<ProfileSystem>().AddSoul(1, 20);
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
        Assert.Equal(80, scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value);
    }

    /// <summary>
    /// 设置菜单先保存再离开时，已写入的当前档案不得被进关快照回滚。
    /// </summary>
    [Fact]
    public void EndSessionWithoutRollback_PreservesSavedProgress()
    {
        var scope = new TestGameScope([(1, 0)], 80, 25);
        LevelSystem system = scope.GetSystem<LevelSystem>();
        system.Prepare(1);
        system.StartSession([1]);
        scope.GetSystem<ProfileSystem>().AddSoul(1, 20);

        system.EndSession(rollback: false);
        Assert.Null(scope.Level.EntrySnapshot);
        Assert.Equal(100, scope.Profile.GetSoul(scope.Profile.HeroOf(PlayerSlot.P1)).Value);
    }

    /// <summary>
    /// 重复开始不得替换已经存在的回滚时间边界。
    /// </summary>
    [Fact]
    public void Begin_RejectsDuplicateSession_WithoutReplacingSnapshot()
    {
        var scope = new TestGameScope([(1, 0)], 80, 25);
        LevelSystem system = scope.GetSystem<LevelSystem>();
        system.Prepare(1);
        LevelModel levelModel = scope.Level;
        system.StartSession([1]);
        GameData snapshot = levelModel.EntrySnapshot;
        Assert.Throws<InvalidOperationException>(() => system.StartSession([1]));
        Assert.Same(snapshot, levelModel.EntrySnapshot);
    }
}
