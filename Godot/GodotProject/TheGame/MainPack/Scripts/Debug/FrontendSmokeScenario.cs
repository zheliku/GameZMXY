using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GameConfig;
using GameConfig.Constant;
using GameConfig.Skills;
using GameLogic;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Config;
using GameLogic.Equipment;
using GameLogic.Heroes;
using GameLogic.Input;
using GameLogic.Level;
using GameLogic.Procedure.FlowData;
using GameLogic.Profile;
using GameLogic.Save;
using GameLogic.Save.Data;
using GameLogic.Save.Slots;
using GameLogic.Skills;
using GameLogic.UI;
using GameLogic.UI.Widgets;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Json;
using GodotGameFramework.Sound;
using GodotGameFramework.UI;
using Newtonsoft.Json.Linq;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 在真实框架内验证菜单导航、关卡选择、设置保存后返回、普通离开回滚、音乐代理和菜单复用；不增加正式观测 API。
/// </summary>
public sealed partial class FrontendSmokeScenario
{
    private readonly Node _driver; // 引擎帧与窗口服务来源。
    private readonly Task _task; // 回归任务，故障也必须报告失败。
    private string _failure; // 已捕获的首个失败证据。

    /// <summary>
    /// 启动一次完整入口回归。
    /// </summary>
    /// <param name="driver">真实场景树中的测试驱动节点。</param>
    public FrontendSmokeScenario(Node driver)
    {
        _driver = driver;
        _task = RunAsync();
    }

    /// <summary>
    /// 回归任务是否结束。
    /// </summary>
    public bool IsDone => _task.IsCompleted;

    /// <summary>
    /// 首个失败原因；成功时为空。
    /// </summary>
    public string Failure => _failure ?? _task.Exception?.ToString();

    /// <summary>
    /// 实际拖动原生滑条，检查两组独立控制、静音恢复、界面音与持久设置一致。
    /// </summary>
    /// <param name="settings">当前真实设置窗口。</param>
    /// <returns>交互完成的任务。</returns>
    private async Task CheckVolumeSlidersAsync(SettingsForm settings)
    {
        HSlider music = settings.GetNode<HSlider>("Scroll/Content/Options/m_AudioPanel/Music/m_MusicVolume");
        HSlider effect = settings.GetNode<HSlider>("Scroll/Content/Options/m_AudioPanel/Effect/m_EffectVolume");
        float effectBefore = GF.Sound.GetSoundGroup("SFX").Volume;
        await DragVolumeAsync(music, .43f);
        float musicValue = (float)music.Value / 100;
        Check(Math.Abs(musicValue - .43f) < .03 && GF.Sound.GetSoundGroup("Music").Volume == musicValue &&
            GF.Sound.GetSoundGroup("SFX").Volume == effectBefore && GF.Setting.GetFloat("Music") == musicValue,
            $"音乐滑条没有独立修改GGF音乐或持久化：slider={music.Value}, group={GF.Sound.GetSoundGroup("Music").Volume}, sfx={GF.Sound.GetSoundGroup("SFX").Volume}, before={effectBefore}");
        await DragVolumeAsync(effect, .67f);
        float effectValue = (float)effect.Value / 100;
        Check(Math.Abs(effectValue - .67f) < .03 && GF.Sound.GetSoundGroup("SFX").Volume == effectValue &&
            GF.Sound.GetSoundGroup("UI").Volume == effectValue && GF.Sound.GetSoundGroup("Music").Volume == musicValue &&
            GF.Setting.GetFloat("SFX") == effectValue && GF.Setting.GetFloat("UI") == effectValue,
            "音效滑条未同步SFX/UI或影响音乐");
        using (var saved = new ConfigFile())
        {
            Check(saved.Load("user://Settings.cfg") == Error.Ok &&
                Math.Abs(saved.GetValue("Settings", "Music").AsDouble() - musicValue) < .001 &&
                Math.Abs(saved.GetValue("Settings", "SFX").AsDouble() - effectValue) < .001 &&
                Math.Abs(saved.GetValue("Settings", "UI").AsDouble() - effectValue) < .001,
                "GGF音量只改了内存，没有写入设置文件");
        }
        await ClickAsync(settings, "Scroll/Content/Options/m_AudioPanel/m_Sound");
        Check(music.Value == 0 && effect.Value == 0, "静音后滑条没有同步");
        await ClickAsync(settings, "Scroll/Content/Options/m_AudioPanel/m_Sound");
        Check(GF.Sound.GetSoundGroup("Music").Volume == musicValue && GF.Sound.GetSoundGroup("SFX").Volume == effectValue,
            "静音开关没有恢复用户拖动后的音量");
        await DragVolumeAsync(music, 0);
        Check(GF.Sound.GetSoundGroup("Music").Volume == 0 && GF.Sound.GetSoundGroup("SFX").Volume == effectValue,
            "音乐零点错误关闭全部声音");
        await DragVolumeAsync(music, 1);
        Check(GF.Sound.GetSoundGroup("Music").Volume == 1, "音乐满量程无法到达100%");
        GD.Print("SMOKE-SETTINGS VOLUME PASS: map entry, native dragging, independent music, SFX/UI, mute restore and persisted values");
    }

    /// <summary>
    /// 通过原生鼠标事件拖动滑条，按抓手宽度换算有效轨道位置。
    /// </summary>
    /// <param name="slider">可见原生滑条。</param>
    /// <param name="ratio">目标百分比比例。</param>
    /// <returns>真实输入完成的任务。</returns>
    private async Task DragVolumeAsync(HSlider slider, float ratio)
    {
        Rect2 rect = slider.GetGlobalRect();
        Vector2 start = rect.Position + new Vector2(5 + (rect.Size.X - 10) * (float)(slider.Value / 100), rect.Size.Y / 2);
        Vector2 end = rect.Position + new Vector2(5 + (rect.Size.X - 10) * ratio, rect.Size.Y / 2);
        await MoveMouseAsync(start);
        _driver.GetViewport().PushInput(new InputEventMouseButton { Position = start, GlobalPosition = start, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        await MoveMouseAsync(end);
        _driver.GetViewport().PushInput(new InputEventMouseButton { Position = end, GlobalPosition = end, ButtonIndex = MouseButton.Left, Pressed = false }, true);
        await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>
    /// 其他战斗回归通过真实菜单进入关卡，生产流程不读取烟测参数。
    /// </summary>
    /// <param name="levelId">实际点击的地图地点，默认花果山。</param>
    /// <returns>已进入关卡流程时返回 true，否则继续等待菜单资源加载。</returns>
    public static bool AdvanceToLevel(int levelId = 1)
    {
        if (GF.Procedure.CurrentProcedure is ProcedureLevel)
        {
            return true;
        }

        if (GF.UI.GetUIForm(ResourcesCollectionConstant.UI_LoadingForm) != null)
        {
            return false;
        }
        // GGF 的全组枚举顺序不保证最后一项就是当前模态窗，明确选择可见的存档页优先。
        Control[] forms = GF.UI.GetAllLoadedUIForms().OfType<Control>().Where(form => form.Visible).ToArray();
        Control form = forms.FirstOrDefault(form => form is SaveForm)
            ?? forms.FirstOrDefault(form => form is CharacterForm or MapForm or StartForm);
        bool coop = OS.GetCmdlineUserArgs().Contains("--coop-only");
        string button = form switch
        {
            StartForm start => start.GetNode<Control>("m_MainMenu").Visible
                ? "m_MainMenu/m_NewGame" : coop ? "m_PlayerCountMenu/m_Double" : "m_PlayerCountMenu/m_Single",
            SaveForm save when GF.Fsm.GetModel<SaveModel>().Phase == SaveListState.Ready => "Slots/m_Slot1",
            CharacterForm character when SmokeInspection.CallbackCount(character, "CharacterConfirmed") != 0 => coop
                ? (SmokeInspection.ReadField<List<int>>(character, "_chosenHeroIds").Count == 0 ? "Roles/m_Wukong" : "Roles/m_Sanzang")
                : OS.GetCmdlineUserArgs().Contains("--tangseng-only") ? "Roles/m_Sanzang" : "Roles/m_Wukong",
            MapForm => levelId switch { 2 => "m_Shuiliandong", 3 => "m_Taohuayuan", 4 => "m_Bianfuku", 5 => "m_Qixingdong", 6 => "m_Jialuoluo", 7 => "m_Qianan", 8 => "m_Donghai", 9 => "m_Shazhiwoliu", 10 => "m_Niumogu", _ => "m_Huaguoshan" },
            _ => null,
        };
        if (form?.Visible == true && button != null)
        {
            // 专项第二/三关夹具准备已通关前置关卡，不修改正式存档或生产导航规则。
            if (form is MapForm && levelId > 1)
            {
                for (int id = GF.Fsm.GetModel<ProfileModel>().HighestClearedLevelId.Value + 1; id < levelId; id++)
                {
                    GF.Fsm.GetSystem<ProfileSystem>().RecordLevelClear(id);
                }
            }

            Press(form, button);
        }
        return false;
    }

    /// <summary>
    /// 验证三个界面、返回链路、隔离档案和真实战斗开关。
    /// </summary>
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
            await ClickAsync(start, "m_MainMenu/m_Help");
            ScrollContainer helpScroll = start.GetNode<ScrollContainer>("StatusScroll");
            Label helpText = helpScroll.GetNode<Label>("m_Status");
            await CaptureAsync("start_help");
            Check(helpText.Text == "P1：A/D 移动 · J 攻击 · K 跳跃 · 空格 无双\nP2：←/→ 移动 · 小键盘1 攻击 · 2 跳跃 · 0 无双\n左下角设置中返回地图" &&
                helpText.GetThemeFontSize("font_size") == 19 && helpText.AutowrapMode == TextServer.AutowrapMode.WordSmart &&
                helpScroll.GetGlobalRect().End.X <= 940 && helpScroll.GetVScrollBar().Visible,
                "标题帮助原文、字号、自动换行或滚动区域错误");
            await MoveMouseAsync(helpScroll.GetGlobalRect().GetCenter());
            for (int wheel = 0; wheel < 12; wheel++)
            {
                Vector2 position = helpScroll.GetGlobalRect().GetCenter();
                _driver.GetViewport().PushInput(new InputEventMouseButton
                {
                    Position = position,
                    GlobalPosition = position,
                    ButtonIndex = MouseButton.WheelDown,
                    Pressed = true
                }, true);
                _driver.GetViewport().PushInput(new InputEventMouseButton
                {
                    Position = position,
                    GlobalPosition = position,
                    ButtonIndex = MouseButton.WheelDown,
                    Pressed = false
                }, true);
                await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            Check(helpScroll.ScrollVertical > 0 &&
                helpScroll.GetVScrollBar().Value + helpScroll.GetVScrollBar().Page >= helpScroll.GetVScrollBar().MaxValue - 1,
                "标题帮助无法用鼠标滚动到末尾");
            await CaptureAsync("start_help_bottom");
            await ClickAsync(start, "m_MainMenu/m_Notice");
            await CaptureAsync("start_notice");
            await CheckQuestionTipsAsync();
            await CheckMusicAsync("map_theme.mp3");
            await ClickAsync(start, "m_MainMenu/m_Settings");
            SettingsForm titleSettings = await WaitForAsync<SettingsForm>();
            Check(!GF.Base.IsGamePaused, "标题设置不应暂停关卡时钟");
            Check(!titleSettings.GetNode<BaseButton>("Scroll/Content/Options/Navigation/m_ReturnMap").Visible, "标题设置错误显示返回地图");
            await CaptureAsync("title_settings");
            // 官方面板的一项开关同时控制三组声音，关闭后设置持久化。
            if (GF.Sound.GetSoundGroup("Music").Volume == 0 && GF.Sound.GetSoundGroup("SFX").Volume == 0 && GF.Sound.GetSoundGroup("UI").Volume == 0)
            {
                await ClickAsync(titleSettings, "Scroll/Content/Options/m_AudioPanel/m_Sound");
            }

            await ClickAsync(titleSettings, "Scroll/Content/Options/m_AudioPanel/m_Sound");
            Check(new[] { "Music", "SFX", "UI" }.All(group => GF.Sound.GetSoundGroup(group).Volume == 0 && GF.Setting.GetFloat(group) == 0), "统一声音关闭未生效或未持久化");
            await ClickAsync(titleSettings, "Scroll/Content/Options/m_AudioPanel/m_Sound");
            Check(new[] { "Music", "SFX", "UI" }.All(group => GF.Sound.GetSoundGroup(group).Volume > 0), "统一声音恢复未生效");
            TextureButton close = titleSettings.GetNode<TextureButton>("Scroll/m_Close");
            Check(close.Size.IsEqualApprox(new Vector2(41, 41)) && close.TextureHover.ResourcePath.EndsWith("menu_close_hover.png"), "关闭按钮未使用原生尺寸及悬停图");
            await MoveMouseAsync(close.GetGlobalRect().GetCenter());
            await CaptureAsync("settings_close_hover");
            await ClickAsync(titleSettings, "Scroll/Content/Options/Navigation/m_Help");
            Check(titleSettings.GetNode<Label>("Scroll/Content/m_HelpText").Visible, "设置帮助未显示");
            await ClickAsync(titleSettings, "Scroll/Content/Options/m_Continue");
            await ClickAsync(titleSettings, "Scroll/m_Close");
            Check(!GF.UI.HasUIForm(titleSettings.SerialId), "标题设置关闭后仍接收交互");
            await ClickAsync(start, "m_MainMenu/m_LoadGame");

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
            {
                await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            Check(!GF.UI.HasUIForm(saves.SerialId) &&
                            SmokeInspection.CountBindingHandlers(GF.Fsm.GetModel<SaveModel>().Directory, saves) == 0,
                            "存档页关闭后仍订阅目录变化");
            Check(GF.Procedure.CurrentProcedure is ProcedureTitle && !Find<CharacterForm>().Any(x => x.Visible),
                "关闭存档页的迟到按钮信号仍触发导航");
            await ClickAsync(start, "m_MainMenu/m_LoadGame");
            Check(ReferenceEquals(saves, await WaitForSaveAsync()) &&
                SmokeInspection.CountBindingHandlers(GF.Fsm.GetModel<SaveModel>().Directory, saves) == 1 &&
                SmokeInspection.CallbackCount(saves, "SaveSlotSelected") == 1,
                "存档页未复用 GGF 池实例或重复订阅目录");
            if (OS.GetCmdlineUserArgs().Contains("--save-delete-only"))
            {
                await CheckSaveDeletionAsync(saves);
                return;
            }
            saves = await CheckCoopMenusAsync(saves);
            await ClickAsync(saves, "m_Close");
            start = await WaitForAsync<StartForm>();
            await ClickAsync(start, "m_MainMenu/m_NewGame");
            await ClickAsync(start, "m_PlayerCountMenu/m_Single");
            CharacterForm character = await WaitForAsync<CharacterForm>();
            ProcedureOwner flow = SmokeInspection.ReadField<GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>>(
                GF.Procedure.CurrentProcedure, "_owner");
            CheckTypedFsmData(flow);
            await CheckCancelledLoadAsync(flow);
            SaveSystem firstSaveSystem = flow.GetSystem<SaveSystem>();
            Check(flow.GetModel<ProfileModel>() == null && flow.GetSystem<EquipmentSystem>() == null,
                "选角前已按默认英雄创建档案作用域");
            await CheckCharacterHoverAsync(character);
            await ClickAsync(character, "Roles/m_Wukong");
            saves = await WaitForSaveAsync();
            Check(character.GetNode<Label>("m_PlayerOne").Visible, "保存选格前丢失已确认席位");
            await ClickAsync(saves, "Slots/m_Slot2");
            Check(!character.GetNode<Label>("m_PlayerOne").Visible, "选角关闭后仍保留玩家标记");

            MapForm map = await WaitForAsync<MapForm>();
            ProfileModel firstPlayerModel = flow.GetModel<ProfileModel>();
            EquipmentSystem firstEquipmentSystem = flow.GetSystem<EquipmentSystem>();
            Check(ReferenceEquals(flow.GetArchitecture(), ((IBelongToArchitecture)firstSaveSystem).GetArchitecture()) &&
                ReferenceEquals(((IBelongToArchitecture)firstSaveSystem).GetArchitecture(), ((IBelongToArchitecture)firstEquipmentSystem).GetArchitecture()) &&
                ReferenceEquals(ConfigSystem.Instance.Tables, firstSaveSystem.GetModel<ConfigurationModel>().Tables) &&
                ReferenceEquals(flow.GetUtility<ExperienceCurveUtility>(), firstSaveSystem.GetUtility<ExperienceCurveUtility>()),
                "共享配置、经验曲线或模块没有使用主流程唯一的数据来源");
            Check(firstPlayerModel.HeroOf(PlayerSlot.P1) == 1 && firstPlayerModel.HeroIds.SequenceEqual(new[] { 1 }) &&
                GF.Archive.CurrentData.Heroes[0].HeroId == 1, "悟空新档没有按首次选角创建身份");
            Check(SmokeInspection.CallbackCount(character, "CharacterConfirmed") == 0 &&
                SmokeInspection.CallbackCount(character, "CharacterCancelled") == 0 &&
                SmokeInspection.CallbackCount(start, "NewGameRequested") == 0 &&
                SmokeInspection.CallbackCount(start, "LoadGameRequested") == 0,
                "离开选角或标题后仍保留导航回调");
            Check(ReferenceEquals(firstPlayerModel, flow.GetModel<ProfileModel>()) &&
                ReferenceEquals(firstSaveSystem, flow.GetSystem<SaveSystem>()) && ReferenceEquals(firstEquipmentSystem, flow.GetSystem<EquipmentSystem>()),
                "选角到地图更换了档案作用域");
            Check(!Find<AbstractHeroEntity>().Any() && !Find<LevelViewController>().Any(), "点击花果山之前已创建关卡");
            await CaptureAsync("map_form");
            if (OS.GetCmdlineUserArgs().Contains("--map-buttons-only"))
            {
                await MoveMouseAsync(new Vector2(900, 560));
                TextureButton settingsToken = map.GetNode<TextureButton>("m_Settings");
                AnimationPlayer animation = settingsToken.GetNode<AnimationPlayer>("m_SettingsAnimation");
                Button skillButton = map.GetNode<Button>("m_Skills");
                TextureButton saveButton = map.GetNode<TextureButton>("m_Save");
                AnimatedSprite2D skillArtwork = skillButton.GetNode<AnimatedSprite2D>("Artwork");
                Label settingsCaption = settingsToken.GetNode<Label>("Caption");
                Check(!map.HasNode("m_Back"), "地图仍保留独立返回主菜单");
                await _driver.ToSignal(_driver.GetTree().CreateTimer(.9), SceneTreeTimer.SignalName.Timeout);
                Check(skillArtwork.Animation == "idle" && skillArtwork.Frame == 0 && !skillArtwork.IsPlaying() &&
                    skillArtwork.SpriteFrames.GetFrameCount("idle") == 1 &&
                    skillArtwork.SpriteFrames.GetAnimationLoopMode("hover") == SpriteFrames.LoopMode.None,
                    "官方技能普通态没有静止或展开仍被设置为循环");
                Check(settingsCaption.GetThemeFont("font").ResourcePath.EndsWith("dfp_hai_bao_w12.ttc") &&
                    settingsCaption.GetThemeFontSize("font_size") == 20 && settingsCaption.GetThemeConstant("shadow_outline_size") > 0,
                    "设置标题没有使用海报字形、匹配字号和黑白描边");
                Check(saveButton.TextureHover.ResourcePath.EndsWith("map_save_game_hover.png") &&
                    saveButton.TextureHover != saveButton.TextureNormal, "保存按钮仍然缺少官方悬停态");
                await MoveMouseAsync(saveButton.GetGlobalRect().GetCenter());
                Check(saveButton.GetDrawMode() == BaseButton.DrawMode.Hover, "保存按钮没有响应真实悬停");
                await CaptureAsync("map_save_official_hover");
                await MoveMouseAsync(new Vector2(900, 560));
                Check(settingsToken.TextureNormal.ResourcePath.EndsWith("map_return_title.png") &&
                    settingsToken.GetNode<Label>("Caption").Text == "游戏设置", "设置入口没有复用原版令牌");
                await CaptureAsync("map_settings_token_idle");
                await MoveMouseAsync(settingsToken.GetGlobalRect().GetCenter());
                await WaitUntilAsync(() => settingsToken.SelfModulate.R > 1.05f, "设置令牌悬停未提亮");
                Check(settingsToken.Scale == Vector2.One &&
                    animation.GetAnimation("hover").LoopMode == Animation.LoopModeEnum.None,
                    "设置令牌仍反复缩放");
                await CaptureAsync("map_settings_token_hover");
                await MoveMouseAsync(new Vector2(900, 560));
                Check(settingsToken.Scale == Vector2.One && Math.Abs(settingsToken.SelfModulate.R - .9f) < .001,
                    "设置令牌移出后未恢复常态亮度");
                await ClickAsync(map, "m_Settings");
                SettingsForm settingsForm = await WaitForAsync<SettingsForm>();
                await ClickAsync(settingsForm, "Scroll/m_Close");
                await CheckMapSkillPurchaseAsync(map, flow);
                // 制造尚未手动保存的地图进度，不能用刚保存过的空变化证明返回会写盘。
                flow.GetSystem<ProfileSystem>().AddSoul(GF.Fsm.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1), 17);
                flow.GetSystem<ProfileSystem>().AddExperience(GF.Fsm.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1), 3);
                await ClickAsync(map, "m_Settings");
                settingsForm = await WaitForAsync<SettingsForm>();
                SaveModel saveModel = flow.GetModel<SaveModel>();
                long originalUnit = saveModel.ActiveSlotUnitId;
                try
                {
                    // 仅使当前隔离档案的外键校验失败，不改文件或框架；失败后必须留在原地图。
                    saveModel.ActiveSlotUnitId = originalUnit + 1;
                    await ClickAsync(settingsForm, "Scroll/Content/Options/Navigation/m_ReturnTitle");
                    await WaitUntilAsync(() => saveModel.ManualSave.Value.Phase == SaveWritePhase.Failed,
                        "返回保存失败没有发布失败状态");
                    Check(GF.Procedure.CurrentProcedure is ProcedureMap &&
                        ReferenceEquals(firstPlayerModel, flow.GetModel<ProfileModel>()) && firstPlayerModel.Initialized,
                        "存档失败仍返回标题或销毁了当前档案");
                }
                finally
                {
                    saveModel.ActiveSlotUnitId = originalUnit;
                }
                await ClickAsync(settingsForm, "Scroll/m_Close");
                await ReturnMapToTitleAsync(map);
                await WaitForAsync<StartForm>();
                Check(flow.GetModel<ProfileModel>() == null && SmokeInspection.CallbackCount(settingsForm, "TitleRequested") == 0,
                    "设置返回主菜单没有关闭导航订阅和档案作用域");
                await ClickAsync(await WaitForAsync<StartForm>(), "m_MainMenu/m_LoadGame");
                SaveForm savedForms = await WaitForSaveAsync();
                await ClickAsync(savedForms, "Slots/m_Slot2");
                MapForm reusedMap = await WaitForAsync<MapForm>();
                Check(ReferenceEquals(map, reusedMap), "设置返回后没有复用地图窗口");
                await ClickAsync(reusedMap, "m_Settings");
                settingsForm = await WaitForAsync<SettingsForm>();
                Check(SmokeInspection.CallbackCount(settingsForm, "TitleRequested") == 1,
                    "地图设置复用时重复或漏绑导航");
                await CaptureAsync("map_settings_navigation");
                await ClickAsync(settingsForm, "Scroll/m_Close");
                await MoveMouseAsync(new Vector2(900, 560));
                await CaptureAsync("map_buttons_aligned");
                GD.Print("SMOKE-MAP-BUTTONS PASS: official button states, save-on-return, failed-save retention, disk snapshot, reload and reuse.");
                return;
            }
            if (OS.GetCmdlineUserArgs().Contains("--level-guide-only"))
            {
                await CheckLevelGuideAsync(map);
                return;
            }
            await ClickAsync(map, "m_Settings");
            SettingsForm mapSettings = await WaitForAsync<SettingsForm>();
            Check(!GF.Base.IsGamePaused && ReferenceEquals(mapSettings, titleSettings), "地图设置错误暂停或未复用窗口");
            Check(!mapSettings.GetNode<Control>("Scroll/Content/Options/Navigation/m_ReturnMap").Visible, "地图设置多出返回地图");
            await CheckVolumeSlidersAsync(mapSettings);
            await _driver.ToSignal(_driver.GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);
            await CaptureAsync("map_settings_volume");
            await ClickAsync(mapSettings, "Scroll/m_Close");
            await ClickAsync(map, "m_Settings");
            mapSettings = await WaitForAsync<SettingsForm>();
            Check(Math.Abs(mapSettings.GetNode<HSlider>("Scroll/Content/Options/m_AudioPanel/Music/m_MusicVolume").Value / 100 - GF.Sound.GetSoundGroup("Music").Volume) < .001,
                "地图重新打开设置没有恢复音量");
            await ClickAsync(mapSettings, "Scroll/m_Close");

            if (OS.GetCmdlineUserArgs().Contains("--map-coast-only"))
            {
                await CheckCoastalMapAsync(map);
                return;
            }
            await CheckMapSkillPurchaseAsync(map, flow);
            TextureButton location = map.GetNode<TextureButton>("m_Huaguoshan");
            Check(location.TextureNormal.ResourcePath.EndsWith("map_huaguoshan_normal.png", StringComparison.Ordinal), "花果山未使用原项目地点图片");
            Check(location is MapLocationButton, "花果山未使用随显示图像变化的命中控件");
            await MoveMouseAsync(new Vector2(30, 30));
            AnimationPlayer currentHint = map.GetNode<AnimationPlayer>("m_LocationAnimation");
            Check(currentHint.IsPlaying() && currentHint.CurrentAnimation == "location/current", "最新关卡没有默认闪烁提示");
            currentHint.Seek(0, true);
            currentHint.Pause();
            Sprite2D hintSprite = location.GetNode<Sprite2D>("CurrentHint");
            int currentLow = hintSprite.Frame;
            await CaptureAsync("map_current_low");
            currentHint.Play("location/current");
            currentHint.Seek(.5, true);
            currentHint.Pause();
            Check(hintSprite.Visible && hintSprite.Frame != currentLow && !location.GetNode<TextureRect>("LocationGlow").Visible,
                "最新关卡未使用原版图序列，或仍叠加悬停轮廓");
            Check(hintSprite.Scale == Vector2.One, "默认提示错误放大了地点");
            await CaptureAsync("map_current");
            currentHint.Play("location/current");
            await CheckLocationHitRegionAsync(map, location);
            await MoveMouseAsync(location.GetGlobalRect().GetCenter());
            AnimationPlayer hover = map.GetNode<AnimationPlayer>("m_LocationAnimation");
            Check(Mathf.IsEqualApprox(hover.SpeedScale, 1f) && hover.IsPlaying() && hover.CurrentAnimation == "location/hover", "地点悬停未以正常速度播放放大与黄光动画");
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
                using var low = Image.LoadFromFile("res://.godot/validation/frontend/map_hover_low.png");
                using var high = Image.LoadFromFile("res://.godot/validation/frontend/map_hover.png");
                int changed = 0;
                Rect2 region = location.GetGlobalRect();
                for (int y = (int)region.Position.Y; y < (int)region.End.Y; y++)
                {
                    for (int x = (int)region.Position.X; x < (int)region.End.X; x++)
                    {
                        if (low.GetPixel(x, y) != high.GetPixel(x, y))
                        {
                            changed++;
                        }
                    }
                }

                Check(changed > 100, "黄色光晕两次采样没有产生实际像素变化");
            }
            await MoveMouseAsync(new Vector2(30, 30));
            Check(hover.IsPlaying() && hover.CurrentAnimation == "location/current" && hintSprite.Visible &&
                !location.GetNode<TextureRect>("LocationGlow").Visible, "离开地点后动画没有复位");
            await CheckMusicAsync("map_theme.mp3");

            // 保存图标写入第二格；第六格建档后再读第二格，不能误读最新档。
            flow.GetSystem<ProfileSystem>().AddSoul(GF.Fsm.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1), 7);
            int savedSoul = flow.GetModel<ProfileModel>().GetSoul(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value;
            await ClickAsync(map, "m_Save");
            await WaitUntilAsync(() => !map.GetNode<BaseButton>("m_Save").Disabled, "手动保存反馈超时");
            Check(await firstSaveSystem.PendingWrite && GF.Archive.CurrentCatalogue.SlotNumber == 2, "地图手动保存写错格");
            await CheckSaveFeedbackAsync(map);
            long secondId = GF.Archive.CurrentCatalogue.UnitId;
            int savedMapSerialId = map.SerialId;
            await ReturnMapToTitleAsync(map);
            start = await WaitForAsync<StartForm>();
            Check(Find<SaveNoticeForm>().Any(x => x.SerialId != 0), "切换页面关闭了应用级保存提示");
            Check(flow.GetModel<ProfileModel>() == null && ReferenceEquals(firstSaveSystem, flow.GetSystem<SaveSystem>()) && flow.GetSystem<EquipmentSystem>() == null, "返回标题仍持有旧档案作用域");
            await ClickAsync(start, "m_MainMenu/m_LoadGame");
            saves = await WaitForSaveAsync();
            IReadOnlyList<SaveSlotInfo> slots = GF.Fsm.GetModel<SaveModel>().Slots;
            Check(slots[1].State == SaveSlotState.Occupied && slots[1].UnitId == secondId && slots[0].State == SaveSlotState.Empty, "存档摘要或框架空槽位置不正确");
            await MoveMouseAsync(saves.GetNode<BaseButton>("Slots/m_Slot2").GetGlobalRect().GetCenter());
            await CaptureAsync("save_slots_occupied");
            Check(saves.GetNode<BaseButton>("Slots/m_Slot6").Disabled, "读取模式空格仍可点");
            await ClickAsync(saves, "m_Close");
            await ClickAsync(start, "m_MainMenu/m_NewGame");
            await ClickAsync(start, "m_PlayerCountMenu/m_Single");
            character = await WaitForAsync<CharacterForm>();
            await CheckCharacterHoverAsync(character);
            Check(flow.GetModel<ProfileModel>() == null, "第六格选角前已装配默认英雄");
            await ClickAsync(character, "Roles/m_Sanzang");
            saves = await WaitForSaveAsync();
            await ClickAsync(saves, "Slots/m_Slot6");
            MapForm tangsengMap = await WaitForAsync<MapForm>();
            Check(GF.Archive.CurrentCatalogue.SlotNumber == 6 && GF.Archive.CurrentCatalogue.UnitId != secondId, "第六格覆盖了第二格");
            long sixthId = GF.Archive.CurrentCatalogue.UnitId;
            ProfileModel tangsengProfile = flow.GetModel<ProfileModel>();
            Check(tangsengProfile.HeroOf(PlayerSlot.P1) == 2 && tangsengProfile.HeroIds.SequenceEqual(new[] { 2 }) &&
                GF.Archive.CurrentData.Heroes[0].HeroId == 2 && GF.Archive.CurrentData.Heroes.Single().HeroId == 2 &&
                flow.GetModel<SkillModel>().GetLearnedSkills(2).Count == 0,
                "唐僧首次快照、档案或技能书仍使用默认悟空身份");
            GD.Print("SMOKE-TANGSENG-CREATE PASS: selected hero owns the first snapshot, profile and skill book; Wukong slot remains separate.");
            Check(!await firstSaveSystem.SaveCheckpointAsync(firstPlayerModel), "旧档案检查点写入了另一格");
            await ReturnMapToTitleAsync(tangsengMap);
            start = await WaitForAsync<StartForm>();
            await ClickAsync(start, "m_MainMenu/m_LoadGame");
            saves = await WaitForSaveAsync();
            Check(flow.GetModel<SaveModel>().Slots[5].Heroes.Single().Name == "唐僧", "唐僧档案磁盘重读摘要仍显示其他角色");
            await ClickAsync(saves, "Slots/m_Slot2");
            map = await WaitForAsync<MapForm>();
            Check(!character.Visible && GF.Procedure.CurrentProcedure is ProcedureMap, "已有档案未直接进入地图");
            ProfileModel reloadedPlayerModel = flow.GetModel<ProfileModel>();
            Check(!ReferenceEquals(firstPlayerModel, reloadedPlayerModel) &&
                ReferenceEquals(firstSaveSystem, flow.GetSystem<SaveSystem>()) &&
                !ReferenceEquals(firstEquipmentSystem, flow.GetSystem<EquipmentSystem>()), "重新读档复用了已结束的作用域");
            Check(flow.GetModel<ProfileModel>().GetSoul(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value == savedSoul, "重新读档没有从快照重建运行时状态");
            Check(flow.GetSystem<SkillSystem>().IsLearned(GF.Fsm.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1), SkillId.RisingDragon) &&
                flow.GetModel<SkillModel>().GetPassiveLevels(1)[PassiveId.Vitality] == 1,
                "地图购买的主动技能或被动等级没有从磁盘恢复");
            GF.Fsm.GetModel<SaveModel>().ManualSave.Value = new(savedMapSerialId, SaveWritePhase.Succeeded);
            await MoveMouseAsync(new Vector2(30, 30));
            Check(string.IsNullOrEmpty(map.GetNode<BaseButton>("m_Save").TooltipText), "保存按钮仍有多余悬停提示");
            Check(GF.Archive.CurrentCatalogue.UnitId == secondId && GF.Archive.CurrentData.Heroes[0].Soul == savedSoul, "选择第二格读取成最新格或进度丢失");
            // 从普通树身进入悬停，再在仅放大图可见的位置实际点击进关。
            location = map.GetNode<TextureButton>("m_Huaguoshan");
            await MoveMouseAsync(location.GetGlobalRect().GetCenter());
            Vector2 expandedClick = location.GetGlobalTransformWithCanvas() *
                (location.GetNode<TextureRect>("LocationGlow").GetTransform() * new Vector2(40, 87));
            await ClickAsync(map, "m_Huaguoshan", expandedClick);
            BattleForm battleForm = await WaitForAsync<BattleForm>();
            AbstractHeroEntity hero = Find<AbstractHeroEntity>().Single(x => x.IsShown);
            Check(Find<LevelViewController>().Count() == 1, "点击花果山未创建唯一关卡");
            LevelViewController level = SmokeInspection.Level((ProcedureLevel)GF.Procedure.CurrentProcedure);
            LevelModel levelModel = flow.GetModel<LevelModel>();
            HeroModel heroModel = flow.GetModel<HeroModel>();
            HeroSystem heroSystem = flow.GetSystem<HeroSystem>();
            var heroCallbacks = SmokeInspection.ReadField<Dictionary<int, (Action<HeroProgressionState> Progress,
                Action<EquippedItems> Slots, Action<IReadOnlyDictionary<PassiveId, int>> Passives)>>(heroSystem,
                "_subscriptions")[hero.HeroId];
            Check(heroModel.Initialized && ReferenceEquals(heroModel.GetState(hero.HeroId), SmokeInspection.HeroAttributes(hero)),
                "英雄 Entity 没有借用 Architecture 注册的同一运行属性");
            Check(flow.GetSystem<SkillSystem>().IsAvailable(GF.Fsm.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1), SkillId.RisingDragon) &&
                heroModel.GetState(hero.HeroId).MaxHp.Value > ConfigSystem.Instance.Tables.TbHeroGrowthConfig.Get(1).Levels[0].BaseAttributes.MaxHp,
                "地图学习的主动技能或被动属性未在进关后生效");
            Check(SmokeInspection.CountBindingHandlers(flow.GetModel<ProfileModel>().GetProgress(hero.HeroId), heroCallbacks.Progress) == 1 &&
                SmokeInspection.CountBindingHandlers(flow.GetModel<ProfileModel>().GetSlots(hero.HeroId), heroCallbacks.Slots) == 1 &&
                SmokeInspection.CountBindingHandlers(flow.GetModel<SkillModel>().GetPassiveLevels(hero.HeroId), heroCallbacks.Passives) == 1,
                "英雄属性系统没有唯一订阅成长和穿戴来源");
            LevelSystem levelSystem = flow.GetSystem<LevelSystem>();
            Check(ReferenceEquals(flow.GetArchitecture(), ((IBelongToArchitecture)level).GetArchitecture()),
                "关卡没有借用流程持有的档案作用域");
            // 此处从已有规则入口准备HUD状态；真实攻击到命中事件的计数由英雄烟测覆盖。
            await WaitUntilAsync(() => levelModel.SessionActive && levelModel.Outcome.Value == LevelOutcome.Running,
                "关卡会话尚未进入运行态，无法验证连击HUD");
            levelSystem.RecordComboHit();
            levelSystem.RecordComboHit();
            await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.ProcessFrame);
            ComboHud combo = battleForm.GetNode<ComboHud>("m_Combo");
            Check(levelModel.ComboCount.Value == 2 && combo.Visible && combo.GetNode<AnimatedSprite2D>("Digit0").Animation == "2" &&
                combo.Position.IsEqualApprox(new Vector2(694.95f, 234.95f)),
                "连击规则没有同步到战斗HUD");
            await WaitUntilAsync(() => combo.GetNode<AnimatedSprite2D>("Digit0").Frame == 4,
                "官方连击数字没有播放到清晰末帧");
            await CaptureAsync("frontend_combo");
            for (int hit = 2; hit < 12; hit++)
            {
                levelSystem.RecordComboHit();
            }
            await WaitUntilAsync(() => combo.GetNode<AnimatedSprite2D>("Digit1").Frame == 4,
                "两位连击数字没有完成原版五帧入场");
            Check(combo.GetNode<AnimatedSprite2D>("Digit0").Animation == "1" &&
                combo.GetNode<AnimatedSprite2D>("Digit0").Position == new Vector2(-44, 28) &&
                combo.GetNode<AnimatedSprite2D>("Digit1").Animation == "2" &&
                combo.GetNode<AnimatedSprite2D>("Digit1").Position == new Vector2(6, 28),
                "两位连击未保持官方数字身份、起点和间距");
            await CaptureAsync("frontend_combo_two_digits");
            for (int hit = 12; hit < 123; hit++)
            {
                levelSystem.RecordComboHit();
            }
            await WaitUntilAsync(() => combo.GetNode<AnimatedSprite2D>("Digit2").Frame == 4,
                "三位连击数字没有完成原版五帧入场");
            Check(combo.GetNode<AnimatedSprite2D>("Digit0").Position == new Vector2(-95, 28) &&
                combo.GetNode<AnimatedSprite2D>("Digit2").Animation == "3" &&
                combo.GetNode<AnimatedSprite2D>("Digit2").Position == new Vector2(5, 28),
                "三位连击未保持官方布局");
            await CaptureAsync("frontend_combo_three_digits");
            await WaitUntilAsync(() => levelModel.ComboCount.Value == 0, "多位连击没有按原规则断连");
            levelSystem.RecordComboHit();
            levelSystem.RecordComboHit();
            Check(!combo.GetNode<AnimatedSprite2D>("Digit1").Visible &&
                !combo.GetNode<AnimatedSprite2D>("Digit2").Visible, "新连击遗留了上一组高位数字");
            Check(!GF.Debugger.ActiveWindow, "框架调试按钮遮挡头像");
            await CheckMusicAsync("huaguoshan_theme.mp3");
            await CaptureAsync("frontend_battle");
            BattleFormData data = SmokeInspection.ReadField<BattleFormData>(battleForm, "_formData");
            ProfileModel playerModel = SmokeInspection.Flow((ProcedureLevel)GF.Procedure.CurrentProcedure).GetModel<ProfileModel>();
            Check(ReferenceEquals(playerModel, reloadedPlayerModel) &&
                data.Heroes.Count == 1 && ReferenceEquals(data.Heroes[0], hero) && ReferenceEquals(data.ViewController, level), "HUD 与关卡没有借用同一份运行状态");
            int experience = playerModel.GetProgress(playerModel.HeroOf(PlayerSlot.P1)).Value.TotalExperience;
            flow.GetSystem<ProfileSystem>().AddExperience(GF.Fsm.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1), 1);
            await ClickAsync(battleForm, "MenuPanel/m_SettingsButton");
            SettingsForm settings = await WaitForAsync<SettingsForm>();
            Check(GF.Base.IsGamePaused, "战斗设置未暂停");
            int pausedComboCount = levelModel.ComboCount.Value;
            double pausedComboElapsed = SmokeInspection.ReadField<double>(battleForm, "_comboElapsed");
            Check(ReferenceEquals(settings, titleSettings), "设置未复用已经关闭的 GGF 池实例");
            Check(SmokeInspection.CallbackCount(settings, "MapRequested") == 1 &&
                SmokeInspection.CallbackCount(settings, "TitleRequested") == 1,
                "复用设置未绑定当前战斗导航或重复绑定");
            Check(settings.ZIndex > battleForm.ZIndex, "复用设置的显示层级低于 HUD");
            Control scroll = settings.GetNode<Control>("Scroll");
            HSlider musicSlider = settings.GetNode<HSlider>("Scroll/Content/Options/m_AudioPanel/Music/m_MusicVolume");
            HSlider effectSlider = settings.GetNode<HSlider>("Scroll/Content/Options/m_AudioPanel/Effect/m_EffectVolume");
            Label caption = settings.GetNode<Label>("Scroll/Content/Options/m_AudioPanel/Music/Caption");
            Button menuItem = settings.GetNode<Button>("Scroll/Content/Options/m_Continue");
            Check(scroll.Size.X == 326 && musicSlider.Size.X == 160 && effectSlider.Size.X == 160 &&
                Math.Abs(musicSlider.GetGlobalRect().GetCenter().X - menuItem.GetGlobalRect().GetCenter().X) < .1 &&
                Math.Abs(effectSlider.GetGlobalRect().GetCenter().X - musicSlider.GetGlobalRect().GetCenter().X) < .1 &&
                caption.GetThemeFontSize("font_size") == menuItem.GetThemeFontSize("font_size") &&
                caption.GetThemeColor("font_color") == Colors.White && menuItem.GetThemeColor("font_color") == Colors.White,
                $"设置布局：panel={scroll.Size}, music={musicSlider.Size}, effect={effectSlider.Size}, centers={musicSlider.GetGlobalRect().GetCenter().X}/{menuItem.GetGlobalRect().GetCenter().X}, fonts={caption.GetThemeFontSize("font_size")}/{menuItem.GetThemeFontSize("font_size")}, colors={caption.GetThemeColor("font_color")}/{menuItem.GetThemeColor("font_color")}");
            await CaptureAsync("battle_settings");
            Vector2 pausedPosition = hero.Position;
            Input.ActionPress(GameLogic.Input.InputSystem.MoveRight);
            for (int frame = 0; frame < 8; frame++)
            {
                await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            Input.ActionRelease(GameLogic.Input.InputSystem.MoveRight);
            Check(hero.Position.IsEqualApprox(pausedPosition), "设置暂停时英雄仍在移动");
            // 暂停后的同帧旧delta也不能推进规则或HUD渐隐。
            level._Process(2);
            battleForm.OnUpdate(2, 2);
            Check(pausedComboCount == 2 && levelModel.ComboCount.Value == pausedComboCount &&
                SmokeInspection.ReadField<double>(battleForm, "_comboElapsed") == pausedComboElapsed && combo.Visible,
                "设置暂停期间连击计数或HUD渐隐继续推进");
            await ClickAsync(settings, "Scroll/Content/Options/m_Continue");
            Check(!GF.Base.IsGamePaused && battleForm.Visible &&
                SmokeInspection.CountBindingHandlers(levelModel.Outcome, settings) == 0 &&
                SmokeInspection.CallbackCount(settings, "MapRequested") == 0 &&
                SmokeInspection.CallbackCount(settings, "TitleRequested") == 0,
                "继续游戏未恢复战斗或设置仍订阅关卡");
            await WaitUntilAsync(() => levelModel.ComboCount.Value == 0, "恢复后连击没有按超时规则断连清零");
            levelSystem.RecordComboHit();
            Check(levelModel.ComboCount.Value == 1, "断连后的首次命中仍接续旧计数");
            levelSystem.RecordComboHit();
            await ClickAsync(battleForm, "MenuPanel/m_SettingsButton");
            settings = await WaitForAsync<SettingsForm>();
            await ClickAsync(settings, "Scroll/Content/Options/Navigation/m_ReturnMap");
            map = await WaitForAsync<MapForm>();
            Check(!hero.IsShown && !Find<LevelViewController>().Any(), "返回地图未清理英雄与关卡");
            Check(flow.GetModel<ProfileModel>().GetProgress(flow.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)).Value.TotalExperience == experience + 1, "设置返回地图未保存关内经验");
            Check(ReferenceEquals(reloadedPlayerModel, flow.GetModel<ProfileModel>()) &&
                !levelModel.SessionActive && !levelSystem.Initialized && !levelModel.Initialized, "返回地图未保留档案或未解除关卡借用");
            Check(SmokeInspection.ReadField<BattleFormData>(battleForm, "_formData") == null, "HUD 关闭后仍观察旧关卡");
            Check(levelModel.ComboCount.Value == 0 && !combo.Visible &&
                combo.GetChildren().OfType<AnimatedSprite2D>().All(digit => !digit.Visible && !digit.IsPlaying()) &&
                SmokeInspection.CountBindingHandlers(levelModel.ComboCount, battleForm) == 0,
                "离关没有清空连击、HUD数字或旧计数订阅");
            GD.Print("SMOKE-COMBO-LIFECYCLE PASS: HUD sync, paused stale delta, resumed timeout, new chain and exit cleanup.");
            Check(!GF.Base.IsGamePaused, "返回地图后仍处于暂停");
            Check(!heroModel.Initialized && flow.GetModel<HeroModel>() == null &&
                !heroSystem.Initialized && flow.GetSystem<HeroSystem>() == null &&
                SmokeInspection.CountBindingHandlers(playerModel.GetProgress(hero.HeroId), heroCallbacks.Progress) == 0 &&
                SmokeInspection.CountBindingHandlers(flow.GetModel<ProfileModel>().GetSlots(hero.HeroId), heroCallbacks.Slots) == 0 &&
                SmokeInspection.CountBindingHandlers(flow.GetModel<SkillModel>().GetPassiveLevels(hero.HeroId), heroCallbacks.Passives) == 0,
                "离关没有结束英雄运行模型");
            Check(SmokeInspection.CallbackCount(battleForm, "SettingsRequested") == 0 &&
                SmokeInspection.CallbackCount(battleForm, "InventoryRequested") == 0 &&
                SmokeInspection.CallbackCount(battleForm, "SkillsRequested") == 0,
                "离关后 HUD 仍持有流程回调");
            Check(!GF.UI.HasUIForm(settings.SerialId) &&
                SmokeInspection.CountBindingHandlers(levelModel.Outcome, settings) == 0,
                "战斗设置未解除关卡订阅");
            Check(SmokeInspection.CallbackCount(map, "SaveRequested") == 1, "地图保存回调重复绑定");

            // 再入关后从官方第五项返回标题，也必须保存收益并恢复时钟。
            await ClickAsync(map, "m_Huaguoshan");
            battleForm = await WaitForAsync<BattleForm>();
            Check(Find<AbstractHeroEntity>().Count(x => x.IsShown) == 1, "重入生成了重复英雄");
            Check(flow.GetModel<LevelModel>().ComboCount.Value == 0 && !battleForm.GetNode<ComboHud>("m_Combo").Visible,
                "重新进关沿用了旧连击或HUD数字");
            HeroModel reenteredHeroModel = flow.GetModel<HeroModel>();
            Check(reenteredHeroModel.Initialized && !ReferenceEquals(heroModel, reenteredHeroModel),
                "新关卡沿用了已结束的英雄运行模型");
            ProfileModel previousInventory = flow.GetModel<ProfileModel>();
            flow.GetSystem<ProfileSystem>().AddSoul(GF.Fsm.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1), 9);
            GameData battleExitSnapshot = flow.GetUtility<SaveDataUtility>().Capture(previousInventory, flow.GetModel<SkillModel>());
            long battleExitUnit = flow.GetModel<SaveModel>().ActiveSlotUnitId;
            await ClickAsync(battleForm, "MenuPanel/m_SettingsButton");
            settings = await WaitForAsync<SettingsForm>();
            await ClickAsync(settings, "Scroll/Content/Options/Navigation/m_ReturnTitle");
            start = await WaitForAsync<StartForm>();
            await CheckSavedProfileAsync(battleExitSnapshot, battleExitUnit);
            Check(previousInventory.GetSoul(previousInventory.HeroOf(PlayerSlot.P1)).Value == savedSoul + 9 && !previousInventory.Initialized && flow.GetModel<ProfileModel>() == null && !GF.Base.IsGamePaused && !Find<LevelViewController>().Any(), "返回标题未保存收益、恢复时钟或清理关卡");
            Check(flow.GetModel<ProfileModel>() == null && ReferenceEquals(firstSaveSystem, flow.GetSystem<SaveSystem>()) && flow.GetSystem<EquipmentSystem>() == null, "战斗返回标题仍持有档案作用域");
            Check(!GF.UI.HasUIForm(character.SerialId), "关闭后选角仍接收请求");
            // 损坏槽不能被当成空格；读取目录也不能使旧档案向其他槽写入。
            string broken = ProjectSettings.GlobalizePath($"user://{GF.Archive.Setting.Folder}/Data/{sixthId}.sav");
            File.WriteAllText(broken, "{ broken json");
            File.WriteAllText(broken + ".bak", "{ broken json");
            await ClickAsync(start, "m_MainMenu/m_LoadGame");
            saves = await WaitForSaveAsync();
            Check(saves.GetNode<BaseButton>("Slots/m_Slot6").Disabled && GF.Fsm.GetModel<SaveModel>().Slots[1].State == SaveSlotState.Occupied, "损坏格未禁用或影响其他存档");
            SmokeInspection.InvokeProcedureCallback("OnSaveSlotSelected", saves.SerialId, 6);
            for (int frame = 0; frame < 4; frame++)
            {
                await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            Check(GF.Procedure.CurrentProcedure is ProcedureTitle, "损坏格仍能进入读档流程");
            await ClickAsync(saves, "m_Close");
            await CheckMusicAsync("map_theme.mp3");
            SmokeInspection.InvokeProcedureCallback("OnLoadGameRequested", firstTitleSerial);
            for (int frame = 0; frame < 4; frame++)
            {
                await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            Check(GF.Procedure.CurrentProcedure is ProcedureTitle, "旧窗口排队事件改变了新周期流程");
            await CheckFailedProfileRegistrationAsync(flow, start);
        }
        catch (Exception error)
        {
            if (GF.Base.IsGamePaused)
            {
                GF.Base.ResumeGame();
            }
            _failure = error.ToString();
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

    /// <summary>通过真实控件验证人数菜单、双人选角、保存取消和覆盖，结束后清除隔离夹具。</summary>
    /// <param name="saves">当前空目录的读取模式窗口。</param>
    /// <returns>恢复空目录后的读取模式窗口。</returns>
    private async Task<SaveForm> CheckCoopMenusAsync(SaveForm saves)
    {
        SaveModel model = GF.Fsm.GetModel<SaveModel>();
        int initialCatalogueCount = GF.Archive.Catalogues.Count;
        Check(Enumerable.Range(1, 6).All(number => saves.GetNode<BaseButton>($"Slots/m_Slot{number}").Disabled),
            "读取模式的空存档格未全部禁用");
        saves.GetNode<BaseButton>("Slots/m_Slot1").EmitSignal(BaseButton.SignalName.Pressed);
        Check(GF.Procedure.CurrentProcedure is ProcedureTitle, "读取模式空格的迟到信号仍能导航");
        await ClickAsync(saves, "m_Close");
        StartForm start = await WaitForAsync<StartForm>();
        await ClickAsync(start, "m_MainMenu/m_NewGame");
        Check(!start.GetNode<Control>("m_MainMenu").Visible && start.GetNode<Control>("m_PlayerCountMenu").Visible,
            "人数菜单未替换标题主菜单");
        await CaptureAsync("coop_player_count");
        await ClickAsync(start, "m_PlayerCountMenu/m_Back");
        Check(start.GetNode<Control>("m_MainMenu").Visible && !start.GetNode<Control>("m_PlayerCountMenu").Visible,
            "人数菜单返回未恢复主菜单");
        await ClickAsync(start, "m_MainMenu/m_NewGame");
        await ClickAsync(start, "m_PlayerCountMenu/m_Double");
        CharacterForm character = await WaitForAsync<CharacterForm>();
        await ClickAsync(character, "Roles/m_Wukong");
        await ClickAsync(character, "Roles/m_Wukong");
        await MoveMouseAsync(new Vector2(900, 100));
        Check(GF.Procedure.CurrentProcedure is ProcedureCharacter && !Find<SaveForm>().Any(form => form.Visible) &&
            character.GetNode<Label>("m_PlayerOne").Visible && !character.GetNode<Label>("m_PlayerTwo").Visible &&
            Mathf.IsZeroApprox(((ShaderMaterial)character.GetNode<TextureButton>("Roles/m_Wukong").Material)
                .GetShaderParameter("grayscale").AsSingle()), "双人重复选卡未拒绝或鼠标离开清除了已选席位");
        await ClickAsync(character, "m_Reselect");
        await MoveMouseAsync(new Vector2(900, 100));
        Check(!character.GetNode<Label>("m_PlayerOne").Visible && !character.GetNode<Label>("m_PlayerTwo").Visible &&
            character.GetNode("Roles").GetChildren().OfType<Control>().All(card =>
                Mathf.IsEqualApprox(((ShaderMaterial)card.Material).GetShaderParameter("grayscale").AsSingle(), 1f)),
            "重选没有清除已选席位与卡片高亮");
        await ClickAsync(character, "m_Back");
        start = await WaitForAsync<StartForm>();
        Check(GF.Archive.Catalogues.Count == initialCatalogueCount && model.Slots.All(slot => slot.State == SaveSlotState.Empty),
            "返回标题前提前创建了新档案");

        // 先通过单人入口建立可覆盖的隔离档，供取消和真实覆盖比较。
        await ClickAsync(start, "m_MainMenu/m_NewGame");
        await ClickAsync(start, "m_PlayerCountMenu/m_Single");
        character = await WaitForAsync<CharacterForm>();
        Check(!character.GetNode<Control>("m_Reselect").Visible, "单人选角仍显示重选按钮");
        await ClickAsync(character, "Roles/m_Sanzang");
        saves = await WaitForSaveAsync();
        await ClickAsync(saves, "Slots/m_Slot4");
        MapForm map = await WaitForAsync<MapForm>();
        Check(GF.Archive.CurrentData.Heroes.Select(hero => hero.HeroId).SequenceEqual(new[] { 2 }),
            "单人选角未写入真实档案");
        await ReturnMapToTitleAsync(map);
        start = await WaitForAsync<StartForm>();
        await WaitUntilAsync(() => !Find<SaveNoticeForm>().Single().GetNode<Label>("SaveFeedback/m_SaveFeedback").Visible,
            "保存反馈未结束，无法采集无遮挡的菜单证据");
        string folder = ProjectSettings.GlobalizePath($"user://{GF.Archive.Setting.Folder}");
        Dictionary<string, byte[]> unchanged = Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes);
        long oldId = GF.Archive.CurrentCatalogue.UnitId;

        // 第一次取消覆盖并关闭保存页：包括目录和备份在内的所有文件必须逐字节保持。
        for (int attempt = 0; attempt < 2; attempt++)
        {
            await ClickAsync(start, "m_MainMenu/m_NewGame");
            await ClickAsync(start, "m_PlayerCountMenu/m_Double");
            character = await WaitForAsync<CharacterForm>();
            await ClickAsync(character, "Roles/m_Wukong");
            await MoveMouseAsync(character.GetNode<Control>("Roles/m_Sanzang").GetGlobalRect().GetCenter());
            Check(character.GetNode<Label>("m_PlayerTwo").Visible &&
                Mathf.IsEqualApprox(character.GetNode<Label>("m_PlayerTwo").GetGlobalRect().GetCenter().X,
                    character.GetNode<Control>("Roles/m_Sanzang").GetGlobalRect().GetCenter().X),
                "第二席位悬停标记未对齐下一个角色");
            await ClickAsync(character, "Roles/m_Sanzang");
            saves = await WaitForSaveAsync();
            Check(saves.GetNode<Label>("m_Status").Text == "第一次进入游戏请选择存档" &&
                Enumerable.Range(1, 6).All(number => !saves.GetNode<Control>($"Slots/m_Slot{number}/Delete").Visible &&
                    saves.GetNode<BaseButton>($"Slots/m_Slot{number}/Delete").Disabled) &&
                character.GetNode<Label>("m_PlayerOne").Visible && character.GetNode<Label>("m_PlayerTwo").Visible,
                "保存模式提示、删除入口或下层已选标记错误");
            await CaptureAsync("coop_save_mode");
            await ClickAsync(saves, "Slots/m_Slot4");
            Check(saves.GetNode<Control>("DeleteConfirmation").Visible &&
                saves.GetNode<Label>("DeleteConfirmation/Panel/Message").Text.Contains("确定覆盖第 4 个存档吗？") &&
                model.Slots[3].UnitId == oldId, "已有格未先请求覆盖确认");
            await CaptureAsync("coop_overwrite_confirmation");
            if (attempt == 0)
            {
                await ClickAsync(saves, "DeleteConfirmation/Panel/Cancel");
                Check(!saves.GetNode<Control>("DeleteConfirmation").Visible && model.Slots[3].UnitId == oldId &&
                    unchanged.All(file => File.ReadAllBytes(file.Key).SequenceEqual(file.Value)),
                    "取消覆盖仍改变目录或存档字节");
                int serialId = saves.SerialId;
                await ClickAsync(saves, "m_Close");
                start = await WaitForAsync<StartForm>();
                saves.GetNode<BaseButton>("DeleteConfirmation/Panel/Confirm").EmitSignal(BaseButton.SignalName.Pressed);
                Check(!GF.UI.HasUIForm(serialId) && GF.Procedure.CurrentProcedure is ProcedureTitle &&
                    unchanged.Count == Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Length &&
                    unchanged.All(file => File.ReadAllBytes(file.Key).SequenceEqual(file.Value)) &&
                    SmokeInspection.CallbackCount(saves, "SaveFormClosed") == 0,
                    "关闭保存页仍写档、持有回调或迟到确认仍生效");
            }
            else
            {
                await ClickAsync(saves, "DeleteConfirmation/Panel/Confirm");
            }
        }
        map = await WaitForAsync<MapForm>();
        Check(GF.Archive.CurrentCatalogue.SlotNumber == 4 &&
            GF.Archive.CurrentData.Heroes.Select(hero => hero.HeroId).SequenceEqual(new[] { 1, 2 }) &&
            GF.Fsm.GetModel<ProfileModel>().HeroIds.SequenceEqual(new[] { 1, 2 }),
            "确认覆盖未按所选顺序创建全新双人阵容");
        await ReturnMapToTitleAsync(map);
        start = await WaitForAsync<StartForm>();
        await WaitUntilAsync(() => !Find<SaveNoticeForm>().Single().GetNode<Label>("SaveFeedback/m_SaveFeedback").Visible,
            "保存反馈未结束，无法采集无遮挡的菜单证据");
        await ClickAsync(start, "m_MainMenu/m_LoadGame");
        saves = await WaitForSaveAsync();
        Check(saves.GetNode<Label>("Slots/m_Slot4/m_Name4").Text == "悟空 · 唐僧" &&
            saves.GetNode<BaseButton>("Slots/m_Slot6").Disabled &&
            model.Slots[3].Heroes.Select(hero => hero.Name).SequenceEqual(new[] { "悟空", "唐僧" }),
            "双人新档磁盘重读的阵容摘要错误或空格仍可点");
        await CaptureAsync("coop_roster_summary");
        await ClickAsync(saves, "Slots/m_Slot4/Delete");
        await ClickAsync(saves, "DeleteConfirmation/Panel/Confirm");
        await WaitUntilAsync(() => model.Phase == SaveListState.Ready && model.Slots.All(slot => slot.State == SaveSlotState.Empty),
            "双人菜单夹具未清理为空目录");
        GD.Print("SMOKE-COOP-MENU PASS: count/back, duplicate/reselect/back, single creation, save-mode close without writes, cancel byte preservation, confirmed overwrite and double-roster disk summary.");
        return saves;
    }

    /// <summary>
    /// 以隔离档案验证真实删除按钮、确认取消、主备文件和格子复用。
    /// </summary>
    /// <param name="saves">当前六格存档窗口。</param>
    /// <returns>删除与重新建档验证任务。</returns>
    private async Task CheckSaveDeletionAsync(SaveForm saves)
    {
        SaveSystem system = GF.Fsm.GetSystem<SaveSystem>();
        SaveModel model = GF.Fsm.GetModel<SaveModel>();
        Check(saves.GetNode<BaseButton>("Slots/m_Slot2/Delete").Disabled &&
            !saves.GetNode<Control>("Slots/m_Slot2/Delete").Visible, "空存档仍显示删除入口");
        await system.LoadOrCreateAsync(2, 0, [2]);
        await system.LoadOrCreateAsync(3, 0, [1]);
        Tables tables = GF.Fsm.GetModel<ConfigurationModel>().Tables;
        ExperienceCurveUtility curve = GF.Fsm.GetUtility<ExperienceCurveUtility>();
        // 用既有写入入口逐项构造经验等级，独立验证两种席位摘要与删除按钮间距。
        foreach (int[] heroIds in new[] { new[] { 1, 2 }, new[] { 2, 1 } })
        {
            ProfileModel profile = new(heroIds.Select(heroId => (heroId, heroId == 1 ? curve.MaxTotalExperience : 135)),
                curve, tables.TbProfileConfig.EquipmentCapacity);
            SkillModel skills = new(profile.HeroIds);
            GameData snapshot = GF.Fsm.GetUtility<SaveDataUtility>().Capture(profile, skills);
            Task<bool> write = (Task<bool>)typeof(SaveSystem)
                .GetMethod("WriteSnapshotAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(system, [snapshot]);
            Check(await write, "双人摘要夹具未实际写入第三格");
            await system.RefreshDirectoryAsync(default);

            string[] names = heroIds.Select(heroId => tables.TbHeroConfig.Get(heroId).NameCn).ToArray();
            SaveSlotInfo slot = model.Slots[2];
            Check(slot.State == SaveSlotState.Occupied && slot.Heroes.Select(hero => hero.Name).SequenceEqual(names) &&
                slot.Heroes.Select(hero => hero.Level).SequenceEqual(heroIds.Select(heroId => heroId == 1 ? curve.MaxLevel : 2)),
                "双人存档目录没有按席位读取双方名称和等级");
            Check(GF.Archive.Catalogues.Single(value => value.UnitId == slot.UnitId).Name == string.Join(" ", names),
                "双人目录名称没有按席位以单空格连接");
            Label title = saves.GetNode<Label>("Slots/m_Slot3/m_Name3");
            float width = title.GetThemeFont("font").GetStringSize(title.Text, fontSize: title.GetThemeFontSize("font_size")).X;
            Check(title.Text == string.Join(" · ", names) && width <= title.Size.X &&
                title.Position.X + width < saves.GetNode<Control>("Slots/m_Slot3/Delete").Position.X,
                "双人名称缺失、顺序错误、超出标题或遮挡删除按钮");
            Check(saves.GetNode<Label>("Slots/m_Slot2/m_Name2").Text == "唐僧", "单人存档没有统一显示纯角色名");
            await CaptureAsync($"save_slots_coop_{heroIds[0]}");
        }
        GD.Print("SMOKE-SAVE-ROSTER PASS: both seat orders, disk directory, catalogue names, uniform name-only titles and delete clearance.");
        long deletedId = model.Slots[1].UnitId;
        long retainedId = model.Slots[2].UnitId;
        string path = ProjectSettings.GlobalizePath($"user://{GF.Archive.Setting.Folder}/Data/{deletedId}.sav");
        Check(File.Exists(path), "删除夹具没有实际落盘");
        File.Copy(path, path + ".bak", true);
        await WaitUntilAsync(() => !Find<SaveNoticeForm>().Single().GetNode<Label>("SaveFeedback/m_SaveFeedback").Visible,
            "建档保存提示未结束");
        TextureButton delete = saves.GetNode<TextureButton>("Slots/m_Slot2/Delete");
        Check(delete.Visible && delete.Size.IsEqualApprox(new Vector2(24, 24)) && delete.Modulate.A < .5f,
            "有存档的删除入口没有使用缩小压暗的图标");
        Check(saves.GetNode<TextureRect>("DeleteConfirmation/Panel").Texture != null &&
            saves.GetNode<TextureButton>("DeleteConfirmation/Panel/Cancel").TextureNormal != null &&
            saves.GetNode<TextureButton>("DeleteConfirmation/Panel/Confirm").TextureNormal != null,
            "原项目确认框贴图没有载入");
        await CaptureAsync("save_delete_slots");
        await MoveMouseAsync(delete.GetGlobalRect().GetCenter());
        Check(delete.Modulate.IsEqualApprox(Colors.White), "鼠标靠近时未提亮删除入口");
        await CaptureAsync("save_delete_hover");
        await MoveMouseAsync(new Vector2(20, 20));
        Check(delete.Modulate.A < .5f, "鼠标离开后没有恢复暗色删除入口");
        await ClickAsync(saves, "Slots/m_Slot2/Delete");
        Control confirmation = saves.GetNode<Control>("DeleteConfirmation");
        Check(confirmation.Visible && confirmation.GetNode<Label>("Panel/Message").Text.Contains("第 2 个存档"),
            "删除确认层没有显示当前格号");
        Check(GF.Procedure.CurrentProcedure is ProcedureTitle && model.Slots[1].State == SaveSlotState.Occupied,
            "删除按钮透传给格子或在确认前删除");
        await CaptureAsync("save_delete_confirmation");
        await ClickAsync(saves, "DeleteConfirmation/Panel/Cancel");
        Check(File.Exists(path) && File.Exists(path + ".bak") && model.Slots[1].UnitId == deletedId,
            "取消删除仍修改存档");
        await ClickAsync(saves, "Slots/m_Slot2/Delete");
        await ClickAsync(saves, "DeleteConfirmation/Panel/Confirm");
        await WaitUntilAsync(() => model.Phase == SaveListState.Ready && model.Slots[1].State == SaveSlotState.Empty,
            "删除后存档目录未刷新为空格");
        Check(!File.Exists(path) && !File.Exists(path + ".bak") && !File.Exists(path + ".tmp") &&
            model.Slots[2].UnitId == retainedId && model.Slots[2].State == SaveSlotState.Occupied,
            "删除未清主备文件或影响其他格");
        await CaptureAsync("save_delete_completed");
        Check(saves.GetNode<BaseButton>("Slots/m_Slot2").Disabled, "删除后的空格在读取模式仍可点");
        await ClickAsync(saves, "m_Close");
        StartForm start = await WaitForAsync<StartForm>();
        await ClickAsync(start, "m_MainMenu/m_NewGame");
        await ClickAsync(start, "m_PlayerCountMenu/m_Single");
        CharacterForm character = await WaitForAsync<CharacterForm>();
        await ClickAsync(character, "Roles/m_Sanzang");
        saves = await WaitForSaveAsync();
        await ClickAsync(saves, "Slots/m_Slot2");
        MapForm map = await WaitForAsync<MapForm>();
        Check(GF.Archive.CurrentData.Heroes[0].HeroId == 2 && GF.Archive.CurrentData.UnitId != deletedId &&
            GF.Archive.CurrentCatalogue.SlotNumber == 2, "删除后的同格未正确新建唐僧档案");
        await ReturnMapToTitleAsync(map);
        start = await WaitForAsync<StartForm>();
        await ClickAsync(start, "m_MainMenu/m_LoadGame");
        saves = await WaitForSaveAsync();
        Check(model.Slots[1].State == SaveSlotState.Occupied && model.Slots[2].UnitId == retainedId &&
            SmokeInspection.CallbackCount(saves, "SaveSlotDeleteRequested") == 1, "目录重读或删除订阅复用异常");
        await ClickAsync(saves, "Slots/m_Slot3/Delete");
        Check(saves.GetNode<Control>("DeleteConfirmation").Visible, "重开存档页没有显示删除确认层");
        saves.GetNode<BaseButton>("m_Close").EmitSignal(BaseButton.SignalName.Pressed);
        saves.GetNode<BaseButton>("DeleteConfirmation/Panel/Confirm").EmitSignal(BaseButton.SignalName.Pressed);
        Check(model.Slots[2].UnitId == retainedId && GF.Archive.Catalogues.Any(value => value.UnitId == retainedId),
            "已关闭存档窗口的迟到确认仍删除存档");
        GD.Print("SMOKE-SAVE-DELETE PASS: empty hidden, compact dim icon, hover, original artwork, actual mouse, cancel, confirmation, main/backup removal, other slot, same-slot creation, reload and stale confirmation.");
    }

    /// <summary>
    /// 在原生 FSM 验证归属、初始化、权限、缺失依赖、移除与池复用隔离。
    /// </summary>
    /// <param name="flow">正式流程提供的测试宿主。</param>
    private static void CheckTypedFsmData(ProcedureOwner flow)
    {
        ProcedureOwner first = GF.Fsm.CreateFsm<GameFramework.Procedure.IProcedureManager>("ArchitectureProbe1", flow.Owner, new ArchitectureProbeState());
        ProcedureOwner second = GF.Fsm.CreateFsm<GameFramework.Procedure.IProcedureManager>("ArchitectureProbe2", flow.Owner, new ArchitectureProbeState());
        IArchitecture firstArchitecture = first.InitializeArchitecture();
        IArchitecture secondArchitecture = second.InitializeArchitecture();
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
            CheckFailedMemberLifecycle(first);
            Check(!typeof(ICanGetModel).IsAssignableFrom(typeof(IModel)) && !typeof(ICanGetSystem).IsAssignableFrom(typeof(IModel)) &&
                !typeof(IBelongToArchitecture).IsAssignableFrom(typeof(IUtility)) && !typeof(Godot.Node).IsAssignableFrom(typeof(LevelSystem)),
                "Model/Utility 权限或普通 System 边界不正确");
            GF.Fsm.DestroyFsm(first);
            first = GF.Fsm.CreateFsm<GameFramework.Procedure.IProcedureManager>("ArchitectureProbe1", flow.Owner, new ArchitectureProbeState());
            IArchitecture replacementArchitecture = first.InitializeArchitecture();
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
        GD.Print("SMOKE-ARCHITECTURE PASS: identity, isolation, duplicate registration, partial init cleanup, failed removal and FSM reuse");
    }

    /// <summary>
    /// 在真实 FSM 注入模型与系统的初始化、退出及双重失败，检查旧注册已经解除且能重新装配。
    /// </summary>
    /// <param name="flow">只持有测试成员的局部 FSM。</param>
    private static void CheckFailedMemberLifecycle(ProcedureOwner flow)
    {
        foreach ((bool failInit, bool failDeinit) in new[] { (true, false), (false, true), (true, true) })
        {
            var model = new ArchitectureProbeModel(5, failInit, failDeinit);
            Check(ExpectLifecycleFailure(() =>
            {
                flow.RegisterModel(model);
                flow.RemoveModel<ArchitectureProbeModel>();
            }, failInit && failDeinit), "模型生命周期故障没有原样传递");
            Check(!model.Initialized && model.InitCount == 1 && model.DeinitCount == 1 &&
                flow.GetModel<ArchitectureProbeModel>() == null, "失败模型残留注册或重复清理");
            flow.RegisterModel(new ArchitectureProbeModel(6));
            Check(flow.RemoveModel<ArchitectureProbeModel>(), "失败模型阻止后续重新注册");

            var system = new ArchitectureProbeSystem(flow.GetArchitecture(), failInit, failDeinit);
            Check(ExpectLifecycleFailure(() =>
            {
                flow.RegisterSystem(system);
                flow.RemoveSystem<ArchitectureProbeSystem>();
            }, failInit && failDeinit), "系统生命周期故障没有原样传递");
            Check(!system.Initialized && system.InitCount == 1 && system.DeinitCount == 1 &&
                flow.GetSystem<ArchitectureProbeSystem>() == null, "失败系统残留注册或重复清理");
            flow.RegisterSystem(new ArchitectureProbeSystem(flow.GetArchitecture(), false, false));
            Check(flow.RemoveSystem<ArchitectureProbeSystem>(), "失败系统阻止后续重新注册");
        }
    }

    /// <summary>
    /// 只接受夹具主动抛出的生命周期错误；双重失败须保留两个异常。
    /// </summary>
    /// <param name="action">注册或移除操作。</param>
    /// <param name="dualFailure">是否预期初始化与退出均失败。</param>
    /// <returns>捕获到预期故障时为 true。</returns>
    private static bool ExpectLifecycleFailure(Action action, bool dualFailure)
    {
        try
        {
            action();
            return false;
        }
        catch (AggregateException error) when (dualFailure)
        {
            return error.InnerExceptions.Count == 2 && error.InnerExceptions.All(item =>
                item is InvalidOperationException && item.Message.StartsWith("Architecture probe"));
        }
        catch (InvalidOperationException error) when (!dualFailure)
        {
            return error.Message.StartsWith("Architecture probe");
        }
    }

    /// <summary>
    /// 通过重复注册制造真实装配失败，验证档案回收和下一次读档。
    /// </summary>
    /// <param name="flow">正式应用作用域。</param>
    /// <param name="start">本周期标题窗口。</param>
    /// <returns>失败清理及重新读档验证任务。</returns>
    private async Task CheckFailedProfileRegistrationAsync(ProcedureOwner flow, StartForm start)
    {
        SaveSystem saveSystem = flow.GetSystem<SaveSystem>();
        ConfigurationModel configuration = flow.GetModel<ConfigurationModel>();
        await ClickAsync(start, "m_MainMenu/m_LoadGame");
        SaveForm saves = await WaitForSaveAsync();
        // 测试拥有的重复项使真实 ProcedureLoadGame 在恢复档案后注册失败。
        var duplicate = new EquipmentSystem(flow.GetArchitecture());
        flow.RegisterSystem(duplicate);
        await ClickAsync(saves, "Slots/m_Slot2");
        await WaitUntilAsync(() => GF.Procedure.CurrentProcedure is ProcedureLoadGame && !duplicate.Initialized,
            "读档装配失败未清理已注册的档案对象");
        Check(flow.GetModel<ProfileModel>() == null && flow.GetModel<GameLogic.Skills.SkillModel>() == null &&
            flow.GetSystem<ProfileSystem>() == null && flow.GetSystem<EquipmentSystem>() == null &&
            flow.GetSystem<GameLogic.Skills.SkillSystem>() == null &&
            ReferenceEquals(configuration, flow.GetModel<ConfigurationModel>()) &&
            ReferenceEquals(saveSystem, flow.GetSystem<SaveSystem>()), "失败清理遗漏档案对象或移除了应用服务");

        // 正式读档页没有返回按钮，测试使用既有 GGF 状态入口重试。
        SmokeInspection.ChangeProcedure<ProcedureTitle>();
        start = await WaitForAsync<StartForm>();
        await ClickAsync(start, "m_MainMenu/m_LoadGame");
        saves = await WaitForSaveAsync();
        await ClickAsync(saves, "Slots/m_Slot2");
        await WaitForAsync<MapForm>();
        Check(flow.GetModel<ProfileModel>()?.Initialized == true &&
            flow.GetSystem<EquipmentSystem>()?.Initialized == true &&
            flow.GetSystem<GameLogic.Skills.SkillSystem>()?.Initialized == true,
            "失败后再次读档未建立完整档案作用域");
        GD.Print("SMOKE-LOAD-ROLLBACK PASS: partial profile registration is cleared and reload succeeds.");
    }

    /// <summary>
    /// 读档在等待检查点期间取消后，不创建空槽或发布当前槽身份。
    /// </summary>
    /// <param name="flow">正式应用作用域，使用反射控制等待，不增加生产测试入口。</param>
    /// <returns>取消及旧任务观察完成的任务。</returns>
    private static async Task CheckCancelledLoadAsync(ProcedureOwner flow)
    {
        SaveSystem system = flow.GetSystem<SaveSystem>();
        SaveModel model = flow.GetModel<SaveModel>();
        FieldInfo pendingField = typeof(SaveSystem).GetField("_pendingWrite",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Task<bool> previousWrite = system.PendingWrite;
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new System.Threading.CancellationTokenSource();
        long slot = model.ActiveSlotUnitId;
        GameData data = GF.Archive.CurrentData;
        int count = GF.Archive.Catalogues.Count;
        try
        {
            pendingField.SetValue(system, gate.Task);
            Task<(GameData Data, SaveOrigin Origin)> loading = system.LoadOrCreateAsync(6, 0, [], cancellation.Token);
            Check(!loading.IsCompleted, "读档没有等待在途检查点");
            cancellation.Cancel();
            gate.SetResult(true);
            bool cancelled = false;
            try
            {
                await loading;
            }
            catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled && model.ActiveSlotUnitId == slot && ReferenceEquals(GF.Archive.CurrentData, data) &&
                GF.Archive.Catalogues.Count == count, "取消后的旧读档仍创建文件或发布当前槽");
        }
        finally
        {
            gate.TrySetResult(true);
            pendingField.SetValue(system, previousWrite);
        }
        GD.Print("SMOKE-LOAD-CANCEL PASS: cancelled load leaves catalogue and active slot unchanged.");
    }

    /// <summary>
    /// 确认提示关闭释放回调，复用后只执行本次回调。
    /// </summary>
    /// <returns>真实框架提示验证任务。</returns>
    private async Task CheckQuestionTipsAsync()
    {
        int callbacks = 0;
        GF.UI.OpenQuestionTipsAsync("共享提示关闭与复用验证", "取消", "确认", () => callbacks += 10, () => callbacks++);
        QuestionTips first = await WaitForAsync<QuestionTips>();
        int closedTipSerial = first.SerialId;
        Check(GF.UI.HasUIForm(closedTipSerial) && GF.UI.IsValidUIForm(first), "打开提示未登记在 GGF 中");
        GF.UI.CloseUIForm(closedTipSerial);
        Check(first.SerialId == closedTipSerial && !GF.UI.HasUIForm(closedTipSerial) && !GF.UI.IsValidUIForm(first),
            "提示关闭后、回池前框架仍将其视为打开");
        Check(SmokeInspection.ReadField<Action>(first, "_cancel") == null && SmokeInspection.ReadField<Action>(first, "_confirm") == null, "提示关闭后仍持有回调");
        first.GetNode<BaseButton>("Content/VBoxContainer/HBoxContainer/m_Btn_Confirm").EmitSignal(BaseButton.SignalName.Pressed);
        Check(callbacks == 0, "隐藏提示执行了旧回调");
        // GGF 在下一轮更新归还关闭对象，再请求才验证同一实例复用。
        for (int frame = 0; frame < 4; frame++)
        {
            await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        GF.UI.OpenQuestionTipsAsync("复用后执行本次确认", "取消", "确认", null, () => callbacks++);
        QuestionTips second = await WaitForAsync<QuestionTips>();
        Check(ReferenceEquals(first, second), "共享提示没有复用 GGF 池实例");
        await ClickAsync(second, "Content/VBoxContainer/HBoxContainer/m_Btn_Confirm");
        Check(callbacks == 1 && SmokeInspection.ReadField<Action>(second, "_confirm") == null, "关闭清理吞掉了本次回调或重复执行旧回调");
    }

    /// <summary>
    /// 等待六格目录可供选择。
    /// </summary>
    /// <returns>已就绪的存档窗口。</returns>
    private async Task<SaveForm> WaitForSaveAsync()
    {
        SaveForm form = await WaitForAsync<SaveForm>();
        await WaitUntilAsync(() => GF.Fsm.GetModel<SaveModel>().Phase == SaveListState.Ready, "存档目录读取超时");
        return form;
    }

    /// <summary>
    /// 等待异步目录或写入，超时记录业务失败。
    /// </summary>
    /// <param name="ready">目标完成条件。</param>
    /// <param name="failure">超时证据。</param>
    /// <returns>完成条件等待任务。</returns>
    private async Task WaitUntilAsync(Func<bool> ready, string failure)
    {
        ulong deadline = Time.GetTicksMsec() + 15000;
        while (!ready() && Time.GetTicksMsec() < deadline)
        {
            await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        Check(ready(), failure);
    }

    /// <summary>
    /// 等待实际 GGF 窗口打开，给资源加载最多 15 秒。
    /// </summary>
    /// <typeparam name="T">窗口类型。</typeparam>
    /// <returns>已显示窗口。</returns>
    private async Task<T> WaitForAsync<T>() where T : Control
    {
        ulong deadline = Time.GetTicksMsec() + 15000;
        while (Time.GetTicksMsec() < deadline)
        {
            T form = GF.UI.GetAllLoadedUIForms().OfType<T>().FirstOrDefault(x => x.Visible);
            if (form != null && GF.UI.GetUIForm(ResourcesCollectionConstant.UI_LoadingForm) == null)
            {
                SmokeInspection.CheckTooltips(form);
                return form;
            }

            await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        throw new InvalidOperationException($"等待 {typeof(T).Name} 超时。");
    }

    /// <summary>
    /// 等待淡出结束后检查 GGF 的 AudioStreamPlayer 代理，循环取决于正式导入设置。
    /// </summary>
    /// <param name="file">期望的音乐资源文件名。</param>
    /// <returns>音乐检查任务。</returns>
    private async Task CheckMusicAsync(string file)
    {
        for (int frame = 0; frame < 35; frame++)
        {
            await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        AudioStreamPlayer[] music = Find<AudioStreamPlayer>().Where(x => x.Bus == "Music" && x.Playing).ToArray();
        Check(music.Length == 1 && music[0].Stream.ResourcePath.EndsWith(file, StringComparison.Ordinal),
            $"GGF Music 代理未唯一播放 {file}");
        Check(music[0].Stream is AudioStreamMP3 { Loop: true }, "音乐资源未启用循环");
        Check(music[0].Stream.GetLength() > 30, "短确认音被误当成循环音乐");
        Check(!Find<AudioStreamPlayer>().Any(player => player.Bus == "UI" && player.Playing), "确认音播放超过淡出等待时间仍未停止");
    }

    /// <summary>
    /// 真实 GPU 运行时保存正式界面截图；headless 模式只验证行为。
    /// </summary>
    /// <param name="name">截图 basename。</param>
    /// <returns>截图任务。</returns>
    private async Task CaptureAsync(string name)
    {
        if (DisplayServer.GetName() == "headless")
        {
            return;
        }

        for (int frame = 0; frame < 3; frame++)
        {
            await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        // 复用战斗烟测的主动渲染方式，静止或被遮挡的窗口也能产生截图而不等待下一次重绘。
        RenderingServer.ForceDraw(false);
        DirAccess.MakeDirRecursiveAbsolute("res://.godot/validation/frontend");
        _driver.GetViewport().GetTexture().GetImage().SavePng($"res://.godot/validation/frontend/{name}.png");
    }

    /// <summary>
    /// 从真实场景树读取测试所需节点，正式模块不遍历其他模块。
    /// </summary>
    /// <typeparam name="T">节点脚本类型。</typeparam>
    /// <returns>匹配的节点序列。</returns>
    private System.Collections.Generic.IEnumerable<T> Find<T>() where T : Node => _driver.GetTree().Root.FindChildren("*", "", true, false).OfType<T>();

    /// <summary>
    /// 激活真实已绑定的按钮，禁止对隐藏或禁用控件伪造成功。
    /// </summary>
    /// <param name="form">所属 GGF 窗口。</param>
    /// <param name="path">窗口内按钮路径。</param>
    private static void Press(Control form, string path)
    {
        BaseButton button = form.GetNode<BaseButton>(path);
        Check(form.Visible && !button.Disabled && button.GetGlobalRect().HasArea(), $"按钮不可操作：{path}");
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    /// <summary>
    /// 使用视口局部坐标分发鼠标移动，避免窗口缩放改变测试命中位置。
    /// </summary>
    /// <param name="position">视口内鼠标坐标。</param>
    /// <returns>输入分发和布局更新任务。</returns>
    private async Task MoveMouseAsync(Vector2 position)
    {
        for (int frame = 0; frame < 3; frame++)
        {
            await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        // 布局稳定后立即分发；坐标已在视口空间，不再按原生窗口尺寸变换。
        _driver.GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
    }

    /// <summary>
    /// 用真实鼠标验证已开放角色预览、未开放角色无交互及窗口复用复位。
    /// </summary>
    /// <param name="character">当前正式选角窗口。</param>
    /// <returns>四栏交互检查任务。</returns>
    private async Task CheckCharacterHoverAsync(CharacterForm character)
    {
        Control[] roles = character.GetNode("Roles").GetChildren().OfType<Control>().ToArray();
        Label marker = character.GetNode<Label>("m_PlayerOne");
        Check(roles.Length == 4 && roles.Take(2).All(role => role is TextureButton) &&
            roles.Skip(2).All(role => role is TextureRect), "角色节点未按两名已开放角色和两栏占位分组");
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
            {
                Check(Mathf.IsEqualApprox(marker.GetGlobalRect().GetCenter().X, role.GetGlobalRect().GetCenter().X), "1P 未对齐已开放角色");
            }

            Check(roles.All(card => Mathf.IsEqualApprox(((ShaderMaterial)card.Material).GetShaderParameter("grayscale").AsSingle(), card == role && card is TextureButton ? 0f : 1f)), "已开放角色悬停未恢复颜色、未开放角色变色或旧卡片未复位");
            Check((_driver.GetViewport().GuiGetHoveredControl() == role) == selectable, "已开放角色不能悬停或未开放角色仍接收鼠标");
            Check(role.GetCursorShape() == (role is TextureButton ? Control.CursorShape.PointingHand : Control.CursorShape.Arrow), "角色光标没有反映可选状态");
            if (role.Name == "m_Sanzang")
            {
                await CaptureAsync("character_sanzang_hover");
            }

            if (role is TextureRect)
            {
                Check(role.MouseFilter == Control.MouseFilterEnum.Ignore && string.IsNullOrEmpty(role.TooltipText), "未开放角色仍存在鼠标交互或悬停提示");
                Vector2 position = role.GetGlobalRect().GetCenter();
                _driver.GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true }, true);
                await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.ProcessFrame);
                _driver.GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false }, true);
                await MoveMouseAsync(position);
                Check(GF.Procedure.CurrentProcedure is ProcedureCharacter, "尚未开放的角色触发了导航");
            }
        }
        await MoveMouseAsync(roles[0].GetGlobalRect().GetCenter());
        await CaptureAsync("character_form");
        await MoveMouseAsync(new Vector2(-5, -5));
        Check(!marker.Visible, "离开角色后 1P 仍显示");
    }

    /// <summary>
    /// 从地图打开设置，经实际导航按钮返回主菜单。
    /// </summary>
    /// <param name="map">本次显示的地图。</param>
    /// <returns>设置导航提交完成的任务。</returns>
    private async Task ReturnMapToTitleAsync(MapForm map)
    {
        ProfileModel profile = GF.Fsm.GetModel<ProfileModel>();
        GameData expected = GF.Fsm.GetUtility<SaveDataUtility>().Capture(profile, GF.Fsm.GetModel<SkillModel>());
        long unitId = GF.Fsm.GetModel<SaveModel>().ActiveSlotUnitId;
        await ClickAsync(map, "m_Settings");
        SettingsForm settings = await WaitForAsync<SettingsForm>();
        Check(settings.GetNode<Control>("Scroll/Content/Options/Navigation/m_ReturnTitle").Visible &&
            SmokeInspection.CallbackCount(settings, "TitleRequested") == 1,
            "地图设置没有显示或绑定返回主菜单");
        await ClickAsync(settings, "Scroll/Content/Options/Navigation/m_ReturnTitle");
        await WaitForAsync<StartForm>();
        await CheckSavedProfileAsync(expected, unitId);
    }

    /// <summary>
    /// 直接读取指定磁盘槽，对照返回前的完整档案快照，拒绝以内存镜像代替落盘证据。
    /// </summary>
    /// <param name="expected">返回前捕获的独立档案。</param>
    /// <param name="unitId">本次档案文件编号。</param>
    /// <returns>真实读取与字段比对任务。</returns>
    private static async Task CheckSavedProfileAsync(GameData expected, long unitId)
    {
        ArchiveSetting setting = GF.Archive.Setting;
        GameData actual = await EasySave.LoadFromUserAsync<GameData>($"{setting.Folder}/Data/{unitId}.sav",
            setting.EnableAesEncryption, setting.KEY, setting.Salt);
        var before = JObject.FromObject(expected);
        var saved = JObject.FromObject(actual);
        foreach (string field in new[] { "SaveVersion", "Heroes", "EquipmentItems",
            "HighestClearedLevelId", "EastSeaBannerAvailable" })
        {
            Check(JToken.DeepEquals(before[field], saved[field]), $"返回主菜单未保存当前档案字段：{field}");
        }
        GD.Print("SMOKE-RETURN-SAVE PASS: complete profile, skills and equipment read directly from disk.");
    }

    /// <summary>
    /// 在没有战斗作用域的地图中通过真实控件学习主动和被动技能，再检查窗口复用与状态同步。
    /// </summary>
    /// <param name="map">本次地图窗口。</param>
    /// <param name="flow">持有档案和技能的主流程作用域。</param>
    /// <returns>地图学技交互检查任务；后续主流程继续验证磁盘重载和进关生效。</returns>
    private async Task CheckMapSkillPurchaseAsync(MapForm map, ProcedureOwner flow)
    {
        Tables tables = ConfigSystem.Instance.Tables;
        SkillSystem skillSystem = flow.GetSystem<SkillSystem>();
        ProfileModel profile = flow.GetModel<ProfileModel>();
        SkillModel skills = flow.GetModel<SkillModel>();
        int activeCost = tables.TbSkillConfig.Get(SkillId.RisingDragon).LearnSoul;
        int passiveCost = tables.TbPassiveConfig.Get(PassiveId.Vitality).Levels[0].LearnSoul;
        flow.GetSystem<ProfileSystem>().AddSoul(GF.Fsm.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1), activeCost + passiveCost);
        int initialSoul = profile.GetSoul(profile.HeroOf(PlayerSlot.P1)).Value;
        Check(flow.GetModel<HeroModel>() == null && flow.GetModel<LevelModel>() == null &&
            !Find<AbstractHeroEntity>().Any() && !Find<LevelViewController>().Any(), "地图学技前已创建战斗英雄或关卡");

        AnimatedSprite2D skillArtwork = map.GetNode<AnimatedSprite2D>("m_Skills/Artwork");
        Check((skillArtwork.GlobalPosition - map.GlobalPosition).IsEqualApprox(new Vector2(77.5f, 85)) &&
            string.IsNullOrEmpty(map.GetNode<Button>("m_Skills").Text) &&
            skillArtwork.SpriteFrames.GetFrameCount("hover") == 15 &&
            skillArtwork.SpriteFrames.GetFrameCount("available") == 19,
            "地图技能入口未使用对齐后的布局及完整官方悬停/可购动画");
        await MoveMouseAsync(skillArtwork.GlobalPosition);
        Check(skillArtwork.Animation == "hover", "地图技能真实悬停未切换官方第2态");
        int hoverFrame = skillArtwork.Frame;
        await WaitUntilAsync(() => skillArtwork.Frame != hoverFrame, "官方地图技能悬停子动画没有播放");
        await WaitUntilAsync(() => !skillArtwork.IsPlaying(), "官方技能展开到第15帧后没有停止");
        Check(skillArtwork.Frame == 14, "官方技能展开没有停在末帧");
        await _driver.ToSignal(_driver.GetTree().CreateTimer(.8), SceneTreeTimer.SignalName.Timeout);
        Check(skillArtwork.Frame == 14 && !skillArtwork.IsPlaying(), "保持悬停时技能展开被循环重播");
        await CaptureAsync("map_skill_entry_hover");
        await MoveMouseAsync(new Vector2(-5, -5));
        Check(skillArtwork.Animation == "idle" && skillArtwork.Frame == 0, "移出地图技能入口未恢复官方静止第1态");
        await CaptureAsync("map_skill_entry_idle");
        // 原版从技能页回到地图会重新按余额选择第3态。
        await ClickAsync(map, "m_Skills");
        SkillForm preview = await WaitForAsync<SkillForm>();
        await ClickAsync(preview, "Content/Header/m_Back");
        Check(initialSoul >= 160 && skillArtwork.Animation == "available", "地图技能返回后未恢复原版可购提示");
        int availableFrame = skillArtwork.Frame;
        await WaitUntilAsync(() => skillArtwork.Frame != availableFrame, "官方地图可购发光子动画没有播放");
        await CaptureAsync("map_skill_entry_available");
        await ClickAsync(map, "m_Skills");
        SkillForm form = await WaitForAsync<SkillForm>();
        Check(GF.Procedure.CurrentProcedure is ProcedureMap && !GF.Base.IsGamePaused &&
            flow.GetModel<HeroModel>() == null && flow.GetModel<LevelModel>() == null,
            "地图技能窗改变流程、暂停或创建了战斗模型");
        await CaptureAsync("map_skill_active");
        await ClickAsync(form, "Content/m_ActivePage/m_WukongRows/Skill1/IconCell/Icon");
        Check(skillSystem.IsLearned(GF.Fsm.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1), SkillId.RisingDragon) && profile.GetSoul(profile.HeroOf(PlayerSlot.P1)).Value == initialSoul - activeCost &&
            form.GetNode<BaseButton>("Content/m_ActivePage/m_WukongRows/Skill1/IconCell/Icon").Disabled &&
            form.GetNode<Label>("Content/Footer/Balance/m_Soul").Text == profile.GetSoul(profile.HeroOf(PlayerSlot.P1)).Value.ToString(),
            "地图主动购买未同步学习、扣费、图标或余额");

        await ClickAsync(form, "Content/Footer/m_Passive");
        await ClickAsync(form, "Content/m_PassivePage/m_PassiveRows/Passive1/Upgrade");
        Check(skills.GetPassiveLevels(1)[PassiveId.Vitality] == 1 &&
            profile.GetSoul(profile.HeroOf(PlayerSlot.P1)).Value == initialSoul - activeCost - passiveCost &&
            form.GetNode<Label>("Content/m_PassivePage/m_PassiveRows/Passive1/Level").Text == "1" &&
            form.GetNode<Label>("Content/Footer/Balance/m_Soul").Text == profile.GetSoul(profile.HeroOf(PlayerSlot.P1)).Value.ToString(),
            "地图被动购买未同步等级、扣费或余额");
        await CaptureAsync("map_skill_passive");
        int firstSerial = form.SerialId;
        await ClickAsync(form, "Content/Header/m_Back");
        Check(!GF.UI.HasUIForm(firstSerial) && !GF.Base.IsGamePaused && map.Visible,
            "地图技能窗关闭后仍保留交互、暂停或遮挡地图");
        await ClickAsync(map, "m_Skills");
        form = await WaitForAsync<SkillForm>();
        Check(form.GetNode<BaseButton>("Content/m_ActivePage/m_WukongRows/Skill1/IconCell/Icon").Disabled &&
            form.GetNode<Label>("Content/Footer/Balance/m_Soul").Text == profile.GetSoul(profile.HeroOf(PlayerSlot.P1)).Value.ToString(),
            "重开地图技能窗未显示已购买状态");
        await ClickAsync(form, "Content/Header/m_Back");
        Check(flow.GetModel<HeroModel>() == null && flow.GetModel<LevelModel>() == null &&
            !Find<AbstractHeroEntity>().Any() && !Find<LevelViewController>().Any(), "地图购买技能创建了战斗实体或模型");
        GD.Print("SMOKE-MAP-SKILLS PASS: native map entry, active/passive purchase, wallet refresh, reopen and no battle scope.");
    }

    /// <summary>
    /// 验证真实写入后的提示、自动消失、错误结果及再次保存时的动画复位。
    /// </summary>
    /// <param name="map">已经完成一次实际保存的地图窗口。</param>
    /// <returns>保存提示交互检查任务。</returns>
    private async Task CheckSaveFeedbackAsync(MapForm map)
    {
        SaveNoticeForm notice = Find<SaveNoticeForm>().First(x => x.SerialId != 0);
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
        await WaitUntilAsync(() => map.GetNode<BaseButton>("m_Save").SelfModulate == new Color(1f, .55f, .55f), "失败结果未更新按钮状态");
        Check(!feedback.Visible && !map.GetNode<BaseButton>("m_Save").Disabled, "写入失败误报成功或不能重试");
        await CaptureAsync("map_save_failure");
        await ClickAsync(map, "m_Save");
        await WaitUntilAsync(() => !map.GetNode<BaseButton>("m_Save").Disabled, "再次保存未完成");
        // 绑定结果同步提交，GGF 的一次性保存成功事件在下一轮分发。
        await WaitUntilAsync(() => feedback.Visible && animation.IsPlaying(), "再次保存成功事件未显示提示");
        Check(feedback.Text == "保存成功！" && Mathf.IsEqualApprox(feedback.Modulate.A, 1f), "再次实际保存未刷新提示或沿用旧透明度");
    }

    /// <summary>
    /// 以真实鼠标从透明输入区进入浅岸、东海及鲨之涡流，并经设置返回地图验证重入。
    /// </summary>
    /// <param name="map">本轮地图窗口。</param>
    /// <returns>三处海域地点的点击与真实关卡进入任务。</returns>
    private async Task CheckCoastalMapAsync(MapForm map)
    {
        ProfileSystem profile = GF.Fsm.GetSystem<ProfileSystem>();
        for (int id = 1; id <= 6; id++)
        {
            profile.RecordLevelClear(id);
        }
        foreach ((string name, int id) in new[] { ("Qianan", 7), ("Donghai", 8), ("Shazhiwoliu", 9) })
        {
            TextureButton button = map.GetNode<TextureButton>("m_" + name);
            Check(!button.Disabled && button.TextureClickMask != null, "沿海地点未开放或缺少官方透明点击区");
            await MoveMouseAsync(new Vector2(30, 30));
            // 官方透明输入形状经过各自的缩放和平移，中心不等于沿海大画布中心。
            Vector2 point = name switch
            {
                "Qianan" => new Vector2(450, 329.1f),
                "Donghai" => new Vector2(556.7f, 435.5f),
                _ => new Vector2(766.55f, 500.15f),
            };
            await MoveMouseAsync(point);
            AnimationPlayer animation = map.GetNode<AnimationPlayer>("m_LocationAnimation");
            for (int sample = 0; sample <= 4; sample++)
            {
                animation.Seek(sample / 4d, true);
                await MoveMouseAsync(point);
                Check(_driver.GetViewport().GuiGetHoveredControl() == button &&
                    button.GetNode<TextureRect>("LocationGlow").Visible, "透明岸内不能稳定保持悬停：" + name);
            }
            await CaptureAsync("map_" + name.ToLowerInvariant() + "_hover");
            await ClickAsync(map, "m_" + name, point);
            await WaitUntilAsync(() => Find<LevelViewController>().Any() && Find<AbstractHeroEntity>().Any(hero => hero.IsShown), "沿海点击没有进入关卡：" + name);
            BattleForm battle = await WaitForAsync<BattleForm>();
            Check(GF.Fsm.GetModel<LevelModel>().LevelId == id, "沿海点击进入了错误关卡");
            await CaptureAsync("entered_" + name.ToLowerInvariant());
            await ClickAsync(battle, "MenuPanel/m_SettingsButton");
            SettingsForm settings = await WaitForAsync<SettingsForm>();
            await ClickAsync(settings, "Scroll/Content/Options/Navigation/m_ReturnMap");
            map = await WaitForAsync<MapForm>();
            profile.RecordLevelClear(id);
            GD.Print($"SMOKE-COAST-MAP PASS: {name}, transparent center, hover cycle, actual click, level={id}, return map");
        }
    }

    /// <summary>
    /// 验证普通图透明区拒绝输入，悬停图扩展区保持悬停且跟随节点缩放。
    /// </summary>
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
                Check(map.GetNode<AnimationPlayer>("m_LocationAnimation").CurrentAnimation == "location/current" && _driver.GetViewport().GuiGetHoveredControl() != location, $"透明区域触发悬停：{local}");
                _driver.GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true }, true);
                await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.ProcessFrame);
                _driver.GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false }, true);
                await MoveMouseAsync(position);
                Check(!pressed && GF.Procedure.CurrentProcedure is ProcedureMap, $"透明区域触发进关：{local}");
            }
        }
        finally
        {
            location.Pressed -= observe;
        }

        AnimationPlayer hover = map.GetNode<AnimationPlayer>("m_LocationAnimation");
        TextureRect glow = location.GetNode<TextureRect>("LocationGlow");
        Vector2 expanded = location.GetGlobalTransformWithCanvas() * (glow.GetTransform() * new Vector2(40, 87));
        await MoveMouseAsync(location.GetGlobalRect().GetCenter());
        for (int sample = 0; sample <= 5; sample++)
        {
            hover.Seek(sample / 5.0, true);
            await MoveMouseAsync(expanded);
            Check(hover.IsPlaying() && glow.Visible && _driver.GetViewport().GuiGetHoveredControl() == location,
                "鼠标仍在放大图内时提前结束悬停");
        }
        await CaptureAsync("map_hover_expanded_hit");

        // 检查器进一步放大到按钮矩形外时，也应按真实子图变换命中。
        Vector2 originalScale = glow.Scale;
        try
        {
            glow.Scale = new Vector2(1.6f, 1.6f);
            // 左侧是操作菜单、右侧是水帘洞，探针取顶端树冠，避开相邻控件。
            Vector2 outerLocal = glow.GetTransform() * new Vector2(92, 25);
            Check(!new Rect2(Vector2.Zero, location.Size).HasPoint(outerLocal), "扩展探针未离开原按钮矩形");
            await MoveMouseAsync(location.GetGlobalTransformWithCanvas() * outerLocal);
            Check(hover.IsPlaying() && glow.Visible && _driver.GetViewport().GuiGetHoveredControl() == location,
                $"放大树冠未保持悬停：local={outerLocal}, hovered={_driver.GetViewport().GuiGetHoveredControl()?.GetPath()}, glow={glow.Visible}");
        }
        finally
        {
            glow.Scale = originalScale;
        }
        await MoveMouseAsync(new Vector2(30, 30));
        Check(hover.IsPlaying() && hover.CurrentAnimation == "location/current" && !glow.Visible &&
            location.GetNode<Sprite2D>("CurrentHint").Visible, "离开放大图后未恢复最新进度提示");
        await MoveMouseAsync(location.GlobalPosition + new Vector2(3, 3));
        Check(hover.CurrentAnimation == "location/current" && _driver.GetViewport().GuiGetHoveredControl() != location, "默认提示的透明区域错误接收输入");
    }

    /// <summary>
    /// 通过真实鼠标按下和松开验证点击、遮挡、坐标与按钮绑定。
    /// </summary>
    /// <param name="form">窗口。</param>
    /// <param name="path">按钮路径。</param>
    /// <param name="positionOverride">可选的视口点击位置；默认按钮中心。</param>
    /// <returns>点击分发任务。</returns>
    private async Task ClickAsync(Control form, string path, Vector2? positionOverride = null)
    {
        BaseButton button = form.GetNode<BaseButton>(path);
        Check(form.IsVisibleInTree() && button.IsVisibleInTree() && !button.Disabled, $"按钮不可点击：{path}");
        Vector2 position = positionOverride ?? button.GetGlobalRect().GetCenter();
        bool pressed = false;
        Action observePress = () => pressed = true;
        button.Pressed += observePress;
        await MoveMouseAsync(position);
        // 原生装备tooltip在下一帧关闭；移动与点击分帧，避免点击仍发给旧弹窗。
        for (int frame = 0; frame < 3; frame++)
        {
            await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Check(button.GetCursorShape() == Control.CursorShape.PointingHand, $"可点击按钮未显示手形光标：{form.Name}/{path}");
        _driver.GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        _driver.GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false }, true);
        for (int frame = 0; frame < 3; frame++)
        {
            await _driver.ToSignal(_driver.GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (GodotObject.IsInstanceValid(button))
        {
            button.Pressed -= observePress;
        }

        if (!pressed)
        {
            await CaptureAsync("click_failure");
        }

        Check(pressed, $"真实鼠标未触发按钮：{path} @ {position}，窗口 {form.Name} Z={form.ZIndex}，悬停节点 {_driver.GetViewport().GuiGetHoveredControl()?.GetPath()}");
    }

    /// <summary>
    /// 失败立即结束回归并保留首个证据。
    /// </summary>
    /// <param name="condition">期望条件。</param>
    /// <param name="message">失败原因。</param>
    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// 仅供原生 FSM 作用域验证的空状态。
    /// </summary>
    private sealed class ArchitectureProbeState : GameFramework.Fsm.FsmState<GameFramework.Procedure.IProcedureManager>
    {
    }

    /// <summary>
    /// 仅供测试验证注册生命周期，不让正式钱包为测试继承 Model。
    /// </summary>
    /// <param name="value">测试值。</param>
    /// <param name="failInit">是否在初始化时模拟故障。</param>
    /// <param name="failDeinit">是否在退出时模拟故障。</param>
    private sealed class ArchitectureProbeModel(int value, bool failInit = false, bool failDeinit = false) : AbstractModel
    {
        /// <summary>
        /// 只读测试值。
        /// </summary>
        public int Value { get; } = value;

        /// <summary>
        /// 实际初始化次数。
        /// </summary>
        public int InitCount { get; private set; }

        /// <summary>
        /// 实际解除次数。
        /// </summary>
        public int DeinitCount { get; private set; }

        /// <inheritdoc />
        protected override void OnInit()
        {
            InitCount++;
            if (failInit)
            {
                throw new InvalidOperationException("Architecture probe init");
            }
        }

        /// <inheritdoc />
        protected override void OnDeinit()
        {
            DeinitCount++;
            if (failDeinit)
            {
                throw new InvalidOperationException("Architecture probe deinit");
            }
        }
    }

    /// <summary>
    /// 只供测试验证真实 System 注册事务；不访问游戏模型或节点。
    /// </summary>
    /// <param name="architecture">所属测试作用域。</param>
    /// <param name="failInit">是否在初始化时模拟故障。</param>
    /// <param name="failDeinit">是否在退出时模拟故障。</param>
    private sealed class ArchitectureProbeSystem(IArchitecture architecture, bool failInit, bool failDeinit)
        : AbstractSystem(architecture)
    {
        /// <summary>
        /// 实际初始化次数。
        /// </summary>
        public int InitCount { get; private set; }

        /// <summary>
        /// 实际解除次数。
        /// </summary>
        public int DeinitCount { get; private set; }

        /// <inheritdoc />
        protected override void OnInit()
        {
            InitCount++;
            if (failInit)
            {
                throw new InvalidOperationException("Architecture probe init");
            }
        }

        /// <inheritdoc />
        protected override void OnDeinit()
        {
            DeinitCount++;
            if (failDeinit)
            {
                throw new InvalidOperationException("Architecture probe deinit");
            }
        }
    }
}
