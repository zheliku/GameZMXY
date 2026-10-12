using GameLogic;
using GameLogic.Entity;
using GameLogic.Monsters;
using System;
using System.Threading;
using GameConfig.Level;
using GameLogic.Heroes;
using Godot;
using GodotGameFramework.Entity;

namespace GameLogic.Level;

/// <summary>
/// 关卡模块唯一入口：拥有阶段推进、关内收益、检查点和结局。
/// 内部节点负责空间、刷怪和相机；流程负责装配与导航。
/// </summary>
public partial class LevelModule : Node2D
{
    [Export] private int m_LevelId = 1; // 关卡配置主键。
    [Export] private LevelSpawnPointsNode m_SpawnPoints; // 生成点目录，必需。
    [Export] private LevelTriggersNode m_Triggers; // 特殊关卡使用的可选触发区集合。
    [Export] private LevelGatesNode m_Gates; // 阶段物理门与区域，必需。
    [Export] private LevelSpawnerNode m_Spawner; // 配方调度和关卡实体所有权，必需。
    [Export] private LevelCameraNode m_Camera; // 实际取景和阶段抵达事件，必需。
    [Export] private LevelExitNode m_Exit; // 清场后开放的出口交互，必需。

    private LevelConfig m_Config; // 本场景对应的只读配置。
    private LevelStageSequence m_Sequence; // 会话当前阶段的唯一权威。
    private HeroEntity m_Player; // 拥有者注入的玩家，供特殊触发器监听与相机跟随。

    /// <summary>守关对手显示、死亡或解除绑定，HUD 据此刷新生命条。</summary>
    public event Action FeaturedOpponentChanged;

    /// <summary>本阶段指定展示的普通怪物；死亡或离开会话后为空。</summary>
    public MonsterEntity FeaturedOpponent { get; private set; }

    /// <summary>实体显示失败，交给会话拥有者结束关卡并报告错误。</summary>
    public event Action<Exception> Failed;

    /// <summary>可前进窗口开关变化；HUD 订阅后读取 <see cref="TravelAvailable"/>。</summary>
    public event Action TravelAvailableChanged;

    /// <summary>非最终阶段清波后到下一场开战前的可前进窗口。</summary>
    public bool TravelAvailable { get; private set; }

    /// <summary>玩家出生点的世界坐标，Initialize 后可读。</summary>
    public Vector2 PlayerSpawnPosition => m_SpawnPoints.PositionOf(m_Config.PlayerSpawnPointId);

    /// <summary>校验场景身份和必需绑定，再由各职责所有者验证配置。</summary>
    /// <exception cref="InvalidOperationException">绑定缺失、配置不存在、场景不匹配或管理器校验失败时抛出。</exception>
    public void Initialize()
    {
        if (m_SpawnPoints == null || m_Gates == null || m_Spawner == null || m_Camera == null || m_Exit == null)
        {
            throw new InvalidOperationException("关卡必须绑定 SpawnPoints、Gates、Spawner、Camera 和 Exit。");
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
    /// <param name="context">档案与存档模块；关内收益由本模块统一提交或回滚。</param>
    /// <param name="cancellationToken">流程离开时取消在途实体显示。</param>
    public void StartSession(EntityComponent entities, HeroEntity player, GameContext context, CancellationToken cancellationToken)
    {
        SetTravelAvailable(false);
        m_Player = player;
        BeginProgress(context);
        m_Spawner.StartSession(entities, m_Camera, cancellationToken);
        m_Spawner.StageCleared += OnStageCleared;
        m_Spawner.MonsterDefeated += OnMonsterDefeated;
        m_Spawner.MonsterShown += OnMonsterShown;
        m_Exit.Entered += OnExitEntered;
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
        AbandonProgress();
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
        m_Spawner.MonsterDefeated -= OnMonsterDefeated;
        m_Spawner.MonsterShown -= OnMonsterShown;
        m_Exit.Entered -= OnExitEntered;
        m_Exit.Disable();
        ClearFeaturedOpponent();
        m_Spawner.SpawnFailed -= OnSpawnFailed;
        m_Spawner.StopSession();
        m_Sequence.Stop();
        m_Player = null;
        SetTravelAvailable(false);
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
        SetTravelAvailable(false);
    }

    /// <summary>清波后推进阶段；最终清波仅开放出口，使用出口才结算。</summary>
    private void OnStageCleared()
    {
        m_Gates.ReleaseRegion(m_Sequence.Current.StageOrder);
        m_Sequence.ClearBattle();
        if (m_Sequence.Phase == LevelStagePhase.Travelling)
        {
            BeginTravel();
            SetTravelAvailable(true);
        }
        else
        {
            SetTravelAvailable(false);
            m_Exit.Enable(m_Player);
        }
    }

    /// <summary>出口交互才提交通关；清场本身不保存、不导航。</summary>
    private void OnExitEntered()
    {
        if (m_Sequence.Phase != LevelStagePhase.AwaitingExit || !m_Player.IsAlive) return;
        m_Sequence.Complete();
        SubmitOutcome(LevelOutcome.Cleared);
    }

    /// <summary>按阶段配置绑定需展示的普通怪物，不改变它的战斗行为。</summary>
    /// <param name="monster">已经归属本关的怪物。</param>
    private void OnMonsterShown(MonsterEntity monster)
    {
        if (monster.Config.EntityId != m_Sequence.Current.FeaturedOpponentId) return;
        ClearFeaturedOpponent();
        FeaturedOpponent = monster;
        monster.Died += OnFeaturedOpponentDied;
        FeaturedOpponentChanged?.Invoke();
    }

    /// <summary>守关对手死亡后撤销展示；其余怪物仍由调度器判定清场。</summary>
    /// <param name="actor">已死亡的守关对手。</param>
    private void OnFeaturedOpponentDied(ActorEntity actor) => ClearFeaturedOpponent();

    /// <summary>解除对手事件，避免池复用或关卡离开后残留旧生命条。</summary>
    private void ClearFeaturedOpponent()
    {
        if (FeaturedOpponent == null) return;
        FeaturedOpponent.Died -= OnFeaturedOpponentDied;
        FeaturedOpponent = null;
        FeaturedOpponentChanged?.Invoke();
    }

    /// <summary>更新可前进窗口并在变化时通知。</summary>
    /// <param name="available">是否处于可前进窗口。</param>
    private void SetTravelAvailable(bool available)
    {
        if (TravelAvailable == available)
        {
            return;
        }

        TravelAvailable = available;
        TravelAvailableChanged?.Invoke();
    }

    /// <summary>显示失败属于会话所有者，先清理再上报，不留下锁死的战斗区域。</summary>
    /// <param name="error">实体显示抛出的异常。</param>
    private void OnSpawnFailed(Exception error)
    {
        Cleanup();
        Failed?.Invoke(error);
    }
}
