using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameConfig;
using GameConfig.Entity;
using GameFramework;
using GameFramework.Entity;
using GameFramework.Procedure;
using GameLogic;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Attributes;
using GameLogic.Battle.Damage;
using GameLogic.Config;
using GameLogic.Drops;
using GameLogic.Enemies;
using GameLogic.Enemies.AI;
using GameLogic.Equipment;
using GameLogic.FloatingText;
using GameLogic.Heroes;
using GameLogic.Input;
using GameLogic.Profile;
using GameLogic.Save;
using GameLogic.Skills;
using GameLogic.Utility;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using GodotGameFramework.NodePool;
using GodotGameFramework.Sound;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 怪物与角色测试场地（独立场景，不进入游戏流程）。
///
/// 用法：在 Godot 里对 <c>test_arena.tscn</c> 按 F6“运行当前场景”即可，不需要改流程或配置表。
/// 场景内自带一份**真实的** <c>Framework/GameFramework.tscn</c>（去掉 Procedure 流程），由测试入口直接装配资源组和临时 FSM；
/// 将怪物实体场景拖进 <c>Monsters</c> 容器、摆好位置即可测试，不读取玩家档案。
///
/// 占位节点只提供“哪只怪、放在哪”，真正的实体仍由 <see cref="GF.Entity"/> 池化创建，生命周期与正式关卡一致。
/// </summary>
public partial class TestArenaController : Node2D
{
    [Export] private PackedScene _frameworkScene; // 真实框架场景；仅去掉 Procedure 流程后复用。

    [Export] private EntityId _heroEntityId = EntityId.Wukong; // 测试用玩家实体枚举。

    [Export] private Node2D _playerStart; // 玩家出生点（Marker2D）。

    [Export] private Node2D _enemies; // 怪物占位容器：拖入怪物实体场景即可测试。

    [Export] private Camera2D _camera; // 跟随玩家的测试相机；限位由场景 Camera2D 设置。
    [Export] private FloatingTextPresenter _floatingText; // 与正式关卡共用的飘字组件。

    private Node2D _hero; // 已显示的玩家节点，供相机跟随。
    private float _cameraY; // 相机固定的纵向位置。
    private readonly HashSet<int> _ownedIds = new(); // 只接受本场地创建的实体反馈，退出时按编号隐藏。
    private CancellationTokenSource _cancellation; // 场地退出后不再登记晚到的显示结果。
    private ProcedureOwner _testFlow; // 独立 F6 场地拥有的原生 FSM；正常游戏内运行时借用现有主 FSM。
    private EntityComponent _entities; // 本次启动捕获的实体服务。
    private EventComponent _events; // 独立场地退出时，在框架关闭前触发所属会话清理。

    /// <summary>
    /// 场景单独卸载时结束本场地会话；整体关停时事件组件已提前触发同一清理。
    /// </summary>
    public override void _ExitTree() => StopSession();

    /// <summary>
    /// 结束场地拥有的实体、订阅与业务作用域；服务先退出或场景单独卸载时均可调用。
    /// </summary>
    private void StopSession()
    {
        if (IsInstanceValid(_events))
        {
            _events.TreeExiting -= StopSession;
        }
        _events = null;
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
        _floatingText.StopSession();
        if (IsInstanceValid(_entities))
        {
            foreach (int id in _ownedIds)
            {
                _entities.HideEntitySafe(id);
            }
        }

        _ownedIds.Clear();
        _entities = null;
        if (_testFlow != null)
        {
            _testFlow.RemoveSystem<HeroSystem>();
            _testFlow.RemoveSystem<HeroStatusSystem>();
            _testFlow.RemoveModel<HeroModel>();
            GF.Fsm.DestroyFsm(_testFlow);
        }
        _testFlow = null;
    }

    /// <summary>
    /// 准备框架、取走怪物占位节点，然后异步启动测试场地。
    /// </summary>
    public override void _Ready()
    {
        _cameraY = _camera.GlobalPosition.Y;
        // 独立 F6 运行时复用真实框架，但不启动正式流程。
        if (GameEntry.GetComponent<EntityComponent>() == null)
        {
            Node framework = _frameworkScene.Instantiate();
            if (framework.GetNodeOrNull("Procedure") is Node procedure)
            {
                framework.RemoveChild(procedure);
                procedure.Free();
            }
            AddChild(framework);
            // 子节点按逆序退出，框架最后释放，保证飘字回收时节点池仍存活。
            MoveChild(framework, 0);
        }

        // 测试侧直接建立分组，不为 F6 在正式流程增加注册包装。
        foreach (EntityGroup group in GF.Entity.EntityGroupRes.EntityGroups)
        {
            if (!GF.Entity.HasEntityGroup(group.Name) && !GF.Entity.AddEntityGroup(group.Name, group.ReleaseInterval,
                    group.Capacity, group.ExpireTime, group.Priority))
            {
                throw new InvalidOperationException($"实体组 {group.Name} 创建失败。");
            }
        }
        foreach (UIGroup group in GF.UI.UIGroupRes.Groups)
        {
            if (!GF.UI.HasUIGroup(group.Name) && !GF.UI.AddUIGroup(group.Name, group.Depth))
            {
                throw new InvalidOperationException($"UI 组 {group.Name} 创建失败。");
            }
        }
        foreach (SoundGroup group in GF.Sound.SoundGroupRes.SoundGroups)
        {
            if (!GF.Sound.HasSoundGroup(group.Name) && !GF.Sound.AddSoundGroup(group.Name, group.AgentCounts,
                    group.AvoidBeingReplacedBySamePriority))
            {
                throw new InvalidOperationException($"声音组 {group.Name} 创建失败。");
            }
        }
        GF.Sound.SetVolume(SoundComponent.DefaultMusicGroup, GF.Setting.GetFloat(SoundComponent.DefaultMusicGroup, 1));
        GF.Sound.SetVolume(SoundComponent.DefaultSfxGroup, GF.Setting.GetFloat(SoundComponent.DefaultSfxGroup, 1));
        GF.Sound.SetVolume(SoundComponent.DefaultUiGroup, GF.Setting.GetFloat(SoundComponent.DefaultUiGroup, 1));
        GF.Debugger.ActiveWindow = false;
        NodePool.Instance.Active();
        LayerMask.Instance.Active();
        _entities = GF.Entity;
        // F6 不运行 Procedure；由场地自己在 Event 退出、Base 关闭模块前释放业务作用域。
        _events = GF.Event;
        _events.TreeExiting += StopSession;
        _cancellation = new CancellationTokenSource();
        List<(EntityId Id, Vector2 Position)> monsters = TakeMonsterPlaceholders();
        _ = StartAsync(monsters, _entities, _cancellation.Token);
    }

    /// <summary>
    /// 相机水平跟随玩家；纵向保持场景初始高度，限位交给场景 Camera2D。
    /// </summary>
    /// <param name="delta">距上一帧的秒数，不使用。</param>
    public override void _Process(double delta)
    {
        if (_hero == null || !IsInstanceValid(_hero))
        {
            return;
        }

        _camera.GlobalPosition = new Vector2(_hero.GlobalPosition.X, _cameraY);
    }

    /// <summary>
    /// 等框架组件就绪后显示玩家与全部占位怪物。
    /// </summary>
    /// <param name="monsters">占位节点提供的怪物枚举与出生坐标。</param>
    /// <param name="entities">本场地捕获的实体服务。</param>
    /// <param name="cancellationToken">场地退出时取消。</param>
    /// <returns>启动完成的异步任务。</returns>
    private async Task StartAsync(List<(EntityId Id, Vector2 Position)> monsters, EntityComponent entities,
        CancellationToken cancellationToken)
    {
        try
        {
            // 等一帧，确保框架组件的 OnEnter 已完成。
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            cancellationToken.ThrowIfCancellationRequested();
            if (!GF.Fsm.HasFsm<IProcedureManager>())
            {
                _testFlow = GF.Fsm.CreateFsm<IProcedureManager>(GameFrameworkEntry.GetModule<IProcedureManager>(), new ArenaState());
                IArchitecture architecture = _testFlow.InitializeArchitecture();
                _testFlow.RegisterUtility(new ArchiveUtility<GameCatalogue, GameData>(GF.Archive));
                _testFlow.RegisterUtility(new SpatialUtility());
                _testFlow.RegisterUtility(new SaveDataUtility());
                _testFlow.RegisterUtility(new ConfigIdUtility());
                _testFlow.RegisterModel(new ConfigurationModel(ConfigSystem.Instance.Tables));
                _testFlow.RegisterUtility(new ExperienceCurveUtility(_testFlow.GetModel<ConfigurationModel>().Tables.TbHeroLevelConfig.DataList));
                _testFlow.RegisterModel(new SaveModel());
                int heroId = ConfigSystem.Instance.Tables.TbHeroConfig.DataList.First(x => x.EntityId == _heroEntityId).Id;
                _testFlow.RegisterModel(new ProfileModel([(heroId, 0)], _testFlow.GetUtility<ExperienceCurveUtility>(),
                    ConfigSystem.Instance.Tables.TbProfileConfig.EquipmentCapacity));
                _testFlow.RegisterModel(new HeroModel());
                _testFlow.RegisterModel(new SkillModel(_testFlow.GetModel<ProfileModel>().HeroIds));
                _testFlow.RegisterSystem(new ConfigurationSystem(architecture));
                _testFlow.RegisterSystem(new CombatSystem(architecture));
                _testFlow.RegisterSystem(new InputSystem(architecture));
                _testFlow.RegisterSystem(new EnemyAiSystem(architecture));
                _testFlow.RegisterSystem(new LootSystem(architecture));
                _testFlow.RegisterSystem(new ProfileSystem(architecture));
                _testFlow.RegisterSystem(new EquipmentSystem(architecture));
                _testFlow.RegisterSystem(new SkillSystem(architecture));
                _testFlow.RegisterSystem(new HeroStatusSystem(architecture));
                _testFlow.RegisterSystem(new HeroSystem(architecture));
                _testFlow.RegisterSystem(new SaveSystem(architecture));
            }
            _floatingText.StartSession(_ownedIds.Contains);
            await SpawnHeroAsync(entities, cancellationToken);
            foreach ((EntityId id, Vector2 position) in monsters)
            {
                await ShowOwnedEntityAsync(entities, id, new EnemySpawn(position), cancellationToken);
                Log.Info("[TestArena] 显示怪物 {0} @ {1}", id, position);
            }

            Log.Info("[TestArena] 测试场地就绪：玩家 {0}，怪物 {1} 只", _heroEntityId, monsters.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception error)
        {
            // 异步任务不能静默失败，否则场地看起来“什么都没发生”。
            GD.PushError($"[TestArena] 启动失败：{error}");
        }
    }

    /// <summary>
    /// 显示测试玩家并放到出生点。属性只来自出战装配：用建档规则创建一份仅内存的临时档案，不读写存档。
    /// </summary>
    /// <param name="entities">本场地捕获的实体服务。</param>
    /// <param name="cancellationToken">场地退出时取消。</param>
    /// <returns>显示完成的异步任务。</returns>
    private async Task SpawnHeroAsync(EntityComponent entities, CancellationToken cancellationToken)
    {
        IEntity entity = await ShowOwnedEntityAsync(entities, _heroEntityId,
            new HeroSpawn(PlayerSlot.P1, _playerStart.GlobalPosition), cancellationToken);
        if (entity?.Handle is not Node2D hero)
        {
            GD.PushError($"[TestArena] 玩家实体显示失败：{_heroEntityId}");
            return;
        }

        _hero = hero;
        Log.Info("[TestArena] 显示玩家 {0} @ {1}", _heroEntityId, hero.GlobalPosition);
    }

    /// <summary>
    /// 登记本场地的实体；退出后才返回的显示结果由捕获的旧服务回收。
    /// </summary>
    /// <param name="entities">发起显示的实体服务。</param>
    /// <param name="id">实体配置编号。</param>
    /// <param name="data">本次显示参数。</param>
    /// <param name="cancellationToken">场地退出时取消。</param>
    /// <returns>已经登记本场所有权的实体。</returns>
    /// <exception cref="InvalidOperationException">实体显示失败。</exception>
    /// <exception cref="OperationCanceledException">场地已退出。</exception>
    private async Task<IEntity> ShowOwnedEntityAsync(EntityComponent entities, EntityId id, object data,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (data is EnemySpawn spawn)
        {
            // 独立场地也拥有连续形态，后继不能成为离场后无人清理的实体。
            data = spawn with
            {
                SpawnFailed = error => GD.PushError($"[TestArena] 敌人行为失败：{error}"),
                ContinueEncounterAsync = async (previousId, nextId, nextSpawn) =>
                {
                    await ShowOwnedEntityAsync(entities, nextId, nextSpawn, cancellationToken);
                },
            };
        }
        IEntity entity = await entities.ShowEntityAsync(id, data)
            ?? throw new InvalidOperationException($"测试场地实体显示失败：{id}");
        if (cancellationToken.IsCancellationRequested)
        {
            if (IsInstanceValid(entities))
            {
                entities.HideEntitySafe(entity.Id);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
        _ownedIds.Add(entity.Id);
        return entity;
    }

    /// <summary>
    /// 取出并移除 Monsters 容器中的怪物占位节点，返回它们提供的枚举与坐标。
    /// </summary>
    /// <returns>每只待生成怪物的枚举与出生坐标。</returns>
    private List<(EntityId Id, Vector2 Position)> TakeMonsterPlaceholders()
    {
        List<(EntityId Id, Vector2 Position)> monsters = new();
        foreach (Node child in _enemies.GetChildren())
        {
            if (child is not AbstractEnemyEntity placeholder)
            {
                continue;
            }

            monsters.Add((SmokeInspection.ReadField<EntityId>(placeholder, "_enemyEntityId"), placeholder.GlobalPosition));
            _enemies.RemoveChild(placeholder);
            placeholder.QueueFree();
        }

        return monsters;
    }

    /// <summary>
    /// 只提供原生 FSM 宿主，不启动正式流程导航。
    /// </summary>
    private sealed class ArenaState : GameFramework.Fsm.FsmState<IProcedureManager>
    {
    }
}
