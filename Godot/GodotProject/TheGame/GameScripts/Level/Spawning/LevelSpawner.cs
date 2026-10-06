using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameConfig.Level;
using GameFramework.Entity;
using GameFramework.Event;
using GameLogic.Event;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;

namespace GameLogic.Level;

/// <summary>执行当前阶段的生成调度，持有关卡实体所有权并依据死亡事件判定清波。</summary>
public partial class LevelSpawner : Node
{
    private readonly HashSet<int> m_OwnedIds = new(); // 本关创建的全部运行实体 ID，含死亡动画中的实体。
    private readonly HashSet<int> m_ActiveIds = new(); // 当前阶段仍存活的实体 ID，死亡事件只扣减一次。
    private LevelSpawnPointSet m_SpawnPoints; // 初始化阶段校验过的生成点目录。
    private EntityComponent m_Entities; // 会话注入的实体服务。
    private CancellationTokenSource m_Cancellation; // 停止会话时立即作废在途显示结果。
    private LevelSpawnSchedule m_Schedule; // 唯一当前阶段的生成计划。
    private bool m_SessionActive; // 会话已注入实体服务并订阅死亡事件。

    /// <summary>当前阶段配方发完且存活与在途名额归零，只报告一次。</summary>
    public event Action StageCleared;

    /// <summary>当前会话显示实体失败；控制器负责结束会话。</summary>
    public event Action<Exception> SpawnFailed;

    /// <summary>初始禁用游戏帧调度，加载期不生成实体。</summary>
    public override void _Ready() => SetProcess(false);

    /// <summary>在初始化边界验证配方和怪物实体配置，运行时不重复查询怪物数值表。</summary>
    /// <param name="config">本场景对应的关卡配置。</param>
    /// <param name="spawnPoints">初始化阶段已校验的生成点目录。</param>
    /// <exception cref="InvalidOperationException">配方缺失、数值非法或怪物外键不唯一时抛出。</exception>
    public void Initialize(LevelConfig config, LevelSpawnPointSet spawnPoints)
    {
        m_SpawnPoints = spawnPoints;
        foreach (LevelStage stage in config.Stages)
        {
            if (stage.Recipes.Count == 0)
            {
                throw new InvalidOperationException($"阶段 {stage.StageOrder} 没有生成配方。");
            }

            foreach (LevelSpawnRecipe recipe in stage.Recipes)
            {
                if (recipe.Count <= 0 || !float.IsFinite(recipe.Delay) || recipe.Delay < 0f ||
                    !float.IsFinite(recipe.Interval) || recipe.Interval < 0f)
                {
                    throw new InvalidOperationException($"阶段 {stage.StageOrder} 配方 {recipe.SpawnPointId} 数值非法。");
                }

                if (ConfigSystem.Instance.Tables.TbMonsterConfig.DataList.Count(x => x.EntityId == recipe.MonsterEntityId) != 1 ||
                    ConfigSystem.Instance.Tables.TbEntityConfig.DataList.Count(x => x.EntityId == recipe.MonsterEntityId) != 1)
                {
                    throw new InvalidOperationException($"怪物配方必须唯一关联 MonsterConfig 和 EntityConfig：{recipe.MonsterEntityId}");
                }
            }
        }
    }

    /// <summary>创建会话所有权并连接死亡事件；不启用调度，不创建任何怪物。</summary>
    /// <param name="entities">关卡流程注入的实体服务。</param>
    /// <param name="cancellationToken">流程离开时取消在途实体显示。</param>
    public void StartSession(EntityComponent entities, CancellationToken cancellationToken)
    {
        m_Entities = entities;
        m_Cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        m_SessionActive = true;
        GF.Event.Subscribe(MonsterDiedEventArgs.EventId, OnMonsterDied);
    }

    /// <summary>启动当前阶段的生成计划，延迟按游戏帧计时。</summary>
    /// <param name="stage">已开战的当前阶段。</param>
    public void StartStage(LevelStage stage)
    {
        m_Schedule = new LevelSpawnSchedule(stage);
        SetProcess(true);
    }

    /// <summary>提交到期配方，统一预约名额；暂停时由场景树停止游戏时间。</summary>
    /// <param name="delta">本帧经过的游戏秒数。</param>
    public override void _Process(double delta)
    {
        m_Schedule.Advance(delta);
        while (m_Schedule.TryTake(out LevelSpawnRecipe recipe))
        {
            _ = ShowMonsterAsync(recipe, m_Schedule, m_Entities, m_Cancellation.Token);
            if (!IsProcessing())
            {
                // 池化显示可能同步清波并停止调度，本帧不再提交剩余配方。
                return;
            }
        }
    }

    /// <summary>停止生成、取消在途登记、退订死亡事件并清理本关所有实体；重复调用直接返回。</summary>
    public void StopSession()
    {
        if (!m_SessionActive)
        {
            return; // 未开始或已经停止的会话没有资源需要释放。
        }

        m_SessionActive = false;
        SetProcess(false);
        // 停止边界先取消，再释放所有权；异步结果使用捕获的旧服务和令牌自行回收。
        m_Cancellation.Cancel();
        m_Cancellation.Dispose();
        m_Cancellation = null;
        GF.Event.Unsubscribe(MonsterDiedEventArgs.EventId, OnMonsterDied);
        foreach (int id in m_OwnedIds)
        {
            m_Entities.HideEntitySafe(id);
        }

        m_OwnedIds.Clear();
        m_ActiveIds.Clear();
        m_Schedule = null;
        m_Entities = null;
    }

    /// <summary>显示一只配方怪物并登记所有权；使用捕获的会话快照，避免卸载后访问已清理字段。</summary>
    /// <param name="recipe">已预约名额的生成配方。</param>
    /// <param name="schedule">提交该配方的调度，失败时归还名额。</param>
    /// <param name="entities">本次显示使用的实体服务快照。</param>
    /// <param name="cancellationToken">会话取消令牌，取消后晚到的结果由本方法回收。</param>
    /// <returns>显示与登记完成的异步任务。</returns>
    private async Task ShowMonsterAsync(LevelSpawnRecipe recipe, LevelSpawnSchedule schedule,
        EntityComponent entities, CancellationToken cancellationToken)
    {
        try
        {
            // 显示 API 可能从池中同步返回，名额已由调度器提前预约。
            IEntity entity = await entities.ShowEntityAsync(recipe.MonsterEntityId, m_SpawnPoints.PositionOf(recipe.SpawnPointId));
            if (entity == null)
            {
                throw new InvalidOperationException($"怪物显示失败：{recipe.MonsterEntityId}");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                entities.HideEntitySafe(entity.Id);
                return;
            }

            m_OwnedIds.Add(entity.Id);
            m_ActiveIds.Add(entity.Id);
        }
        catch (Exception error)
        {
            schedule.Release();
            if (!cancellationToken.IsCancellationRequested)
            {
                SpawnFailed?.Invoke(error);
            }
        }
    }

    /// <summary>存活实体死亡时归还名额；配方与名额都清空后报告本阶段清波。</summary>
    /// <param name="sender">事件源，不使用。</param>
    /// <param name="args">死亡事件参数，只在回调生命周期内读取运行 ID。</param>
    private void OnMonsterDied(object sender, GameEventArgs args)
    {
        if (args is MonsterDiedEventArgs died && m_ActiveIds.Remove(died.EntityId))
        {
            m_Schedule.Release();
            if (m_Schedule.IsCleared)
            {
                // 先停调度再分发，控制器可以同步开放下一段通路。
                SetProcess(false);
                StageCleared?.Invoke();
            }
        }
    }
}
