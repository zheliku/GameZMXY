using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameConfig;
using GameConfig.Constant;
using GameConfig.Entity;
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
using GameLogic.Manager;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 游戏流程。
/// </summary>
public class ProcedureGame : ProcedureBase
{
    private const int InitialLevelId = 1; // 进入游戏流程时用于筛选关卡多行配置的分组键。

    private WukongEntity m_Hero; // 当前流程创建并拥有的玩家实体。

    private LevelController m_Level; // 当前流程加载并拥有的关卡根控制器。

    private CancellationTokenSource m_SessionCancellation; // 离开流程时取消正在执行的实体生成。

    private int m_EntrySerial; // 让异步完成结果与当前流程进入周期绑定。

    private int m_LevelOwnerEntry; // 记录当前加载场景由哪个流程周期负责卸载。

    private string m_LevelScenePath; // 从 Luban LevelConfig 读取的场景路径。

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

            // LevelConfig 是每阶段一行的 list 表；读取 StageOrder=1 行取得关卡级元数据。
            GameConfig.Level.LevelConfig levelConfig = ConfigSystem.Instance.Tables.TbLevelConfig.DataList
                .FirstOrDefault(x => x.LevelId == InitialLevelId && x.StageOrder == 1);
            if (levelConfig == null)
            {
                throw new InvalidOperationException($"关卡配置不存在或缺少首阶段：LevelId={InitialLevelId}");
            }
            m_LevelScenePath = levelConfig.ScenePath;
            Log.Info("[ProcedureGame] 加载关卡场景：{0}", m_LevelScenePath);

            // 关卡场景只提供空间与稳定锚点；实体和生成配方由 GF.Entity + Luban 表驱动。
            // 场景加载本身不取消：若流程在加载中离开，继续等待成功事件后由 entrySerial 分支卸载，避免 additive 场景泄漏。
            Node2D levelNode = await LoadLevelSceneAsync(m_LevelScenePath, CancellationToken.None);
            if (entrySerial != m_EntrySerial)
            {
                if (m_LevelOwnerEntry == 0 && GF.Scene.IsSceneLoaded(m_LevelScenePath))
                {
                    GF.Scene.UnloadScene(m_LevelScenePath);
                }
                return;
            }

            // 先登记场景所有权，再校验根节点；失败路径也能卸载已加载场景。
            m_LevelOwnerEntry = entrySerial;
            m_Level = levelNode as LevelController;
            if (m_Level == null)
            {
                throw new InvalidOperationException("Level_1 场景根节点未绑定 LevelController。");
            }

            m_Level.Initialize();

            // 共享飘字服务挂在实体系统所在世界坐标系。
            DamagePopManager.Instance.Activate(GF.Entity);

            // 玩家出生位置由场景 SpawnPoint 提供。
            WukongEntity hero = await GF.Entity.ShowEntityAsync<WukongEntity>(EntityId.Wukong, null);
            if (entrySerial != m_EntrySerial)
            {
                GF.Entity.HideEntitySafe(hero);
                return;
            }

            if (hero == null)
            {
                throw new InvalidOperationException("Failed to create Wukong entity.");
            }

            m_Hero = hero;
            m_Hero.GlobalPosition = m_Level.PlayerSpawnPosition;

            // 首阶段配方由关卡表读取；MonsterEntity.OnShow 将 marker 世界坐标作为巡逻圆心。
            // 阶段配方从合并后的 LevelStageConfig 行读取。
            await m_Level.StartFirstStageAsync(GF.Entity, m_SessionCancellation.Token);
            if (entrySerial != m_EntrySerial)
            {
                return;
            }

            (GF.UI.GetUIForm(ResourcesCollectionConstant.UI_LoadingForm) as LoadingForm)?.CloseLoading();
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

    private static async Task<Node2D> LoadLevelSceneAsync(string scenePath, CancellationToken cancellationToken) // 订阅场景结果事件并安全等待加载完成。
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
            if (args is GodotGameFramework.Scene.LoadSceneSuccessEventArgs success &&
                success.SceneAssetName == scenePath)
            {
                tcs.TrySetResult(success.SceneInstance as Node2D);
                Log.Info("[ProcedureGame] 关卡场景加载成功：{0}", scenePath);
            }
        };
        EventHandler<GameEventArgs> onFailure = (_, args) =>
        {
            if (args is GodotGameFramework.Scene.LoadSceneFailureEventArgs failure &&
                failure.SceneAssetName == scenePath)
            {
                tcs.TrySetException(new InvalidOperationException(failure.ErrorMessage));
                Log.Error("[ProcedureGame] 关卡场景加载失败：{0}", failure.ErrorMessage);
            }
        };

        // 事件参数由框架池化，只在回调中复制场景节点引用。
        GF.Event.Subscribe(GodotGameFramework.Scene.LoadSceneSuccessEventArgs.EventId, onSuccess);
        GF.Event.Subscribe(GodotGameFramework.Scene.LoadSceneFailureEventArgs.EventId, onFailure);
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
            GF.Event.Unsubscribe(GodotGameFramework.Scene.LoadSceneSuccessEventArgs.EventId, onSuccess);
            GF.Event.Unsubscribe(GodotGameFramework.Scene.LoadSceneFailureEventArgs.EventId, onFailure);
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

        m_Level = null;
        m_Hero = null;
    }

    private void CleanupSession(int entrySerial) // 释放此流程周期拥有的实体、订阅和关卡场景。
    {
        // 先停共享服务和关卡事件，再隐藏实体，最后卸载场景。
        DamagePopManager.Instance.Deactivate();
        m_Level?.Cleanup(GF.Entity);
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
}
