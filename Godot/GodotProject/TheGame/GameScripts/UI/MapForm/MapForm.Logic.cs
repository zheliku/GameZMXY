using System;
using System.Collections.Generic;
using GameConfig.Level;
using GameConfig.Sound;
using GameFramework.Event;
using GameFramework.UI;
using GameLogic;
using GameLogic.Architecture;
using GameLogic.Audio;
using GameLogic.Config;
using GameLogic.Level;
using GameLogic.Profile;
using GameLogic.Save;
using GameLogic.UI.Widgets;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Localization;
using GodotGameFramework.Sound;
using GodotGameFramework.UI;

namespace GameLogic.UI;

/// <summary>
/// 地图界面：显示旧项目地点图片，以点击事件提交所选关卡。
/// </summary>
public partial class MapForm : IController
{
    private (MapLocationButton Button, StringName HoverAnimation,
        StringName CurrentAnimation, StringName ResetAnimation)[] _locations; // 本窗口唯一地点绑定，关卡身份归按钮原生场景。
    private bool _isBound; // 当前打开周期拥有保存状态订阅，关闭后不再借用架构。

    /// <summary>
    /// 由所属流程处理 SaveRequested；参数为本次窗口编号。
    /// </summary>
    public event Action<int> SaveRequested;

    /// <summary>
    /// 请求地图流程打开设置；窗口编号拒绝旧界面的迟到交互。
    /// </summary>
    public event Action<int> SettingsRequested;

    /// <summary>
    /// 请求地图流程打开技能目录；参数为本次地图窗口编号。
    /// </summary>
    public event Action<int> SkillsRequested;

    /// <summary>
    /// 由所属流程处理 LevelSelected；参数为本次窗口编号及选择编号。
    /// </summary>
    public event Action<int, int> LevelSelected;

    /// <summary>
    /// 设置框架字段；按钮事件只在新建实例时绑定一次。
    /// </summary>
    /// <param name="serialId">窗口打开编号。</param>
    /// <param name="uiFormAssetName">窗口资源路径。</param>
    /// <param name="uiGroup">所属界面组。</param>
    /// <param name="pauseCoveredUIForm">是否暂停被覆盖的窗口。</param>
    /// <param name="isNewInstance">是否首次创建节点。</param>
    /// <param name="userData">本次打开参数。</param>
    public void OnInit(int serialId, string uiFormAssetName, IUIGroup uiGroup, bool pauseCoveredUIForm,
        bool isNewInstance, object userData)
    {
        _serialId = serialId;
        _uiFormAssetName = uiFormAssetName;
        _uiGroup = uiGroup;
        _depthInUIGroup = 0;
        _pauseCoveredUIForm = pauseCoveredUIForm;
        UIStringKeys.ForEach(key => key.SetLocalizationValue());
        if (isNewInstance)
        {
            _settings.Pressed += OnSettingsClicked;
            _settings.MouseEntered += OnSettingsMouseEntered;
            _settings.MouseExited += OnSettingsMouseExited;
            _skills.Pressed += OnSkillsClicked;
            _skills.MouseEntered += OnSkillsMouseEntered;
            _skills.MouseExited += OnSkillsMouseExited;
            _save.Pressed += OnSaveClicked;
            InitializeLocations();
            ValidateLocations();
            BindLocationEvents();
        }
    }

    /// <summary>
    /// 绑定本次状态源并立即刷新页面。
    /// </summary>
    /// <param name="userData">本次窗口打开参数。</param>
    public void OnOpen(object userData)
    {
        Unbind();
        _isBound = true;
        try
        {
            this.GetModel<SaveModel>().ManualSave.RegisterWithInitValue(RefreshManualSave);
            this.GetModel<ProfileModel>().HighestClearedLevelId.RegisterWithInitValue(RefreshLevelProgress);
        }
        catch
        {
            Unbind();
            throw;
        }
        _save.Disabled = false;
        _save.MouseDefaultCursorShape = CursorShape.PointingHand;
        _save.SelfModulate = Colors.White;
        ResetLocationHover();
        PlayCurrentLocationHint();
        ResetSkillsHint();
        _settingsAnimation.Play("RESET");
        _settingsAnimation.Advance(0);
        Visible = true;
    }

    /// <summary>
    /// 释放本次状态源引用，保留可复用的窗口节点。
    /// </summary>
    /// <param name="isShutdown">是否框架关停。</param>
    /// <param name="userData">关闭参数，不保存。</param>
    public void OnClose(bool isShutdown, object userData)
    {
        SettingsRequested = null;
        SkillsRequested = null;
        SaveRequested = null;
        LevelSelected = null;
        Unbind();
        if (IsInstanceValid(_locationAnimation))
        {
            ResetLocationHover();
        }

        if (!isShutdown && IsInstanceValid(this))
        {
            Visible = false;
        }
    }

    /// <summary>
    /// 回池时清除本次打开的引用和框架编号。
    /// </summary>
    public void OnRecycle()
    {
        Unbind();
        if (IsInstanceValid(_locationAnimation))
        {
            ResetLocationHover();
        }

        _serialId = 0;
        _depthInUIGroup = 0;
        _pauseCoveredUIForm = true;
        if (IsInstanceValid(this))
        {
            Visible = false;
        }
    }

    /// <summary>
    /// 界面暂停；点击导航不依赖轮询。
    /// </summary>
    public void OnPause()
    {
    }

    /// <summary>
    /// 恢复界面暂停。
    /// </summary>
    public void OnResume()
    {
    }

    /// <summary>
    /// 窗口被覆盖。
    /// </summary>
    public void OnCover()
    {
    }

    /// <summary>
    /// 窗口恢复显示。
    /// </summary>
    public void OnReveal()
    {
        if (_isBound)
        {
            ResetSkillsHint();
        }
    }

    /// <summary>
    /// 框架要求的更新入口；数值和导航通过事件更新。
    /// </summary>
    /// <param name="elapseSeconds">游戏帧间隔秒数。</param>
    /// <param name="realElapseSeconds">真实帧间隔秒数。</param>
    public void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
    }

    /// <summary>
    /// 读取所属游戏流程的架构入口。
    /// </summary>
    /// <returns>当前流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();

    /// <summary>
    /// 只在本周期仍拥有订阅时退订；回池后的重复清理不查询旧架构。
    /// </summary>
    private void Unbind()
    {
        if (IsInstanceValid(_skillsArtwork))
        {
            _skillsArtwork.Stop();
        }
        if (!_isBound)
        {
            return;
        }

        _isBound = false;
        this.GetModel<SaveModel>().ManualSave.UnRegister(RefreshManualSave);
        this.GetModel<ProfileModel>().HighestClearedLevelId.UnRegister(RefreshLevelProgress);
    }

    /// <summary>
    /// 重新绑定显式传入的状态源。
    /// </summary>
    /// <param name="userData">新打开参数；null 保持当前绑定。</param>
    public void OnRefocus(object userData)
    {
        if (userData != null)
        {
            OnOpen(userData);
        }
    }

    /// <summary>
    /// 记录框架分配的窗口层级。
    /// </summary>
    /// <param name="uiGroupDepth">界面组深度。</param>
    /// <param name="depthInUIGroup">窗口在组内的深度。</param>
    public void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
    {
        _depthInUIGroup = depthInUIGroup;
        ZIndex = depthInUIGroup;
    }

    /// <summary>
    /// 请求流程提交当前档案，等待期间禁用重复点击。
    /// </summary>
    private void OnSaveClicked()
    {
        if (!GF.UI.HasUIForm(SerialId) || _save.Disabled)
        {
            return;
        }

        _save.Disabled = true;
        _save.MouseDefaultCursorShape = CursorShape.Arrow;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        SaveRequested?.Invoke(SerialId);
    }

    /// <summary>
    /// 显示属于本窗口的保存阶段和结果。
    /// </summary>
    /// <param name="result">模型已提交的保存状态。</param>
    private void RefreshManualSave(ManualSaveState result)
    {
        if (!GF.UI.HasUIForm(SerialId) || result.FormSerialId != SerialId)
        {
            return;
        }

        _save.Disabled = result.Phase == SaveWritePhase.Writing;
        _save.MouseDefaultCursorShape = _save.Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
        if (_save.Disabled)
        {
            return;
        }

        _save.SelfModulate = result.Phase == SaveWritePhase.Succeeded ? new Color(0.65f, 1f, 0.65f) : new Color(1f, 0.55f, 0.55f);
    }

    /// <summary>
    /// 由地图流程处理已有配置的关卡请求。
    /// </summary>
    /// <param name="levelId">地图地点对应的关卡编号。</param>
    private void SelectLevel(int levelId)
    {
        if (!GF.UI.HasUIForm(SerialId) || !this.GetSystem<ProfileSystem>().CanEnterLevel(levelId))
        {
            return;
        }

        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        LevelSelected?.Invoke(SerialId, levelId);
    }

    /// <summary>
    /// 播放指定按钮的悬停动画，其他地点恢复原来的尺寸和命中轮廓。
    /// </summary>
    /// <param name="button">发出悬停事件的地点按钮。</param>
    /// <exception cref="InvalidOperationException">按钮没有登记地点动画。</exception>
    private void PlayLocationHover(MapLocationButton button)
    {
        if (!GF.UI.HasUIForm(SerialId) || !this.GetSystem<ProfileSystem>().CanEnterLevel(button.LevelId))
        {
            return;
        }

        foreach ((MapLocationButton Button, StringName HoverAnimation, StringName CurrentAnimation, StringName ResetAnimation) location in _locations)
        {
            if (location.Button != button)
            {
                continue;
            }

            ResetLocationHover();
            _locationAnimation.Play(location.HoverAnimation);
            _locationAnimation.Advance(0);
            return;
        }

        throw new InvalidOperationException($"地图按钮未登记动画：{button.Name}");
    }

    /// <summary>
    /// 鼠标离开后继续显示最新开放关卡的边缘闪烁。
    /// </summary>
    private void OnLocationMouseExited()
    {
        ResetLocationHover();
        if (GF.UI.HasUIForm(SerialId))
        {
            PlayCurrentLocationHint();
        }
    }

    /// <summary>
    /// 只闪烁本档案最新开放且已实现的地点，历史关卡仍可悬停和重玩。
    /// </summary>
    private void PlayCurrentLocationHint()
    {
        int latest = this.GetSystem<ProfileSystem>().LatestLevelId;
        foreach ((MapLocationButton Button, StringName HoverAnimation, StringName CurrentAnimation, StringName ResetAnimation) location in _locations)
        {
            if (location.Button.LevelId != latest)
            {
                continue;
            }

            _locationAnimation.Play(location.CurrentAnimation);
            _locationAnimation.Advance(0);
            return;
        }

        throw new InvalidOperationException($"最新开放关卡缺少地图地点：{latest}");
    }

    /// <summary>
    /// 进度改变或窗口重开时同步可交互地点，并清除旧提示。
    /// </summary>
    /// <param name="highestClearedLevelId">本次通知的最远通关记录。</param>
    private void RefreshLevelProgress(int highestClearedLevelId)
    {
        foreach ((MapLocationButton Button, StringName HoverAnimation, StringName CurrentAnimation, StringName ResetAnimation) location in _locations)
        {
            MapLocationButton button = location.Button;
            button.Disabled = !this.GetSystem<ProfileSystem>().CanEnterLevel(button.LevelId);
            button.MouseDefaultCursorShape = button.Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
        }
        ResetLocationHover();
        PlayCurrentLocationHint();
    }

    /// <summary>
    /// 清除悬停参数，窗口复用时从普通图像开始。
    /// </summary>
    private void ResetLocationHover()
    {
        foreach ((MapLocationButton Button, StringName HoverAnimation, StringName CurrentAnimation, StringName ResetAnimation) location in _locations)
        {
            _locationAnimation.Play(location.ResetAnimation);
            _locationAnimation.Advance(0);
        }
        _locationAnimation.Stop();
    }

    /// <summary>
    /// 打开地图设置，导航和窗口请求生命周期交给所属流程。
    /// </summary>
    private void OnSettingsClicked()
    {
        if (!GF.UI.HasUIForm(SerialId))
        {
            return;
        }
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        SettingsRequested?.Invoke(SerialId);
    }

    /// <summary>
    /// 请求地图流程打开主动与被动技能购买窗口。
    /// </summary>
    private void OnSkillsClicked()
    {
        if (!GF.UI.HasUIForm(SerialId))
        {
            return;
        }

        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        SkillsRequested?.Invoke(SerialId);
    }

    /// <summary>
    /// 官方地图初入或从购技窗返回时，按余额选择静止普通态或可购发光。
    /// </summary>
    private void ResetSkillsHint()
    {
        ProfileModel profile = this.GetModel<ProfileModel>();
        _skillsArtwork.Stop();
        _skillsArtwork.Play(profile.GetSoul(profile.HeroOf(PlayerSlot.P1)).Value >= 160 ? "available" : "idle");
    }

    /// <summary>
    /// 设置令牌悬停时短暂提亮，不缩放图案和文字。
    /// </summary>
    private void OnSettingsMouseEntered() => _settingsAnimation.Play("hover");

    /// <summary>
    /// 移出设置令牌后恢复常态亮度。
    /// </summary>
    private void OnSettingsMouseExited()
    {
        _settingsAnimation.Play("RESET");
        _settingsAnimation.Advance(0);
    }

    /// <summary>
    /// 官方悬停切第2态，十五帧展开播一次后停在末帧。
    /// </summary>
    private void OnSkillsMouseEntered()
    {
        _skillsArtwork.Stop();
        _skillsArtwork.Play("hover");
    }

    /// <summary>
    /// 官方移出后恢复静止普通态，重新进入地图前不再显示可购发光。
    /// </summary>
    private void OnSkillsMouseExited()
    {
        _skillsArtwork.Stop();
        _skillsArtwork.Play("idle");
    }

    /// <summary>
    /// 集中声明地点按钮及三种原生动画；导航和图鉴共用按钮场景中的关卡编号。
    /// </summary>
    private void InitializeLocations()
    {
        _locations =
        [
            (_huaguoshan, "location/hover", "location/current", "location/RESET"),
            (_shuiliandong, "shuiliandong/hover", "shuiliandong/current", "shuiliandong/RESET"),
            (_taohuayuan, "taohuayuan/hover", "taohuayuan/current", "taohuayuan/RESET"),
            (_bianfuku, "bianfuku/hover", "bianfuku/current", "bianfuku/RESET"),
            (_qixingdong, "qixingdong/hover", "qixingdong/current", "qixingdong/RESET"),
            (_jialuoluo, "jialuoluo/hover", "jialuoluo/current", "jialuoluo/RESET"),
            (_qianan, "qianan/hover", "qianan/current", "qianan/RESET"),
            (_donghai, "donghai/hover", "donghai/current", "donghai/RESET"),
            (_shazhiwoliu, "shazhiwoliu/hover", "shazhiwoliu/current", "shazhiwoliu/RESET"),
            (_niumogu, "niumogu/hover", "niumogu/current", "niumogu/RESET")
        ];
    }

    /// <summary>
    /// 在窗口初始化时校验地点身份、动画和主线覆盖，不在运行期静默回退。
    /// </summary>
    /// <exception cref="InvalidOperationException">地点重复、身份无效、动画缺失或主线漏配。</exception>
    private void ValidateLocations()
    {
        TbLevelConfig levelDefinitions = this.GetModel<ConfigurationModel>().Tables.TbLevelConfig;
        var registeredLevels = new HashSet<int>();
        foreach ((MapLocationButton Button, StringName HoverAnimation, StringName CurrentAnimation, StringName ResetAnimation) location in _locations)
        {
            int levelId = location.Button.LevelId;
            if (levelDefinitions.GetOrDefault(levelId) == null || levelId == DragonPalaceRules.LevelId)
            {
                throw new InvalidOperationException($"地图地点必须对应可见主线关卡：{levelId}");
            }
            if (!registeredLevels.Add(levelId))
            {
                throw new InvalidOperationException($"地图地点重复：{levelId}");
            }
            if (!_locationAnimation.HasAnimation(location.HoverAnimation) ||
                !_locationAnimation.HasAnimation(location.CurrentAnimation) ||
                !_locationAnimation.HasAnimation(location.ResetAnimation))
            {
                throw new InvalidOperationException($"地图地点缺少原生动画：{levelId}");
            }
        }

        // 隐藏龙宫仍从场景入口进入；其他已配置关卡都必须有地图地点。
        foreach (LevelConfig level in levelDefinitions.DataList)
        {
            if (level.LevelId != DragonPalaceRules.LevelId && !registeredLevels.Contains(level.LevelId))
            {
                throw new InvalidOperationException($"主线关卡缺少地图地点：{level.LevelId}");
            }
        }
    }

    /// <summary>
    /// 只在首次创建窗口时绑定具名事件，点击与悬停的调用路径可直接查找。
    /// </summary>
    private void BindLocationEvents()
    {
        _huaguoshan.Pressed += OnHuaguoshanClicked;
        _shuiliandong.Pressed += OnShuiliandongClicked;
        _taohuayuan.Pressed += OnTaohuayuanClicked;
        _bianfuku.Pressed += OnBianfukuClicked;
        _qixingdong.Pressed += OnQixingdongClicked;
        _jialuoluo.Pressed += OnJialuoluoClicked;
        _qianan.Pressed += OnQiananClicked;
        _donghai.Pressed += OnDonghaiClicked;
        _shazhiwoliu.Pressed += OnShazhiwoliuClicked;
        _niumogu.Pressed += OnNiumoguClicked;

        _huaguoshan.MouseEntered += OnHuaguoshanMouseEntered;
        _huaguoshan.MouseExited += OnLocationMouseExited;
        _shuiliandong.MouseEntered += OnShuiliandongMouseEntered;
        _shuiliandong.MouseExited += OnLocationMouseExited;
        _taohuayuan.MouseEntered += OnTaohuayuanMouseEntered;
        _taohuayuan.MouseExited += OnLocationMouseExited;
        _bianfuku.MouseEntered += OnBianfukuMouseEntered;
        _bianfuku.MouseExited += OnLocationMouseExited;
        _qixingdong.MouseEntered += OnQixingdongMouseEntered;
        _qixingdong.MouseExited += OnLocationMouseExited;
        _jialuoluo.MouseEntered += OnJialuoluoMouseEntered;
        _jialuoluo.MouseExited += OnLocationMouseExited;
        _qianan.MouseEntered += OnQiananMouseEntered;
        _qianan.MouseExited += OnLocationMouseExited;
        _donghai.MouseEntered += OnDonghaiMouseEntered;
        _donghai.MouseExited += OnLocationMouseExited;
        _shazhiwoliu.MouseEntered += OnShazhiwoliuMouseEntered;
        _shazhiwoliu.MouseExited += OnLocationMouseExited;
        _niumogu.MouseEntered += OnNiumoguMouseEntered;
        _niumogu.MouseExited += OnLocationMouseExited;
    }

    /// <summary>
    /// 请求进入花果山，关卡编号取自按钮场景。
    /// </summary>
    private void OnHuaguoshanClicked() => SelectLevel(_huaguoshan.LevelId);

    /// <summary>
    /// 显示花果山的悬停动画。
    /// </summary>
    private void OnHuaguoshanMouseEntered() => PlayLocationHover(_huaguoshan);

    /// <summary>
    /// 请求进入水帘洞，关卡编号取自按钮场景。
    /// </summary>
    private void OnShuiliandongClicked() => SelectLevel(_shuiliandong.LevelId);

    /// <summary>
    /// 显示水帘洞的悬停动画。
    /// </summary>
    private void OnShuiliandongMouseEntered() => PlayLocationHover(_shuiliandong);

    /// <summary>
    /// 请求进入桃花源，关卡编号取自按钮场景。
    /// </summary>
    private void OnTaohuayuanClicked() => SelectLevel(_taohuayuan.LevelId);

    /// <summary>
    /// 显示桃花源的悬停动画。
    /// </summary>
    private void OnTaohuayuanMouseEntered() => PlayLocationHover(_taohuayuan);

    /// <summary>
    /// 请求进入蝙蝠窟，关卡编号取自按钮场景。
    /// </summary>
    private void OnBianfukuClicked() => SelectLevel(_bianfuku.LevelId);

    /// <summary>
    /// 显示蝙蝠窟的悬停动画。
    /// </summary>
    private void OnBianfukuMouseEntered() => PlayLocationHover(_bianfuku);

    /// <summary>
    /// 请求进入七星洞，关卡编号取自按钮场景。
    /// </summary>
    private void OnQixingdongClicked() => SelectLevel(_qixingdong.LevelId);

    /// <summary>
    /// 显示七星洞的悬停动画。
    /// </summary>
    private void OnQixingdongMouseEntered() => PlayLocationHover(_qixingdong);

    /// <summary>
    /// 请求进入迦楼罗之穴，关卡编号取自按钮场景。
    /// </summary>
    private void OnJialuoluoClicked() => SelectLevel(_jialuoluo.LevelId);

    /// <summary>
    /// 显示迦楼罗之穴的悬停动画。
    /// </summary>
    private void OnJialuoluoMouseEntered() => PlayLocationHover(_jialuoluo);

    /// <summary>
    /// 请求进入浅岸，关卡编号取自按钮场景。
    /// </summary>
    private void OnQiananClicked() => SelectLevel(_qianan.LevelId);

    /// <summary>
    /// 显示浅岸的悬停动画。
    /// </summary>
    private void OnQiananMouseEntered() => PlayLocationHover(_qianan);

    /// <summary>
    /// 请求进入东海，关卡编号取自按钮场景。
    /// </summary>
    private void OnDonghaiClicked() => SelectLevel(_donghai.LevelId);

    /// <summary>
    /// 显示东海的悬停动画。
    /// </summary>
    private void OnDonghaiMouseEntered() => PlayLocationHover(_donghai);

    /// <summary>
    /// 请求进入鲨之涡流，关卡编号取自按钮场景。
    /// </summary>
    private void OnShazhiwoliuClicked() => SelectLevel(_shazhiwoliu.LevelId);

    /// <summary>
    /// 显示鲨之涡流的悬停动画。
    /// </summary>
    private void OnShazhiwoliuMouseEntered() => PlayLocationHover(_shazhiwoliu);

    /// <summary>
    /// 请求进入牛魔谷，关卡编号取自按钮场景。
    /// </summary>
    private void OnNiumoguClicked() => SelectLevel(_niumogu.LevelId);

    /// <summary>
    /// 显示牛魔谷的悬停动画。
    /// </summary>
    private void OnNiumoguMouseEntered() => PlayLocationHover(_niumogu);

}
