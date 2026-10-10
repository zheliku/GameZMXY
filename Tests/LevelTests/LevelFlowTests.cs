using System;
using System.IO;
using System.Linq;
using GameConfig.Entity;
using GameConfig.Level;
using GameLogic.Level;
using Luban;
using Xunit;

namespace GameLogic.Level.Tests;

/// <summary>从真实 Luban 产物验证阶段、并行配方和实际相机中心的回归契约。</summary>
public sealed class LevelFlowTests
{
    private static LevelConfig LoadLevel() // 与游戏读取相同二进制，覆盖嵌套列表布局而非复制配置对象。
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Data", "level_tblevelconfig.bytes");
        return new TbLevelConfig(new ByteBuf(File.ReadAllBytes(path))).Get(1);
    }

    /// <summary>官方花果山前四波保留两种小怪和并行配方，由相机抵达激活，暂不包含 Boss 波。</summary>
    [Fact]
    public void NestedConfigKeepsParallelRecipesAndCameraActivation()
    {
        LevelConfig level = LoadLevel();
        Assert.Equal(4, level.Stages.Count);
        Assert.Equal(new[] { 3, 7, 6, 7 }, level.Stages.Select(x => x.Recipes.Sum(r => r.Count)));
        Assert.Equal(new[] { EntityId.HuaguoshanMonkey, EntityId.DemonMonkey },
            level.Stages[1].Recipes.Select(x => x.MonsterEntityId));
        Assert.All(level.Stages, x => Assert.Equal(StageActivation.CameraArrived, x.Activation));
        Assert.All(level.Stages, x => Assert.True(x.SpawnDelay >= 0));
    }

    /// <summary>加载期不开始战斗，清除只能推进紧邻下一阶段，末段清除完成会话。</summary>
    [Fact]
    public void SequenceWaitsForArrivalAfterEveryClear()
    {
        LevelStageSequence sequence = new(LoadLevel().Stages);
        Assert.Equal(LevelStagePhase.Ready, sequence.Phase);
        Assert.Throws<InvalidOperationException>(sequence.StartBattle);
        sequence.Begin();
        for (int i = 1; i <= 4; i++)
        {
            Assert.Equal(i, sequence.Current.StageOrder);
            Assert.Equal(LevelStagePhase.Travelling, sequence.Phase);
            sequence.StartBattle();
            sequence.ClearBattle();
        }

        Assert.Equal(LevelStagePhase.Completed, sequence.Phase);
        Assert.Throws<InvalidOperationException>(sequence.StartBattle);
    }

    /// <summary>重复抵达或清除不能让同一阶段启动两次或跳过阶段。</summary>
    [Fact]
    public void SequenceRejectsDuplicateBattleTransitions()
    {
        LevelStageSequence sequence = new(LoadLevel().Stages);
        sequence.Begin();
        sequence.StartBattle();
        Assert.Throws<InvalidOperationException>(sequence.StartBattle);
        sequence.ClearBattle();
        Assert.Throws<InvalidOperationException>(sequence.ClearBattle);
        sequence.Stop();
        Assert.Throws<InvalidOperationException>(sequence.StartBattle);
    }

    /// <summary>特殊触发阶段必须有可读触发区 ID，相机阶段不能携带无消费者的触发区外键。</summary>
    [Theory]
    [InlineData(StageActivation.Trigger, "special_area", true)]
    [InlineData(StageActivation.Trigger, "", false)]
    [InlineData(StageActivation.CameraArrived, "special_area", false)]
    [InlineData(StageActivation.CameraArrived, "", true)]
    public void ActivationHasExplicitTriggerContract(StageActivation activation, string triggerId, bool valid)
    {
        LevelStage stage = CreateCapacityStage(activation, triggerId);
        if (valid)
        {
            LevelStageSequence sequence = new(new[] { stage });
            sequence.Begin();
            Assert.Equal(LevelStagePhase.Travelling, sequence.Phase);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => new LevelStageSequence(new[] { stage }));
        }
    }

    /// <summary>阶段延迟之前不刷怪，到期后同帧取出 a 与 b 的首只怪物。</summary>
    [Fact]
    public void StageDelayThenBothRecipesBecomeReadyTogether()
    {
        LevelStage stage = CreateCapacityStage(maxActive: 2, spawnDelay: 0.3f);
        LevelSpawnSchedule schedule = new(stage);
        schedule.Advance(stage.SpawnDelay * 0.5);
        Assert.False(schedule.TryTake(out _));
        schedule.Advance(stage.SpawnDelay * 0.5);
        Assert.True(schedule.TryTake(out LevelSpawnRecipe a));
        Assert.True(schedule.TryTake(out LevelSpawnRecipe b));
        Assert.Equal("a", a.SpawnPointId);
        Assert.Equal("b", b.SpawnPointId);
        Assert.False(schedule.TryTake(out _));
    }

    /// <summary>全部配方发完仍等待存活名额，清空后才允许清波。</summary>
    [Fact]
    public void ClearRequiresRecipesFinishedAndAllEntitiesReleased()
    {
        LevelStage stage = LoadLevel().Stages[0];
        LevelSpawnSchedule schedule = new(stage);
        int count = 0;
        for (int frame = 0; frame < 100; frame++)
        {
            schedule.Advance(0.1);
            while (schedule.TryTake(out _))
            {
                count++;
            }
        }

        Assert.Equal(stage.Recipes.Sum(x => x.Count), count);
        Assert.False(schedule.IsCleared);
        for (int i = 0; i < count; i++)
        {
            schedule.Release();
        }

        Assert.True(schedule.IsCleared);
        Assert.False(schedule.TryTake(out _));
    }

    /// <summary>预约名额计入上限，只有释放后下一条配方才能继续。</summary>
    [Fact]
    public void PendingShowsRespectCapacityAndRoundRobinFairness()
    {
        LevelStage stage = CreateCapacityStage();
        LevelSpawnSchedule schedule = new(stage);
        Assert.True(schedule.TryTake(out LevelSpawnRecipe a));
        Assert.False(schedule.TryTake(out _));
        schedule.Release();
        Assert.True(schedule.TryTake(out LevelSpawnRecipe b));
        Assert.NotEqual(a.SpawnPointId, b.SpawnPointId);
        Assert.Equal(1, schedule.ActiveCount);
    }

    /// <summary>角色在中央死区内移动时相机保持不动。</summary>
    [Theory]
    [InlineData(470f)]
    [InlineData(500f)]
    [InlineData(440f)]
    public void CameraHoldsInsideCentralWindow(float target)
    {
        Assert.Equal(470f, LevelCamera.StepCenter(470f, target, 40f, 470f, 1210f, 10f));
    }

    /// <summary>相机停止在墙前时，角色可继续移动，脚本中心仍等于实际显示中心。</summary>
    [Fact]
    public void LockedCameraNeverAccumulatesPositionBehindLimit()
    {
        float center = 1210f;
        for (float player = 1250f; player <= 1660f; player += 10f)
        {
            center = LevelCamera.StepCenter(center, player, 40f, 470f, 1210f, 10f);
            Assert.Equal(1210f, center);
        }
    }

    /// <summary>角色已走到右墙时开放下一段，相机首帧只移动允许的距离。</summary>
    [Fact]
    public void OpeningGateDoesNotSnapToPlayerAtRightWall()
    {
        float center = LevelCamera.StepCenter(1210f, 1660f, 40f, 470f, 2310f, 10f);
        Assert.Equal(1220f, center);
        for (int i = 0; i < 50; i++)
        {
            float next = LevelCamera.StepCenter(center, 1660f, 40f, 470f, 2310f, 10f);
            Assert.InRange(next - center, 0f, 10f);
            center = next;
        }

        Assert.Equal(1620f, center);
    }

    /// <summary>正常行进抵达右界时，角色仍位于屏幕中央的小死区附近。</summary>
    [Fact]
    public void CameraRightEdgeArrivesBeforePlayerReachesWall()
    {
        float center = 470f;
        float player = 300f;
        while (center < 1210f)
        {
            player += 3f;
            center = LevelCamera.StepCenter(center, player, 40f, 470f, 1210f, 10f);
        }

        Assert.InRange(player - center, 40f, 43f);
        Assert.Equal(1680f, center + 470f);
        Assert.True(player < 1680f - 400f);
    }

    private static LevelStage CreateCapacityStage(StageActivation activation = StageActivation.CameraArrived,
        string triggerId = "", int maxActive = 1, float spawnDelay = 0f) // 使用真实序列化协议建立容量与激活契约边界。
    {
        ByteBuf bytes = new();
        bytes.WriteInt(1);
        bytes.WriteString("Capacity");
        bytes.WriteInt((int)activation);
        bytes.WriteString(triggerId);
        bytes.WriteString("");
        bytes.WriteInt(maxActive);
        bytes.WriteFloat(spawnDelay);
        bytes.WriteSize(2);
        foreach (string point in new[] { "a", "b" })
        {
            bytes.WriteInt(1);
            bytes.WriteString(point);
            bytes.WriteInt(2);
            bytes.WriteFloat(0f);
            bytes.WriteFloat(0f);
        }

        return new LevelStage(bytes);
    }
}
