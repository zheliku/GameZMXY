using System;
using System.Threading;
using System.Threading.Tasks;
using GameConfig;
using GameConfig.Constant;
using GameConfig.Level;
using GameFramework.Event;
using GameFramework.Procedure;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using GodotGameFramework.HotUpdate;
using GodotGameFramework.Scene;
using GodotGameFramework.UI;
using GameLogic;
using GameLogic.Entity.Heroes;
using GameLogic.Level;
using GameLogic.Profile;
using GameLogic.Session;
using GameLogic.UI;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 关卡流程：负责一次关卡运行的装配与拆除——加载关卡场景、按档案显示英雄、创建 <see cref="LevelRun"/>、打开 HUD；
/// 运行结束（通关/死亡已提交检查点）后重新进入本流程开下一局。
/// 收益结算与关卡事务归 <see cref="LevelRun"/>，持久状态归 <see cref="GameContext"/>，本流程只管作用域的创建顺序与逆序清理。
/// </summary>
public class ProcedureLevel : ProcedureBase
{
    private const int InitialLevelId = 1; // 当前游戏入口使用的关卡配置主键（以后由地图流程传入）。

    private ProcedureOwner m_Owner; // 当前流程状态机，重开关卡时使用。

    private GameContext m_Context; // 档案作用域（来自流程数据）。

    private HeroEntity m_Hero; // 当前流程创建并拥有的玩家实体。

    private LevelController m_Level; // 当前流程加载并拥有的关卡根控制器。

    private LevelRun m_Run; // 本次关卡运行。

    private DamagePopPresenter m_DamagePops; // 本次关卡的伤害飘字表现。

    private CancellationTokenSource m_SessionCancellation; // 离开流程时取消正在执行的实体生成。

    private int m_EntrySerial; // 让异步完成结果与当前流程进入周期绑定。

    private int m_LevelOwnerEntry; // 记录当前加载场景由哪个流程周期负责卸载。

    private string m_LevelScenePath; // 从 Luban LevelConfig 读取的场景路径。

    private int m_HudSerialId; // 本会话打开的战斗 HUD 编号；0 表示未打开。

    private SceneTreeTimer m_RestartTimer; // 结局后等待重开的计时器。

    private bool m_StartupMarked; // 流程状态实例随 App 存续，仅首次成功开局标记热更启动。

    /// <summary>
    /// 进入流程：读取档案作用域 → 加载关卡场景 → 显示英雄 → 关闭加载界面 → 开始关卡会话与运行 → 打开 HUD。
    /// </summary>
    /// <param name="procedureOwner">当前流程状态机。</param>
    protected internal override async void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);
        int entrySerial = ++m_EntrySerial;
        m_Owner = procedureOwner;

        try
        {
            m_SessionCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = m_SessionCancellation.Token;
            var entities = GF.Entity;
            Tables tables = ConfigSystem.Instance.Tables;
            m_Context = procedureOwner.GetData<GameContextVariable>(GameContext.DataKey)?.Value
                ?? throw new InvalidOperationException("流程数据缺少 GameContext（读档流程未完成）。");

            // 上一局的检查点必须先落盘：重开前等待，失败只记录（档案仍在内存，下一检查点会再写）。
            if (!await m_Context.Save.Pending)
            {
                Log.Error("[ProcedureLevel] 上一个检查点写入失败，进度仍保留在内存中。");
            }

            if (entrySerial != m_EntrySerial)
            {
                return;
            }

            // LevelConfig 是以 LevelId 为主键的 map 表：一条记录包含关卡元数据和嵌套的阶段/配方列表。
            LevelConfig levelConfig = tables.TbLevelConfig.GetOrDefault(InitialLevelId)
                ?? throw new InvalidOperationException($"关卡配置不存在：LevelId={InitialLevelId}");
            m_LevelScenePath = levelConfig.ScenePath;
            string scenePath = m_LevelScenePath;
            Log.Info("[ProcedureLevel] 加载关卡场景：{0}", m_LevelScenePath);

            // 场景加载本身不取消：若流程在加载中离开，继续等待成功事件后由 entrySerial 分支卸载，避免 additive 场景泄漏。
            Node2D levelNode = await LoadLevelSceneAsync(scenePath, CancellationToken.None);
            if (entrySerial != m_EntrySerial)
            {
                if (m_LevelOwnerEntry == 0 && GF.Scene.IsSceneLoaded(scenePath))
                {
                    GF.Scene.UnloadScene(scenePath);
                }
                return;
            }

            // 先登记场景所有权，再校验根节点；失败路径也能卸载已加载场景。
            m_LevelOwnerEntry = entrySerial;
            m_Level = levelNode as LevelController
                ?? throw new InvalidOperationException($"{m_LevelScenePath} 场景根节点未绑定 LevelController。");
            m_Level.Initialize();
            m_Level.Failed += OnLevelFailed;

            // 飘字挂在实体系统所在世界坐标系，随本关结束释放。
            m_DamagePops = new DamagePopPresenter(GF.Entity);

            // 英雄只接收由档案构建的出战装配；等待后使用捕获的实体服务清理旧结果。
            HeroRecord record = m_Context.Profile.ActiveHero;
            HeroLoadout loadout = m_Context.StatBuilder.Build(record);
            WukongEntity hero = await entities.ShowEntityAsync<WukongEntity>(GameConfig.Entity.EntityId.Wukong, loadout);
            if (entrySerial != m_EntrySerial)
            {
                entities.HideEntitySafe(hero);
                return;
            }

            m_Hero = hero ?? throw new InvalidOperationException("Failed to create Wukong entity.");
            if (hero.Config == null || !hero.IsShown)
            {
                throw new InvalidOperationException("英雄初始化失败。");
            }
            m_Hero.GlobalPosition = m_Level.PlayerSpawnPosition;

            // 先关闭加载界面，再开放相机与阶段事件，首波等待相机抵达首阶段右界。
            if (GF.UI.GetUIForm(ResourcesCollectionConstant.UI_LoadingForm) is LoadingForm loading)
            {
                await loading.CloseLoadingAsync(cancellationToken);
            }

            if (entrySerial != m_EntrySerial)
            {
                return;
            }

            m_Level.StartSession(entities, m_Hero, cancellationToken);
            m_Run = new LevelRun(m_Context, m_Level, m_Hero, tables);
            m_Run.Ended += OnRunEnded;

            // 先订阅结果再提交请求，缓存命中也能处理；失败参数只在事件分发期间使用。
            GF.Event.Subscribe(OpenUIFormFailureEventArgs.EventId, OnHudOpenFailed);
            m_HudSerialId = GF.UI.OpenUIForm(UIFormId.BattleHud, new BattleHudData(m_Hero, record.Progression, m_Level));
            Log.Info("[ProcedureLevel] 已提交战斗 HUD 请求：{0}", m_HudSerialId);

            // 首次进入可玩状态：后续崩溃不再归因于热更。
            if (!m_StartupMarked)
            {
                m_StartupMarked = true;
                HotUpdateSafetyGuard.MarkStartupSuccess();
            }
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                return;
            }

            Log.Error("[ProcedureLevel] 关卡启动失败：{0}", ex);
            if (entrySerial == m_EntrySerial)
            {
                CleanupSession(entrySerial);
                (GF.UI.GetUIForm(ResourcesCollectionConstant.UI_LoadingForm) as LoadingForm)?.CloseLoading();
            }
        }
    }

    /// <summary>
    /// 离开流程：未结束的关卡运行按"中途放弃"回滚（不写盘）；框架关停时只退订、不写盘。
    /// </summary>
    /// <param name="procedureOwner">当前流程状态机。</param>
    /// <param name="isShutdown">是否因框架关闭而离开。</param>
    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        base.OnLeave(procedureOwner, isShutdown);
        int entrySerial = m_EntrySerial;
        m_EntrySerial++;
        m_SessionCancellation?.Cancel();
        m_SessionCancellation?.Dispose();
        m_SessionCancellation = null;

        if (!isShutdown)
        {
            CleanupSession(entrySerial);
        }
        else
        {
            // 关停：进程即将结束，只解除本作用域订阅；磁盘上仍是上一个检查点。
            m_Run?.Detach();
            m_DamagePops = null;
        }

        m_Run = null;
        m_Level = null;
        m_Hero = null;
        m_Context = null;
        m_Owner = null;
    }

    /// <summary>订阅场景结果并等待加载，命中已加载场景时直接返回。</summary>
    /// <param name="scenePath">关卡场景路径。</param>
    /// <param name="cancellationToken">等待取消令牌；流程退出后的卸载由进入周期校验负责。</param>
    /// <returns>加载后的关卡根节点。</returns>
    private static async Task<Node2D> LoadLevelSceneAsync(string scenePath, CancellationToken cancellationToken)
    {
        // 重入时复用已经挂树的场景实例。
        if (GF.Scene.IsSceneLoaded(scenePath))
        {
            return GF.Scene.GetLoadedScene<Node2D>(scenePath);
        }

        // 先订阅后提交请求，处理资源命中缓存时的同步成功回调。
        TaskCompletionSource<Node2D> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<GameEventArgs> onSuccess = (_, args) =>
        {
            if (args is LoadSceneSuccessEventArgs success &&
                success.SceneAssetName == scenePath)
            {
                tcs.TrySetResult(success.SceneInstance as Node2D);
                Log.Info("[ProcedureLevel] 关卡场景加载成功：{0}", scenePath);
            }
        };
        EventHandler<GameEventArgs> onFailure = (_, args) =>
        {
            if (args is LoadSceneFailureEventArgs failure &&
                failure.SceneAssetName == scenePath)
            {
                tcs.TrySetException(new InvalidOperationException(failure.ErrorMessage));
                Log.Error("[ProcedureLevel] 关卡场景加载失败：{0}", failure.ErrorMessage);
            }
        };

        // 事件参数由框架池化，只在回调中复制场景节点引用。
        GF.Event.Subscribe(LoadSceneSuccessEventArgs.EventId, onSuccess);
        GF.Event.Subscribe(LoadSceneFailureEventArgs.EventId, onFailure);
        try
        {
            // 使用 Additive 保留常驻框架根场景。
            GF.Scene.LoadScene(scenePath, LoadSceneMode.Additive);
            Log.Info("[ProcedureLevel] 已提交关卡加载请求：{0}", scenePath);
            using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
            {
                return await tcs.Task;
            }
        }
        finally
        {
            // 无论加载成功、失败或取消都解除全局事件订阅。
            GF.Event.Unsubscribe(LoadSceneSuccessEventArgs.EventId, onSuccess);
            GF.Event.Unsubscribe(LoadSceneFailureEventArgs.EventId, onFailure);
        }
    }

    /// <summary>
    /// 停止本周期并按创建逆序拆除：HUD → 关卡运行（未结束则回滚）→ 飘字 → 关卡会话 → 英雄 → 场景；重复清理安全。
    /// </summary>
    /// <param name="entrySerial">被清理的流程进入周期。</param>
    private void CleanupSession(int entrySerial)
    {
        // 启动或运行失败也必须使在途等待失效，随后重新进入可创建新周期。
        if (m_EntrySerial == entrySerial)
        {
            m_EntrySerial++;
        }
        m_SessionCancellation?.Cancel();
        m_SessionCancellation?.Dispose();
        m_SessionCancellation = null;
        m_RestartTimer = null;

        // 先关 HUD（退订英雄/成长/关卡）再拆会话，避免界面在已清理的引用上刷新。
        if (m_HudSerialId != 0)
        {
            if (GF.UI.HasUIForm(m_HudSerialId) || GF.UI.IsLoadingUIForm(m_HudSerialId))
            {
                GF.UI.CloseUIForm(m_HudSerialId);
                Log.Info("[ProcedureLevel] 战斗 HUD 关闭：{0}", m_HudSerialId);
            }

            m_HudSerialId = 0;
        }
        if (GF.Event.Check(OpenUIFormFailureEventArgs.EventId, OnHudOpenFailed))
        {
            GF.Event.Unsubscribe(OpenUIFormFailureEventArgs.EventId, OnHudOpenFailed);
        }

        // 未结束的运行按中途放弃回滚；已提交的运行不受影响。
        if (m_Run != null)
        {
            m_Run.Ended -= OnRunEnded;
            m_Run.Abandon();
            m_Run = null;
        }

        // 先停表现和关卡事件，再隐藏实体，最后卸载场景。
        m_DamagePops?.Dispose();
        m_DamagePops = null;
        if (m_Level != null)
        {
            m_Level.Failed -= OnLevelFailed;
            m_Level.Cleanup();
        }
        GF.Entity.HideEntitySafe(m_Hero);
        bool ownsScene = m_LevelOwnerEntry == entrySerial;
        if (ownsScene)
        {
            if (m_LevelScenePath != null && GF.Scene.IsSceneLoaded(m_LevelScenePath))
            {
                GF.Scene.UnloadScene(m_LevelScenePath);
            }

            m_LevelOwnerEntry = 0;
        }

        m_Level = null;
        m_Hero = null;
        if (ownsScene)
        {
            m_LevelScenePath = null;
        }
    }

    /// <summary>
    /// 关卡运行结束：按配置等待后重开本关（死亡含死亡动画、通关留出完成表现；以后由结算界面与地图流程接管）；放弃不重开。
    /// </summary>
    /// <param name="outcome">运行结局。</param>
    private void OnRunEnded(LevelRunOutcome outcome)
    {
        if (outcome == LevelRunOutcome.Abandoned || m_Owner == null)
        {
            return;
        }

        var battle = ConfigSystem.Instance.Tables.TbBattleConfig;
        float delay = outcome == LevelRunOutcome.Defeated ? battle.DeathRestartDelay : battle.ClearRestartDelay;
        int entrySerial = m_EntrySerial;
        ProcedureOwner owner = m_Owner;

        // 事件回调内不直接切换状态（离开会拆除发出事件的对象）：至少延到下一帧，按游戏时间计时（暂停时一起停）。
        m_RestartTimer = ((SceneTree)Engine.GetMainLoop()).CreateTimer(Mathf.Max(delay, 0.01f), processAlways: false);
        m_RestartTimer.Timeout += () => Restart(owner, entrySerial);
    }

    /// <summary>若仍处于同一进入周期，重新进入本流程开下一局（GGF 允许切换到当前状态：先 OnLeave 再 OnEnter）。</summary>
    /// <param name="owner">流程状态机。</param>
    /// <param name="entrySerial">发起重开时的进入周期。</param>
    private void Restart(ProcedureOwner owner, int entrySerial)
    {
        if (entrySerial != m_EntrySerial || owner.IsDestroyed)
        {
            return;
        }

        ChangeState<ProcedureLevel>(owner);
    }

    /// <summary>HUD 打开失败时只处理本流程拥有的请求，统一结束关卡。</summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="args">池化事件参数，只在本次分发内读取。</param>
    private void OnHudOpenFailed(object sender, GameEventArgs args)
    {
        if (args is OpenUIFormFailureEventArgs failure && failure.SerialId == m_HudSerialId)
        {
            Log.Error("[ProcedureLevel] 战斗 HUD 打开失败：{0}", failure.ErrorMessage);
            CleanupSession(m_EntrySerial);
        }
    }

    /// <summary>关卡异步显示失败时统一清理（运行回滚），不留下锁死的战斗区域。</summary>
    /// <param name="error">关卡运行错误。</param>
    private void OnLevelFailed(Exception error)
    {
        Log.Error("[ProcedureLevel] 关卡运行失败：{0}", error);
        CleanupSession(m_EntrySerial);
    }
}
