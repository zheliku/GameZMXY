using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GameConfig;
using GameConfig.Constant;
using GameConfig.Hero;
using GameConfig.Level;
using GameConfig.Sound;
using GameFramework.Event;
using GameFramework.Procedure;
using GameLogic;
using GameLogic.Architecture;
using GameLogic.Audio;
using GameLogic.Battle;
using GameLogic.Battle.Attributes;
using GameLogic.Config;
using GameLogic.Equipment;
using GameLogic.Heroes;
using GameLogic.Level;
using GameLogic.Procedure.FlowData;
using GameLogic.Profile;
using GameLogic.Save;
using GameLogic.UI;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using GodotGameFramework.Scene;
using GodotGameFramework.UI;
using IUIForm = GameFramework.UI.IUIForm;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 关卡流程：负责一次关卡运行的装配与拆除——加载关卡场景、按档案显示英雄、开始 <see cref="LevelSystem"/>、打开 HUD；
/// 通关或死亡提交检查点后显示结算，再由玩家返回地图或重试。
/// 收益结算与关卡事务归 <see cref="LevelSystem"/>，档案状态由流程 FSM 持有，本流程只管作用域的创建顺序与逆序清理。
/// </summary>
/// <remarks>本流程拥有本周期的场景加载、英雄显示、窗口与音乐请求；从流程 FSM 借用档案和系统，离关不移除档案。</remarks>
public class ProcedureLevel : ProcedureBase
{
    private bool _pendingSettlement; // 待下一流程帧打开结算，避免输入回调内操作窗口。
    private int _inventorySerialId; // 本周期的背包窗口，包括在途请求。
    private int _skillSerialId; // 本周期的技能目录窗口，包括在途请求。
    private int _settlementSerialId; // 本周期的结算窗口。
    private bool _navigating; // 检查点等待期间拒绝重复导航。
    private int _pendingHiddenLevelId; // 原生入口完成后，下一流程帧重入隐藏关。
    private ProcedureOwner _owner; // 当前流程状态机，重开关卡时使用。

    private readonly List<AbstractHeroEntity> _heroes = new(); // 本流程显示并拥有的参战英雄，按席位排列。

    private LevelViewController _levelViewController; // 流程加载并卸载的场景控制器。
    private LevelModel _levelModel; // 借用：由流程 FSM 独立拥有的本局状态。
    private LevelSystem _levelSystem; // 借用：流程 FSM 独立注册的关卡业务系统。

    private CancellationTokenSource _sessionCancellation; // 离开流程时取消正在执行的实体生成。

    private int _entrySerial; // 让异步完成结果与当前流程进入周期绑定。

    private int _levelOwnerEntry; // 记录当前加载场景由哪个流程周期负责卸载。

    private string _levelScenePath; // 从 Luban LevelConfig 读取的场景路径。

    private int _battleFormSerialId; // 本会话打开的战斗 HUD 编号；0 表示未打开。
    private TaskCompletionSource _battleFormReady; // 本周期 HUD 完成装配后才开放关卡。

    private int _musicSerial; // 本关 GGF 音乐请求，清理时连在途加载一起取消。
    private int _settingsSerialId; // 本周期的设置窗口，含正在加载的请求。

    /// <summary>
    /// 进入流程：并行准备场景和 HUD 资源 → 显示英雄并装配 HUD → 关闭加载界面 → 开始关卡。
    /// </summary>
    /// <param name="procedureOwner">当前流程状态机。</param>
    protected internal override async void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);
        int entrySerial = ++_entrySerial;
        var shownHeroIds = new List<int>(); // 本周期已显示的编号；晚到回收不触碰新周期复用的实体实例。
        _owner = procedureOwner;


        try
        {
            _sessionCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = _sessionCancellation.Token;
            EntityComponent entities = GF.Entity;
            Tables tables = procedureOwner.GetModel<ConfigurationModel>().Tables;
            ProfileModel profile = procedureOwner.GetModel<ProfileModel>();
            SaveSystem saveSystem = procedureOwner.GetSystem<SaveSystem>();

            // 上一局的检查点必须先落盘：重开前等待，失败只记录（档案仍在内存，下一检查点会再写）。
            if (!await saveSystem.PendingWrite)
            {
                Log.Error("[ProcedureLevel] 上一个检查点写入失败，进度仍保留在内存中。");
            }

            if (entrySerial != _entrySerial)
            {
                return;
            }

            // LevelConfig 是以 LevelId 为主键的 map 表：一条记录包含关卡元数据和嵌套的阶段/配方列表。
            int selectedLevelId = procedureOwner.GetData<VarInt32>(FlowKeys.SelectedLevelId)?.Value
                                  ?? throw new InvalidOperationException("关卡流程缺少地图选择结果。");
            LevelConfig levelConfig = tables.TbLevelConfig.GetOrDefault(selectedLevelId)
                                      ?? throw new InvalidOperationException($"关卡配置不存在：LevelId={selectedLevelId}");
            _levelScenePath = levelConfig.ScenePath;
            string scenePath = _levelScenePath;
            var loadingWatch = System.Diagnostics.Stopwatch.StartNew();
            Log.Info("[ProcedureLevel] 加载关卡场景：{0}", _levelScenePath);

            // 场景加载本身不取消：若流程在加载中离开，继续等待成功事件后由 entrySerial 分支卸载，避免 additive 场景泄漏。
            string battleFormPath = tables.TbUIFormConfig.DataList.Single(form => form.UIFormId == UIFormId.BattleForm).AssetPath;
            Task<Resource> battleFormAsset = GF.Resource.LoadAssetAsync(battleFormPath);
            HeroConfig[] heroConfigs = profile.HeroIds.Select(tables.TbHeroConfig.Get).ToArray();
            Task<Resource>[] heroAssets = heroConfigs.Select(config =>
                GF.Resource.LoadAssetAsync(tables.TbEntityConfig.Get(config.EntityId).AssetPath)).ToArray();
            Task<Node2D> levelScene = LoadLevelSceneAsync(scenePath, CancellationToken.None);
            try
            {
                await Task.WhenAll(heroAssets.Cast<Task>().Append(levelScene).Append(battleFormAsset));
            }
            catch
            {
                // 任一并行加载失败，仍回收已经成功挂树且未交给新周期的场景。
                if (levelScene.IsCompletedSuccessfully && _levelOwnerEntry == 0 && GF.Scene.IsSceneLoaded(scenePath))
                {
                    GF.Scene.UnloadScene(scenePath);
                }

                throw;
            }
            Node2D levelNode = levelScene.Result;
            Log.Info("[ProcedureLevel] 加载耗时：场景与HUD资源 {0} ms", loadingWatch.ElapsedMilliseconds);
            if (entrySerial != _entrySerial)
            {
                if (_levelOwnerEntry == 0 && GF.Scene.IsSceneLoaded(scenePath))
                {
                    GF.Scene.UnloadScene(scenePath);
                }

                return;
            }

            // 先登记场景所有权，再校验根节点；失败路径也能卸载已加载场景。
            _levelOwnerEntry = entrySerial;
            LevelViewController levelViewController = levelNode as LevelViewController
                      ?? throw new InvalidOperationException($"{_levelScenePath} 场景根节点未绑定 LevelViewController。");
            _levelViewController = levelViewController;
            _levelModel = new LevelModel();
            procedureOwner.RegisterModel(_levelModel);
            procedureOwner.RegisterModel(new HeroModel());
            procedureOwner.RegisterSystem(new HeroStatusSystem(procedureOwner.GetArchitecture()));
            procedureOwner.RegisterSystem(new HeroSystem(procedureOwner.GetArchitecture()));
            _levelSystem = new LevelSystem(procedureOwner.GetArchitecture());
            procedureOwner.RegisterSystem(_levelSystem);
            levelViewController.Initialize(procedureOwner.GetArchitecture());
            _musicSerial = GF.Sound.PlayConfigured(levelViewController.MusicId, 0.4f);
            levelViewController.Failed += OnLevelFailed;
            levelViewController.HiddenLevelRequested += OnHiddenLevelRequested;
            levelViewController.EntranceOpacityChanged += OnEntranceOpacityChanged;

            // 阵容顺序即席位；显示成功即登记，后续任意失败由同一清理路径回收。
            for (int i = 0; i < heroConfigs.Length; i++)
            {
                HeroConfig config = heroConfigs[i];
                PlayerSlot slot = (PlayerSlot)(i + 1);
                AbstractHeroEntity hero = await entities.ShowEntityAsync<AbstractHeroEntity>(config.EntityId,
                    new HeroSpawn(slot, SpawnPositionOf(slot)));
                if (entrySerial != _entrySerial)
                {
                    // 按本周期原编号回收，避免旧引用已经被新周期复用时误隐藏新英雄。
                    foreach (int id in shownHeroIds)
                    {
                        entities.HideEntitySafe(id);
                    }
                    entities.HideEntitySafe(hero);
                    return;
                }

                _heroes.Add(hero ?? throw new InvalidOperationException($"英雄实体创建失败：{config.NameCn}。"));
                shownHeroIds.Add(hero.Id);
                if (hero.Config == null || !hero.IsShown)
                {
                    throw new InvalidOperationException("英雄初始化失败。");
                }
            }
            GC.KeepAlive(heroAssets);
            Log.Info("[ProcedureLevel] 加载耗时：英雄显示 {0} ms", loadingWatch.ElapsedMilliseconds);

            // HUD 依赖与场景并行加载；先装配并绑定窗口，避免开战后再等首帧 UI 加载。
            var battleFormReady = new TaskCompletionSource();
            _battleFormReady = battleFormReady;
            GF.Event.Subscribe(OpenUIFormSuccessEventArgs.EventId, OnFormOpened);
            GF.Event.Subscribe(OpenUIFormFailureEventArgs.EventId, OnBattleFormOpenFailed);
            _battleFormSerialId = GF.UI.OpenUIForm(UIFormId.BattleForm, new BattleFormData(_heroes, _levelViewController));
            IUIForm battleForm = GF.UI.GetUIForm(_battleFormSerialId);
            SetFormCallbacks(battleForm, bind: true);
            if (battleForm != null)
            {
                battleFormReady.TrySetResult();
            }

            await battleFormReady.Task.WaitAsync(cancellationToken);
            Log.Info("[ProcedureLevel] 加载耗时：HUD就绪 {0} ms", loadingWatch.ElapsedMilliseconds);
            if (entrySerial != _entrySerial)
            {
                return;
            }
            // 保持预载资源存活到 GGF 窗口取得自己的资源引用，不另建常驻缓存。
            GC.KeepAlive(battleFormAsset.Result);

            // 首波与相机在 HUD 就绪后再开放；加载页覆盖期间不推进关卡。
            if (GF.UI.GetUIForm(ResourcesCollectionConstant.UI_LoadingForm) is LoadingForm loading)
            {
                await loading.CloseLoadingAsync(cancellationToken);
            }

            if (entrySerial != _entrySerial)
            {
                return;
            }

            _levelModel.Outcome.Register(OnLevelEnded);
            _levelViewController.StartSession(entities, _heroes, cancellationToken);

            Log.Info("[ProcedureLevel] 战斗 HUD 已就绪，开始关卡：{0}", _battleFormSerialId);
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                return;
            }

            Log.Error("[ProcedureLevel] 关卡启动失败：{0}", ex);
            if (entrySerial == _entrySerial)
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
        {
            GF.Event.Unsubscribe(OpenUIFormSuccessEventArgs.EventId, OnFormOpened);
        }

        SetFormCallbacks(GF.UI.GetUIForm(_battleFormSerialId), bind: false);
        SetFormCallbacks(GF.UI.GetUIForm(_settingsSerialId), bind: false);
        SetFormCallbacks(GF.UI.GetUIForm(_settlementSerialId), bind: false);


        _pendingSettlement = false;
        base.OnLeave(procedureOwner, isShutdown);
        int entrySerial = _entrySerial;
        _entrySerial++;
        _sessionCancellation?.Cancel();
        _sessionCancellation?.Dispose();
        _sessionCancellation = null;
        _battleFormReady = null;

        if (!isShutdown)
        {
            CleanupSession(entrySerial);
        }
        else
        {
            // 窗口退订仍需访问本流程模型，必须在流程FSM被销毁前关闭；不提交存档或回滚。
            CloseForms();
            if (_musicSerial != 0)
            {
                GF.Sound.StopSound(_musicSerial, 0f);
                _musicSerial = 0;
            }

            _levelViewController?.UnsubscribeProgressEvents();
        }

        _levelViewController = null;
        _levelSystem = null;
        _levelModel = null;
        _heroes.Clear();
        _owner = null;
    }

    /// <summary>
    /// 在事件栈退出后的流程帧打开结算，死亡先等待真实动作完成。
    /// </summary>
    /// <param name="procedureOwner">当前流程状态机。</param>
    /// <param name="elapseSeconds">本帧游戏秒数。</param>
    /// <param name="realElapseSeconds">本帧实际秒数。</param>
    protected internal override void OnUpdate(ProcedureOwner procedureOwner, float elapseSeconds,
        float realElapseSeconds)
    {
        base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);
        if (_pendingHiddenLevelId != 0)
        {
            procedureOwner.SetData(FlowKeys.SelectedLevelId, (VarInt32)_pendingHiddenLevelId);
            _pendingHiddenLevelId = 0;
            ChangeState<ProcedureLevel>(procedureOwner);
            return;
        }
        if (!_pendingSettlement)
        {
            return;
        }

        _pendingSettlement = false;
        // 结算属于本关流程：停止旧音乐（包括在途请求），成功和失败曲仍由同一个 GGF 请求拥有者清理。
        if (_musicSerial != 0)
        {
            GF.Sound.StopSound(_musicSerial);
        }
        // 结算马上暂停场景；框架的淡入Tween跟随场景暂停，因此此处直接按设置音量起播。
        _musicSerial = GF.Sound.PlayConfigured(_levelModel.Outcome.Value == LevelOutcome.Cleared
            ? SoundId.SettlementVictory : SoundId.SettlementDefeat);
        _settlementSerialId = GF.UI.OpenUIForm(UIFormId.SettlementForm, new SettlementFormData(_levelModel.Result));
        SetFormCallbacks(GF.UI.GetUIForm(_settlementSerialId), bind: true);
    }

    /// <summary>
    /// 收到左下角设置点击后打开暂停菜单。
    /// </summary>
    /// <param name="formSerialId">发起操作的当前窗口编号。</param>
    private void OnSettingsRequested(int formSerialId)
    {
        if (formSerialId != _battleFormSerialId ||
            _levelModel?.Outcome.Value != LevelOutcome.Running)
        {
            return;
        }

        if (HasWindow(_settingsSerialId) || HasWindow(_inventorySerialId) || HasWindow(_skillSerialId))
        {
            return;
        }

        _settingsSerialId = GF.UI.OpenUIForm(UIFormId.SettingsForm);
        SetFormCallbacks(GF.UI.GetUIForm(_settingsSerialId), bind: true);
    }

    /// <summary>按席位借用本周期已显示的英雄，未参战席位返回空。</summary>
    /// <param name="slot">请求的玩家席位。</param>
    /// <returns>本周期的参战英雄，或空。</returns>
    private AbstractHeroEntity HeroAt(PlayerSlot slot)
    {
        return _heroes.FirstOrDefault(hero => hero.Slot == slot);
    }

    /// <summary>按席位计算出生点：P1 用关卡出生点，P2 在其右侧 60 像素同一高度。</summary>
    /// <param name="slot">本次显示的玩家席位。</param>
    /// <returns>该席位的世界出生坐标，单位像素。</returns>
    private Vector2 SpawnPositionOf(PlayerSlot slot)
    {
        return _levelViewController.PlayerSpawnPosition + new Vector2(slot == PlayerSlot.P1 ? 0 : 60, 0);
    }

    /// <summary>
    /// 在同一关卡中互斥打开背包与设置。
    /// </summary>
    /// <param name="formSerialId">发起操作的当前窗口编号。</param>
    /// <param name="slot">请求打开背包的玩家席位。</param>
    private void OnInventoryRequested(int formSerialId, PlayerSlot slot)
    {
        AbstractHeroEntity hero = HeroAt(slot);
        if (formSerialId != _battleFormSerialId ||
            _levelModel?.Outcome.Value != LevelOutcome.Running || hero == null || !hero.IsAlive)
        {
            return;
        }

        if (HasWindow(_settingsSerialId) || HasWindow(_inventorySerialId) || HasWindow(_skillSerialId))
        {
            return;
        }

        _inventorySerialId =
                    GF.UI.OpenUIForm(UIFormId.InventoryForm, new InventoryFormData(hero));
    }

    /// <summary>
    /// 互斥打开本关技能目录；默认展示发起请求的英雄。
    /// </summary>
    /// <param name="formSerialId">发起操作的当前窗口编号。</param>
    /// <param name="slot">请求打开技能目录的玩家席位。</param>
    private void OnSkillsRequested(int formSerialId, PlayerSlot slot)
    {
        AbstractHeroEntity hero = HeroAt(slot);
        if (formSerialId != _battleFormSerialId ||
            _levelModel?.Outcome.Value != LevelOutcome.Running || hero == null || !hero.IsAlive)
        {
            return;
        }

        if (HasWindow(_settingsSerialId) || HasWindow(_inventorySerialId) || HasWindow(_skillSerialId))
        {
            return;
        }

        _skillSerialId = GF.UI.OpenUIForm(UIFormId.SkillForm, new SkillFormData(hero.HeroId));
    }

    /// <summary>
    /// 设置的放弃与结算的返回地图使用各自的窗口编号校验。
    /// </summary>
    /// <param name="formSerialId">发起操作的当前窗口编号。</param>
    private async void OnMapRequested(int formSerialId)
    {
        int serial = formSerialId;
        if (serial != 0 && serial == _settlementSerialId && _levelModel?.Result != null)
        {
            NavigateAfterCheckpointAsync(_owner, _entrySerial, retry: false);
        }
        else if (serial != 0 && serial == _settingsSerialId && _levelModel?.Outcome.Value == LevelOutcome.Running)
        {
            await SaveAndNavigateAsync(_owner, _entrySerial, serial, returnToTitle: false);
        }
    }

    /// <summary>
    /// 只有当前失败结算窗口可以重新挑战。
    /// </summary>
    /// <param name="formSerialId">发起操作的当前窗口编号。</param>
    private void OnRetryRequested(int formSerialId)
    {
        int serial = formSerialId;
        if (serial != 0 && serial == _settlementSerialId && _levelModel?.Outcome.Value == LevelOutcome.Defeated)
        {
            NavigateAfterCheckpointAsync(_owner, _entrySerial, retry: true);
        }
    }

    /// <summary>
    /// 检查窗口是否仍打开或正在加载。
    /// </summary>
    /// <param name="serial">窗口请求编号；零表示无请求。</param>
    /// <returns>窗口仍属于本周期时为 true。</returns>
    private static bool HasWindow(int serial)
    {
        return serial != 0 && (GF.UI.HasUIForm(serial) || GF.UI.IsLoadingUIForm(serial));
    }

    /// <summary>
    /// 从设置返回标题，先保存当前档案再正常离开关卡。
    /// </summary>
    /// <param name="formSerialId">发起操作的当前窗口编号。</param>
    private async void OnTitleRequested(int formSerialId)
    {
        if (formSerialId == _settingsSerialId &&
            _levelModel?.Outcome.Value == LevelOutcome.Running)
        {
            await SaveAndNavigateAsync(_owner, _entrySerial, formSerialId, returnToTitle: true);
        }
    }

    /// <summary>
    /// 设置菜单离开未结束关卡前保存当前档案，再沿正常流程导航。
    /// </summary>
    /// <param name="owner">当前流程状态机。</param>
    /// <param name="entrySerial">当前关卡进入周期。</param>
    /// <param name="formSerialId">设置窗口编号。</param>
    /// <param name="returnToTitle">是否返回标题；否则返回地图。</param>
    private async Task SaveAndNavigateAsync(ProcedureOwner owner, int entrySerial,
        int formSerialId, bool returnToTitle)
    {
        if (entrySerial != _entrySerial || owner.IsDestroyed || _navigating)
        {
            return;
        }

        _navigating = true;
        CancellationToken token = _sessionCancellation?.Token ?? CancellationToken.None;
        try
        {
            SaveSystem saveSystem = owner.GetSystem<SaveSystem>();
            bool saved = await saveSystem.SaveManuallyAsync(formSerialId, token);
            token.ThrowIfCancellationRequested();
            if (!saved || entrySerial != _entrySerial || owner.IsDestroyed)
            {
                Log.Error("[ProcedureLevel] 设置返回前存档失败，保留当前关卡。");
                _navigating = false;
                return;
            }

            owner.GetSystem<LevelSystem>().EndSession(rollback: false);

            if (returnToTitle)
            {
                ChangeState<ProcedureTitle>(owner);
            }
            else
            {
                ChangeState<ProcedureMap>(owner);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            Log.Error("[ProcedureLevel] 设置返回前保存失败：{0}", error);
            _navigating = false;
        }
    }

    /// <summary>
    /// 订阅场景结果并等待加载，命中已加载场景时直接返回。
    /// </summary>
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
        SetFormCallbacks(GF.UI.GetUIForm(_battleFormSerialId), bind: false);
        SetFormCallbacks(GF.UI.GetUIForm(_settingsSerialId), bind: false);
        SetFormCallbacks(GF.UI.GetUIForm(_settlementSerialId), bind: false);
        _pendingSettlement = false;
        _navigating = false;
        _pendingHiddenLevelId = 0;
        if (_levelViewController != null)
        {
            _levelViewController.HiddenLevelRequested -= OnHiddenLevelRequested;
            _levelViewController.EntranceOpacityChanged -= OnEntranceOpacityChanged;
        }
        // 启动或运行失败也必须使在途等待失效，随后重新进入可创建新周期。
        if (_entrySerial == entrySerial)
        {
            _entrySerial++;
        }

        _sessionCancellation?.Cancel();
        _sessionCancellation?.Dispose();
        _sessionCancellation = null;
        _battleFormReady = null;
        if (GF.Event.Check(OpenUIFormSuccessEventArgs.EventId, OnFormOpened))
        {
            GF.Event.Unsubscribe(OpenUIFormSuccessEventArgs.EventId, OnFormOpened);
        }
        // 模态窗口先解除暂停和运行订阅，再清理 HUD、实体及关卡事务。
        CloseForms();
        if (_musicSerial != 0)
        {
            GF.Sound.StopSound(_musicSerial, 0.2f);
        }

        _musicSerial = 0;

        if (GF.Event.Check(OpenUIFormFailureEventArgs.EventId, OnBattleFormOpenFailed))
        {
            GF.Event.Unsubscribe(OpenUIFormFailureEventArgs.EventId, OnBattleFormOpenFailed);
        }

        // 先停表现和关卡事件，再隐藏实体，最后卸载场景。
        if (_levelViewController != null)
        {
            _levelViewController.Failed -= OnLevelFailed;
            _levelModel.Outcome.UnRegister(OnLevelEnded);
            _levelViewController.Cleanup();
        }

        if (_levelSystem != null)
        {
            _owner.RemoveSystem<LevelSystem>();
        }

        if (_levelModel != null)
        {
            _owner.RemoveModel<LevelModel>();
        }

        foreach (AbstractHeroEntity hero in _heroes)
        {
            GF.Entity.HideEntitySafe(hero);
        }
        _heroes.Clear();
        _owner.RemoveSystem<HeroSystem>();
        _owner.RemoveSystem<HeroStatusSystem>();
        _owner.RemoveModel<HeroModel>();
        bool ownsScene = _levelOwnerEntry == entrySerial;
        if (ownsScene)
        {
            if (_levelScenePath != null && GF.Scene.IsSceneLoaded(_levelScenePath))
            {
                GF.Scene.UnloadScene(_levelScenePath);
            }

            _levelOwnerEntry = 0;
        }

        _levelViewController = null;
        _levelSystem = null;
        _levelModel = null;
        if (ownsScene)
        {
            _levelScenePath = null;
        }
    }

    /// <summary>
    /// 在模型和流程仍有效时关闭本关拥有的窗口与在途请求；关停与普通离关共用相同顺序。
    /// </summary>
    private void CloseForms()
    {
        foreach (int serialId in new[]
                 { _inventorySerialId, _skillSerialId, _settlementSerialId, _settingsSerialId, _battleFormSerialId })
        {
            if (HasWindow(serialId))
            {
                GF.UI.CloseUIForm(serialId);
            }
        }

        _inventorySerialId = _skillSerialId = _settlementSerialId = _settingsSerialId = _battleFormSerialId = 0;
    }

    /// <summary>
    /// 场景已完成胜败表现后登记结算，下一流程帧打开窗口；不重复等待已经结束的死亡动画。
    /// </summary>
    /// <param name="outcome">本次关卡结局。</param>
    private void OnLevelEnded(LevelOutcome outcome)
    {
        if (outcome is not (LevelOutcome.Cleared or LevelOutcome.Defeated) || _owner == null)
        {
            return;
        }

        _pendingSettlement = true;
    }

    /// <summary>
    /// 场景只请求已完成淡出的目标，不在动画或物理回调中拆除当前流程。
    /// </summary>
    /// <param name="levelId">已由关卡系统校验的隐藏关目标。</param>
    private void OnHiddenLevelRequested(int levelId)
    {
        if (_levelModel?.Outcome.Value == LevelOutcome.Transferred && !_navigating)
        {
            _navigating = true;
            _pendingHiddenLevelId = levelId;
        }
    }

    /// <summary>
    /// 场景淡出只更新当前流程拥有的HUD，不让场景持有GGF窗口引用。
    /// </summary>
    /// <param name="opacity">原生动画给出的0至1透明度。</param>
    private void OnEntranceOpacityChanged(float opacity)
    {
        if (GF.UI.GetUIForm(_battleFormSerialId) is CanvasItem form)
        {
            form.Modulate = new Color(1, 1, 1, opacity);
        }
    }

    /// <summary>
    /// 等待检查点后执行玩家选择；旧周期异步结果不触发导航。
    /// </summary>
    /// <param name="owner">流程状态机。</param>
    /// <param name="entrySerial">发起重开时的进入周期。</param>
    /// <param name="retry">失败后重试为 true，否则返回地图。</param>
    private async void NavigateAfterCheckpointAsync(ProcedureOwner owner, int entrySerial, bool retry)
    {
        if (entrySerial != _entrySerial || owner.IsDestroyed || _navigating)
        {
            return;
        }

        _navigating = true;

        // 返回地图前等待本次检查点完成；写入失败仍保留内存进度，地图可手动重试保存。
        Task<bool> write = _levelSystem?.CheckpointWrite;
        try
        {
            if (write != null && !await write)
            {
                Log.Error("[ProcedureLevel] 检查点写入失败，当前进度仍保留在内存中，请在地图重新保存。");
            }
        }
        catch (Exception error)
        {
            // 框架正常IO失败返回false；真正faulted任务也沿相同的内存保留路径导航，不让async void异常逃逸。
            Log.Error("[ProcedureLevel] 检查点任务异常，当前进度仍保留在内存中，请在地图重新保存：{0}", error);
        }

        if (entrySerial != _entrySerial || owner.IsDestroyed)
        {
            return;
        }

        if (retry)
        {
            ChangeState<ProcedureLevel>(owner);
        }
        else
        {
            ChangeState<ProcedureMap>(owner);
        }
    }

    /// <summary>
    /// 战斗窗口打开失败时只处理本流程拥有的请求，统一结束关卡。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="args">池化事件参数，只在本次分发内读取。</param>
    private void OnBattleFormOpenFailed(object sender, GameEventArgs args)
    {
        if (args is OpenUIFormFailureEventArgs failure && (failure.SerialId == _battleFormSerialId ||
                                                           failure.SerialId == _inventorySerialId ||
                                                           failure.SerialId == _skillSerialId ||
                                                           failure.SerialId == _settlementSerialId))
        {
            Log.Error("[ProcedureLevel] 战斗窗口打开失败：{0}", failure.ErrorMessage);
            CleanupSession(_entrySerial);
        }
    }

    /// <summary>
    /// 关卡异步显示失败时统一清理（运行回滚），不留下锁死的战斗区域。
    /// </summary>
    /// <param name="error">关卡运行错误。</param>
    private void OnLevelFailed(Exception error)
    {
        Log.Error("[ProcedureLevel] 关卡运行失败：{0}", error);
        CleanupSession(_entrySerial);
    }

    /// <summary>
    /// 异步窗口打开后接上本流程拥有的回调。
    /// </summary>
    /// <param name="sender">框架 UI。</param>
    /// <param name="args">当次打开结果。</param>
    private void OnFormOpened(object sender, GameEventArgs args)
    {
        if (args is OpenUIFormSuccessEventArgs opened)
        {
            SetFormCallbacks(opened.UIForm, bind: true);
            if (opened.UIForm.SerialId == _battleFormSerialId)
            {
                _battleFormReady?.TrySetResult();
            }
        }
    }

    /// <summary>
    /// 按实际窗口类型绑定本流程回调；窗口编号拒绝旧作用域和无关实例。
    /// </summary>
    /// <param name="form">已打开窗口；在途时为空。</param>
    /// <param name="bind">绑定或解除。</param>
    private void SetFormCallbacks(IUIForm form, bool bind)
    {
        if (bind && (form == null || !GF.UI.IsValidUIForm(form)))
        {
            return;
        }

        switch (form)
        {
            case BattleForm battleForm when battleForm.SerialId == _battleFormSerialId:
                battleForm.SettingsRequested -= OnSettingsRequested;
                if (bind)
                {
                    battleForm.SettingsRequested += OnSettingsRequested;
                }

                battleForm.InventoryRequested -= OnInventoryRequested;
                if (bind)
                {
                    battleForm.InventoryRequested += OnInventoryRequested;
                }

                battleForm.SkillsRequested -= OnSkillsRequested;
                if (bind)
                {
                    battleForm.SkillsRequested += OnSkillsRequested;
                }

                break;
            case SettingsForm settingsForm when settingsForm.SerialId == _settingsSerialId:
                settingsForm.TitleRequested -= OnTitleRequested;
                if (bind)
                {
                    settingsForm.TitleRequested += OnTitleRequested;
                }

                settingsForm.MapRequested -= OnMapRequested;
                if (bind)
                {
                    settingsForm.MapRequested += OnMapRequested;
                }

                break;
            case SettlementForm settlementForm when settlementForm.SerialId == _settlementSerialId:
                settlementForm.MapRequested -= OnMapRequested;
                if (bind)
                {
                    settlementForm.MapRequested += OnMapRequested;
                }

                settlementForm.RetryRequested -= OnRetryRequested;
                if (bind)
                {
                    settlementForm.RetryRequested += OnRetryRequested;
                }

                break;
        }
    }
}
