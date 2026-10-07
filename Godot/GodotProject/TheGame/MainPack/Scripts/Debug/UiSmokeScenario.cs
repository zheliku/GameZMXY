using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GameConfig;
using GameConfig.Constant;
using GameConfig.Entity;
using GameFramework.Event;
using GameFramework.Fsm;
using GameFramework.Procedure;
using GameLogic;
using GameLogic.Archive;
using GameLogic.Entity.Heroes;
using GameLogic.Entity.Monsters;
using GameLogic.Level;
using GameLogic.UI;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Entity;
using GodotGameFramework.UI;
using Newtonsoft.Json;

/// <summary>用真实 GGF 窗口、对象池和流程验证 HUD 数据绑定与隔离存档重入。</summary>
public sealed class UiSmokeScenario
{
    private readonly Node m_Driver; // 提供真实引擎帧信号的烟测节点。
    private readonly Task m_RunTask; // 已捕获异常的测试任务。
    private HeroEntity m_ExtraHero; // 测试绑定切换时创建的额外英雄，结束时归还。
    private MonsterEntity m_Monster; // 复用验证创建的怪物，结束时归还。
    private BattleHud m_TestHud; // 当前由测试打开的 HUD，结束时关闭。

    /// <summary>开始一次 UI 回归，原流程与测试使用同一套框架服务。</summary>
    /// <param name="driver">烟测帧驱动节点。</param>
    /// <param name="hero">正式流程已显示的英雄。</param>
    /// <param name="level">正式关卡。</param>
    /// <param name="hud">正式流程已打开的 HUD。</param>
    public UiSmokeScenario(Node driver, HeroEntity hero, LevelController level, BattleHud hud)
    {
        m_Driver = driver;
        m_RunTask = RunAsync(hero, level, hud);
    }

    /// <summary>回归是否已结束。</summary>
    public bool IsDone => m_RunTask.IsCompleted;

    /// <summary>首个失败原因，成功时为 null。</summary>
    public string Failure { get; private set; }

    /// <summary>依次验证显示、解绑、复用、异步结果与真实流程保存重入。</summary>
    /// <param name="hero">初始英雄。</param>
    /// <param name="level">初始关卡。</param>
    /// <param name="hud">初始 HUD。</param>
    /// <returns>整个回归完成的任务，异常被转换为 Failure。</returns>
    private async Task RunAsync(HeroEntity hero, LevelController level, BattleHud hud)
    {
        try
        {
            // 旧存档使用字段，现存快照使用属性；JSON 名称与结构必须保持兼容。
            GameData legacy = JsonConvert.DeserializeObject<GameData>(
                "{\"UnitId\":12,\"Score\":6,\"Player\":{\"Level\":3,\"TotalExperience\":90,\"Gold\":7}}");
            Check(legacy.Player.Level == 3 && legacy.Player.TotalExperience == 90 && legacy.Player.Gold == 7,
                "旧 Player 普通值 JSON 无法读取");
            string json = JsonConvert.SerializeObject(legacy);
            Check(!json.Contains("Changed") && !json.Contains("m_Value") &&
                JsonConvert.DeserializeObject<GameData>(json).Player.Gold == 7, "存档包含运行时属性或无法往返");
            hero.SetPhysicsProcess(false);
            CheckDisplay(hud, hero, level);
            CloseHud(hud);
            await WaitFramesAsync(2);

            // 在打开前设定非默认数值，验证初始化读取，而非依赖后续变化事件。
            hero.Level.Value = 3;
            hero.MaxHp.Value = 200;
            hero.Hp.Value = 140;
            hero.WsMax.Value = 100;
            hero.WsValue.Value = 35;
            level.TravelAvailable.Value = true;
            m_TestHud = await OpenHudAsync(hero, level);
            Check(ReferenceEquals(hud, m_TestHud), "HUD 未从真实窗口池复用");
            CheckDisplay(m_TestHud, hero, level);
            Check(m_TestHud.GetNode<Sprite2D>("MenuPanel/m_WsMax").Frame == 3, "无双 35% 段位帧没有绑定");

            ResourceBar bar = m_TestHud.GetNode<ResourceBar>("StatusPanel/m_HpBar");
            TextureProgressBar delay = bar.GetNode<TextureProgressBar>("m_HpBarDelay");
            Check(Near(delay.Value, bar.Value), "首次打开残影未直接对齐");
            hero.Hp.Value -= 40;
            hero.WsValue.Value += 20;
            hero.Level.Value = 4;
            level.TravelAvailable.Value = false;
            CheckDisplay(m_TestHud, hero, level);
            Check(m_TestHud.GetNode<Sprite2D>("MenuPanel/m_WsMax").Frame == 5, "无双 55% 段位帧未更新");
            Check(delay.Value > bar.Value, "受伤后残影没有保留旧比例");
            await WaitUntilAsync(() => Near(delay.Value, bar.Value), "生命残影未追平");

            // 关闭仍保留节点：修改旧对象不应刷新文本、无双条或前进动画。
            CloseHud(m_TestHud);
            double closedHp = bar.Value;
            double closedWs = hud.GetNode<TextureProgressBar>("MenuPanel/WsFrame/m_WsBar").Value;
            string closedLevel = hud.GetNode<Label>("StatusPanel/m_LevelLabel").Text;
            hero.Hp.Value -= 5;
            hero.Level.Value = 7;
            hero.WsValue.Value = 90;
            level.TravelAvailable.Value = true;
            Check(Near(bar.Value, closedHp), "关闭后血条仍响应旧属性");
            Check(Near(hud.GetNode<TextureProgressBar>("MenuPanel/WsFrame/m_WsBar").Value, closedWs),
                "关闭后无双条仍响应旧属性");
            Check(hud.GetNode<Label>("StatusPanel/m_LevelLabel").Text == closedLevel, "关闭后等级仍响应旧属性");
            Check(!hud.GetNode<AnimatedSprite2D>("m_Go").IsPlaying(), "关闭后 Go 动画重新开始");
            await WaitFramesAsync(2);

            m_TestHud = await OpenHudAsync(hero, level);
            PlayerSaveData input = new() { Level = 5, TotalExperience = 120, Gold = 40 };
            m_ExtraHero = await GF.Entity.ShowEntityAsync<WukongEntity>(EntityId.Wukong, input);
            m_ExtraHero.SetPhysicsProcess(false);
            input.Gold = 999;
            Check(m_ExtraHero.Gold.Value == 40, "英雄保留了可变存档对象引用");
            GF.UI.RefocusUIForm(m_TestHud, new BattleHudContext(m_ExtraHero, level));
            CheckDisplay(m_TestHud, m_ExtraHero, level);
            hero.Level.Value = 9;
            hero.Hp.Value = 1;
            CheckDisplay(m_TestHud, m_ExtraHero, level);
            CloseHud(m_TestHud);
            await WaitFramesAsync(2);
            await CheckEntityReuseAsync();
            await CheckExperienceAsync(level);

            // 加载中的冷窗口由正式流程退出清理；同时保存持久值，重入必须读回同一快照。
            hero.Level.Value = 1;
            hero.TotalExperience.Value = 0;
            hero.Gold.Value = 57;
            hero.GainExperience(517);
            // 读取实际磁盘快照，证明经验自动保存不依赖离开流程。
            Task saveTask = (Task)typeof(ProcedureGame)
                .GetField("m_SaveTask", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(GF.Procedure.CurrentProcedure);
            await saveTask;
            var setting = GF.Archive.Setting;
            GameData disk = await GodotGameFramework.Json.EasySave.LoadFromUserAsync<GameData>(
                $"{setting.Folder}/Data/{GF.Archive.CurrentCatalogue.UnitId}.sav",
                setting.EnableAesEncryption, setting.KEY, setting.Salt);
            Check(disk?.Player.Level == 4 && disk.Player.TotalExperience == 517 && disk.Player.Gold == 57,
                "经验变更没有自动保存到磁盘");
            await CheckLoadingCancellationAsync(hero, level);
            BattleHud reenteredHud = await WaitForHudAsync();
            HeroEntity reenteredHero = FindHero();
            CheckProgress(reenteredHero);
            CheckDisplay(reenteredHud, reenteredHero, FindLevel());
            Check(GF.Archive.CurrentData.Player.Gold == 57, "重入未读回退出快照");

            // 将池中同一窗口的必需导出引用临时置空，验证真实 OnOpen 失败由流程清理。
            ResourceBar savedBar = reenteredHud.GetNode<ResourceBar>("StatusPanel/m_HpBar");
            CloseHud(reenteredHud);
            await WaitFramesAsync(2);
            reenteredHud.Set("m_HpBar", default(Variant));
            string openFailure = null;
            EventHandler<GameEventArgs> onFailure = (_, args) =>
            {
                if (args is OpenUIFormFailureEventArgs failed && failed.UIFormAssetName == ResourcesCollectionConstant.UIs_BattleHud)
                {
                    openFailure = failed.ErrorMessage;
                }
            };
            GF.Event.Subscribe(OpenUIFormFailureEventArgs.EventId, onFailure);
            try
            {
                ReenterGame();
                await WaitUntilAsync(() => openFailure != null && FindHero() == null && CurrentHud() == null,
                    "HUD 打开失败未完整结束正式流程");
                Check(openFailure.Contains("必须绑定"), "没有观察到必需节点装配失败");
            }
            finally
            {
                GF.Event.Unsubscribe(OpenUIFormFailureEventArgs.EventId, onFailure);
                reenteredHud.Set("m_HpBar", savedBar);
            }
            ReenterGame();
            BattleHud recovered = await WaitForHudAsync();
            CheckProgress(FindHero());
            CheckDisplay(recovered, FindHero(), FindLevel());
            GD.Print("SMOKE-UI: 四类资源条、连升/满级、窗口/实体复用、取消、失败恢复和磁盘保存重入验证完成");
        }
        catch (Exception error)
        {
            Failure = error.ToString();
        }
        finally
        {
            CloseHud(m_TestHud);
            GF.Entity.HideEntitySafe(m_ExtraHero);
            GF.Entity.HideEntitySafe(m_Monster);
        }
    }

    /// <summary>在真实池化英雄与 HUD 上验证跨级余量、成长补满、满级与无残影资源条。</summary>
    /// <param name="level">只提供 HUD 前进提示的正式关卡。</param>
    /// <returns>英雄成长与满级显示验证完成的任务。</returns>
    private async Task CheckExperienceAsync(LevelController level)
    {
        m_ExtraHero = await GF.Entity.ShowEntityAsync<WukongEntity>(EntityId.Wukong, null);
        m_ExtraHero.SetPhysicsProcess(false);
        m_TestHud = await OpenHudAsync(m_ExtraHero, level);
        m_ExtraHero.Hp.Value = 1;
        m_ExtraHero.Mp.Value = 1;
        m_ExtraHero.GainExperience(139);
        Check(m_ExtraHero.Level.Value == 1 && m_ExtraHero.Experience.Value == 139 && m_ExtraHero.Hp.Value == 1,
            "升级前门槛或生命恢复时机错误");
        CheckDisplay(m_TestHud, m_ExtraHero, level);
        m_ExtraHero.GainExperience(378);
        Check(m_ExtraHero.Level.Value == 4 && m_ExtraHero.TotalExperience.Value == 517 &&
            m_ExtraHero.Experience.Value == 37 && m_ExtraHero.MaxExperience.Value == 200,
            "连升未保留余量或等级错误");
        Check(m_ExtraHero.MaxHp.Value == 230 && m_ExtraHero.MaxMp.Value == 95 &&
            m_ExtraHero.Hp.Value == 230 && m_ExtraHero.Mp.Value == 95,
            "升级未按原表成长并恢复生命和魔法");
        CheckDisplay(m_TestHud, m_ExtraHero, level);

        // 满级明确显示 MAX，再奖励和负数输入不能改变合法累计量。
        m_ExtraHero.GainExperience(int.MaxValue);
        Check(m_ExtraHero.Level.Value == 55 && m_ExtraHero.Experience.Value == 0 &&
            m_ExtraHero.MaxExperience.Value == 0, "满级没有封顶");
        CheckDisplay(m_TestHud, m_ExtraHero, level);
        int capped = m_ExtraHero.TotalExperience.Value;
        m_ExtraHero.GainExperience(1);
        Check(m_ExtraHero.TotalExperience.Value == capped, "满级仍累计奖励");
        bool rejected = false;
        try { m_ExtraHero.GainExperience(-1); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected && m_ExtraHero.TotalExperience.Value == capped, "负经验奖励未被拒绝");

        // 所有条都必须在关闭时退订；节点仍在池内且未销毁。
        ResourceBar expBar = m_TestHud.GetNode<ResourceBar>("StatusPanel/m_ExpBar");
        ResourceBar mpBar = m_TestHud.GetNode<ResourceBar>("StatusPanel/m_MpBar");
        CloseHud(m_TestHud);
        m_ExtraHero.Experience.Value = 10;
        m_ExtraHero.MaxExperience.Value = 100;
        m_ExtraHero.Mp.Value = 0;
        Check(Near(expBar.Value, 1) && Near(mpBar.Value, 1), "关闭后经验或魔法仍响应旧属性");
        GF.Entity.HideEntitySafe(m_ExtraHero);
        m_ExtraHero = null;
        await WaitFramesAsync(2);
    }

    /// <summary>使用真实实体池，验证英雄和怪物显示时重置数值且属性容器保持稳定。</summary>
    /// <returns>两种实体复用完成的任务。</returns>
    private async Task CheckEntityReuseAsync()
    {
        HeroEntity previous = m_ExtraHero;
        var hp = previous.Hp;
        previous.Hp.Value = 1;
        previous.MaxHp.Value = 1;
        previous.WsValue.Value = 70;
        previous.WsMax.Value = 1;
        GF.Entity.HideEntitySafe(previous);
        await WaitFramesAsync(2);
        m_ExtraHero = await GF.Entity.ShowEntityAsync<WukongEntity>(EntityId.Wukong,
            new PlayerSaveData { Level = 2, TotalExperience = 8, Gold = 11 });
        m_ExtraHero.SetPhysicsProcess(false);
        Check(ReferenceEquals(previous, m_ExtraHero) && ReferenceEquals(hp, m_ExtraHero.Hp), "英雄属性容器未随池实例稳定复用");
        Check(m_ExtraHero.Level.Value == 2 && m_ExtraHero.TotalExperience.Value == 140 && m_ExtraHero.Gold.Value == 11,
            "英雄复用残留上次的持久数值");
        Check(m_ExtraHero.MaxHp.Value == m_ExtraHero.Config.BaseHp + m_ExtraHero.Config.GrowHp &&
            m_ExtraHero.Hp.Value == m_ExtraHero.MaxHp.Value && m_ExtraHero.WsValue.Value == 0 &&
            m_ExtraHero.WsMax.Value == ConfigSystem.Instance.Tables.TbBattleConfig.Data.WsMax,
            "英雄复用未按配置恢复生命与无双");
        GF.Entity.HideEntitySafe(m_ExtraHero);
        await WaitFramesAsync(2);
        m_ExtraHero = await GF.Entity.ShowEntityAsync<WukongEntity>(EntityId.Wukong, null);
        m_ExtraHero.SetPhysicsProcess(false);
        Check(m_ExtraHero.Level.Value == 1 && m_ExtraHero.TotalExperience.Value == 0 && m_ExtraHero.Gold.Value == 0 &&
            m_ExtraHero.Hp.Value == m_ExtraHero.Config.BaseHp, "测试场地 null 输入未恢复默认初始值");
        GF.Entity.HideEntitySafe(m_ExtraHero);
        m_ExtraHero = null;

        var config = ConfigSystem.Instance.Tables.TbMonsterConfig.DataList[0];
        m_Monster = (MonsterEntity)await GF.Entity.ShowEntityAsync(config.EntityId, Vector2.Zero);
        m_Monster.SetAiEnabled(false);
        m_Monster.SetPhysicsProcess(false);
        MonsterEntity previousMonster = m_Monster;
        var monsterHp = m_Monster.Hp;
        m_Monster.Hp.Value = 0;
        m_Monster.MaxHp.Value = 1;
        GF.Entity.HideEntitySafe(m_Monster);
        await WaitFramesAsync(2);
        m_Monster = (MonsterEntity)await GF.Entity.ShowEntityAsync(config.EntityId, Vector2.Zero);
        m_Monster.SetAiEnabled(false);
        m_Monster.SetPhysicsProcess(false);
        Check(ReferenceEquals(previousMonster, m_Monster) && ReferenceEquals(monsterHp, m_Monster.Hp),
            "怪物属性容器未稳定复用");
        Check(m_Monster.Hp.Value == config.Hp && m_Monster.MaxHp.Value == config.Hp && !m_Monster.Dead,
            "怪物复用未按配置恢复生命与死亡事实");
        GF.Entity.HideEntitySafe(m_Monster);
        m_Monster = null;
    }

    /// <summary>创建缓存内的冷窗口资源，交给正式流程退出清理并观察晚到结果。</summary>
    /// <param name="hero">绑定英雄。</param>
    /// <param name="level">绑定关卡。</param>
    /// <returns>加载与关闭验证完成的任务。</returns>
    private async Task CheckLoadingCancellationAsync(HeroEntity hero, LevelController level)
    {
        string path = $"res://.godot/validation/hud_cancel_{Guid.NewGuid():N}.tscn";
        Node fixture = GD.Load<PackedScene>(ResourcesCollectionConstant.UIs_BattleHud).Instantiate();
        PackedScene packed = new();
        Check(packed.Pack(fixture) == Error.Ok && ResourceSaver.Save(packed, path) == Error.Ok, "创建冷加载 UI 夹具失败");
        fixture.Free();
        int serial = 0;
        bool opened = false;
        EventHandler<GameEventArgs> onSuccess = (_, args) =>
        {
            if (args is OpenUIFormSuccessEventArgs success && success.UIForm.SerialId == serial)
            {
                opened = true;
            }
        };
        GF.Event.Subscribe(OpenUIFormSuccessEventArgs.EventId, onSuccess);
        try
        {
            serial = GF.UI.OpenUIForm(path, "Normal", new BattleHudContext(hero, level));
            Check(GF.UI.IsLoadingUIForm(serial), "取消用例没有覆盖实际加载中的窗口请求");
            // 调试夹具只注入请求所有权，不改源表或业务公开接口；实际取消必须由 OnLeave 执行。
            typeof(ProcedureGame).GetField("m_HudSerialId", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(GF.Procedure.CurrentProcedure, serial);
            ReenterGame();
            await WaitFramesAsync(60);
            Check(!opened && !GF.UI.HasUIForm(serial) && !GF.UI.IsLoadingUIForm(serial), "取消的 HUD 晚到后仍打开");
        }
        finally
        {
            GF.Event.Unsubscribe(OpenUIFormSuccessEventArgs.EventId, onSuccess);
            if (GF.UI.HasUIForm(serial) || GF.UI.IsLoadingUIForm(serial))
            {
                GF.UI.CloseUIForm(serial);
            }
        }
    }

    /// <summary>通过正式窗口 API 打开 HUD，等待实际完成。</summary>
    /// <param name="hero">绑定英雄。</param>
    /// <param name="level">绑定关卡。</param>
    /// <returns>本次请求打开的 HUD。</returns>
    private async Task<BattleHud> OpenHudAsync(HeroEntity hero, LevelController level)
    {
        int serial = GF.UI.OpenUIForm(UIFormId.BattleHud, new BattleHudContext(hero, level));
        await WaitUntilAsync(() => GF.UI.GetUIForm(serial) is BattleHud, "HUD 请求未完成");
        return (BattleHud)GF.UI.GetUIForm(serial);
    }

    /// <summary>等待重入后的正式 HUD 与英雄就绪，排除仍在回收的旧窗口。</summary>
    /// <returns>当前正式 HUD。</returns>
    private async Task<BattleHud> WaitForHudAsync()
    {
        await WaitUntilAsync(() => CurrentHud() != null && FindHero() != null, "游戏流程重入后 HUD 未就绪");
        return CurrentHud();
    }

    /// <summary>使用真实流程状态机触发一次普通离开与再次进入。</summary>
    private static void ReenterGame()
    {
        ((Fsm<IProcedureManager>)GF.Fsm.GetFsm<IProcedureManager>()).ChangeState<ProcedureGame>();
    }

    /// <summary>查找当前已显示的英雄，场景树查询只用于调试断言。</summary>
    /// <returns>当前英雄或 null。</returns>
    private HeroEntity FindHero() => m_Driver.GetTree().Root.FindChildren("*", "CharacterBody2D", true, false)
        .OfType<HeroEntity>().FirstOrDefault(x => x.IsShown);

    /// <summary>查找当前正式关卡，排除已经等待释放的旧节点。</summary>
    /// <returns>当前关卡或 null。</returns>
    private LevelController FindLevel() => m_Driver.GetTree().Root.FindChildren("*", "Node2D", true, false)
        .OfType<LevelController>().FirstOrDefault(x => !x.IsQueuedForDeletion());

    /// <summary>读取当前正式 HUD。</summary>
    /// <returns>当前窗口或 null。</returns>
    private static BattleHud CurrentHud() => GF.UI.GetUIForm(ResourcesCollectionConstant.UIs_BattleHud) as BattleHud;

    /// <summary>关闭仍由框架管理的窗口，重复调用安全。</summary>
    /// <param name="hud">待关闭窗口，可为空。</param>
    private static void CloseHud(BattleHud hud)
    {
        if (hud != null && GF.UI.HasUIForm(hud.SerialId))
        {
            GF.UI.CloseUIForm(hud.SerialId);
        }
    }

    /// <summary>比对实际控件与实体属性，而不是只检查通知次数。</summary>
    /// <param name="hud">实际窗口。</param>
    /// <param name="hero">预期绑定英雄。</param>
    /// <param name="level">预期绑定关卡。</param>
    private static void CheckDisplay(BattleHud hud, HeroEntity hero, LevelController level)
    {
        foreach (string path in new[] { "StatusPanel/m_HpBar", "StatusPanel/m_MpBar", "StatusPanel/m_ExpBar", "MenuPanel/WsFrame/m_WsBar" })
        {
            ResourceBar resourceBar = hud.GetNode<ResourceBar>(path);
            Check(resourceBar.Visible && resourceBar.TextureProgress != null, $"资源条隐藏或填充纹理未加载：{path}");
        }
        Check(Near(hud.GetNode<ResourceBar>("StatusPanel/m_HpBar").Value, (double)hero.Hp.Value / hero.MaxHp.Value), "血条填充与当前英雄不一致");
        Check(hud.GetNode<Label>("StatusPanel/m_HpBar/m_HpText").Text == $"{hero.Hp.Value}/{hero.MaxHp.Value}", "生命文本不一致");
        Check(hud.GetNode<Label>("StatusPanel/m_LevelLabel").Text == hero.Level.Value.ToString(), "等级文本不一致");
        Check(Near(hud.GetNode<ResourceBar>("StatusPanel/m_MpBar").Value, (double)hero.Mp.Value / hero.MaxMp.Value), "魔法条不一致");
        Check(hud.GetNode<Label>("StatusPanel/m_MpBar/MpText").Text == $"{hero.Mp.Value}/{hero.MaxMp.Value}", "魔法文本不一致");
        double experienceRatio = hero.MaxExperience.Value == 0 ? 1 : (double)hero.Experience.Value / hero.MaxExperience.Value;
        Check(Near(hud.GetNode<ResourceBar>("StatusPanel/m_ExpBar").Value, experienceRatio), "经验条不一致");
        Check(hud.GetNode<Label>("StatusPanel/m_ExpBar/ExpText").Text ==
            (hero.MaxExperience.Value == 0 ? "MAX" : $"{hero.Experience.Value}/{hero.MaxExperience.Value}"), "经验文本不一致");
        Check(Near(hud.GetNode<TextureProgressBar>("MenuPanel/WsFrame/m_WsBar").Value, (double)hero.WsValue.Value / hero.WsMax.Value), "无双条不一致");
        Check(hud.GetNode<AnimatedSprite2D>("m_Go").Visible == level.TravelAvailable.Value, "Go 显示与关卡属性不一致");
    }

    /// <summary>验证从退出快照恢复的持久数值。</summary>
    /// <param name="hero">重新显示的英雄。</param>
    private static void CheckProgress(HeroEntity hero) => Check(hero.Level.Value == 4 &&
        hero.TotalExperience.Value == 517 && hero.Gold.Value == 57, "退出保存后的等级、经验或金币没有恢复");

    /// <summary>比较控件比例，容纳浮点显示误差。</summary>
    /// <param name="actual">控件值。</param>
    /// <param name="expected">预期值。</param>
    /// <returns>误差是否足够小。</returns>
    private static bool Near(double actual, double expected) => Math.Abs(actual - expected) < 0.0001;

    /// <summary>断言失败时终止当前回归并保留具体原因。</summary>
    /// <param name="condition">断言条件。</param>
    /// <param name="message">失败原因。</param>
    /// <exception cref="InvalidOperationException">条件不成立。</exception>
    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>按真实引擎帧等待，不创建后台轮询任务。</summary>
    /// <param name="frames">帧数。</param>
    /// <returns>指定帧数完成的任务。</returns>
    private async Task WaitFramesAsync(int frames)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    /// <summary>等待实际引擎条件，超过十秒报告失败。</summary>
    /// <param name="condition">完成条件。</param>
    /// <param name="message">超时原因。</param>
    /// <returns>条件成立的任务。</returns>
    /// <exception cref="InvalidOperationException">等待超时。</exception>
    private async Task WaitUntilAsync(Func<bool> condition, string message)
    {
        ulong start = Time.GetTicksMsec();
        while (!condition())
        {
            Check(Time.GetTicksMsec() - start < 10000, message);
            await WaitFramesAsync(1);
        }
    }
}
