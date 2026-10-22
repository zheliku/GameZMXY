using GameLogic.Battle;
using GameFramework.Procedure;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;
using GameLogic.Architecture;
using GameLogic.Equipment;
using GameLogic.Battle.Damage;
using GameLogic.Drops;
using GameLogic.Input;
using GameLogic.Enemies.AI;
using GameLogic.Save;
using GameLogic.Utility;
using GameLogic;
using GameLogic.Battle.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameConfig;
using GameConfig.Entity;
using GameFramework;
using GameFramework.Entity;
using GameLogic.Config;
using GameLogic.Enemies;
using GameLogic.Profile;
using GameLogic.Skills;
using GameLogic.FloatingText;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using GodotGameFramework.NodePool;
using GodotGameFramework.Sound;

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
    [Export] private PackedScene m_FrameworkScene; // 真实框架场景；仅去掉 Procedure 流程后复用。

    [Export] private EntityId m_HeroEntityId = EntityId.Wukong; // 测试用玩家实体枚举。

    [Export] private Node2D m_PlayerStart; // 玩家出生点（Marker2D）。

    [Export] private Node2D m_Enemies; // 怪物占位容器：拖入怪物实体场景即可测试。

    [Export] private Camera2D m_Camera; // 跟随玩家的测试相机；限位由场景 Camera2D 设置。
    [Export] private FloatingTextPresenter m_FloatingText; // 与正式关卡共用的飘字组件。

    private Node2D m_Hero; // 已显示的玩家节点，供相机跟随。
    private float m_CameraY; // 相机固定的纵向位置。
    private readonly HashSet<int> m_OwnedIds = new(); // 只接受本场地创建的实体反馈，退出时按编号隐藏。
    private CancellationTokenSource m_Cancellation; // 场地退出后不再登记晚到的显示结果。
    private ProcedureOwner m_TestFlow; // 独立 F6 场地拥有的原生 FSM；正常游戏内运行时借用现有主 FSM。
    private EntityComponent m_Entities; // 本次启动捕获的实体服务。

    /// <summary>离开场景树时取消显示任务、停止飘字并清理本场地实体。</summary>
    public override void _ExitTree()
    {
        m_Cancellation?.Cancel();
        m_Cancellation?.Dispose();
        m_Cancellation = null;
        m_FloatingText.StopSession();
        if (IsInstanceValid(m_Entities))
            foreach (int id in m_OwnedIds)
                m_Entities.HideEntitySafe(id);
        m_OwnedIds.Clear();
        m_Entities = null;
        if (m_TestFlow != null)
        {
            m_TestFlow.RemoveSystem<HeroSystem>();
            GF.Fsm.DestroyFsm(m_TestFlow);
        }
        m_TestFlow = null;
    }

    /// <summary>准备框架、取走怪物占位节点，然后异步启动测试场地。</summary>
    public override void _Ready()
    {
        m_CameraY = m_Camera.GlobalPosition.Y;
        // 独立 F6 运行时复用真实框架，但不启动正式流程。
        if (GameEntry.GetComponent<EntityComponent>() == null)
        {
            Node framework = m_FrameworkScene.Instantiate();
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
        foreach (var group in GF.Entity.EntityGroupRes.EntityGroups)
        {
            if (!GF.Entity.HasEntityGroup(group.Name) && !GF.Entity.AddEntityGroup(group.Name, group.ReleaseInterval,
                    group.Capacity, group.ExpireTime, group.Priority))
                throw new InvalidOperationException($"实体组 {group.Name} 创建失败。");
        }
        foreach (var group in GF.UI.UIGroupRes.Groups)
        {
            if (!GF.UI.HasUIGroup(group.Name) && !GF.UI.AddUIGroup(group.Name, group.Depth))
                throw new InvalidOperationException($"UI 组 {group.Name} 创建失败。");
        }
        foreach (var group in GF.Sound.SoundGroupRes.SoundGroups)
        {
            if (!GF.Sound.HasSoundGroup(group.Name) && !GF.Sound.AddSoundGroup(group.Name, group.AgentCounts,
                    group.AvoidBeingReplacedBySamePriority))
                throw new InvalidOperationException($"声音组 {group.Name} 创建失败。");
        }
        GF.Sound.SetVolume(SoundComponent.DefaultMusicGroup, GF.Setting.GetFloat(SoundComponent.DefaultMusicGroup, 1));
        GF.Sound.SetVolume(SoundComponent.DefaultSfxGroup, GF.Setting.GetFloat(SoundComponent.DefaultSfxGroup, 1));
        GF.Sound.SetVolume(SoundComponent.DefaultUiGroup, GF.Setting.GetFloat(SoundComponent.DefaultUiGroup, 1));
        GF.Debugger.ActiveWindow = false;
        NodePool.Instance.Active();
        LayerMask.Instance.Active();
        m_Entities = GF.Entity;
        m_Cancellation = new CancellationTokenSource();
        List<(EntityId Id, Vector2 Position)> monsters = TakeMonsterPlaceholders();
        _ = StartAsync(monsters, m_Entities, m_Cancellation.Token);
    }

    /// <summary>相机水平跟随玩家；纵向保持场景初始高度，限位交给场景 Camera2D。</summary>
    /// <param name="delta">距上一帧的秒数，不使用。</param>
    public override void _Process(double delta)
    {
        if (m_Hero == null || !IsInstanceValid(m_Hero))
        {
            return;
        }

        m_Camera.GlobalPosition = new Vector2(m_Hero.GlobalPosition.X, m_CameraY);
    }

    /// <summary>等框架组件就绪后显示玩家与全部占位怪物。</summary>
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
                m_TestFlow = GF.Fsm.CreateFsm<IProcedureManager>(GameFrameworkEntry.GetModule<IProcedureManager>(), new ArenaState());
                var architecture = m_TestFlow.InitializeArchitecture();
                m_TestFlow.RegisterUtility(new ArchiveUtility<GameCatalogue, GameData>(GF.Archive));
                m_TestFlow.RegisterUtility(new SpatialUtility());
                m_TestFlow.RegisterUtility(new SaveDataUtility());
                m_TestFlow.RegisterModel(new ConfigurationModel(ConfigSystem.Instance.Tables));
                m_TestFlow.RegisterUtility(new ExperienceCurve(m_TestFlow.GetModel<ConfigurationModel>().Tables.TbHeroLevelConfig.DataList));
                m_TestFlow.RegisterModel(new SaveModel());
                int heroId = ConfigSystem.Instance.Tables.TbHeroConfig.DataList.First(x => x.EntityId == m_HeroEntityId).Id;
                m_TestFlow.RegisterModel(new ProfileModel(heroId, [(heroId, 0)], m_TestFlow.GetUtility<ExperienceCurve>(),
                    ConfigSystem.Instance.Tables.TbProfileConfig.EquipmentCapacity));
                m_TestFlow.RegisterModel(new HeroModel());
                m_TestFlow.RegisterModel(new SkillModel(m_TestFlow.GetModel<ProfileModel>().HeroIds));
                m_TestFlow.RegisterSystem(new ConfigurationSystem(architecture));
                m_TestFlow.RegisterSystem(new CombatSystem(architecture));
                m_TestFlow.RegisterSystem(new InputSystem(architecture));
                m_TestFlow.RegisterSystem(new EnemyAiSystem(architecture));
                m_TestFlow.RegisterSystem(new LootSystem(architecture));
                m_TestFlow.RegisterSystem(new ProfileSystem(architecture));
                m_TestFlow.RegisterSystem(new EquipmentSystem(architecture));
                m_TestFlow.RegisterSystem(new SkillSystem(architecture));
                m_TestFlow.RegisterSystem(new HeroSystem(architecture));
                m_TestFlow.RegisterSystem(new SaveSystem(architecture));
            }
            m_FloatingText.StartSession(m_OwnedIds.Contains);
            await SpawnHeroAsync(entities, cancellationToken);
            foreach ((EntityId id, Vector2 position) in monsters)
            {
                await ShowOwnedEntityAsync(entities, id, new EnemySpawn(position), cancellationToken);
                Log.Info("[TestArena] 显示怪物 {0} @ {1}", id, position);
            }

            Log.Info("[TestArena] 测试场地就绪：玩家 {0}，怪物 {1} 只", m_HeroEntityId, monsters.Count);
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
    /// <returns>显示完成的异步任务。</returns>
    /// <param name="entities">本场地捕获的实体服务。</param>
    /// <param name="cancellationToken">场地退出时取消。</param>
    private async Task SpawnHeroAsync(EntityComponent entities, CancellationToken cancellationToken)
    {
        IEntity entity = await ShowOwnedEntityAsync(entities, m_HeroEntityId, null, cancellationToken);
        if (entity?.Handle is not Node2D hero)
        {
            GD.PushError($"[TestArena] 玩家实体显示失败：{m_HeroEntityId}");
            return;
        }

        hero.GlobalPosition = m_PlayerStart.GlobalPosition;
        m_Hero = hero;
        Log.Info("[TestArena] 显示玩家 {0} @ {1}", m_HeroEntityId, hero.GlobalPosition);
    }

    /// <summary>登记本场地的实体；退出后才返回的显示结果由捕获的旧服务回收。</summary>
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
        IEntity entity = await entities.ShowEntityAsync(id, data)
            ?? throw new InvalidOperationException($"测试场地实体显示失败：{id}");
        if (cancellationToken.IsCancellationRequested)
        {
            if (IsInstanceValid(entities))
                entities.HideEntitySafe(entity.Id);
            cancellationToken.ThrowIfCancellationRequested();
        }
        m_OwnedIds.Add(entity.Id);
        return entity;
    }

    /// <summary>取出并移除 Monsters 容器中的怪物占位节点，返回它们提供的枚举与坐标。</summary>
    /// <returns>每只待生成怪物的枚举与出生坐标。</returns>
    private List<(EntityId Id, Vector2 Position)> TakeMonsterPlaceholders()
    {
        List<(EntityId Id, Vector2 Position)> monsters = new();
        foreach (Node child in m_Enemies.GetChildren())
        {
            if (child is not AbstractEnemyEntity placeholder)
            {
                continue;
            }

            monsters.Add((SmokeInspection.ReadField<EntityId>(placeholder, "m_EnemyEntityId"), placeholder.GlobalPosition));
            m_Enemies.RemoveChild(placeholder);
            placeholder.QueueFree();
        }

        return monsters;
    }

    /// <summary>只提供原生 FSM 宿主，不启动正式流程导航。</summary>
    private sealed class ArenaState : GameFramework.Fsm.FsmState<IProcedureManager> { }
}
