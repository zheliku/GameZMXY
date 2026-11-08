using System;
using System.Linq;
using System.Threading;
using GameConfig.Level;
using GameConfig.Sound;
using GameFramework.Event;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Config;
using GameLogic.Enemies;
using GameLogic.Entity;
using GameLogic.Equipment;
using GameLogic.Event.Combat;
using GameLogic.FloatingText;
using GameLogic.Heroes;
using GameLogic.Heroes.Body;
using GameLogic.Profile;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;

namespace GameLogic.Level;

/// <summary>
/// 关卡场景 ViewController：拥有节点协作、掉落表现和英雄订阅；业务数据与结算归 Model/System。
/// </summary>
public partial class LevelViewController : Node2D, IController
{
    [Export] private SoundId _musicId; // 关卡场景绑定的背景音乐，播放请求由流程拥有。
    [Export] private int _levelId = 1; // 关卡配置主键。
    [Export] private LevelSpawnPointsNode _spawnPoints; // 生成点目录。
    [Export] private LevelTriggersNode _triggers; // 可选区域触发器。
    [Export] private LevelGatesNode _gates; // 阶段物理门。
    [Export] private LevelSpawnerNode _spawner; // 配方调度和实体所有权。
    [Export] private LevelCameraNode _camera; // 取景和抵达事件。
    [Export] private LevelExitNode _exit; // 可选出口；原版无出口的终关在演出/最终清场后自动结算。
    [Export] private FloatingTextPresenter _floatingText; // 本场飘字表现。
    [Export] private string[] _featuredOpponentSpawnPointIds = []; // 使用顶部生命条的生成点；仅影响场景展示。

    private IArchitecture _architecture; // 借用流程 FSM 的入口，初始化绑定后不跨作用域转移。
    private AbstractHeroEntity _player; // 借用流程显示并隐藏的英雄。
    private bool _sessionActive; // 当前节点订阅是否已建立。
    private HeroDeathPresentation _heroDeathPresentation; // 原生死亡演出阶段；只在本关流程帧提交完成。

    /// <summary>
    /// 守关对手显示、死亡或解除绑定，HUD 据此刷新生命条。
    /// </summary>
    public event Action FeaturedOpponentChanged;

    /// <summary>
    /// 实体显示失败，交给会话拥有者结束关卡并报告错误。
    /// </summary>
    public event Action<Exception> Failed;

    /// <summary>
    /// 交给关卡流程播放的音乐绑定。
    /// </summary>
    public SoundId MusicId => _musicId;

    /// <summary>
    /// 本局唯一状态源。
    /// </summary>
    private LevelModel LevelModel => this.GetModel<LevelModel>();

    /// <summary>
    /// 按主键借用共享定义。
    /// </summary>
    private LevelConfig Config => this.GetModel<ConfigurationModel>().Tables.TbLevelConfig.Get(_levelId);

    /// <summary>
    /// 本阶段指定展示的普通怪物；死亡或离开会话后为空。
    /// </summary>
    public AbstractEnemyEntity FeaturedOpponent { get; private set; }

    /// <summary>
    /// 玩家出生点的世界坐标，注册初始化完成后可读。
    /// </summary>
    public Vector2 PlayerSpawnPosition => _spawnPoints.PositionOf(Config.PlayerSpawnPointId);

    /// <summary>
    /// 把游戏帧交给本局状态；暂停后不消费本帧已经采样的旧delta。
    /// </summary>
    /// <param name="delta">游戏秒数。</param>
    public override void _Process(double delta)
    {
        if (_sessionActive && !GF.Base.IsGamePaused)
        {
            this.GetSystem<LevelSystem>().AdvanceTime(delta);
            TickLoot((float)delta);
            AdvanceEntrance();
            if (_heroDeathPresentation == HeroDeathPresentation.Completed && LevelModel.Outcome.Value == LevelOutcome.Running)
            {
                _heroDeathPresentation = HeroDeathPresentation.None;
                Complete(LevelOutcome.Defeated);
            }
        }
    }

    /// <summary>
    /// 返回本场景装配时绑定的入口。
    /// </summary>
    /// <returns>所属游戏架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => _architecture ?? throw new InvalidOperationException("关卡 ViewController 尚未初始化。");

    /// <summary>
    /// 绑定游戏入口，校验场景身份并初始化场景节点。
    /// </summary>
    /// <param name="architecture">所属游戏入口。</param>
    /// <exception cref="InvalidOperationException">绑定缺失、配置不存在、场景不匹配或管理器校验失败时抛出。</exception>
    public void Initialize(IArchitecture architecture)
    {
        _architecture = architecture ?? throw new ArgumentNullException(nameof(architecture));
        if (_spawnPoints == null || _gates == null || _spawner == null || _camera == null ||
            _floatingText == null)
        {
            throw new InvalidOperationException("关卡必须绑定 SpawnPoints、Gates、Spawner、Camera 和 FloatingText。");
        }

        this.GetSystem<LevelSystem>().Prepare(_levelId);
        InitializeEntrance();
        if (!string.Equals(Config.ScenePath, SceneFilePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"关卡场景不匹配：{Config.ScenePath} / {SceneFilePath}");
        }

        // 初始化不订阅玩法事件；失败仍由流程完整卸载场景。
        _spawnPoints.Initialize(Config);
        _gates.Initialize(Config, _camera.SceneLeft, _camera.SceneRight, _camera.ViewSize.X);
        if (_triggers == null && Config.Stages.Exists(x => x.Activation == StageActivation.Trigger))
        {
            throw new InvalidOperationException("Trigger 阶段必须绑定触发器集合。");
        }

        _triggers?.Initialize(Config);
        _spawner.Initialize(Config, _spawnPoints);
        LevelSpawnRecipe[] recipes = Config.Stages.SelectMany(stage => stage.Recipes).ToArray();
        // 出口目标必须是末阶段唯一的一只实体，避免重复流误开放出口。
        string exitTarget = _levelId == BatCaveRules.LevelId ? BatCaveRules.StatueSpawnPointId : null;
        if (!string.IsNullOrEmpty(exitTarget) && _exit == null)
        {
            throw new InvalidOperationException("指定出口目标的关卡必须绑定出口节点。");
        }

        if (!string.IsNullOrEmpty(exitTarget) &&
                    (recipes.Count(recipe => recipe.SpawnPointId == exitTarget) != 1 ||
                     !Config.Stages[^1].Recipes.Exists(recipe => recipe.SpawnPointId == exitTarget && recipe.Count == 1)))
        {
            throw new InvalidOperationException($"出口目标必须引用末阶段唯一、数量为一的配方：{exitTarget}");
        }

        foreach (string id in _featuredOpponentSpawnPointIds)
        {
            if (string.IsNullOrWhiteSpace(id) || _featuredOpponentSpawnPointIds.Count(value => value == id) != 1 ||
                recipes.Count(recipe => recipe.SpawnPointId == id) != 1 ||
                recipes.Single(recipe => recipe.SpawnPointId == id).Count != 1)
            {
                throw new InvalidOperationException($"顶部生命条必须绑定唯一、仅生成一只怪物的生成点：{id}");
            }
        }
    }

    /// <summary>
    /// 在玩家显示、加载界面关闭后开放玩法事件与相机跟随。
    /// </summary>
    /// <param name="entities">关卡流程注入的实体服务。</param>
    /// <param name="player">本会话的玩家节点。</param>
    /// <param name="cancellationToken">流程离开时取消在途实体显示。</param>
    public void StartSession(EntityComponent entities, AbstractHeroEntity player,
        CancellationToken cancellationToken)
    {
        if (_sessionActive)
        {
            throw new InvalidOperationException("关卡会话已经开始。");
        }

        _player = player;
        _heroDeathPresentation = HeroDeathPresentation.None;
        this.GetSystem<LevelSystem>().StartSession(this.GetModel<ProfileModel>().HeroIds);
        _exitRequested = false;
        _lootRandom.Randomize();
        _player.Died += OnHeroDied;
        _player.HealthRestored += OnHeroHealthRestored;
        this.GetModel<ProfileModel>().GetSlots(_player.HeroId).Register(OnEquipmentChanged);
        LevelModel.Outcome.Register(OnSessionEnded);
        _sessionActive = true;
        GF.Event.Subscribe(DamageDealtEventArgs.EventId, OnDamageDealt);
        StartEntrance();
        _spawner.StartSession(entities, _camera, cancellationToken);
        _floatingText.StartSession(OwnsActor);
        _spawner.StageCleared += OnStageCleared;
        _spawner.EnemyDefeated += OnEnemyDefeated;
        _spawner.EnemyShown += OnEnemyShown;
        if (_exit != null)
        {
            _exit.Entered += OnExitContact;
        }

        _spawner.SpawnFailed += OnSpawnFailed;
        _camera.RightBoundaryReached += OnCameraArrived;
        if (_triggers != null)
        {
            _triggers.Activated += OnTriggerContact;
        }

        _camera.Follow(player);
        BeginTravel();
    }

    /// <summary>
    /// 停止信号、调度和跟随并清理本关实体；初始化失败与流程离开时可重复调用。
    /// </summary>
    public void Cleanup()
    {
        if (_sessionActive)
        {
            GF.Event.Unsubscribe(DamageDealtEventArgs.EventId, OnDamageDealt);
        }

        _floatingText?.StopSession();
        StopEntrance();
        UnsubscribeProgressEvents();
        if (_architecture != null)
        {
            this.GetSystem<LevelSystem>()?.EndSession();
        }

        ClearLoot();
        _sessionActive = false;

        if (_triggers != null)
        {
            _triggers.Activated -= OnTriggerContact;
            _triggers.EndWatching();
        }

        // 初始化可能在任一绑定处失败；清理只依赖本场景已取得的节点。
        if (_camera != null)
        {
            _camera.RightBoundaryReached -= OnCameraArrived;
            _camera.StopFollowing();
        }
        if (_exit != null)
        {
            _exit.Entered -= OnExitContact;
            _exit.Disable();
        }
        ClearFeaturedOpponent();
        _heroDeathPresentation = HeroDeathPresentation.None;
        if (_spawner != null)
        {
            _spawner.StageCleared -= OnStageCleared;
            _spawner.EnemyDefeated -= OnEnemyDefeated;
            _spawner.EnemyShown -= OnEnemyShown;
            _spawner.SpawnFailed -= OnSpawnFailed;
            _spawner.StopSession();
        }
        _player = null;
    }

    /// <summary>
    /// 飘字只接收本场玩家及本关刷怪服务拥有的运行实体。
    /// </summary>
    /// <param name="entityId">伤害发生时复制的受击实体编号。</param>
    /// <returns>是否归属本会话。</returns>
    private bool OwnsActor(int entityId) => _player?.Id == entityId || _spawner.OwnsEntity(entityId);

    /// <summary>
    /// 只把当前玩家对本关敌人的有效命中交给连击规则；闪避和无攻击实体的持续扣血不累计。
    /// </summary>
    /// <param name="sender">本次伤害通知的发送者，不保存。</param>
    /// <param name="args">只在分发期间读取的池化伤害结果。</param>
    private void OnDamageDealt(object sender, GameEventArgs args)
    {
        if (!_sessionActive || args is not DamageDealtEventArgs damage ||
            damage.AttackerEntityId != _player?.Id || damage.TargetIsHero || damage.Result.IsMiss ||
            !_spawner.OwnsEntity(damage.TargetEntityId))
        {
            return;
        }

        this.GetSystem<LevelSystem>().RecordComboHit();
    }

    /// <summary>
    /// 清波后开放当前阶段通路并设置相机右界，特殊阶段同时启用触发区监听。
    /// </summary>
    private void BeginTravel()
    {
        LevelStage stage = LevelModel.Sequence.Current;
        _gates.BeginTravel(stage.StageOrder);
        _camera.TravelTo(_gates.BoundsOf(stage.StageOrder).Right);
        if (stage.Activation == StageActivation.Trigger)
        {
            _triggers.BeginWatching(stage.TriggerId);
        }
    }

    /// <summary>
    /// 普通阶段由实际相机右缘抵达启动；特殊阶段继续等待区域事件。
    /// </summary>
    private void OnCameraArrived()
    {
        if (LevelModel.Sequence.Current.Activation == StageActivation.CameraArrived)
        {
            StartBattle();
        }
    }

    /// <summary>
    /// 先迁移阶段状态并锁定物理区域，再提交当前阶段的并行配方调度。
    /// </summary>
    private void StartBattle()
    {
        this.GetSystem<LevelSystem>().StartBattle();
        LevelStage stage = LevelModel.Sequence.Current;
        _gates.LockRegion(stage.StageOrder);
        _camera.LockLeft(_gates.BoundsOf(stage.StageOrder).Left);
        _spawner.StartStage(stage);
    }

    /// <summary>
    /// 清波后推进阶段；有出口时等待交互，无出口终关在最终演出和飞行奖励完成后结算。
    /// </summary>
    private void OnStageCleared()
    {
        if (LevelModel.Sequence.Phase != LevelStagePhase.Fighting)
        {
            return; // 目标死亡可能已开放出口，清波不能再次推进。
        }

        _gates.ReleaseRegion(LevelModel.Sequence.Current.StageOrder);
        this.GetSystem<LevelSystem>().ClearBattle();
        if (LevelModel.Sequence.Phase == LevelStagePhase.Travelling)
        {
            BeginTravel();
        }
        else
        {
            if (_exit == null)
            {
                OnExitEntered();
            }
            else
            {
                _exit.Enable();
            }
        }
    }

    /// <summary>
    /// 出口交互或无出口终关的最终清场请求通关，等待飞行奖励后提交。
    /// </summary>
    private void OnExitEntered()
    {
        if (LevelModel.Sequence.Phase != LevelStagePhase.AwaitingExit || !_player.IsAlive)
        {
            return;
        }
        // 已按 W 时等最后的球实际接触再保存，避免把尚未拾取的收益丢在卸载场景中。
        _exitRequested = true;
        if (_lootOrbs.Count != 0)
        {
            return;
        }

        Complete(LevelOutcome.Cleared);
    }

    /// <summary>
    /// 只接受本会话英雄的区域接触，先停止监听再开始战斗。
    /// </summary>
    /// <param name="hero">触发区检测到的英雄。</param>
    private void OnTriggerContact(AbstractHeroEntity hero)
    {
        if (!_sessionActive || !ReferenceEquals(hero, _player) || !hero.IsAlive)
        {
            return;
        }

        _triggers.EndWatching();
        StartBattle();
    }

    /// <summary>
    /// 只接受本会话英雄的出口交互，拒绝的接触不消耗出口。
    /// </summary>
    /// <param name="hero">出口检测到的英雄。</param>
    private void OnExitContact(AbstractHeroEntity hero)
    {
        if (!_sessionActive || !ReferenceEquals(hero, _player) || !hero.IsAlive)
        {
            return;
        }

        _exit.Disable();
        OnExitEntered();
    }

    /// <summary>
    /// 按场景生成点绑定需展示的怪物，不改变它的战斗行为。
    /// </summary>
    /// <param name="monster">已经归属本关的怪物。</param>
    /// <param name="spawnPointId">本次配方使用的生成点。</param>
    private void OnEnemyShown(AbstractEnemyEntity monster, string spawnPointId)
    {
        if (!_featuredOpponentSpawnPointIds.Contains(spawnPointId))
        {
            return;
        }

        ClearFeaturedOpponent();
        FeaturedOpponent = monster;
        monster.Died += OnFeaturedOpponentDied;
        FeaturedOpponentChanged?.Invoke();
    }

    /// <summary>
    /// 守关对手死亡后撤销展示；其余怪物仍由调度器判定清场。
    /// </summary>
    /// <param name="actor">已死亡的守关对手。</param>
    private void OnFeaturedOpponentDied(AbstractActorEntity actor) => ClearFeaturedOpponent();

    /// <summary>
    /// 解除对手事件，避免池复用或关卡离开后残留旧生命条。
    /// </summary>
    private void ClearFeaturedOpponent()
    {
        if (FeaturedOpponent == null)
        {
            return;
        }

        FeaturedOpponent.Died -= OnFeaturedOpponentDied;
        FeaturedOpponent = null;
        FeaturedOpponentChanged?.Invoke();
    }

    /// <summary>
    /// 显示失败属于会话所有者，先清理再上报，不留下锁死的战斗区域。
    /// </summary>
    /// <param name="error">实体显示抛出的异常。</param>
    private void OnSpawnFailed(Exception error)
    {
        Cleanup();
        Failed?.Invoke(error);
    }

    /// <summary>
    /// 英雄死亡立即回收未领取奖励，墓碑演出结束后提交失败结算。
    /// </summary>
    /// <param name="actor">本关英雄。</param>
    private void OnHeroDied(AbstractActorEntity actor)
    {
        if (!_sessionActive || !ReferenceEquals(actor, _player) || _heroDeathPresentation != HeroDeathPresentation.None)
        {
            return;
        }

        _heroDeathPresentation = HeroDeathPresentation.Playing;
        ClearLoot(); // 未领取奖励在死亡时回收，失败界面仍等待墓碑演出。
        _player.DeathPresentationFinished += OnHeroDeathPresentationFinished;
    }

    /// <summary>
    /// 原生死亡片段结束时只登记完成，下一流程帧提交失败，避免信号遍历中清理场景。
    /// </summary>
    private void OnHeroDeathPresentationFinished()
    {
        if (!_sessionActive || _heroDeathPresentation != HeroDeathPresentation.Playing)
        {
            return;
        }

        _player.DeathPresentationFinished -= OnHeroDeathPresentationFinished;
        _heroDeathPresentation = HeroDeathPresentation.Completed;
    }

    /// <summary>
    /// 传递结局时生命比例，不把实体传入业务模块。
    /// </summary>
    /// <param name="outcome">本局结局。</param>
    private void Complete(LevelOutcome outcome)
    {
        this.GetSystem<LevelSystem>().Complete(outcome,
        (int)Math.Round(100.0 * this.GetModel<HeroModel>().Hp.Value / this.GetModel<HeroModel>().MaxHp.Value));
    }

    /// <summary>
    /// 模型结局确定后停止奖励表现和订阅。
    /// </summary>
    /// <param name="outcome">已确定的结局。</param>
    private void OnSessionEnded(LevelOutcome outcome)
    {
        if (outcome == LevelOutcome.Running)
        {
            return;
        }

        ClearLoot();
        UnsubscribeProgressEvents();
    }

    /// <summary>
    /// 装备变化后刷新节点属性；业务模块不持有英雄。
    /// </summary>
    /// <param name="slots">已经提交的穿戴快照。</param>
    private void OnEquipmentChanged(EquippedItems slots) => _player.RefreshAppearance();

    /// <summary>
    /// 解除本关模型、英雄和原生演出订阅，关停先核对节点有效性。
    /// </summary>
    public void UnsubscribeProgressEvents()
    {
        if (!_sessionActive)
        {
            return;
        }

        _player.Died -= OnHeroDied;
        _player.HealthRestored -= OnHeroHealthRestored;
        if (_heroDeathPresentation == HeroDeathPresentation.Playing)
        {
            _player.DeathPresentationFinished -= OnHeroDeathPresentationFinished;
        }

        _heroDeathPresentation = HeroDeathPresentation.None;
        this.GetModel<ProfileModel>().GetSlots(_player.HeroId).UnRegister(OnEquipmentChanged);
        LevelModel.Outcome.UnRegister(OnSessionEnded);
    }

    /// <summary>
    /// 本关拥有的死亡演出阶段，不另建与动画竞争的秒数计时器。
    /// </summary>
    private enum HeroDeathPresentation
    {
        None,
        Playing,
        Completed,
    }
}
