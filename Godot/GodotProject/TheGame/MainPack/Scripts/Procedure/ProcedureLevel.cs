using GameLogic.Battle.Attributes;
using GameLogic.Startup.FlowData;
using GameLogic.Event.Navigation;
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
using GodotGameFramework.Scene;
using GodotGameFramework.UI;
using GameLogic;
using GameLogic.Heroes;
using GameLogic.Level;
using GameLogic.Profile;
using GameLogic.UI;
using GameLogic.Audio;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 关卡流程：负责一次关卡运行的装配与拆除——加载关卡场景、按档案显示英雄、开始 <see cref="LevelModule"/>、打开 HUD；
/// 通关或死亡提交检查点后显示结算，再由玩家返回地图或重试。
/// 收益结算与关卡事务归 <see cref="LevelModule"/>，持久状态归 <see cref="GameContext"/>，本流程只管作用域的创建顺序与逆序清理。
/// </summary>
public class ProcedureLevel : ProcedureBase
{
    private bool m_PendingSettlement; // 待下一流程帧打开结算，避免输入回调内操作窗口。
    private float m_TransitionDelay; // 失败结算前保留死亡动画的游戏秒数。
    private int m_InventorySerialId; // 本周期的背包窗口，包括在途请求。
    private int m_SettlementSerialId; // 本周期的结算窗口。
    private bool m_Navigating; // 检查点等待期间拒绝重复导航。
    private ProcedureOwner m_Owner; // 当前流程状态机，重开关卡时使用。

    private HeroEntity m_Hero; // 当前流程创建并拥有的玩家实体。

    private LevelModule m_Level; // 当前流程加载并拥有的关卡根控制器。

    private DamagePopPresenter m_DamagePops; // 本次关卡的伤害飘字表现。

    private CancellationTokenSource m_SessionCancellation; // 离开流程时取消正在执行的实体生成。

    private int m_EntrySerial; // 让异步完成结果与当前流程进入周期绑定。

    private int m_LevelOwnerEntry; // 记录当前加载场景由哪个流程周期负责卸载。

    private string m_LevelScenePath; // 从 Luban LevelConfig 读取的场景路径。

    private int m_HudSerialId; // 本会话打开的战斗 HUD 编号；0 表示未打开。

    private int m_MusicSerial; // 本关 GGF 音乐请求，清理时连在途加载一起取消。
    private int m_SettingsSerialId; // 本周期的设置窗口，含正在加载的请求。

    /// <summary>
    /// 进入流程：读取档案作用域 → 加载关卡场景 → 显示英雄 → 关闭加载界面 → 开始关卡模块 → 打开 HUD。
    /// </summary>
    /// <param name="procedureOwner">当前流程状态机。</param>
    protected internal override async void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);
        int entrySerial = ++m_EntrySerial;
        m_Owner = procedureOwner;
        GF.Event.Subscribe(SettingsRequestedEventArgs.EventId, OnSettingsRequested);
        GF.Event.Subscribe(InventoryRequestedEventArgs.EventId, OnInventoryRequested);
        GF.Event.Subscribe(RetryLevelRequestedEventArgs.EventId, OnRetryRequested);
        GF.Event.Subscribe(MapRequestedEventArgs.EventId, OnMapRequested);
        GF.Event.Subscribe(TitleRequestedEventArgs.EventId, OnTitleRequested);

        try
        {
            m_SessionCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = m_SessionCancellation.Token;
            var entities = GF.Entity;
            Tables tables = ConfigSystem.Instance.Tables;
            GameContext session = procedureOwner.GetData<GameContextVariable>(FlowKeys.GameContext)?.Value
                ?? throw new InvalidOperationException("流程数据缺少 GameContext（读档流程未完成）。");

            // 上一局的检查点必须先落盘：重开前等待，失败只记录（档案仍在内存，下一检查点会再写）。
            if (!await session.Save.PendingWrite)
            {
                Log.Error("[ProcedureLevel] 上一个检查点写入失败，进度仍保留在内存中。");
            }

            if (entrySerial != m_EntrySerial)
            {
                return;
            }

            // LevelConfig 是以 LevelId 为主键的 map 表：一条记录包含关卡元数据和嵌套的阶段/配方列表。
            int selectedLevelId = procedureOwner.GetData<VarInt32>(FlowKeys.SelectedLevelId)?.Value
                ?? throw new InvalidOperationException("关卡流程缺少地图选择结果。");
            LevelConfig levelConfig = tables.TbLevelConfig.GetOrDefault(selectedLevelId)
                ?? throw new InvalidOperationException($"关卡配置不存在：LevelId={selectedLevelId}");
            m_MusicSerial = GF.Sound.PlayConfigured(levelConfig.MusicId, 0.4f);
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
            m_Level = levelNode as LevelModule
                ?? throw new InvalidOperationException($"{m_LevelScenePath} 场景根节点未绑定 LevelModule。");
            m_Level.Initialize();
            m_Level.Failed += OnLevelFailed;

            // 飘字挂在实体系统所在世界坐标系，随本关结束释放。
            m_DamagePops = new DamagePopPresenter(GF.Entity);

            // 英雄只接收由档案构建的出战装配；等待后使用捕获的实体服务清理旧结果。
            HeroProgression record = session.Profile.ActiveHero;
            HeroLoadout loadout = session.Equipment.CreateLoadout(session.Profile);
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

            m_Level.Ended += OnLevelEnded;
            m_Level.StartSession(entities, m_Hero, session, cancellationToken);

            // 先订阅结果再提交请求，缓存命中也能处理；失败参数只在事件分发期间使用。
            GF.Event.Subscribe(OpenUIFormFailureEventArgs.EventId, OnHudOpenFailed);
            m_HudSerialId = GF.UI.OpenUIForm(UIFormId.BattleHud, new BattleHudData(m_Hero, record, m_Level));
            Log.Info("[ProcedureLevel] 已提交战斗 HUD 请求：{0}", m_HudSerialId);
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

    /// <summary>收到左下角设置点击后打开暂停菜单。</summary>
    /// <param name="sender">战斗 HUD。</param>
    /// <param name="args">本次设置请求。</param>
    private void OnSettingsRequested(object sender, GameEventArgs args)
    {
        if (((SettingsRequestedEventArgs)args).FormSerialId != m_HudSerialId || m_Level?.Outcome != LevelOutcome.Running) return;
        if (HasWindow(m_SettingsSerialId) || HasWindow(m_InventorySerialId)) return;
        m_SettingsSerialId = GF.UI.OpenUIForm(UIFormId.SettingsForm, new SettingsFormData(m_Level));
    }

    /// <summary>在同一关卡中互斥打开背包与设置。</summary>
    /// <param name="sender">战斗 HUD。</param>
    /// <param name="args">本次背包请求。</param>
    private void OnInventoryRequested(object sender, GameEventArgs args)
    {
        if (((InventoryRequestedEventArgs)args).FormSerialId != m_HudSerialId || m_Level?.Outcome != LevelOutcome.Running) return;
        if (HasWindow(m_SettingsSerialId) || HasWindow(m_InventorySerialId)) return;
        var context = m_Owner.GetData<GameContextVariable>(FlowKeys.GameContext).Value;
        m_InventorySerialId = GF.UI.OpenUIForm(UIFormId.InventoryForm, new InventoryFormData(m_Hero, context.Profile, m_Level));
    }

    /// <summary>设置的放弃与结算的返回地图使用各自的窗口编号校验。</summary>
    /// <param name="sender">发出请求的窗口。</param>
    /// <param name="args">返回地图请求。</param>
    private void OnMapRequested(object sender, GameEventArgs args)
    {
        int serial = ((MapRequestedEventArgs)args).FormSerialId;
        if (serial != 0 && serial == m_SettlementSerialId && m_Level?.Result != null)
            NavigateAfterCheckpoint(m_Owner, m_EntrySerial, retry: false);
        else if (serial != 0 && serial == m_SettingsSerialId && m_Level?.Outcome == LevelOutcome.Running)
            ChangeState<ProcedureMap>(m_Owner);
    }

    /// <summary>只有当前失败结算窗口可以重新挑战。</summary>
    /// <param name="sender">结算窗口。</param>
    /// <param name="args">重试请求。</param>
    private void OnRetryRequested(object sender, GameEventArgs args)
    {
        int serial = ((RetryLevelRequestedEventArgs)args).FormSerialId;
        if (serial != 0 && serial == m_SettlementSerialId && m_Level?.Outcome == LevelOutcome.Defeated)
            NavigateAfterCheckpoint(m_Owner, m_EntrySerial, retry: true);
    }

    /// <summary>检查窗口是否仍打开或正在加载。</summary>
    /// <param name="serial">窗口请求编号；零表示无请求。</param>
    /// <returns>窗口仍属于本周期时为 true。</returns>
    private static bool HasWindow(int serial) => serial != 0 && (GF.UI.HasUIForm(serial) || GF.UI.IsLoadingUIForm(serial));

    /// <summary>从设置返回标题，离开边界负责回滚未完成的关卡。</summary>
    /// <param name="sender">当前设置窗口。</param>
    /// <param name="args">返回标题请求。</param>
    private void OnTitleRequested(object sender, GameEventArgs args)
    {
        if (((TitleRequestedEventArgs)args).FormSerialId == m_SettingsSerialId && m_Level?.Outcome == LevelOutcome.Running)
            ChangeState<ProcedureTitle>(m_Owner);
    }

    /// <summary>
    /// 离开流程：未结束的关卡运行按"中途放弃"回滚（不写盘）；框架关停时只退订、不写盘。
    /// </summary>
    /// <param name="procedureOwner">当前流程状态机。</param>
    /// <param name="isShutdown">是否因框架关闭而离开。</param>
    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        GF.Event.Unsubscribe(SettingsRequestedEventArgs.EventId, OnSettingsRequested);
        GF.Event.Unsubscribe(InventoryRequestedEventArgs.EventId, OnInventoryRequested);
        GF.Event.Unsubscribe(RetryLevelRequestedEventArgs.EventId, OnRetryRequested);
        GF.Event.Unsubscribe(MapRequestedEventArgs.EventId, OnMapRequested);
        GF.Event.Unsubscribe(TitleRequestedEventArgs.EventId, OnTitleRequested);
        m_PendingSettlement = false;
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
            m_Level?.Detach();
            m_DamagePops = null;
        }

        m_Level = null;
        m_Hero = null;
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
    /// 停止本周期并按创建逆序拆除：HUD → 飘字 → 关卡模块（未结束则回滚） → 英雄 → 场景；重复清理安全。
    /// </summary>
    /// <param name="entrySerial">被清理的流程进入周期。</param>
    private void CleanupSession(int entrySerial)
    {
        m_PendingSettlement = false;
        m_Navigating = false;
        // 启动或运行失败也必须使在途等待失效，随后重新进入可创建新周期。
        if (m_EntrySerial == entrySerial)
        {
            m_EntrySerial++;
        }
        m_SessionCancellation?.Cancel();
        m_SessionCancellation?.Dispose();
        m_SessionCancellation = null;
        // 模态窗口先解除暂停和运行订阅，再清理 HUD、实体及关卡事务。
        if (HasWindow(m_InventorySerialId)) GF.UI.CloseUIForm(m_InventorySerialId);
        if (HasWindow(m_SettlementSerialId)) GF.UI.CloseUIForm(m_SettlementSerialId);
        m_InventorySerialId = m_SettlementSerialId = 0;
        if (m_SettingsSerialId != 0 && (GF.UI.HasUIForm(m_SettingsSerialId) || GF.UI.IsLoadingUIForm(m_SettingsSerialId)))
            GF.UI.CloseUIForm(m_SettingsSerialId);
        m_SettingsSerialId = 0;
        if (m_MusicSerial != 0) GF.Sound.StopSound(m_MusicSerial, 0.2f);
        m_MusicSerial = 0;

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

        // 先停表现和关卡事件，再隐藏实体，最后卸载场景。
        m_DamagePops?.Dispose();
        m_DamagePops = null;
        if (m_Level != null)
        {
            m_Level.Failed -= OnLevelFailed;
            m_Level.Ended -= OnLevelEnded;
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

    /// <summary>登记结算请求；出口不附加等待，死亡保留配置的播放时间。</summary>
    /// <param name="outcome">本次关卡结局。</param>
    private void OnLevelEnded(LevelOutcome outcome)
    {
        if (outcome == LevelOutcome.Abandoned || m_Owner == null) return;
        m_PendingSettlement = true;
        m_TransitionDelay = outcome == LevelOutcome.Defeated
            ? ConfigSystem.Instance.Tables.TbBattleConfig.DeathSettlementDelay : 0f;
    }

    /// <summary>在事件栈退出后的流程帧打开结算，死亡动画等待按游戏秒数推进。</summary>
    /// <param name="procedureOwner">当前流程状态机。</param>
    /// <param name="elapseSeconds">本帧游戏秒数。</param>
    /// <param name="realElapseSeconds">本帧实际秒数。</param>
    protected internal override void OnUpdate(ProcedureOwner procedureOwner, float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);
        if (!m_PendingSettlement) return;
        m_TransitionDelay -= elapseSeconds;
        if (m_TransitionDelay > 0f) return;
        m_PendingSettlement = false;
        m_SettlementSerialId = GF.UI.OpenUIForm(UIFormId.SettlementForm, m_Level.Result);
    }

    /// <summary>等待检查点后执行玩家选择；旧周期异步结果不触发导航。</summary>
    /// <param name="owner">流程状态机。</param>
    /// <param name="entrySerial">发起重开时的进入周期。</param>
    /// <param name="retry">失败后重试为 true，否则返回地图。</param>
    private async void NavigateAfterCheckpoint(ProcedureOwner owner, int entrySerial, bool retry)
    {
        if (entrySerial != m_EntrySerial || owner.IsDestroyed || m_Navigating) return;
        m_Navigating = true;

        // 返回地图前等待本次检查点完成；写入失败仍保留内存进度，地图可手动重试保存。
        Task<bool> write = m_Level?.CheckpointWrite;
        if (write != null && !await write)
            Log.Error("[ProcedureLevel] 检查点写入失败，当前进度仍保留在内存中，请在地图重新保存。");
        if (entrySerial != m_EntrySerial || owner.IsDestroyed) return;
        if (retry) ChangeState<ProcedureLevel>(owner);
        else ChangeState<ProcedureMap>(owner);
    }

    /// <summary>战斗窗口打开失败时只处理本流程拥有的请求，统一结束关卡。</summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="args">池化事件参数，只在本次分发内读取。</param>
    private void OnHudOpenFailed(object sender, GameEventArgs args)
    {
        if (args is OpenUIFormFailureEventArgs failure && (failure.SerialId == m_HudSerialId || failure.SerialId == m_InventorySerialId || failure.SerialId == m_SettlementSerialId))
        {
            Log.Error("[ProcedureLevel] 战斗窗口打开失败：{0}", failure.ErrorMessage);
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
