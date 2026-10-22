using IUIForm = GameFramework.UI.IUIForm;
using GameLogic.Battle;
using GameLogic.Config;
using GameLogic.Architecture;
using GameLogic.Equipment;
using GameLogic.Save;
using GameLogic.Battle.Attributes;
using GameLogic.Procedure.FlowData;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameConfig;
using GameConfig.Constant;
using GameConfig.Level;
using GameConfig.Sound;
using GameFramework.Event;
using GameFramework.Procedure;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using GodotGameFramework.Scene;
using GodotGameFramework.UI;
using GameLogic;
using GameLogic.Heroes;
using GameLogic.Heroes.Body;
using GameLogic.Level;
using GameLogic.Profile;
using GameLogic.UI;
using GameLogic.Audio;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 关卡流程：负责一次关卡运行的装配与拆除——加载关卡场景、按档案显示英雄、开始 <see cref="LevelSystem"/>、打开 HUD；
/// 通关或死亡提交检查点后显示结算，再由玩家返回地图或重试。
/// 收益结算与关卡事务归 <see cref="LevelSystem"/>，档案状态由流程 FSM 持有，本流程只管作用域的创建顺序与逆序清理。
/// </summary>
/// <remarks>本流程拥有本周期的场景加载、英雄显示、窗口与音乐请求；从流程 FSM 借用档案和系统，离关不移除档案。</remarks>
public class ProcedureLevel : ProcedureBase
{
    private bool m_PendingSettlement; // 待下一流程帧打开结算，避免输入回调内操作窗口。
    private int m_InventorySerialId; // 本周期的背包窗口，包括在途请求。
    private int m_SkillSerialId; // 本周期的技能目录窗口，包括在途请求。
    private int m_SettlementSerialId; // 本周期的结算窗口。
    private bool m_Navigating; // 检查点等待期间拒绝重复导航。
    private ProcedureOwner m_Owner; // 当前流程状态机，重开关卡时使用。

    private AbstractHeroEntity m_Hero; // 当前流程创建并拥有的玩家实体。

    private LevelViewController m_LevelViewController; // 流程加载并卸载的场景控制器。
    private LevelModel m_LevelModel; // 借用：由流程 FSM 独立拥有的本局状态。
    private LevelSystem m_LevelSystem; // 借用：流程 FSM 独立注册的关卡业务系统。


    private CancellationTokenSource m_SessionCancellation; // 离开流程时取消正在执行的实体生成。

    private int m_EntrySerial; // 让异步完成结果与当前流程进入周期绑定。

    private int m_LevelOwnerEntry; // 记录当前加载场景由哪个流程周期负责卸载。

    private string m_LevelScenePath; // 从 Luban LevelConfig 读取的场景路径。

    private int m_BattleFormSerialId; // 本会话打开的战斗 HUD 编号；0 表示未打开。
    private TaskCompletionSource m_BattleFormReady; // 本周期 HUD 完成装配后才开放关卡。

    private int m_MusicSerial; // 本关 GGF 音乐请求，清理时连在途加载一起取消。
    private int m_SettingsSerialId; // 本周期的设置窗口，含正在加载的请求。

    /// <summary>
    /// 进入流程：并行准备场景和 HUD 资源 → 显示英雄并装配 HUD → 关闭加载界面 → 开始关卡。
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
            Tables tables = procedureOwner.GetModel<ConfigurationModel>().Tables;
            var profile = procedureOwner.GetModel<ProfileModel>();
            var saveSystem = procedureOwner.GetSystem<SaveSystem>();

            // 上一局的检查点必须先落盘：重开前等待，失败只记录（档案仍在内存，下一检查点会再写）。
            if (!await saveSystem.PendingWrite)
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
            m_LevelScenePath = levelConfig.ScenePath;
            string scenePath = m_LevelScenePath;
            Log.Info("[ProcedureLevel] 加载关卡场景：{0}", m_LevelScenePath);

            // 场景加载本身不取消：若流程在加载中离开，继续等待成功事件后由 entrySerial 分支卸载，避免 additive 场景泄漏。
            string battleFormPath = tables.TbUIFormConfig.DataList.Single(form => form.UIFormId == UIFormId.BattleForm).AssetPath;
            var battleFormAsset = GF.Resource.LoadAssetAsync(battleFormPath);
            Task<Node2D> levelScene = LoadLevelSceneAsync(scenePath, CancellationToken.None);
            try
            {
                await Task.WhenAll(levelScene, battleFormAsset);
            }
            catch
            {
                // 任一并行加载失败，仍回收已经成功挂树且未交给新周期的场景。
                if (levelScene.IsCompletedSuccessfully && m_LevelOwnerEntry == 0 && GF.Scene.IsSceneLoaded(scenePath))
                    GF.Scene.UnloadScene(scenePath);
                throw;
            }
            Node2D levelNode = levelScene.Result;
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
            var levelViewController = levelNode as LevelViewController
                      ?? throw new InvalidOperationException($"{m_LevelScenePath} 场景根节点未绑定 LevelViewController。");
            m_LevelViewController = levelViewController;
            m_LevelModel = new LevelModel();
            procedureOwner.RegisterModel(m_LevelModel);
            procedureOwner.RegisterModel(new HeroModel());
            procedureOwner.RegisterSystem(new HeroSystem(procedureOwner.GetArchitecture()));
            m_LevelSystem = new LevelSystem(procedureOwner.GetArchitecture());
            procedureOwner.RegisterSystem(m_LevelSystem);
            levelViewController.Initialize(procedureOwner.GetArchitecture());
            m_MusicSerial = GF.Sound.PlayConfigured(levelViewController.MusicId, 0.4f);
            levelViewController.Failed += OnLevelFailed;

            // 属性系统已准备全部数据，实体显示参数不再携带模型或装配包装。
            WukongEntity hero =
                await entities.ShowEntityAsync<WukongEntity>(GameConfig.Entity.EntityId.Wukong);
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

            m_Hero.GlobalPosition = m_LevelViewController.PlayerSpawnPosition;

            // HUD 依赖与场景并行加载；先装配并绑定窗口，避免开战后再等首帧 UI 加载。
            var battleFormReady = new TaskCompletionSource();
            m_BattleFormReady = battleFormReady;
            GF.Event.Subscribe(OpenUIFormSuccessEventArgs.EventId, OnFormOpened);
            GF.Event.Subscribe(OpenUIFormFailureEventArgs.EventId, OnBattleFormOpenFailed);
            m_BattleFormSerialId = GF.UI.OpenUIForm(UIFormId.BattleForm, new BattleFormData(m_Hero, m_LevelViewController));
            var battleForm = GF.UI.GetUIForm(m_BattleFormSerialId);
            SetFormCallbacks(battleForm, bind: true);
            if (battleForm != null)
                battleFormReady.TrySetResult();
            await battleFormReady.Task.WaitAsync(cancellationToken);
            // 保持预载资源存活到 GGF 窗口取得自己的资源引用，不另建常驻缓存。
            GC.KeepAlive(battleFormAsset.Result);

            // 首波与相机在 HUD 就绪后再开放；加载页覆盖期间不推进关卡。
            if (GF.UI.GetUIForm(ResourcesCollectionConstant.UI_LoadingForm) is LoadingForm loading)
            {
                await loading.CloseLoadingAsync(cancellationToken);
            }

            if (entrySerial != m_EntrySerial)
            {
                return;
            }

            m_LevelModel.Outcome.Register(OnLevelEnded);
            m_LevelViewController.StartSession(entities, m_Hero, cancellationToken);

            Log.Info("[ProcedureLevel] 战斗 HUD 已就绪，开始关卡：{0}", m_BattleFormSerialId);
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
        if (GF.Event.Check(OpenUIFormSuccessEventArgs.EventId, OnFormOpened))
            GF.Event.Unsubscribe(OpenUIFormSuccessEventArgs.EventId, OnFormOpened);
        SetFormCallbacks(GF.UI.GetUIForm(m_BattleFormSerialId), bind: false);
        SetFormCallbacks(GF.UI.GetUIForm(m_SettingsSerialId), bind: false);
        SetFormCallbacks(GF.UI.GetUIForm(m_SettlementSerialId), bind: false);


        m_PendingSettlement = false;
        base.OnLeave(procedureOwner, isShutdown);
        int entrySerial = m_EntrySerial;
        m_EntrySerial++;
        m_SessionCancellation?.Cancel();
        m_SessionCancellation?.Dispose();
        m_SessionCancellation = null;
        m_BattleFormReady = null;

        if (!isShutdown)
        {
            CleanupSession(entrySerial);
        }
        else
        {
            // 关停：进程即将结束，只解除本作用域订阅；磁盘上仍是上一个检查点。
            m_LevelViewController?.UnsubscribeProgressEvents();
        }

        m_LevelViewController = null;
        m_LevelSystem = null;
        m_LevelModel = null;
        m_Hero = null;
        m_Owner = null;
    }

    /// <summary>在事件栈退出后的流程帧打开结算，死亡先等待真实动作完成。</summary>
    /// <param name="procedureOwner">当前流程状态机。</param>
    /// <param name="elapseSeconds">本帧游戏秒数。</param>
    /// <param name="realElapseSeconds">本帧实际秒数。</param>
    protected internal override void OnUpdate(ProcedureOwner procedureOwner, float elapseSeconds,
        float realElapseSeconds)
    {
        base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);
        if (!m_PendingSettlement)
            return;
        m_PendingSettlement = false;
        // 结算属于本关流程：停止旧音乐（包括在途请求），成功和失败曲仍由同一个 GGF 请求拥有者清理。
        if (m_MusicSerial != 0)
            GF.Sound.StopSound(m_MusicSerial);
        m_MusicSerial = GF.Sound.PlayConfigured(m_LevelModel.Outcome.Value == LevelOutcome.Cleared
            ? SoundId.SettlementVictory : SoundId.SettlementDefeat, 0.1f);
        m_SettlementSerialId = GF.UI.OpenUIForm(UIFormId.SettlementForm, new SettlementFormData(m_LevelModel.Result));
        SetFormCallbacks(GF.UI.GetUIForm(m_SettlementSerialId), bind: true);
    }

    /// <summary>收到左下角设置点击后打开暂停菜单。</summary>
    /// <param name="formSerialId">发起操作的当前窗口编号。</param>
    private void OnSettingsRequested(int formSerialId)
    {
        if (formSerialId != m_BattleFormSerialId ||
            m_LevelModel?.Outcome.Value != LevelOutcome.Running)
            return;
        if (HasWindow(m_SettingsSerialId) || HasWindow(m_InventorySerialId) || HasWindow(m_SkillSerialId))
            return;
        m_SettingsSerialId = GF.UI.OpenUIForm(UIFormId.SettingsForm);
        SetFormCallbacks(GF.UI.GetUIForm(m_SettingsSerialId), bind: true);
    }

    /// <summary>在同一关卡中互斥打开背包与设置。</summary>
    /// <param name="formSerialId">发起操作的当前窗口编号。</param>
    private void OnInventoryRequested(int formSerialId)
    {
        if (formSerialId != m_BattleFormSerialId ||
            m_LevelModel?.Outcome.Value != LevelOutcome.Running)
            return;
        if (HasWindow(m_SettingsSerialId) || HasWindow(m_InventorySerialId) || HasWindow(m_SkillSerialId))
            return;
        m_InventorySerialId =
            GF.UI.OpenUIForm(UIFormId.InventoryForm, new InventoryFormData(m_Hero));
    }

    /// <summary>互斥打开本关技能目录；展示当前出战角色。</summary>
    /// <param name="formSerialId">发起操作的当前窗口编号。</param>
    private void OnSkillsRequested(int formSerialId)
    {
        if (formSerialId != m_BattleFormSerialId ||
            m_LevelModel?.Outcome.Value != LevelOutcome.Running ||
            m_Hero == null || !m_Hero.IsAlive)
            return;
        if (HasWindow(m_SettingsSerialId) || HasWindow(m_InventorySerialId) || HasWindow(m_SkillSerialId))
            return;
        m_SkillSerialId = GF.UI.OpenUIForm(UIFormId.SkillForm);
    }

    /// <summary>设置的放弃与结算的返回地图使用各自的窗口编号校验。</summary>
    /// <param name="formSerialId">发起操作的当前窗口编号。</param>
    private void OnMapRequested(int formSerialId)
    {
        int serial = formSerialId;
        if (serial != 0 && serial == m_SettlementSerialId && m_LevelModel?.Result != null)
            NavigateAfterCheckpointAsync(m_Owner, m_EntrySerial, retry: false);
        else if (serial != 0 && serial == m_SettingsSerialId && m_LevelModel?.Outcome.Value == LevelOutcome.Running)
            ChangeState<ProcedureMap>(m_Owner);
    }

    /// <summary>只有当前失败结算窗口可以重新挑战。</summary>
    /// <param name="formSerialId">发起操作的当前窗口编号。</param>
    private void OnRetryRequested(int formSerialId)
    {
        int serial = formSerialId;
        if (serial != 0 && serial == m_SettlementSerialId && m_LevelModel?.Outcome.Value == LevelOutcome.Defeated)
            NavigateAfterCheckpointAsync(m_Owner, m_EntrySerial, retry: true);
    }

    /// <summary>检查窗口是否仍打开或正在加载。</summary>
    /// <param name="serial">窗口请求编号；零表示无请求。</param>
    /// <returns>窗口仍属于本周期时为 true。</returns>
    private static bool HasWindow(int serial) =>
        serial != 0 && (GF.UI.HasUIForm(serial) || GF.UI.IsLoadingUIForm(serial));

    /// <summary>从设置返回标题，离开边界负责回滚未完成的关卡。</summary>
    /// <param name="formSerialId">发起操作的当前窗口编号。</param>
    private void OnTitleRequested(int formSerialId)
    {
        if (formSerialId == m_SettingsSerialId &&
            m_LevelModel?.Outcome.Value == LevelOutcome.Running)
            ChangeState<ProcedureTitle>(m_Owner);
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
    /// 停止本周期并按创建逆序拆除：HUD → 飘字 → 关卡系统（未结束则回滚） → 英雄 → 场景；重复清理安全。
    /// </summary>
    /// <param name="entrySerial">被清理的流程进入周期。</param>
    private void CleanupSession(int entrySerial)
    {
        SetFormCallbacks(GF.UI.GetUIForm(m_BattleFormSerialId), bind: false);
        SetFormCallbacks(GF.UI.GetUIForm(m_SettingsSerialId), bind: false);
        SetFormCallbacks(GF.UI.GetUIForm(m_SettlementSerialId), bind: false);
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
        m_BattleFormReady = null;
        if (GF.Event.Check(OpenUIFormSuccessEventArgs.EventId, OnFormOpened))
            GF.Event.Unsubscribe(OpenUIFormSuccessEventArgs.EventId, OnFormOpened);
        // 模态窗口先解除暂停和运行订阅，再清理 HUD、实体及关卡事务。
        if (HasWindow(m_InventorySerialId))
            GF.UI.CloseUIForm(m_InventorySerialId);
        if (HasWindow(m_SkillSerialId))
            GF.UI.CloseUIForm(m_SkillSerialId);
        if (HasWindow(m_SettlementSerialId))
            GF.UI.CloseUIForm(m_SettlementSerialId);
        m_InventorySerialId = m_SkillSerialId = m_SettlementSerialId = 0;
        if (m_SettingsSerialId != 0 &&
            (GF.UI.HasUIForm(m_SettingsSerialId) || GF.UI.IsLoadingUIForm(m_SettingsSerialId)))
            GF.UI.CloseUIForm(m_SettingsSerialId);
        m_SettingsSerialId = 0;
        if (m_MusicSerial != 0)
            GF.Sound.StopSound(m_MusicSerial, 0.2f);
        m_MusicSerial = 0;

        // 先关 HUD（退订英雄/成长/关卡）再拆会话，避免界面在已清理的引用上刷新。
        if (m_BattleFormSerialId != 0)
        {
            if (GF.UI.HasUIForm(m_BattleFormSerialId) || GF.UI.IsLoadingUIForm(m_BattleFormSerialId))
            {
                GF.UI.CloseUIForm(m_BattleFormSerialId);
                Log.Info("[ProcedureLevel] 战斗 HUD 关闭：{0}", m_BattleFormSerialId);
            }

            m_BattleFormSerialId = 0;
        }

        if (GF.Event.Check(OpenUIFormFailureEventArgs.EventId, OnBattleFormOpenFailed))
        {
            GF.Event.Unsubscribe(OpenUIFormFailureEventArgs.EventId, OnBattleFormOpenFailed);
        }

        // 先停表现和关卡事件，再隐藏实体，最后卸载场景。
        if (m_LevelViewController != null)
        {
            m_LevelViewController.Failed -= OnLevelFailed;
            m_LevelModel.Outcome.UnRegister(OnLevelEnded);
            m_LevelViewController.Cleanup();
        }

        if (m_LevelSystem != null)
            m_Owner.RemoveSystem<LevelSystem>();
        if (m_LevelModel != null)
            m_Owner.RemoveModel<LevelModel>();
        GF.Entity.HideEntitySafe(m_Hero);
        m_Owner.RemoveSystem<HeroSystem>();
        m_Owner.RemoveModel<HeroModel>();
        bool ownsScene = m_LevelOwnerEntry == entrySerial;
        if (ownsScene)
        {
            if (m_LevelScenePath != null && GF.Scene.IsSceneLoaded(m_LevelScenePath))
            {
                GF.Scene.UnloadScene(m_LevelScenePath);
            }

            m_LevelOwnerEntry = 0;
        }

        m_LevelViewController = null;
        m_LevelSystem = null;
        m_LevelModel = null;
        m_Hero = null;
        if (ownsScene)
        {
            m_LevelScenePath = null;
        }
    }

    /// <summary>出口直接登记结算；失败等待原生死亡动画结束，离开流程取消旧周期等待。</summary>
    /// <param name="outcome">本次关卡结局。</param>
    private async void OnLevelEnded(LevelOutcome outcome)
    {
        if (outcome is not (LevelOutcome.Cleared or LevelOutcome.Defeated) || m_Owner == null)
            return;
        int entrySerial = m_EntrySerial;
        CancellationToken token = m_SessionCancellation.Token;
        var hero = m_Hero;
        var owner = m_Owner;
        try
        {
            if (outcome == LevelOutcome.Defeated)
            {
                // 在身体死亡状态开始前订阅；动画暂停时自然继续等待，不另建秒数计时。
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                void OnAnimationFinished(StringName animation)
                {
                    if (animation == HeroAnims.Death)
                        completion.TrySetResult();
                }
                var player = hero.AnimPlayer;
                player.AnimationFinished += OnAnimationFinished;
                try
                {
                    using var registration = token.Register(() => completion.TrySetCanceled(token));
                    await completion.Task;
                }
                finally
                {
                    if (GodotObject.IsInstanceValid(player))
                        player.AnimationFinished -= OnAnimationFinished;
                }
            }
            if (entrySerial == m_EntrySerial && !owner.IsDestroyed && !token.IsCancellationRequested)
                m_PendingSettlement = true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    /// <summary>等待检查点后执行玩家选择；旧周期异步结果不触发导航。</summary>
    /// <param name="owner">流程状态机。</param>
    /// <param name="entrySerial">发起重开时的进入周期。</param>
    /// <param name="retry">失败后重试为 true，否则返回地图。</param>
    private async void NavigateAfterCheckpointAsync(ProcedureOwner owner, int entrySerial, bool retry)
    {
        if (entrySerial != m_EntrySerial || owner.IsDestroyed || m_Navigating)
            return;
        m_Navigating = true;

        // 返回地图前等待本次检查点完成；写入失败仍保留内存进度，地图可手动重试保存。
        Task<bool> write = m_LevelSystem?.CheckpointWrite;
        if (write != null && !await write)
            Log.Error("[ProcedureLevel] 检查点写入失败，当前进度仍保留在内存中，请在地图重新保存。");
        if (entrySerial != m_EntrySerial || owner.IsDestroyed)
            return;
        if (retry)
            ChangeState<ProcedureLevel>(owner);
        else
            ChangeState<ProcedureMap>(owner);
    }

    /// <summary>战斗窗口打开失败时只处理本流程拥有的请求，统一结束关卡。</summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="args">池化事件参数，只在本次分发内读取。</param>
    private void OnBattleFormOpenFailed(object sender, GameEventArgs args)
    {
        if (args is OpenUIFormFailureEventArgs failure && (failure.SerialId == m_BattleFormSerialId ||
                                                           failure.SerialId == m_InventorySerialId ||
                                                           failure.SerialId == m_SkillSerialId ||
                                                           failure.SerialId == m_SettlementSerialId))
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

    /// <summary>异步窗口打开后接上本流程拥有的回调。</summary>
    /// <param name="sender">框架 UI。</param>
    /// <param name="args">当次打开结果。</param>
    private void OnFormOpened(object sender, GameEventArgs args)
    {
        if (args is OpenUIFormSuccessEventArgs opened)
        {
            SetFormCallbacks(opened.UIForm, bind: true);
            if (opened.UIForm.SerialId == m_BattleFormSerialId)
                m_BattleFormReady?.TrySetResult();
        }
    }

    /// <summary>按实际窗口类型绑定本流程回调；窗口编号拒绝旧作用域和无关实例。</summary>
    /// <param name="form">已打开窗口；在途时为空。</param>
    /// <param name="bind">绑定或解除。</param>
    private void SetFormCallbacks(IUIForm form, bool bind)
    {
        if (bind && (form == null || !GF.UI.IsValidUIForm(form)))
            return;
        switch (form)
        {
            case BattleForm battleForm when battleForm.SerialId == m_BattleFormSerialId:
                battleForm.SettingsRequested -= OnSettingsRequested;
                if (bind)
                    battleForm.SettingsRequested += OnSettingsRequested;
                battleForm.InventoryRequested -= OnInventoryRequested;
                if (bind)
                    battleForm.InventoryRequested += OnInventoryRequested;
                battleForm.SkillsRequested -= OnSkillsRequested;
                if (bind)
                    battleForm.SkillsRequested += OnSkillsRequested;
                break;
            case SettingsForm settingsForm when settingsForm.SerialId == m_SettingsSerialId:
                settingsForm.TitleRequested -= OnTitleRequested;
                if (bind)
                    settingsForm.TitleRequested += OnTitleRequested;
                settingsForm.MapRequested -= OnMapRequested;
                if (bind)
                    settingsForm.MapRequested += OnMapRequested;
                break;
            case SettlementForm settlementForm when settlementForm.SerialId == m_SettlementSerialId:
                settlementForm.MapRequested -= OnMapRequested;
                if (bind)
                    settlementForm.MapRequested += OnMapRequested;
                settlementForm.RetryRequested -= OnRetryRequested;
                if (bind)
                    settlementForm.RetryRequested += OnRetryRequested;
                break;
        }
    }
}
