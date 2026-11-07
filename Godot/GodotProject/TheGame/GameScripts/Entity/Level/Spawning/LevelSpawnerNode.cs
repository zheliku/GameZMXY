using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameConfig.Enemy;
using GameConfig.Entity;
using GameConfig.Level;
using GameFramework.Entity;
using GameFramework.Event;
using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Enemies;
using GameLogic.Event.Combat;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;

namespace GameLogic.Level;

/// <summary>
/// 执行当前阶段的生成调度，持有关卡实体所有权并依据死亡事件判定清波。
/// 只报告"本关怪物被击败"这一事实；经验、掉落等收益由关卡运行结算，不在这里查表。
/// </summary>
public partial class LevelSpawnerNode : Node
{
    private readonly HashSet<int> _ownedIds = new(); // 本关创建的全部运行实体 ID，含死亡动画中的实体。
    private readonly Dictionary<int, string> _activeEnemies = new(); // 存活实体及其配方生成点；移除成功才报告一次击败。
    private readonly Dictionary<int, string> _transformingEnemies = new(); // 已结算死亡但尚待后继显示的形态，继续占用原配方名额。
    private LevelSpawnPointsNode _spawnPoints; // 初始化阶段校验过的生成点目录。
    private EntityComponent _entities; // 会话注入的实体服务。
    private CancellationTokenSource _cancellation; // 停止会话时立即作废在途显示结果。
    private LevelSpawnSchedule _schedule; // 唯一当前阶段的生成计划。
    private bool _sessionActive; // 会话已注入实体服务并订阅死亡事件。
    private Node2D _patrolAnchor; // 当前相机中心，供场外出生的怪物朝战斗视野巡逻。

    /// <summary>
    /// 当前阶段配方发完且存活与在途名额归零，只报告一次。
    /// </summary>
    public event Action StageCleared;

    /// <summary>
    /// 本会话怪物死亡且首次归还名额时同步报告一次；死亡参数只在当前回调期间有效，禁止保存引用。
    /// </summary>
    public event Action<EnemyDiedEventArgs, string> EnemyDefeated;

    /// <summary>
    /// 本会话怪物已显示并登记所有权，供关卡绑定守关展示。
    /// </summary>
    public event Action<AbstractEnemyEntity, string> EnemyShown;

    /// <summary>
    /// 当前会话显示实体失败；控制器负责结束会话。
    /// </summary>
    public event Action<Exception> SpawnFailed;

    /// <summary>
    /// 初始禁用游戏帧调度，加载期不生成实体。
    /// </summary>
    public override void _Ready() => SetProcess(false);

    /// <summary>
    /// 提交到期配方，统一预约名额；暂停时由场景树停止游戏时间。
    /// </summary>
    /// <param name="delta">本帧经过的游戏秒数。</param>
    public override void _Process(double delta)
    {
        _schedule.Advance(delta);
        while (_schedule.TryTake(out LevelSpawnRecipe recipe))
        {
            _ = ShowEnemyAsync(recipe, _schedule, _entities, _cancellation.Token);
            if (!IsProcessing())
            {
                // 池化显示可能同步清波并停止调度，本帧不再提交剩余配方。
                return;
            }
        }
    }

    /// <summary>
    /// 判断运行实体是否由本会话创建，包含仍在播放死亡动画的怪物。
    /// </summary>
    /// <param name="entityId">GGF 本次显示分配的运行实体编号。</param>
    /// <returns>是否仍归本会话所有。</returns>
    public bool OwnsEntity(int entityId) => _ownedIds.Contains(entityId);

    /// <summary>
    /// 在初始化边界验证配方和怪物实体配置，运行时不重复查表。
    /// </summary>
    /// <param name="config">本场景对应的关卡配置。</param>
    /// <param name="spawnPoints">初始化阶段已校验的生成点目录。</param>
    /// <exception cref="InvalidOperationException">配方缺失、数值非法或怪物外键不唯一时抛出。</exception>
    public void Initialize(LevelConfig config, LevelSpawnPointsNode spawnPoints)
    {
        _spawnPoints = spawnPoints;
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

                EnemyConfig monster =
                    GF.Fsm.GetModel<ConfigurationModel>().Tables.TbEnemyConfig.DataList.SingleOrDefault(x =>
                        x.EntityId == recipe.EnemyEntityId);
                if (monster == null ||
                    GF.Fsm.GetModel<ConfigurationModel>().Tables.TbEntityConfig.DataList.Count(x => x.EntityId == monster.EntityId) !=
                    1)
                {
                    throw new InvalidOperationException($"怪物配方必须关联有效配置和唯一实体资源：{recipe.EnemyEntityId}");
                }
            }
        }
    }

    /// <summary>
    /// 创建会话所有权并连接死亡事件；不启用调度，不创建任何怪物。
    /// </summary>
    /// <param name="entities">关卡流程注入的实体服务。</param>
    /// <param name="patrolAnchor">当前取景中心。</param>
    /// <param name="cancellationToken">流程离开时取消在途实体显示。</param>
    public void StartSession(EntityComponent entities, Node2D patrolAnchor, CancellationToken cancellationToken)
    {
        _entities = entities;
        _patrolAnchor = patrolAnchor;
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _sessionActive = true;
        GF.Event.Subscribe(EnemyDiedEventArgs.EventId, OnEnemyDied);
    }

    /// <summary>
    /// 启动当前阶段的生成计划，延迟按游戏帧计时。
    /// </summary>
    /// <param name="stage">已开战的当前阶段。</param>
    public void StartStage(LevelStage stage)
    {
        _schedule = new LevelSpawnSchedule(stage);
        SetProcess(true);
    }

    /// <summary>
    /// 停止生成、取消在途登记、退订死亡事件并清理本关所有实体；重复调用直接返回。
    /// </summary>
    public void StopSession()
    {
        if (!_sessionActive)
        {
            return; // 未开始或已经停止的会话没有资源需要释放。
        }

        _sessionActive = false;
        SetProcess(false);
        // 停止边界先取消，再释放所有权；异步结果使用捕获的旧服务和令牌自行回收。
        _cancellation.Cancel();
        _cancellation.Dispose();
        _cancellation = null;
        GF.Event.Unsubscribe(EnemyDiedEventArgs.EventId, OnEnemyDied);
        foreach (int id in _ownedIds)
        {
            _entities.HideEntitySafe(id);
        }

        _ownedIds.Clear();
        _activeEnemies.Clear();
        _transformingEnemies.Clear();
        _schedule = null;
        _entities = null;
        _patrolAnchor = null;
    }

    /// <summary>
    /// 显示一只配方怪物并登记所有权；使用捕获的会话快照，避免卸载后访问已清理字段。
    /// </summary>
    /// <param name="recipe">已预约名额的生成配方。</param>
    /// <param name="schedule">提交该配方的调度，失败时归还名额。</param>
    /// <param name="entities">本次显示使用的实体服务快照。</param>
    /// <param name="cancellationToken">会话取消令牌，取消后晚到的结果由本方法回收。</param>
    /// <returns>显示与登记完成的异步任务。</returns>
    private async Task ShowEnemyAsync(LevelSpawnRecipe recipe, LevelSpawnSchedule schedule,
        EntityComponent entities, CancellationToken cancellationToken)
    {
        try
        {
            // 等待前捕获空间位置，旧会话结果不再读取可替换的索引。
            Vector2 spawnPosition = _spawnPoints.PositionOf(recipe.SpawnPointId);
            Node2D patrolAnchor = _patrolAnchor;
            // 显示 API 可能从池中同步返回，名额已由调度器提前预约。
            EnemySpawn spawn = new(spawnPosition, error =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        SpawnFailed?.Invoke(error);
                    }
                }, (previousId, nextId, nextSpawn) =>
                                    ContinueEncounterAsync(previousId, nextId, nextSpawn, schedule, entities, patrolAnchor, cancellationToken));
            IEntity entity = await entities.ShowEntityAsync(recipe.EnemyEntityId, spawn);
            if (entity == null)
            {
                throw new InvalidOperationException($"怪物显示失败：{recipe.EnemyEntityId}");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                entities.HideEntitySafe(entity.Id);
                return;
            }

            _ownedIds.Add(entity.Id);
            _activeEnemies.Add(entity.Id, recipe.SpawnPointId);
            ((AbstractEnemyEntity)entity).SetPatrolAnchor(patrolAnchor);
            EnemyShown?.Invoke((AbstractEnemyEntity)entity, recipe.SpawnPointId);
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

    /// <summary>
    /// 用后继运行实体接替已死亡形态，原配方名额在异步等待期间保持预约。
    /// </summary>
    /// <param name="previousId">本次死亡形态的运行编号。</param>
    /// <param name="nextId">具体敌人状态选择的后继资源身份。</param>
    /// <param name="spawn">后继形态的出生参数。</param>
    /// <param name="schedule">原配方调度。</param>
    /// <param name="entities">原会话实体服务。</param>
    /// <param name="patrolAnchor">原会话取景中心。</param>
    /// <param name="cancellationToken">原会话取消令牌。</param>
    /// <returns>交接登记完成；失败由调用形态经原会话失败出口上报。</returns>
    private async Task ContinueEncounterAsync(int previousId, EntityId nextId, EnemySpawn spawn,
        LevelSpawnSchedule schedule, EntityComponent entities, Node2D patrolAnchor, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (!_transformingEnemies.Remove(previousId, out string spawnPointId))
        {
            throw new InvalidOperationException($"后继形态没有待交接的配方所有权：{previousId}");
        }

        // 捕获原周期的回调，不能让新形态在换关后引用下一局的服务。
        EnemySpawn nextSpawn = spawn with
        {
            SpawnFailed = error =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    SpawnFailed?.Invoke(error);
                }
            },
            ContinueEncounterAsync = (id, entityId, data) =>
                ContinueEncounterAsync(id, entityId, data, schedule, entities, patrolAnchor, cancellationToken),
        };
        IEntity entity = await entities.ShowEntityAsync(nextId, nextSpawn);
        if (entity == null)
        {
            throw new InvalidOperationException($"后继形态显示失败：{nextId}");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            entities.HideEntitySafe(entity.Id);
            return;
        }

        _ownedIds.Add(entity.Id);
        _activeEnemies.Add(entity.Id, spawnPointId);
        ((AbstractEnemyEntity)entity).SetPatrolAnchor(patrolAnchor);
        EnemyShown?.Invoke((AbstractEnemyEntity)entity, spawnPointId);
    }

    /// <summary>
    /// 存活实体死亡时归还名额；配方与名额都清空后报告本阶段清波。
    /// </summary>
    /// <param name="sender">事件源，不使用。</param>
    /// <param name="args">死亡事件参数，只在回调生命周期内读取运行 ID。</param>
    private void OnEnemyDied(object sender, GameEventArgs args)
    {
        if (args is EnemyDiedEventArgs died && _activeEnemies.Remove(died.EntityId, out string spawnPointId))
        {
            if (died.EncounterComplete)
            {
                _schedule.Release();
            }
            else
            {
                _transformingEnemies.Add(died.EntityId, spawnPointId);
            }

            EnemyDefeated?.Invoke(died, spawnPointId);
            if (!_sessionActive)
            {
                return; // 掉落显示失败可能已让模块同步结束会话。
            }

            if (_schedule.IsCleared)
            {
                // 先停调度再分发，控制器可以同步开放下一段通路。
                SetProcess(false);
                StageCleared?.Invoke();
            }
        }
    }
}
