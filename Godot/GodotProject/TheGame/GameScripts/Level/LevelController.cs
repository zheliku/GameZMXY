using System;
using System.Threading;
using GameConfig.Level;
using Godot;
using GodotGameFramework.Entity;

namespace GameLogic.Level;

/// <summary>协调关卡会话和唯一当前阶段，空间、刷怪和相机行为由各职责所有者执行。</summary>
public partial class LevelController : Node2D
{
    [Export] private int m_LevelId = 1; // 关卡配置主键。
    [Export] private LevelSpawnPointSet m_SpawnPoints; // 生成点目录，必需。
    [Export] private LevelStageTriggerSet m_Triggers; // 特殊关卡使用的可选触发区集合。
    [Export] private LevelStageGateSet m_Gates; // 阶段物理门与区域，必需。
    [Export] private LevelSpawner m_Spawner; // 配方调度和关卡实体所有权，必需。
    [Export] private LevelCamera m_Camera; // 实际取景和阶段抵达事件，必需。

    private LevelConfig m_Config; // 本场景对应的只读配置。
    private LevelStageSequence m_Sequence; // 会话当前阶段的唯一权威。
    private Node2D m_Player; // 拥有者注入的玩家，仅供特殊触发器监听。

    /// <summary>关卡已全部清波；出口和结算可订阅此事件。</summary>
    public event Action Completed;

    /// <summary>实体显示失败，交给会话拥有者结束关卡并报告错误。</summary>
    public event Action<Exception> Failed;

    /// <summary>玩家出生点的世界坐标，Initialize 后可读。</summary>
    public Vector2 PlayerSpawnPosition => m_SpawnPoints.PositionOf(m_Config.PlayerSpawnPointId);

    /// <summary>当前阶段顺序，供 HUD 和调试观测。</summary>
    public int StageOrder => m_Sequence.Current.StageOrder;

    /// <summary>当前会话阶段状态。</summary>
    public LevelStagePhase Phase => m_Sequence.Phase;

    /// <summary>校验场景身份和必需绑定，再由各职责所有者验证配置。</summary>
    /// <exception cref="InvalidOperationException">绑定缺失、配置不存在、场景不匹配或管理器校验失败时抛出。</exception>
    public void Initialize()
    {
        if (m_SpawnPoints == null || m_Gates == null || m_Spawner == null || m_Camera == null)
        {
            throw new InvalidOperationException("关卡必须绑定 SpawnPoints、Gates、Spawner 和 Camera。");
        }

        m_Config = ConfigSystem.Instance.Tables.TbLevelConfig.GetOrDefault(m_LevelId)
            ?? throw new InvalidOperationException($"关卡配置不存在：LevelId={m_LevelId}");
        if (!string.Equals(m_Config.ScenePath, SceneFilePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"关卡场景不匹配：{m_Config.ScenePath} / {SceneFilePath}");
        }

        // 初始化不订阅玩法事件；失败仍由流程完整卸载场景。
        m_Sequence = new LevelStageSequence(m_Config.Stages);
        m_SpawnPoints.Initialize(m_Config);
        m_Gates.Initialize(m_Config, m_Camera.SceneLeft, m_Camera.SceneRight, m_Camera.ViewSize.X);
        if (m_Triggers == null && m_Config.Stages.Exists(x => x.Activation == StageActivation.Trigger))
        {
            throw new InvalidOperationException("Trigger 阶段必须绑定触发器集合。");
        }

        m_Triggers?.Initialize(m_Config);
        m_Spawner.Initialize(m_Config, m_SpawnPoints);
    }

    /// <summary>在玩家显示、加载界面关闭后开放玩法事件与相机跟随。</summary>
    /// <param name="entities">关卡流程注入的实体服务。</param>
    /// <param name="player">本会话的玩家节点。</param>
    /// <param name="cancellationToken">流程离开时取消在途实体显示。</param>
    public void StartSession(EntityComponent entities, Node2D player, CancellationToken cancellationToken)
    {
        m_Player = player;
        m_Spawner.StartSession(entities, cancellationToken);
        m_Spawner.StageCleared += OnStageCleared;
        m_Spawner.SpawnFailed += OnSpawnFailed;
        m_Camera.RightBoundaryReached += OnCameraArrived;
        if (m_Triggers != null)
        {
            m_Triggers.Activated += StartBattle;
        }

        m_Sequence.Begin();
        m_Camera.Follow(player);
        BeginTravel();
    }

    /// <summary>停止信号、调度和跟随并清理本关实体；初始化失败与流程离开时可重复调用。</summary>
    public void Cleanup()
    {
        if (m_Sequence == null || m_Sequence.Phase == LevelStagePhase.Stopped)
        {
            return; // 未完成初始化或已经停止的会话没有需要解除的订阅。
        }

        if (m_Triggers != null)
        {
            m_Triggers.Activated -= StartBattle;
            m_Triggers.EndWatching();
        }

        m_Camera.RightBoundaryReached -= OnCameraArrived;
        m_Camera.StopFollowing();
        m_Spawner.StageCleared -= OnStageCleared;
        m_Spawner.SpawnFailed -= OnSpawnFailed;
        m_Spawner.StopSession();
        m_Sequence.Stop();
        m_Player = null;
    }

    /// <summary>清波后开放当前阶段通路并设置相机右界，特殊阶段同时启用触发区监听。</summary>
    private void BeginTravel()
    {
        LevelStage stage = m_Sequence.Current;
        m_Gates.BeginTravel(stage.StageOrder);
        m_Camera.TravelTo(m_Gates.BoundsOf(stage.StageOrder).Right);
        if (stage.Activation == StageActivation.Trigger)
        {
            m_Triggers.BeginWatching(stage.TriggerId, m_Player);
        }
    }

    /// <summary>普通阶段由实际相机右缘抵达启动；特殊阶段继续等待区域事件。</summary>
    private void OnCameraArrived()
    {
        if (m_Sequence.Current.Activation == StageActivation.CameraArrived)
        {
            StartBattle();
        }
    }

    /// <summary>先迁移阶段状态并锁定物理区域，再提交当前阶段的并行配方调度。</summary>
    private void StartBattle()
    {
        m_Sequence.StartBattle();
        LevelStage stage = m_Sequence.Current;
        m_Gates.LockRegion(stage.StageOrder);
        m_Camera.LockLeft(m_Gates.BoundsOf(stage.StageOrder).Left);
        m_Spawner.StartStage(stage);
    }

    /// <summary>当前阶段清除后开放出口并推进一次；最终清波交给出口和结算。</summary>
    private void OnStageCleared()
    {
        m_Gates.ReleaseRegion(m_Sequence.Current.StageOrder);
        m_Sequence.ClearBattle();
        if (m_Sequence.Phase == LevelStagePhase.Travelling)
        {
            BeginTravel();
        }
        else
        {
            Completed?.Invoke();
        }
    }

    /// <summary>显示失败属于会话所有者，先清理再上报，不留下锁死的战斗区域。</summary>
    /// <param name="error">实体显示抛出的异常。</param>
    private void OnSpawnFailed(Exception error)
    {
        Cleanup();
        Failed?.Invoke(error);
    }
}
