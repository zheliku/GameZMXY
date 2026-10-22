using GameLogic.Battle;
using GameLogic.Config;
using GameLogic.Architecture;
using GameLogic.Entity;
using GameLogic.Enemies;
using System;
using System.Linq;
using System.Threading;
using GameConfig.Level;
using GameConfig.Sound;
using GameLogic.Heroes;
using GameLogic.FloatingText;
using GameLogic.Profile;
using GameLogic.Equipment;
using Godot;
using GodotGameFramework.Entity;

namespace GameLogic.Level;

/// <summary>关卡场景 ViewController：拥有节点协作、掉落表现和英雄订阅；业务数据与结算归 Model/System。</summary>
public partial class LevelViewController : Node2D, IController
{
    [Export] private SoundId m_MusicId; // 关卡场景绑定的背景音乐，播放请求由流程拥有。
    [Export] private int m_LevelId = 1; // 关卡配置主键。
    [Export] private LevelSpawnPointsNode m_SpawnPoints; // 生成点目录。
    [Export] private LevelTriggersNode m_Triggers; // 可选区域触发器。
    [Export] private LevelGatesNode m_Gates; // 阶段物理门。
    [Export] private LevelSpawnerNode m_Spawner; // 配方调度和实体所有权。
    [Export] private LevelCameraNode m_Camera; // 取景和抵达事件。
    [Export] private LevelExitNode m_Exit; // 出口交互。
    [Export] private FloatingTextPresenter m_FloatingText; // 本场飘字表现。
    [Export] private string[] m_FeaturedOpponentSpawnPointIds = []; // 使用顶部生命条的生成点；仅影响场景展示。

    private IArchitecture m_Architecture; // 借用流程 FSM 的入口，初始化绑定后不跨作用域转移。
    private AbstractHeroEntity m_Player; // 借用流程显示并隐藏的英雄。
    private bool m_SessionActive; // 当前节点订阅是否已建立。

    /// <summary>守关对手显示、死亡或解除绑定，HUD 据此刷新生命条。</summary>
    public event Action FeaturedOpponentChanged;

    /// <summary>实体显示失败，交给会话拥有者结束关卡并报告错误。</summary>
    public event Action<Exception> Failed;

    /// <summary>交给关卡流程播放的音乐绑定。</summary>
    public SoundId MusicId => m_MusicId;

    /// <summary>本局唯一状态源。</summary>
    private LevelModel LevelModel => this.GetModel<LevelModel>();

    /// <summary>按主键借用共享定义。</summary>
    private LevelConfig Config => this.GetModel<ConfigurationModel>().Tables.TbLevelConfig.Get(m_LevelId);

    /// <summary>本阶段指定展示的普通怪物；死亡或离开会话后为空。</summary>
    public AbstractEnemyEntity FeaturedOpponent { get; private set; }

    /// <summary>玩家出生点的世界坐标，注册初始化完成后可读。</summary>
    public Vector2 PlayerSpawnPosition => m_SpawnPoints.PositionOf(Config.PlayerSpawnPointId);

    /// <summary>把游戏帧交给本局状态；暂停时 delta 为零。</summary>
    /// <param name="delta">游戏秒数。</param>
    public override void _Process(double delta)
    {
        if (m_SessionActive)
        {
            this.GetSystem<LevelSystem>().AdvanceTime(delta);
            TickLoot((float)delta);
        }
    }

    /// <summary>返回本场景装配时绑定的入口。</summary>
    /// <returns>所属游戏架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() =>
        m_Architecture ?? throw new InvalidOperationException("关卡 ViewController 尚未初始化。");

    /// <summary>绑定游戏入口，校验场景身份并初始化场景节点。</summary>
    /// <param name="architecture">所属游戏入口。</param>
    /// <exception cref="InvalidOperationException">绑定缺失、配置不存在、场景不匹配或管理器校验失败时抛出。</exception>
    public void Initialize(IArchitecture architecture)
    {
        m_Architecture = architecture ?? throw new ArgumentNullException(nameof(architecture));
        if (m_SpawnPoints == null || m_Gates == null || m_Spawner == null || m_Camera == null || m_Exit == null ||
            m_FloatingText == null)
            throw new InvalidOperationException("关卡必须绑定 SpawnPoints、Gates、Spawner、Camera、Exit 和 FloatingText。");
        this.GetSystem<LevelSystem>().Prepare(m_LevelId);
        if (!string.Equals(Config.ScenePath, SceneFilePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"关卡场景不匹配：{Config.ScenePath} / {SceneFilePath}");
        }

        // 初始化不订阅玩法事件；失败仍由流程完整卸载场景。
        m_SpawnPoints.Initialize(Config);
        m_Gates.Initialize(Config, m_Camera.SceneLeft, m_Camera.SceneRight, m_Camera.ViewSize.X);
        if (m_Triggers == null && Config.Stages.Exists(x => x.Activation == StageActivation.Trigger))
        {
            throw new InvalidOperationException("Trigger 阶段必须绑定触发器集合。");
        }

        m_Triggers?.Initialize(Config);
        m_Spawner.Initialize(Config, m_SpawnPoints);
        var recipes = Config.Stages.SelectMany(stage => stage.Recipes).ToArray();
        foreach (string id in m_FeaturedOpponentSpawnPointIds)
        {
            if (string.IsNullOrWhiteSpace(id) || m_FeaturedOpponentSpawnPointIds.Count(value => value == id) != 1 ||
                recipes.Count(recipe => recipe.SpawnPointId == id) != 1 ||
                recipes.Single(recipe => recipe.SpawnPointId == id).Count != 1)
                throw new InvalidOperationException($"顶部生命条必须绑定唯一、仅生成一只怪物的生成点：{id}");
        }
    }

    /// <summary>在玩家显示、加载界面关闭后开放玩法事件与相机跟随。</summary>
    /// <param name="entities">关卡流程注入的实体服务。</param>
    /// <param name="player">本会话的玩家节点。</param>
    /// <param name="cancellationToken">流程离开时取消在途实体显示。</param>
    public void StartSession(EntityComponent entities, AbstractHeroEntity player,
        CancellationToken cancellationToken)
    {
        if (m_SessionActive)
            throw new InvalidOperationException("关卡会话已经开始。");
        m_Player = player;
        this.GetSystem<LevelSystem>().StartSession(player.HeroId);
        m_ExitRequested = false;
        m_LootRandom.Randomize();
        m_Player.Died += OnHeroDied;
        this.GetModel<ProfileModel>().Slots.Register(OnEquipmentChanged);
        LevelModel.Outcome.Register(OnSessionEnded);
        m_SessionActive = true;
        m_Spawner.StartSession(entities, m_Camera, cancellationToken);
        m_FloatingText.StartSession(OwnsActor);
        m_Spawner.StageCleared += OnStageCleared;
        m_Spawner.EnemyDefeated += OnEnemyDefeated;
        m_Spawner.MonsterShown += OnMonsterShown;
        m_Exit.Entered += OnExitContact;
        m_Spawner.SpawnFailed += OnSpawnFailed;
        m_Camera.RightBoundaryReached += OnCameraArrived;
        if (m_Triggers != null)
        {
            m_Triggers.Activated += OnTriggerContact;
        }

        m_Camera.Follow(player);
        BeginTravel();
    }

    /// <summary>停止信号、调度和跟随并清理本关实体；初始化失败与流程离开时可重复调用。</summary>
    public void Cleanup()
    {
        m_FloatingText?.StopSession();
        UnsubscribeProgressEvents();
        if (m_Architecture != null)
            this.GetSystem<LevelSystem>()?.EndSession();
        ClearLoot();
        m_SessionActive = false;

        if (m_Triggers != null)
        {
            m_Triggers.Activated -= OnTriggerContact;
            m_Triggers.EndWatching();
        }

        // 初始化可能在任一绑定处失败；清理只依赖本场景已取得的节点。
        if (m_Camera != null)
        {
            m_Camera.RightBoundaryReached -= OnCameraArrived;
            m_Camera.StopFollowing();
        }
        if (m_Exit != null)
        {
            m_Exit.Entered -= OnExitContact;
            m_Exit.Disable();
        }
        ClearFeaturedOpponent();
        if (m_Spawner != null)
        {
            m_Spawner.StageCleared -= OnStageCleared;
            m_Spawner.EnemyDefeated -= OnEnemyDefeated;
            m_Spawner.MonsterShown -= OnMonsterShown;
            m_Spawner.SpawnFailed -= OnSpawnFailed;
            m_Spawner.StopSession();
        }
        m_Player = null;
    }

    /// <summary>飘字只接收本场玩家及本关刷怪服务拥有的运行实体。</summary>
    /// <param name="entityId">伤害发生时复制的受击实体编号。</param>
    /// <returns>是否归属本会话。</returns>
    private bool OwnsActor(int entityId) => m_Player?.Id == entityId || m_Spawner.OwnsEntity(entityId);

    /// <summary>清波后开放当前阶段通路并设置相机右界，特殊阶段同时启用触发区监听。</summary>
    private void BeginTravel()
    {
        LevelStage stage = LevelModel.Sequence.Current;
        m_Gates.BeginTravel(stage.StageOrder);
        m_Camera.TravelTo(m_Gates.BoundsOf(stage.StageOrder).Right);
        if (stage.Activation == StageActivation.Trigger)
        {
            m_Triggers.BeginWatching(stage.TriggerId);
        }
    }

    /// <summary>普通阶段由实际相机右缘抵达启动；特殊阶段继续等待区域事件。</summary>
    private void OnCameraArrived()
    {
        if (LevelModel.Sequence.Current.Activation == StageActivation.CameraArrived)
        {
            StartBattle();
        }
    }

    /// <summary>先迁移阶段状态并锁定物理区域，再提交当前阶段的并行配方调度。</summary>
    private void StartBattle()
    {
        this.GetSystem<LevelSystem>().StartBattle();
        LevelStage stage = LevelModel.Sequence.Current;
        m_Gates.LockRegion(stage.StageOrder);
        m_Camera.LockLeft(m_Gates.BoundsOf(stage.StageOrder).Left);
        m_Spawner.StartStage(stage);
    }

    /// <summary>清波后推进阶段；最终清波仅开放出口，使用出口才结算。</summary>
    private void OnStageCleared()
    {
        m_Gates.ReleaseRegion(LevelModel.Sequence.Current.StageOrder);
        this.GetSystem<LevelSystem>().ClearBattle();
        if (LevelModel.Sequence.Phase == LevelStagePhase.Travelling)
        {
            BeginTravel();
        }
        else
        {
            m_Exit.Enable();
        }
    }

    /// <summary>出口交互才提交通关；清场本身不保存、不导航。</summary>
    private void OnExitEntered()
    {
        if (LevelModel.Sequence.Phase != LevelStagePhase.AwaitingExit || !m_Player.IsAlive)
            return;
        // 已按 W 时等最后的球实际接触再保存，避免把尚未拾取的收益丢在卸载场景中。
        m_ExitRequested = true;
        if (m_LootOrbs.Count != 0)
            return;
        Complete(LevelOutcome.Cleared);
    }

    /// <summary>只接受本会话英雄的区域接触，先停止监听再开始战斗。</summary>
    /// <param name="hero">触发区检测到的英雄。</param>
    private void OnTriggerContact(AbstractHeroEntity hero)
    {
        if (!m_SessionActive || !ReferenceEquals(hero, m_Player) || !hero.IsAlive)
            return;
        m_Triggers.EndWatching();
        StartBattle();
    }

    /// <summary>只接受本会话英雄的出口交互，拒绝的接触不消耗出口。</summary>
    /// <param name="hero">出口检测到的英雄。</param>
    private void OnExitContact(AbstractHeroEntity hero)
    {
        if (!m_SessionActive || !ReferenceEquals(hero, m_Player) || !hero.IsAlive)
            return;
        m_Exit.Disable();
        OnExitEntered();
    }

    /// <summary>按场景生成点绑定需展示的怪物，不改变它的战斗行为。</summary>
    /// <param name="monster">已经归属本关的怪物。</param>
    /// <param name="spawnPointId">本次配方使用的生成点。</param>
    private void OnMonsterShown(AbstractEnemyEntity monster, string spawnPointId)
    {
        if (!m_FeaturedOpponentSpawnPointIds.Contains(spawnPointId))
            return;
        ClearFeaturedOpponent();
        FeaturedOpponent = monster;
        monster.Died += OnFeaturedOpponentDied;
        FeaturedOpponentChanged?.Invoke();
    }

    /// <summary>守关对手死亡后撤销展示；其余怪物仍由调度器判定清场。</summary>
    /// <param name="actor">已死亡的守关对手。</param>
    private void OnFeaturedOpponentDied(AbstractActorEntity actor) => ClearFeaturedOpponent();

    /// <summary>解除对手事件，避免池复用或关卡离开后残留旧生命条。</summary>
    private void ClearFeaturedOpponent()
    {
        if (FeaturedOpponent == null)
            return;
        FeaturedOpponent.Died -= OnFeaturedOpponentDied;
        FeaturedOpponent = null;
        FeaturedOpponentChanged?.Invoke();
    }

    /// <summary>显示失败属于会话所有者，先清理再上报，不留下锁死的战斗区域。</summary>
    /// <param name="error">实体显示抛出的异常。</param>
    private void OnSpawnFailed(Exception error)
    {
        Cleanup();
        Failed?.Invoke(error);
    }

    /// <summary>英雄死亡属于表现事实，结算由模块执行。</summary>
    /// <param name="actor">本关英雄。</param>
    private void OnHeroDied(AbstractActorEntity actor) => Complete(LevelOutcome.Defeated);

    /// <summary>传递结局时生命比例，不把实体传入业务模块。</summary>
    /// <param name="outcome">本局结局。</param>
    private void Complete(LevelOutcome outcome) => this.GetSystem<LevelSystem>().Complete(outcome,
        (int)Math.Round(100.0 * this.GetModel<HeroModel>().Hp.Value / this.GetModel<HeroModel>().MaxHp.Value));

    /// <summary>模型结局确定后停止奖励表现和订阅。</summary>
    /// <param name="outcome">已确定的结局。</param>
    private void OnSessionEnded(LevelOutcome outcome)
    {
        if (outcome == LevelOutcome.Running)
            return;
        ClearLoot();
        UnsubscribeProgressEvents();
    }

    /// <summary>装备变化后刷新节点属性；业务模块不持有英雄。</summary>
    /// <param name="slots">已经提交的穿戴快照。</param>
    private void OnEquipmentChanged(EquippedItems slots) => m_Player.RefreshAppearance();

    /// <summary>解除本局纯 C# 订阅；关停时不访问已释放的节点。</summary>
    public void UnsubscribeProgressEvents()
    {
        if (!m_SessionActive)
            return;
        m_Player.Died -= OnHeroDied;
        this.GetModel<ProfileModel>().Slots.UnRegister(OnEquipmentChanged);
        LevelModel.Outcome.UnRegister(OnSessionEnded);
    }
}
