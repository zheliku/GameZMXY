using GameLogic.Battle;
using GameLogic.Config;
using GameLogic.Architecture;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;
using GameLogic.Equipment;
using GameLogic.Profile;
using GameLogic.Input;
using GameLogic.Procedure.FlowData;
using GameLogic.Save.Slots;
using System;
using System.Linq;
using System.IO;
using GameLogic.Save;
using System.Threading.Tasks;
using GameLogic.Heroes;
using GameLogic;
using GameLogic.Level;
using GameLogic.UI;
using GameLogic.UI.Widgets;
using GameConfig.Constant;
using Godot;
using GodotGameFramework;
using GodotGameFramework.UI;
using GodotGameFramework.Sound;

/// <summary>在真实框架内验证菜单导航、关卡选择、退出回滚、音乐代理和菜单复用；不增加正式观测 API。</summary>
public sealed class FrontendSmokeScenario
{
    private readonly Node m_Driver; // 引擎帧与窗口服务来源。
    private readonly Task m_Task; // 回归任务，故障也必须报告失败。
    private string m_Failure; // 已捕获的首个失败证据。

    /// <summary>回归任务是否结束。</summary>
    public bool IsDone => m_Task.IsCompleted;

    /// <summary>首个失败原因；成功时为空。</summary>
    public string Failure => m_Failure ?? m_Task.Exception?.ToString();

    /// <summary>启动一次完整入口回归。</summary>
    /// <param name="driver">真实场景树中的测试驱动节点。</param>
    public FrontendSmokeScenario(Node driver)
    {
        m_Driver = driver;
        m_Task = RunAsync();
    }

    /// <summary>其他战斗回归通过真实菜单进入关卡，生产流程不读取烟测参数。</summary>
    /// <returns>已进入关卡流程时返回 true，否则继续等待菜单资源加载。</returns>
    public static bool AdvanceToLevel()
    {
        if (GF.Procedure.CurrentProcedure is ProcedureLevel)
            return true;
        if (GF.UI.GetUIForm(ResourcesCollectionConstant.UI_LoadingForm) != null)
            return false;
        // GGF 的全组枚举顺序不保证最后一项就是当前模态窗，明确选择可见的存档页优先。
        var forms = GF.UI.GetAllLoadedUIForms().OfType<Control>().Where(form => form.Visible).ToArray();
        var form = forms.FirstOrDefault(form => form is SaveForm)
            ?? forms.FirstOrDefault(form => form is CharacterForm or MapForm or StartForm);
        string button = form switch
        {
            StartForm => "m_Start",
            SaveForm save when GF.Fsm.GetModel<SaveModel>().Phase == SaveListState.Ready => "Slots/m_Slot1",
            CharacterForm => "Roles/m_Wukong",
            MapForm => "m_Huaguoshan",
            _ => null,
        };
        if (form?.Visible == true && button != null)
            Press(form, button);
        return false;
    }

    /// <summary>验证三个界面、返回链路、隔离档案和真实战斗开关。</summary>
    /// <returns>回归任务，异常转换为 Failure。</returns>
    private async Task RunAsync()
    {
        float originalMusic = 0, originalSfx = 0, originalUi = 0;
        bool soundInitialized = false;
        try
        {
            StartForm start = await WaitForAsync<StartForm>();
            originalMusic = GF.Sound.GetSoundGroup("Music").Volume;
            originalSfx = GF.Sound.GetSoundGroup("SFX").Volume;
            originalUi = GF.Sound.GetSoundGroup("UI").Volume;
            soundInitialized = true;
            int firstTitleSerial = start.SerialId;
            Check(!Find<AbstractHeroEntity>().Any() && !Find<LevelViewController>().Any(), "标题阶段已创建关卡或英雄");
            await CaptureAsync("start_form");
            await CheckQuestionTipsAsync();
            await CheckMusicAsync("map_theme.mp3");
            await ClickAsync(start, "m_Settings");
            var titleSettings = await WaitForAsync<SettingsForm>();
            Check(!GF.Base.IsGamePaused, "标题设置不应暂停关卡时钟");
            Check(!titleSettings.GetNode<BaseButton>("Scroll/m_ReturnMap").Visible, "标题设置错误显示返回地图");
            await CaptureAsync("title_settings");
            // 官方面板的一项开关同时控制三组声音，关闭后设置持久化。
            if (GF.Sound.GetSoundGroup("Music").Volume == 0 && GF.Sound.GetSoundGroup("SFX").Volume == 0 && GF.Sound.GetSoundGroup("UI").Volume == 0)
                await ClickAsync(titleSettings, "Scroll/m_Sound");
            await ClickAsync(titleSettings, "Scroll/m_Sound");
            Check(new[] { "Music", "SFX", "UI" }.All(group => GF.Sound.GetSoundGroup(group).Volume == 0 && GF.Setting.GetFloat(group) == 0), "统一声音关闭未生效或未持久化");
            await ClickAsync(titleSettings, "Scroll/m_Sound");
            Check(new[] { "Music", "SFX", "UI" }.All(group => GF.Sound.GetSoundGroup(group).Volume > 0), "统一声音恢复未生效");
            var close = titleSettings.GetNode<TextureButton>("Scroll/m_Close");
            Check(close.Size.IsEqualApprox(new Vector2(41, 41)) && close.TextureHover.ResourcePath.EndsWith("menu_close_hover.png"), "关闭按钮未使用原生尺寸及悬停图");
            await MoveMouseAsync(close.GetGlobalRect().GetCenter());
            await CaptureAsync("settings_close_hover");
            await ClickAsync(titleSettings, "Scroll/m_Help");
            Check(titleSettings.GetNode<Label>("Scroll/m_HelpText").Visible, "设置帮助未显示");
            await ClickAsync(titleSettings, "Scroll/m_Continue");
            await ClickAsync(titleSettings, "Scroll/m_Close");
            Check(!GF.UI.HasUIForm(titleSettings.SerialId), "标题设置关闭后仍接收交互");
            await ClickAsync(start, "m_Start");

            SaveForm saves = await WaitForSaveAsync();
            Check(GF.Fsm.GetModel<SaveModel>().Slots.All(slot => slot.State == SaveSlotState.Empty), "隔离目录首次不为空");
            await CaptureAsync("save_slots_empty");
            int closedSaveSerial = saves.SerialId;
            saves.GetNode<BaseButton>("m_Close").EmitSignal(BaseButton.SignalName.Pressed);
            Check(saves.SerialId == closedSaveSerial && !GF.UI.HasUIForm(closedSaveSerial) && !GF.UI.IsValidUIForm(saves),
                "存档页关闭后、回池前框架仍将其视为打开");
            // 打开成功通知可能比关闭晚到；不能给已关闭窗口重新挂回流程。
            SmokeInspection.InvokeProcedureCallback("SetFormCallbacks", saves, true);
            Check(SmokeInspection.CallbackCount(saves, "SaveSlotSelected") == 0,
                "关闭存档页仍持有流程回调或被迟到通知重新绑定");
            // 此时编号尚未清零；旧节点的迟到信号必须由框架打开状态挡住。
            saves.GetNode<BaseButton>("Slots/m_Slot1").EmitSignal(BaseButton.SignalName.Pressed);
            saves.GetNode<BaseButton>("m_Close").EmitSignal(BaseButton.SignalName.Pressed);
            for (int frame = 0; frame < 2; frame++)
                await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!GF.UI.HasUIForm(saves.SerialId) &&
                SmokeInspection.CountBindingHandlers(GF.Fsm.GetModel<SaveModel>().Directory, saves) == 0,
                "存档页关闭后仍订阅目录变化");
            Check(GF.Procedure.CurrentProcedure is ProcedureTitle && !Find<CharacterForm>().Any(x => x.Visible),
                "关闭存档页的迟到按钮信号仍触发导航");
            await ClickAsync(start, "m_Start");
            Check(ReferenceEquals(saves, await WaitForSaveAsync()) &&
                SmokeInspection.CountBindingHandlers(GF.Fsm.GetModel<SaveModel>().Directory, saves) == 1 &&
                SmokeInspection.CallbackCount(saves, "SaveSlotSelected") == 1,
                "存档页未复用 GGF 池实例或重复订阅目录");
            await ClickAsync(saves, "Slots/m_Slot2");
            CharacterForm character = await WaitForAsync<CharacterForm>();
            var flow = SmokeInspection.ReadField<GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>>(
                GF.Procedure.CurrentProcedure, "m_Owner");
            CheckTypedFsmData(flow);
            var firstPlayerModel = flow.GetModel<ProfileModel>();
            var firstSaveSystem = flow.GetSystem<SaveSystem>();
            var firstEquipmentSystem = flow.GetSystem<EquipmentSystem>();
            Check(ReferenceEquals(flow.GetArchitecture(), ((IBelongToArchitecture)firstSaveSystem).GetArchitecture()) &&
                ReferenceEquals(((IBelongToArchitecture)firstSaveSystem).GetArchitecture(), ((IBelongToArchitecture)firstEquipmentSystem).GetArchitecture()) &&
                ReferenceEquals(ConfigSystem.Instance.Tables, firstSaveSystem.GetModel<ConfigurationModel>().Tables) &&
                ReferenceEquals(flow.GetUtility<ExperienceCurve>(), firstSaveSystem.GetUtility<ExperienceCurve>()),
                "共享配置、经验曲线或模块没有使用主流程唯一的数据来源");
            Check(firstPlayerModel.HeroIds.Count == 1, "选角未使用读档作用域");
            await CheckCharacterHoverAsync(character);
            await ClickAsync(character, "Roles/m_Wukong");
            Check(!character.GetNode<Label>("m_PlayerOne").Visible, "选角关闭后仍保留玩家标记");

            MapForm map = await WaitForAsync<MapForm>();
            Check(SmokeInspection.CallbackCount(character, "CharacterConfirmed") == 0 &&
                SmokeInspection.CallbackCount(start, "StartGameRequested") == 0,
                "离开选角或标题后仍保留导航回调");
            Check(ReferenceEquals(firstPlayerModel, flow.GetModel<ProfileModel>()) &&
                ReferenceEquals(firstSaveSystem, flow.GetSystem<SaveSystem>()) && ReferenceEquals(firstEquipmentSystem, flow.GetSystem<EquipmentSystem>()),
                "选角到地图更换了档案作用域");
            Check(!Find<AbstractHeroEntity>().Any() && !Find<LevelViewController>().Any(), "点击花果山之前已创建关卡");
            await CaptureAsync("map_form");
            TextureButton location = map.GetNode<TextureButton>("m_Huaguoshan");
            Check(location.TextureNormal.ResourcePath.EndsWith("map_huaguoshan_normal.png", StringComparison.Ordinal), "花果山未使用原项目地点图片");
            Check(location is MapLocationButton, "花果山未使用随显示图像变化的命中控件");
            await MoveMouseAsync(new Vector2(30, 30));
            var currentHint = map.GetNode<AnimationPlayer>("m_LocationAnimation");
            Check(currentHint.IsPlaying() && currentHint.CurrentAnimation == "location/current", "最新关卡没有默认闪烁提示");
            currentHint.Seek(0, true);
            currentHint.Pause();
            float currentLow = ((ShaderMaterial)location.GetNode<TextureRect>("LocationGlow").Material).GetShaderParameter("line_thickness").AsSingle();
            await CaptureAsync("map_current_low");
            currentHint.Play("location/current");
            currentHint.Seek(.5, true);
            currentHint.Pause();
            Check(((ShaderMaterial)location.GetNode<TextureRect>("LocationGlow").Material).GetShaderParameter("line_thickness").AsSingle() > currentLow, "最新关卡边缘没有闪烁");
            Check(Mathf.IsEqualApprox(((ShaderMaterial)location.GetNode<TextureRect>("LocationGlow").Material).GetShaderParameter("image_scale").AsSingle(), .9f), "默认提示错误放大了地点");
            await CaptureAsync("map_current");
            currentHint.Play("location/current");
            await CheckLocationHitRegionAsync(map, location);
            await MoveMouseAsync(location.GetGlobalRect().GetCenter());
            var hover = map.GetNode<AnimationPlayer>("m_LocationAnimation");
            Check(Mathf.IsEqualApprox(hover.SpeedScale, 0.4f) && hover.IsPlaying() && hover.CurrentAnimation == "location/hover", "地点悬停未播放放大与黄光动画");
            hover.Seek(0, true);
            hover.Pause();
            float thinOutline = ((ShaderMaterial)location.GetNode<TextureRect>("LocationGlow").Material).GetShaderParameter("line_thickness").AsSingle();
            await CaptureAsync("map_hover_low");
            hover.Play("location/hover");
            hover.Seek(0.5, true);
            hover.Pause();
            Check(((ShaderMaterial)location.GetNode<TextureRect>("LocationGlow").Material).GetShaderParameter("line_thickness").AsSingle() > thinOutline,
                "悬停动画未改变黄色轮廓参数");
            await CaptureAsync("map_hover");
            if (DisplayServer.GetName() != "headless")
            {
                using Image low = Image.LoadFromFile("res://.godot/validation/frontend/map_hover_low.png");
                using Image high = Image.LoadFromFile("res://.godot/validation/frontend/map_hover.png");
                int changed = 0;
                Rect2 region = location.GetGlobalRect();
                for (int y = (int)region.Position.Y; y < (int)region.End.Y; y++)
                    for (int x = (int)region.Position.X; x < (int)region.End.X; x++)
                        if (low.GetPixel(x, y) != high.GetPixel(x, y))
                            changed++;
                Check(changed > 100, "黄色光晕两次采样没有产生实际像素变化");
            }
            await MoveMouseAsync(new Vector2(30, 30));
            Check(hover.IsPlaying() && hover.CurrentAnimation == "location/current" && Mathf.IsEqualApprox(((ShaderMaterial)location.GetNode<TextureRect>("LocationGlow").Material).GetShaderParameter("image_scale").AsSingle(), .9f), "离开地点后动画没有复位");
            await CheckMusicAsync("map_theme.mp3");

            // 保存图标写入第二格；第六格建档后再读第二格，不能误读最新档。
            flow.GetSystem<ProfileSystem>().AddSoul(7);
            int savedSoul = flow.GetModel<ProfileModel>().Soul.Value;
            await ClickAsync(map, "m_Save");
            await WaitUntilAsync(() => !map.GetNode<BaseButton>("m_Save").Disabled, "手动保存反馈超时");
            Check(await firstSaveSystem.PendingWrite && GF.Archive.CurrentCatalogue.SlotNumber == 2, "地图手动保存写错格");
            await CheckSaveFeedbackAsync(map);
            long secondId = GF.Archive.CurrentCatalogue.UnitId;
            int savedMapSerialId = map.SerialId;
            await ClickAsync(map, "m_Back");
            start = await WaitForAsync<StartForm>();
            Check(Find<SaveNoticeForm>().Any(x => x.SerialId != 0), "切换页面关闭了应用级保存提示");
            Check(flow.GetModel<ProfileModel>() == null && ReferenceEquals(firstSaveSystem, flow.GetSystem<SaveSystem>()) && flow.GetSystem<EquipmentSystem>() == null, "返回标题仍持有旧档案作用域");
            await ClickAsync(start, "m_Start");
            saves = await WaitForSaveAsync();
            var slots = GF.Fsm.GetModel<SaveModel>().Slots;
            Check(slots[1].State == SaveSlotState.Occupied && slots[1].UnitId == secondId && slots[0].State == SaveSlotState.Empty, "存档摘要或框架空槽位置不正确");
            await MoveMouseAsync(saves.GetNode<BaseButton>("Slots/m_Slot2").GetGlobalRect().GetCenter());
            await CaptureAsync("save_slots_occupied");
            await ClickAsync(saves, "Slots/m_Slot6");
            character = await WaitForAsync<CharacterForm>();
            await CheckCharacterHoverAsync(character);
            Check(GF.Archive.CurrentCatalogue.SlotNumber == 6 && GF.Archive.CurrentCatalogue.UnitId != secondId, "第六格覆盖了第二格");
            long sixthId = GF.Archive.CurrentCatalogue.UnitId;
            Check(!await firstSaveSystem.SaveCheckpointAsync(firstPlayerModel), "旧档案检查点写入了另一格");
            // 选角没有返回按钮；回归通过既有导航事件结束临时第六格会话。
            SmokeInspection.ChangeProcedure<ProcedureTitle>();
            start = await WaitForAsync<StartForm>();
            await ClickAsync(start, "m_Start");
            saves = await WaitForSaveAsync();
            await ClickAsync(saves, "Slots/m_Slot2");
            map = await WaitForAsync<MapForm>();
            Check(!character.Visible && GF.Procedure.CurrentProcedure is ProcedureMap, "已有档案未直接进入地图");
            var reloadedPlayerModel = flow.GetModel<ProfileModel>();
            Check(!ReferenceEquals(firstPlayerModel, reloadedPlayerModel) &&
                ReferenceEquals(firstSaveSystem, flow.GetSystem<SaveSystem>()) &&
                !ReferenceEquals(firstEquipmentSystem, flow.GetSystem<EquipmentSystem>()), "重新读档复用了已结束的作用域");
            Check(flow.GetModel<ProfileModel>().Soul.Value == savedSoul, "重新读档没有从快照重建运行时状态");
            GF.Fsm.GetModel<SaveModel>().ManualSave.Value = new(savedMapSerialId, SaveWritePhase.Succeeded);
            await MoveMouseAsync(new Vector2(30, 30));
            Check(map.GetNode<BaseButton>("m_Save").TooltipText == "保存游戏", "旧周期保存结果污染了复用地图");
            Check(GF.Archive.CurrentCatalogue.UnitId == secondId && GF.Archive.CurrentData.Soul == savedSoul, "选择第二格读取成最新格或进度丢失");
            // 从普通树身进入悬停，再在仅放大图可见的位置实际点击进关。
            location = map.GetNode<TextureButton>("m_Huaguoshan");
            await MoveMouseAsync(location.GetGlobalRect().GetCenter());
            Vector2 expandedClick = location.GetGlobalTransformWithCanvas() *
                (location.GetNode<TextureRect>("LocationGlow").GetTransform() * new Vector2(40, 87));
            await ClickAsync(map, "m_Huaguoshan", expandedClick);
            BattleHud hud = await WaitForAsync<BattleHud>();
            AbstractHeroEntity hero = Find<AbstractHeroEntity>().Single(x => x.IsShown);
            Check(Find<LevelViewController>().Count() == 1, "点击花果山未创建唯一关卡");
            var level = SmokeInspection.Level((ProcedureLevel)GF.Procedure.CurrentProcedure);
            var levelModel = flow.GetModel<LevelModel>();
            var heroModel = flow.GetModel<HeroModel>();
            var heroSystem = flow.GetSystem<HeroSystem>();
            Check(heroModel.Initialized && ReferenceEquals(heroModel, SmokeInspection.HeroAttributes(hero)),
                "英雄 Entity 没有借用 Architecture 注册的同一运行属性");
            Check(SmokeInspection.CountBindingHandlers(flow.GetModel<ProfileModel>().Progress, heroSystem) == 1 &&
                SmokeInspection.CountBindingHandlers(flow.GetModel<ProfileModel>().Slots, heroSystem) == 1,
                "英雄属性系统没有唯一订阅成长和穿戴来源");
            var levelSystem = flow.GetSystem<LevelSystem>();
            Check(ReferenceEquals(flow.GetArchitecture(), ((IBelongToArchitecture)level).GetArchitecture()),
                "关卡没有借用流程持有的档案作用域");
            Check(!GF.Debugger.ActiveWindow, "框架调试按钮遮挡头像");
            await CheckMusicAsync("huaguoshan_theme.mp3");
            await CaptureAsync("frontend_battle");
            var data = SmokeInspection.ReadField<BattleHudData>(hud, "m_Data");
            var playerModel = SmokeInspection.Flow((ProcedureLevel)GF.Procedure.CurrentProcedure).GetModel<ProfileModel>();
            Check(ReferenceEquals(playerModel, reloadedPlayerModel) &&
                ReferenceEquals(data.Hero, hero) && ReferenceEquals(data.ViewController, level), "HUD 与关卡没有借用同一份运行状态");
            int experience = playerModel.TotalExperience;
            flow.GetSystem<ProfileSystem>().AddExperience(1);
            await ClickAsync(hud, "MenuPanel/m_SettingsButton");
            var settings = await WaitForAsync<SettingsForm>();
            Check(GF.Base.IsGamePaused, "战斗设置未暂停");
            Check(ReferenceEquals(settings, titleSettings), "设置未复用已经关闭的 GGF 池实例");
            Check(SmokeInspection.CallbackCount(settings, "MapRequested") == 1 &&
                SmokeInspection.CallbackCount(settings, "TitleRequested") == 1,
                "复用设置未绑定当前战斗导航或重复绑定");
            Check(settings.ZIndex > hud.ZIndex, "复用设置的显示层级低于 HUD");
            await CaptureAsync("battle_settings");
            Vector2 pausedPosition = hero.Position;
            Input.ActionPress(GameLogic.Input.InputSystem.MoveRight);
            for (int frame = 0; frame < 8; frame++)
                await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.ProcessFrame);
            Input.ActionRelease(GameLogic.Input.InputSystem.MoveRight);
            Check(hero.Position.IsEqualApprox(pausedPosition), "设置暂停时英雄仍在移动");
            await ClickAsync(settings, "Scroll/m_Continue");
            Check(!GF.Base.IsGamePaused && hud.Visible &&
                SmokeInspection.CountBindingHandlers(levelModel.Outcome, settings) == 0 &&
                SmokeInspection.CallbackCount(settings, "MapRequested") == 0 &&
                SmokeInspection.CallbackCount(settings, "TitleRequested") == 0,
                "继续游戏未恢复战斗或设置仍订阅关卡");
            await ClickAsync(hud, "MenuPanel/m_SettingsButton");
            settings = await WaitForAsync<SettingsForm>();
            await ClickAsync(settings, "Scroll/m_ReturnMap");
            map = await WaitForAsync<MapForm>();
            Check(!hero.IsShown && !Find<LevelViewController>().Any(), "返回地图未清理英雄与关卡");
            Check(playerModel.TotalExperience == experience, "中途返回地图未回滚关内收益");
            Check(ReferenceEquals(reloadedPlayerModel, flow.GetModel<ProfileModel>()) &&
                !levelModel.SessionActive && !levelSystem.Initialized && !levelModel.Initialized, "返回地图未保留档案或未解除关卡借用");
            Check(SmokeInspection.ReadField<BattleHudData>(hud, "m_Data") == null, "HUD 关闭后仍观察旧关卡");
            Check(!GF.Base.IsGamePaused, "返回地图后仍处于暂停");
            Check(!heroModel.Initialized && flow.GetModel<HeroModel>() == null &&
                !heroSystem.Initialized && flow.GetSystem<HeroSystem>() == null &&
                SmokeInspection.CountBindingHandlers(playerModel.Progress, heroSystem) == 0 &&
                SmokeInspection.CountBindingHandlers(flow.GetModel<ProfileModel>().Slots, heroSystem) == 0,
                "离关没有结束英雄运行模型");
            Check(SmokeInspection.CallbackCount(hud, "SettingsRequested") == 0 &&
                SmokeInspection.CallbackCount(hud, "InventoryRequested") == 0 &&
                SmokeInspection.CallbackCount(hud, "SkillsRequested") == 0,
                "离关后 HUD 仍持有流程回调");
            Check(!GF.UI.HasUIForm(settings.SerialId) &&
                SmokeInspection.CountBindingHandlers(levelModel.Outcome, settings) == 0,
                "战斗设置未解除关卡订阅");
            Check(SmokeInspection.CallbackCount(map, "SaveRequested") == 1, "地图保存回调重复绑定");

            // 再入关后从官方第五项返回标题，也必须回滚并恢复时钟。
            await ClickAsync(map, "m_Huaguoshan");
            hud = await WaitForAsync<BattleHud>();
            Check(Find<AbstractHeroEntity>().Count(x => x.IsShown) == 1, "重入生成了重复英雄");
            Check(flow.GetModel<HeroModel>().Initialized && !ReferenceEquals(heroModel, flow.GetModel<HeroModel>()),
                "新关卡沿用了已结束的英雄运行模型");
            var previousInventory = flow.GetModel<ProfileModel>();
            flow.GetSystem<ProfileSystem>().AddSoul(9);
            await ClickAsync(hud, "MenuPanel/m_SettingsButton");
            settings = await WaitForAsync<SettingsForm>();
            await ClickAsync(settings, "Scroll/m_ReturnTitle");
            start = await WaitForAsync<StartForm>();
            Check(previousInventory.Soul.Value == savedSoul && !previousInventory.Initialized && flow.GetModel<ProfileModel>() == null && !GF.Base.IsGamePaused && !Find<LevelViewController>().Any(), "返回标题未回滚收益、恢复时钟或清理关卡");
            Check(flow.GetModel<ProfileModel>() == null && ReferenceEquals(firstSaveSystem, flow.GetSystem<SaveSystem>()) && flow.GetSystem<EquipmentSystem>() == null, "战斗返回标题仍持有档案作用域");
            Check(!GF.UI.HasUIForm(character.SerialId), "关闭后选角仍接收请求");
            // 损坏槽不能被当成空格；读取目录也不能使旧档案向其他槽写入。
            string broken = ProjectSettings.GlobalizePath($"user://{GF.Archive.Setting.Folder}/Data/{sixthId}.sav");
            File.WriteAllText(broken, "{ broken json");
            File.WriteAllText(broken + ".bak", "{ broken json");
            await ClickAsync(start, "m_Start");
            saves = await WaitForSaveAsync();
            Check(saves.GetNode<BaseButton>("Slots/m_Slot6").Disabled && GF.Fsm.GetModel<SaveModel>().Slots[1].State == SaveSlotState.Occupied, "损坏格未禁用或影响其他存档");
            SmokeInspection.InvokeProcedureCallback("OnSaveSlotSelected", saves.SerialId, 6);
            for (int frame = 0; frame < 4; frame++)
                await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(GF.Procedure.CurrentProcedure is ProcedureTitle, "损坏格仍能进入读档流程");
            await ClickAsync(saves, "m_Close");
            await CheckMusicAsync("map_theme.mp3");
            SmokeInspection.InvokeProcedureCallback("OnStartGameRequested", firstTitleSerial);
            for (int frame = 0; frame < 4; frame++)
                await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(GF.Procedure.CurrentProcedure is ProcedureTitle, "旧窗口排队事件改变了新周期流程");
        }
        catch (Exception error)
        {
            m_Failure = error.ToString();
            await CaptureAsync("smoke_failure");
        }
        finally
        {
            // 开关回归使用应用设置，但必须恢复用户原有的各组音量。
            if (soundInitialized)
            {
                GF.Sound.SetVolume("Music", originalMusic);
                GF.Sound.SetVolume("SFX", originalSfx);
                GF.Sound.SetVolume("UI", originalUi);
            }
        }
    }

    /// <summary>在原生 FSM 验证归属、初始化、权限、缺失依赖、移除与池复用隔离。</summary>
    /// <param name="flow">正式流程提供的测试宿主。</param>
    private static void CheckTypedFsmData(ProcedureOwner flow)
    {
        var first = GF.Fsm.CreateFsm<GameFramework.Procedure.IProcedureManager>("ArchitectureProbe1", flow.Owner, new ArchitectureProbeState());
        var second = GF.Fsm.CreateFsm<GameFramework.Procedure.IProcedureManager>("ArchitectureProbe2", flow.Owner, new ArchitectureProbeState());
        var firstArchitecture = first.InitializeArchitecture();
        var secondArchitecture = second.InitializeArchitecture();
        try
        {
            Check(firstArchitecture.GetModel<ArchitectureProbeModel>() == null &&
                firstArchitecture.GetSystem<EquipmentSystem>() == null &&
                firstArchitecture.GetUtility<GameLogic.Utility.SpatialUtility>() == null,
                "未注册的 Model、System 或 Utility 没有返回 null");
            ArchitectureProbeModel model = new(1);
            first.RegisterModel(model);
            second.RegisterModel(new ArchitectureProbeModel(2));
            Check(ReferenceEquals(model, first.GetModel<ArchitectureProbeModel>()) && second.GetModel<ArchitectureProbeModel>().Value == 2,
                "模型引用被复制或跨 FSM 串用");
            Check(model.Initialized && model.InitCount == 1 && ReferenceEquals(((IBelongToArchitecture)model).GetArchitecture(), firstArchitecture),
                "Model 未绑定归属或重复初始化");
            EquipmentSystem firstEquipmentSystem = new(firstArchitecture);
            EquipmentSystem secondEquipmentSystem = new(secondArchitecture);
            first.RegisterSystem(firstEquipmentSystem);
            second.RegisterSystem(secondEquipmentSystem);
            Check(ReferenceEquals(firstEquipmentSystem, first.GetSystem<EquipmentSystem>()) && ReferenceEquals(model, firstEquipmentSystem.GetModel<ArchitectureProbeModel>()),
                "System 访问未读取原 FSM 数据");
            second.RemoveSystem<EquipmentSystem>();
            bool foreignSystemRejected = false;
            try
            {
                second.RegisterSystem(firstEquipmentSystem);
            }
            catch (InvalidOperationException)
            {
                foreignSystemRejected = true;
            }
            Check(foreignSystemRejected, "模块错误转移到其他 Architecture");
            second.RegisterSystem(secondEquipmentSystem);
            bool duplicateRejected = false;
            try
            {
                first.RegisterModel(new ArchitectureProbeModel(3));
            }
            catch (InvalidOperationException)
            {
                duplicateRejected = true;
            }
            Check(duplicateRejected && ReferenceEquals(model, first.GetModel<ArchitectureProbeModel>()), "重复注册替换了已借出模型");
            Check(first.RemoveModel<ArchitectureProbeModel>() && !model.Initialized && model.DeinitCount == 1 && second.GetModel<ArchitectureProbeModel>().Value == 2,
                "移除未结束生命周期或影响其他作用域");
            Check(firstArchitecture.GetModel<ArchitectureProbeModel>() == null && firstEquipmentSystem.GetModel<ArchitectureProbeModel>() == null,
                "移除后的模型仍能经架构或系统访问");
            Check(!typeof(ICanGetModel).IsAssignableFrom(typeof(IModel)) && !typeof(ICanGetSystem).IsAssignableFrom(typeof(IModel)) &&
                !typeof(IBelongToArchitecture).IsAssignableFrom(typeof(IUtility)) && !typeof(Godot.Node).IsAssignableFrom(typeof(LevelSystem)),
                "Model/Utility 权限或普通 System 边界不正确");
            GF.Fsm.DestroyFsm(first);
            first = GF.Fsm.CreateFsm<GameFramework.Procedure.IProcedureManager>("ArchitectureProbe1", flow.Owner, new ArchitectureProbeState());
            var replacementArchitecture = first.InitializeArchitecture();
            first.RegisterModel(new ArchitectureProbeModel(4));
            bool staleArchitectureRejected = false;
            try
            {
                firstEquipmentSystem.GetModel<ArchitectureProbeModel>();
            }
            catch (InvalidOperationException)
            {
                staleArchitectureRejected = true;
            }
            Check(staleArchitectureRejected && replacementArchitecture.GetModel<ArchitectureProbeModel>().Value == 4,
                "旧入口穿透到 FSM 复用后的新游戏");
        }
        finally
        {
            GF.Fsm.DestroyFsm(first);
            GF.Fsm.DestroyFsm(second);
        }
        bool destroyedArchitectureRejected = false;
        try
        {
            secondArchitecture.GetModel<ArchitectureProbeModel>();
        }
        catch (InvalidOperationException)
        {
            destroyedArchitectureRejected = true;
        }
        Check(destroyedArchitectureRejected, "FSM 销毁后仍能访问模型");
    }

    /// <summary>确认提示关闭释放回调，复用后只执行本次回调。</summary>
    /// <returns>真实框架提示验证任务。</returns>
    private async Task CheckQuestionTipsAsync()
    {
        int callbacks = 0;
        GF.UI.OpenQuestionTipsAsync("共享提示关闭与复用验证", "取消", "确认", () => callbacks += 10, () => callbacks++);
        var first = await WaitForAsync<QuestionTips>();
        int closedTipSerial = first.SerialId;
        Check(GF.UI.HasUIForm(closedTipSerial) && GF.UI.IsValidUIForm(first), "打开提示未登记在 GGF 中");
        GF.UI.CloseUIForm(closedTipSerial);
        Check(first.SerialId == closedTipSerial && !GF.UI.HasUIForm(closedTipSerial) && !GF.UI.IsValidUIForm(first),
            "提示关闭后、回池前框架仍将其视为打开");
        Check(SmokeInspection.ReadField<Action>(first, "m_Cancel") == null && SmokeInspection.ReadField<Action>(first, "m_Confirm") == null, "提示关闭后仍持有回调");
        first.GetNode<BaseButton>("Content/VBoxContainer/HBoxContainer/m_Btn_Confirm").EmitSignal(BaseButton.SignalName.Pressed);
        Check(callbacks == 0, "隐藏提示执行了旧回调");
        // GGF 在下一轮更新归还关闭对象，再请求才验证同一实例复用。
        for (int frame = 0; frame < 4; frame++)
            await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.ProcessFrame);
        GF.UI.OpenQuestionTipsAsync("复用后执行本次确认", "取消", "确认", null, () => callbacks++);
        var second = await WaitForAsync<QuestionTips>();
        Check(ReferenceEquals(first, second), "共享提示没有复用 GGF 池实例");
        await ClickAsync(second, "Content/VBoxContainer/HBoxContainer/m_Btn_Confirm");
        Check(callbacks == 1 && SmokeInspection.ReadField<Action>(second, "m_Confirm") == null, "关闭清理吞掉了本次回调或重复执行旧回调");
    }

    /// <summary>等待六格目录可供选择。</summary>
    /// <returns>已就绪的存档窗口。</returns>
    private async Task<SaveForm> WaitForSaveAsync()
    {
        var form = await WaitForAsync<SaveForm>();
        await WaitUntilAsync(() => GF.Fsm.GetModel<SaveModel>().Phase == SaveListState.Ready, "存档目录读取超时");
        return form;
    }

    /// <summary>等待异步目录或写入，超时记录业务失败。</summary>
    /// <param name="ready">目标完成条件。</param>
    /// <param name="failure">超时证据。</param>
    /// <returns>完成条件等待任务。</returns>
    private async Task WaitUntilAsync(Func<bool> ready, string failure)
    {
        ulong deadline = Time.GetTicksMsec() + 15000;
        while (!ready() && Time.GetTicksMsec() < deadline)
            await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(ready(), failure);
    }

    /// <summary>等待实际 GGF 窗口打开，给资源加载最多 15 秒。</summary>
    /// <typeparam name="T">窗口类型。</typeparam>
    /// <returns>已显示窗口。</returns>
    private async Task<T> WaitForAsync<T>() where T : Control
    {
        ulong deadline = Time.GetTicksMsec() + 15000;
        while (Time.GetTicksMsec() < deadline)
        {
            var form = GF.UI.GetAllLoadedUIForms().OfType<T>().FirstOrDefault(x => x.Visible);
            if (form != null && GF.UI.GetUIForm(ResourcesCollectionConstant.UI_LoadingForm) == null)
                return form;
            await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        throw new InvalidOperationException($"等待 {typeof(T).Name} 超时。");
    }

    /// <summary>等待淡出结束后检查 GGF 的 AudioStreamPlayer 代理，循环取决于正式导入设置。</summary>
    /// <param name="file">期望的音乐资源文件名。</param>
    /// <returns>音乐检查任务。</returns>
    private async Task CheckMusicAsync(string file)
    {
        for (int frame = 0; frame < 35; frame++)
            await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.ProcessFrame);
        var music = Find<AudioStreamPlayer>().Where(x => x.Bus == "Music" && x.Playing).ToArray();
        Check(music.Length == 1 && music[0].Stream.ResourcePath.EndsWith(file, StringComparison.Ordinal),
            $"GGF Music 代理未唯一播放 {file}");
        Check(music[0].Stream is AudioStreamMP3 { Loop: true }, "音乐资源未启用循环");
        Check(music[0].Stream.GetLength() > 30, "短确认音被误当成循环音乐");
        Check(!Find<AudioStreamPlayer>().Any(player => player.Bus == "UI" && player.Playing), "确认音播放超过淡出等待时间仍未停止");
    }

    /// <summary>真实 GPU 运行时保存正式界面截图；headless 模式只验证行为。</summary>
    /// <param name="name">截图 basename。</param>
    /// <returns>截图任务。</returns>
    private async Task CaptureAsync(string name)
    {
        if (DisplayServer.GetName() == "headless")
            return;
        for (int frame = 0; frame < 3; frame++)
            await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.ProcessFrame);
        await m_Driver.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute("res://.godot/validation/frontend");
        m_Driver.GetViewport().GetTexture().GetImage().SavePng($"res://.godot/validation/frontend/{name}.png");
    }

    /// <summary>从真实场景树读取测试所需节点，正式模块不遍历其他模块。</summary>
    /// <typeparam name="T">节点脚本类型。</typeparam>
    /// <returns>匹配的节点序列。</returns>
    private System.Collections.Generic.IEnumerable<T> Find<T>() where T : Node =>
        m_Driver.GetTree().Root.FindChildren("*", "", true, false).OfType<T>();

    /// <summary>激活真实已绑定的按钮，禁止对隐藏或禁用控件伪造成功。</summary>
    /// <param name="form">所属 GGF 窗口。</param>
    /// <param name="path">窗口内按钮路径。</param>
    private static void Press(Control form, string path)
    {
        BaseButton button = form.GetNode<BaseButton>(path);
        Check(form.Visible && !button.Disabled && button.GetGlobalRect().HasArea(), $"按钮不可操作：{path}");
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    /// <summary>使用视口局部坐标分发鼠标移动，避免窗口缩放改变测试命中位置。</summary>
    /// <param name="position">视口内鼠标坐标。</param>
    /// <returns>输入分发和布局更新任务。</returns>
    private async Task MoveMouseAsync(Vector2 position)
    {
        for (int frame = 0; frame < 3; frame++)
            await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.ProcessFrame);
        // 布局稳定后立即分发；坐标已在视口空间，不再按原生窗口尺寸变换。
        m_Driver.GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
    }

    /// <summary>用真实鼠标验证已开放角色预览、未开放角色无交互及窗口复用复位。</summary>
    /// <param name="character">当前正式选角窗口。</param>
    /// <returns>四栏交互检查任务。</returns>
    private async Task CheckCharacterHoverAsync(CharacterForm character)
    {
        Control[] roles = character.GetNode("Roles").GetChildren().OfType<Control>().ToArray();
        Label marker = character.GetNode<Label>("m_PlayerOne");
        Check(roles.Length == 4 && roles[0] is TextureButton && roles.Skip(1).All(role => role is TextureRect), "角色节点未按四栏职责分组");
        Check(roles.All(role => role.Size.IsEqualApprox(new Vector2(235, 590))) && marker.Text == "1P", "四栏构图或玩家标记错误");
        Check(roles.All(role => role.GetChildCount() == 0), "完整卡片仍叠加重复姓名或笔刷组件");
        await MoveMouseAsync(new Vector2(-5, -5));
        Check(!marker.Visible && roles.All(role => Mathf.IsEqualApprox(((ShaderMaterial)role.Material).GetShaderParameter("grayscale").AsSingle(), 1f)), "无悬停时不是全灰或标记未隐藏");
        await CaptureAsync("character_idle");
        foreach (Control role in roles)
        {
            await MoveMouseAsync(role.GetGlobalRect().GetCenter());
            bool selectable = role is TextureButton;
            Check(marker.Visible == selectable, "未开放角色显示了 1P 或已开放角色缺少标记");
            if (selectable)
                Check(Mathf.IsEqualApprox(marker.GetGlobalRect().GetCenter().X, role.GetGlobalRect().GetCenter().X), "1P 未对齐已开放角色");
            Check(roles.All(card => Mathf.IsEqualApprox(((ShaderMaterial)card.Material).GetShaderParameter("grayscale").AsSingle(), card == role && card is TextureButton ? 0f : 1f)), "已开放角色悬停未恢复颜色、未开放角色变色或旧卡片未复位");
            Check((m_Driver.GetViewport().GuiGetHoveredControl() == role) == selectable, "已开放角色不能悬停或未开放角色仍接收鼠标");
            Check(role.GetCursorShape() == (role is TextureButton ? Control.CursorShape.PointingHand : Control.CursorShape.Arrow), "角色光标没有反映可选状态");
            if (role.Name == "m_Sanzang")
                await CaptureAsync("character_sanzang_hover");
            if (role is TextureRect)
            {
                Check(role.MouseFilter == Control.MouseFilterEnum.Ignore && string.IsNullOrEmpty(role.TooltipText), "未开放角色仍存在鼠标交互或悬停提示");
                Vector2 position = role.GetGlobalRect().GetCenter();
                m_Driver.GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true }, true);
                await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.ProcessFrame);
                m_Driver.GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false }, true);
                await MoveMouseAsync(position);
                Check(GF.Procedure.CurrentProcedure is ProcedureCharacter, "尚未开放的角色触发了导航");
            }
        }
        await MoveMouseAsync(roles[0].GetGlobalRect().GetCenter());
        await CaptureAsync("character_form");
        await MoveMouseAsync(new Vector2(-5, -5));
        Check(!marker.Visible, "离开角色后 1P 仍显示");
    }

    /// <summary>验证真实写入后的提示、自动消失、错误结果及再次保存时的动画复位。</summary>
    /// <param name="map">已经完成一次实际保存的地图窗口。</param>
    /// <returns>保存提示交互检查任务。</returns>
    private async Task CheckSaveFeedbackAsync(MapForm map)
    {
        var notice = Find<SaveNoticeForm>().First(x => x.SerialId != 0);
        Label feedback = notice.GetNode<Label>("SaveFeedback/m_SaveFeedback");
        AnimationPlayer animation = notice.GetNode<AnimationPlayer>("m_SaveFeedbackAnimation");
        await WaitUntilAsync(() => feedback.Visible && animation.IsPlaying(), "实际写盘成功后未显示保存成功提示");
        Check(feedback.Text == "保存成功！", "保存成功提示文本错误");
        Check(feedback.MouseFilter == Control.MouseFilterEnum.Ignore && feedback.GetParent<Control>().MouseFilter == Control.MouseFilterEnum.Ignore, "提示遮挡了地图鼠标输入");
        await CaptureAsync("map_save_success");
        await WaitUntilAsync(() => !feedback.Visible, "保存提示未自动消失");
        Check(!animation.IsPlaying() && Mathf.IsEqualApprox(feedback.Modulate.A, 1f) && feedback.Position.IsEqualApprox(new Vector2(-250, -61)), "提示淡出后未恢复初始位置和透明度");

        // 在现有结果事件边界模拟写入失败，不破坏文件，也不增加生产测试接口。
        GF.Fsm.GetModel<SaveModel>().ManualSave.Value = new(map.SerialId, SaveWritePhase.Failed);
        await WaitUntilAsync(() => map.GetNode<BaseButton>("m_Save").TooltipText.Contains("失败"), "失败结果未更新按钮状态");
        Check(!feedback.Visible && !map.GetNode<BaseButton>("m_Save").Disabled, "写入失败误报成功或不能重试");
        await CaptureAsync("map_save_failure");
        await ClickAsync(map, "m_Save");
        await WaitUntilAsync(() => !map.GetNode<BaseButton>("m_Save").Disabled, "再次保存未完成");
        // 绑定结果同步提交，GGF 的一次性保存成功事件在下一轮分发。
        await WaitUntilAsync(() => feedback.Visible && animation.IsPlaying(), "再次保存成功事件未显示提示");
        Check(feedback.Text == "保存成功！" && Mathf.IsEqualApprox(feedback.Modulate.A, 1f), "再次实际保存未刷新提示或沿用旧透明度");
    }

    /// <summary>验证普通图透明区拒绝输入，悬停图扩展区保持悬停且跟随节点缩放。</summary>
    /// <param name="map">当前地图窗口。</param>
    /// <param name="location">按当前显示图像进行透明轮廓命中的按钮。</param>
    /// <returns>真实鼠标输入回归任务。</returns>
    private async Task CheckLocationHitRegionAsync(MapForm map, TextureButton location)
    {
        Check(location.TextureClickMask == null && location.TextureHover == null, "原生固定遮罩覆盖了动态命中逻辑");
        Vector2 imageOrigin = (location.Size - location.TextureNormal.GetSize()) / 2;
        bool pressed = false;
        Action observe = () => pressed = true;
        location.Pressed += observe;
        try
        {
            foreach (Vector2 local in new[] { new Vector2(3, 3), new Vector2(157, 3), new Vector2(3, 171), new Vector2(157, 171) })
            {
                Vector2 position = location.GlobalPosition + local;
                await MoveMouseAsync(position);
                Check(map.GetNode<AnimationPlayer>("m_LocationAnimation").CurrentAnimation == "location/current" && m_Driver.GetViewport().GuiGetHoveredControl() != location, $"透明区域触发悬停：{local}");
                m_Driver.GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true }, true);
                await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.ProcessFrame);
                m_Driver.GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false }, true);
                await MoveMouseAsync(position);
                Check(!pressed && GF.Procedure.CurrentProcedure is ProcedureMap, $"透明区域触发进关：{local}");
            }
        }
        finally
        {
            location.Pressed -= observe;
        }

        var hover = map.GetNode<AnimationPlayer>("m_LocationAnimation");
        var glow = location.GetNode<TextureRect>("LocationGlow");
        Vector2 expanded = location.GetGlobalTransformWithCanvas() * (glow.GetTransform() * new Vector2(40, 87));
        await MoveMouseAsync(location.GetGlobalRect().GetCenter());
        for (int sample = 0; sample <= 5; sample++)
        {
            hover.Seek(sample / 5.0, true);
            await MoveMouseAsync(expanded);
            Check(hover.IsPlaying() && glow.Visible && m_Driver.GetViewport().GuiGetHoveredControl() == location,
                "鼠标仍在放大图内时提前结束悬停");
        }
        await CaptureAsync("map_hover_expanded_hit");

        // 检查器进一步放大到按钮矩形外时，也应按真实子图变换命中。
        Vector2 originalScale = glow.Scale;
        try
        {
            glow.Scale = new Vector2(1.6f, 1.6f);
            Vector2 outerLocal = glow.GetTransform() * new Vector2(25, 87);
            Check(!new Rect2(Vector2.Zero, location.Size).HasPoint(outerLocal), "扩展探针未离开原按钮矩形");
            await MoveMouseAsync(location.GetGlobalTransformWithCanvas() * outerLocal);
            Check(hover.IsPlaying() && glow.Visible && m_Driver.GetViewport().GuiGetHoveredControl() == location,
                "放大图超出原按钮矩形后不能保持悬停");
        }
        finally
        {
            glow.Scale = originalScale;
        }
        await MoveMouseAsync(new Vector2(30, 30));
        Check(hover.IsPlaying() && hover.CurrentAnimation == "location/current" && glow.Visible, "离开放大图后未恢复最新进度提示");
        await MoveMouseAsync(location.GlobalPosition + new Vector2(3, 3));
        Check(hover.CurrentAnimation == "location/current" && m_Driver.GetViewport().GuiGetHoveredControl() != location, "默认提示的透明区域错误接收输入");
    }

    /// <summary>通过真实鼠标按下和松开验证点击、遮挡、坐标与按钮绑定。</summary>
    /// <param name="form">窗口。</param>
    /// <param name="path">按钮路径。</param>
    /// <param name="positionOverride">可选的视口点击位置；默认按钮中心。</param>
    /// <returns>点击分发任务。</returns>
    private async Task ClickAsync(Control form, string path, Vector2? positionOverride = null)
    {
        var button = form.GetNode<BaseButton>(path);
        Check(form.IsVisibleInTree() && button.IsVisibleInTree() && !button.Disabled, $"按钮不可点击：{path}");
        Vector2 position = positionOverride ?? button.GetGlobalRect().GetCenter();
        bool pressed = false;
        Action observePress = () => pressed = true;
        button.Pressed += observePress;
        await MoveMouseAsync(position);
        Check(button.GetCursorShape() == Control.CursorShape.PointingHand, $"可点击按钮未显示手形光标：{form.Name}/{path}");
        m_Driver.GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        m_Driver.GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false }, true);
        for (int frame = 0; frame < 3; frame++)
            await m_Driver.ToSignal(m_Driver.GetTree(), SceneTree.SignalName.ProcessFrame);
        if (GodotObject.IsInstanceValid(button))
            button.Pressed -= observePress;
        if (!pressed)
            await CaptureAsync("click_failure");
        Check(pressed, $"真实鼠标未触发按钮：{path} @ {position}，窗口 {form.Name} Z={form.ZIndex}，悬停节点 {m_Driver.GetViewport().GuiGetHoveredControl()?.GetPath()}");
    }

    /// <summary>失败立即结束回归并保留首个证据。</summary>
    /// <param name="condition">期望条件。</param>
    /// <param name="message">失败原因。</param>
    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    /// <summary>仅供原生 FSM 作用域验证的空状态。</summary>
    private sealed class ArchitectureProbeState : GameFramework.Fsm.FsmState<GameFramework.Procedure.IProcedureManager> { }

    /// <summary>仅供测试验证注册生命周期，不让正式钱包为测试继承 Model。</summary>
    /// <param name="value">测试值。</param>
    private sealed class ArchitectureProbeModel(int value) : AbstractModel
    {
        /// <summary>只读测试值。</summary>
        public int Value { get; } = value;

        /// <summary>实际初始化次数。</summary>
        public int InitCount { get; private set; }

        /// <summary>实际解除次数。</summary>
        public int DeinitCount { get; private set; }
        /// <inheritdoc />
        protected override void OnInit() => InitCount++;
        /// <inheritdoc />
        protected override void OnDeinit() => DeinitCount++;
    }
}
