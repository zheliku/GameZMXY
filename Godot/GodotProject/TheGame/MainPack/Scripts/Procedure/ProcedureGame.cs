using System;
using System.Threading;
using System.Threading.Tasks;
using GameConfig;
using GameConfig.Constant;
using GameConfig.Entity;
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
using GameLogic.Archive;
using GameLogic.UI;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 游戏流程。
/// </summary>
public class ProcedureGame : ProcedureBase
{
    private const int InitialLevelId = 1; // 当前游戏入口使用的关卡配置主键。

    private WukongEntity m_Hero; // 当前流程创建并拥有的玩家实体。

    private LevelController m_Level; // 当前流程加载并拥有的关卡根控制器。

    private CancellationTokenSource m_SessionCancellation; // 离开流程时取消正在执行的实体生成。

    private int m_EntrySerial; // 让异步完成结果与当前流程进入周期绑定。

    private int m_LevelOwnerEntry; // 记录当前加载场景由哪个流程周期负责卸载。

    private string m_LevelScenePath; // 从 Luban LevelConfig 读取的场景路径。

    private int m_HudSerialId; // 本会话打开的战斗 HUD 编号；0 表示未打开。

    private GameData m_ArchiveData; // 本次读档结果，仅供流程捕获普通数值快照。

    private Task m_SaveTask = Task.CompletedTask; // 上次退出的保存任务，再次进入前先等待。

    /// <summary>
    /// 进入流程。
    /// 加载配置、重置游戏状态、创建并启动游戏状态 FSM。
    /// </summary>
    /// <param name="procedureOwner">当前流程状态机。</param>
    protected internal override async void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);
        int entrySerial = ++m_EntrySerial;

        // 标记启动成功：游戏已进入可玩状态，后续崩溃不再归因于热更
        HotUpdateSafetyGuard.MarkStartupSuccess();

        try
        {
            m_SessionCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = m_SessionCancellation.Token;
            var entities = GF.Entity;

            // 同一流程重入先等待上次写档，避免旧保存覆盖本次读档结果。
            await m_SaveTask;
            if (entrySerial != m_EntrySerial)
            {
                return;
            }
            await GF.Archive.LoadAsync();
            if (entrySerial != m_EntrySerial)
            {
                return;
            }
            GameData archiveData = GF.Archive.CurrentData;
            if (archiveData == null || GF.Archive.CurrentCatalogue == null ||
                archiveData.UnitId != GF.Archive.CurrentCatalogue.UnitId)
            {
                throw new InvalidOperationException("没有成功读取有效的当前存档。");
            }
            archiveData.Player ??= new PlayerSaveData();
            m_ArchiveData = archiveData;

            // LevelConfig 是以 LevelId 为主键的 map 表：一条记录包含关卡元数据和嵌套的阶段/配方列表。
            LevelConfig levelConfig = ConfigSystem.Instance.Tables.TbLevelConfig.GetOrDefault(InitialLevelId);
            if (levelConfig == null)
            {
                throw new InvalidOperationException($"关卡配置不存在：LevelId={InitialLevelId}");
            }
            m_LevelScenePath = levelConfig.ScenePath;
            string scenePath = m_LevelScenePath;
            Log.Info("[ProcedureGame] 加载关卡场景：{0}", m_LevelScenePath);

            // 关卡场景只提供空间与稳定锚点；实体和生成配方由 GF.Entity + Luban 表驱动。
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
            m_Level = levelNode as LevelController;
            if (m_Level == null)
            {
                throw new InvalidOperationException($"{m_LevelScenePath} 场景根节点未绑定 LevelController。");
            }

            m_Level.Initialize();
            m_Level.Failed += OnLevelFailed;

            // 共享飘字服务挂在实体系统所在世界坐标系。
            DamagePopManager.Instance.Activate(GF.Entity);

            // 英雄复制普通存档数值，运行时属性归实体；等待后使用捕获的实体服务清理旧结果。
            WukongEntity hero = await entities.ShowEntityAsync<WukongEntity>(EntityId.Wukong, archiveData.Player);
            if (entrySerial != m_EntrySerial)
            {
                entities.HideEntitySafe(hero);
                return;
            }

            if (hero == null)
            {
                throw new InvalidOperationException("Failed to create Wukong entity.");
            }

            m_Hero = hero;
            if (hero.Config == null || !hero.IsShown)
            {
                throw new InvalidOperationException("英雄初始化失败。");
            }
            m_Hero.GlobalPosition = m_Level.PlayerSpawnPosition;
            m_Hero.TotalExperience.Changed += OnExperienceChanged;

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

            // 先订阅结果再提交请求，缓存命中也能处理；失败参数只在事件分发期间使用。
            GF.Event.Subscribe(OpenUIFormFailureEventArgs.EventId, OnHudOpenFailed);
            m_HudSerialId = GF.UI.OpenUIForm(UIFormId.BattleHud, new BattleHudContext(m_Hero, m_Level));
            Log.Info("[ProcedureGame] 已提交战斗 HUD 请求：{0}", m_HudSerialId);
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                return;
            }

            Log.Error("[ProcedureGame] 关卡启动失败：{0}", ex);
            if (entrySerial == m_EntrySerial)
            {
                CleanupSession(entrySerial);
                (GF.UI.GetUIForm(ResourcesCollectionConstant.UI_LoadingForm) as LoadingForm)?.CloseLoading();
            }
        }
    }

    /// <summary>
    /// 离开流程。
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
        else if (m_Hero != null)
        {
            m_Hero.TotalExperience.Changed -= OnExperienceChanged;
        }

        m_Level = null;
        m_Hero = null;
        m_ArchiveData = null;
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
                Log.Info("[ProcedureGame] 关卡场景加载成功：{0}", scenePath);
            }
        };
        EventHandler<GameEventArgs> onFailure = (_, args) =>
        {
            if (args is LoadSceneFailureEventArgs failure &&
                failure.SceneAssetName == scenePath)
            {
                tcs.TrySetException(new InvalidOperationException(failure.ErrorMessage));
                Log.Error("[ProcedureGame] 关卡场景加载失败：{0}", failure.ErrorMessage);
            }
        };

        // 事件参数由框架池化，只在回调中复制场景节点引用。
        GF.Event.Subscribe(LoadSceneSuccessEventArgs.EventId, onSuccess);
        GF.Event.Subscribe(LoadSceneFailureEventArgs.EventId, onFailure);
        try
        {
            // 使用 Additive 保留常驻框架根场景。
            GF.Scene.LoadScene(scenePath, LoadSceneMode.Additive);
            Log.Info("[ProcedureGame] 已提交关卡加载请求：{0}", scenePath);
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

    /// <summary>停止本周期，先解除 HUD，再捕获存档快照并释放实体与场景；重复清理安全。</summary>
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

        // 先关 HUD（退订英雄/关卡）再拆会话，避免界面在已清理的引用上刷新。
        if (m_HudSerialId != 0)
        {
            if (GF.UI.HasUIForm(m_HudSerialId) || GF.UI.IsLoadingUIForm(m_HudSerialId))
            {
                GF.UI.CloseUIForm(m_HudSerialId);
                Log.Info("[ProcedureGame] 战斗 HUD 关闭：{0}", m_HudSerialId);
            }

            m_HudSerialId = 0;
        }
        if (GF.Event.Check(OpenUIFormFailureEventArgs.EventId, OnHudOpenFailed))
        {
            GF.Event.Unsubscribe(OpenUIFormFailureEventArgs.EventId, OnHudOpenFailed);
        }

        // 回收前复制值；之后的异步保存不再读取实体或已清空的流程字段。
        if (m_Hero?.Config != null && m_ArchiveData != null)
        {
            m_Hero.TotalExperience.Changed -= OnExperienceChanged;
            PlayerSaveData snapshot = new()
            {
                Level = m_Hero.Level.Value,
                TotalExperience = m_Hero.TotalExperience.Value,
                Gold = m_Hero.Gold.Value,
            };
            m_SaveTask = SavePlayerAsync(m_SaveTask, m_ArchiveData, snapshot);
        }
        m_ArchiveData = null;

        // 先停共享服务和关卡事件，再隐藏实体，最后卸载场景。
        DamagePopManager.Instance.Deactivate();
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

    /// <summary>经验结算完成后捕获普通快照，串行保存避免较旧奖励覆盖最新进度。</summary>
    /// <param name="totalExperience">本次结算的累计经验，等级已同步更新。</param>
    private void OnExperienceChanged(int totalExperience)
    {
        PlayerSaveData snapshot = new()
        {
            Level = m_Hero.Level.Value,
            TotalExperience = totalExperience,
            Gold = m_Hero.Gold.Value,
        };
        m_SaveTask = SavePlayerAsync(m_SaveTask, m_ArchiveData, snapshot);
    }

    /// <summary>等待旧写档后覆盖当前存档并观察异常；框架只记录磁盘写入结果。</summary>
    /// <param name="previousSave">已经提交的保存任务。</param>
    /// <param name="data">清理前捕获的存档对象。</param>
    /// <param name="snapshot">清理前捕获的玩家普通数值。</param>
    /// <returns>本次保存调用完成的任务。</returns>
    private static async Task SavePlayerAsync(Task previousSave, GameData data, PlayerSaveData snapshot)
    {
        var archive = GF.Archive;
        try
        {
            // 等待前捕获服务与普通值，之后不读取流程或实体字段。
            await previousSave;
            if (!ReferenceEquals(archive.CurrentData, data))
            {
                throw new InvalidOperationException("当前存档已经切换，拒绝将旧英雄快照写入新存档。");
            }
            data.Player = snapshot;
            await archive.OverWriteAsync();
        }
        catch (Exception ex)
        {
            Log.Error("[ProcedureGame] 保存存档失败：{0}", ex);
        }
    }

    /// <summary>HUD 打开失败时只处理本流程拥有的请求，统一结束关卡。</summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="args">池化事件参数，只在本次分发内读取。</param>
    private void OnHudOpenFailed(object sender, GameEventArgs args)
    {
        if (args is OpenUIFormFailureEventArgs failure && failure.SerialId == m_HudSerialId)
        {
            string message = failure.ErrorMessage;
            Log.Error("[ProcedureGame] 战斗 HUD 打开失败：{0}", message);
            CleanupSession(m_EntrySerial);
        }
    }

    /// <summary>关卡异步显示失败时统一清理，不留下锁死的战斗区域。</summary>
    /// <param name="error">关卡运行错误。</param>
    private void OnLevelFailed(Exception error)
    {
        Log.Error("[ProcedureGame] 关卡运行失败：{0}", error);
        CleanupSession(m_EntrySerial);
    }
}
