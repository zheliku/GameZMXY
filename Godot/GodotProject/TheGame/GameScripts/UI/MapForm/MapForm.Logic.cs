using GameLogic.Architecture;
using System;
using GameFramework.Event;
using GameFramework.UI;
using GameLogic.Audio;
using GameLogic;
using GameLogic.Level;
using GameLogic.Save;
using GameLogic.Profile;
using GameConfig.Sound;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Localization;
using GodotGameFramework.Sound;
using GodotGameFramework.UI;

namespace GameLogic.UI;

/// <summary>地图界面：显示旧项目地点图片，以点击事件提交所选关卡。</summary>
public partial class MapForm : IController
{
    private bool m_IsBound; // 当前打开周期拥有保存状态订阅，关闭后不再借用架构。

    /// <summary>由所属流程处理 TitleRequested；参数为本次窗口编号。</summary>
    public event Action<int> TitleRequested;

    /// <summary>由所属流程处理 SaveRequested；参数为本次窗口编号。</summary>
    public event Action<int> SaveRequested;

    /// <summary>由所属流程处理 LevelSelected；参数为本次窗口编号及选择编号。</summary>
    public event Action<int, int> LevelSelected;


    /// <summary>设置框架字段；按钮事件只在新建实例时绑定一次。</summary>
    /// <param name="serialId">窗口打开编号。</param>
    /// <param name="uiFormAssetName">窗口资源路径。</param>
    /// <param name="uiGroup">所属界面组。</param>
    /// <param name="pauseCoveredUIForm">是否暂停被覆盖的窗口。</param>
    /// <param name="isNewInstance">是否首次创建节点。</param>
    /// <param name="userData">本次打开参数。</param>
    public void OnInit(int serialId, string uiFormAssetName, IUIGroup uiGroup, bool pauseCoveredUIForm,
        bool isNewInstance, object userData)
    {
        m_SerialId = serialId;
        m_UIFormAssetName = uiFormAssetName;
        m_UIGroup = uiGroup;
        m_DepthInUIGroup = 0;
        m_PauseCoveredUIForm = pauseCoveredUIForm;
        UIStringKeys.ForEach(key => key.SetLocalizationValue());
        if (isNewInstance)
        {
            m_Back.Pressed += OnBackClicked;
            m_Save.Pressed += OnSaveClicked;
            m_Huaguoshan.Pressed += OnHuaguoshanClicked;
            m_Shuiliandong.Pressed += OnShuiliandongClicked;
            m_Taohuayuan.Pressed += OnTaohuayuanClicked;
            m_Huaguoshan.MouseEntered += OnLocationMouseEntered;
            m_Huaguoshan.MouseExited += OnLocationMouseExited;
            m_Shuiliandong.MouseEntered += OnShuiliandongMouseEntered;
            m_Shuiliandong.MouseExited += OnLocationMouseExited;
            m_Taohuayuan.MouseEntered += OnTaohuayuanMouseEntered;
            m_Taohuayuan.MouseExited += OnLocationMouseExited;
        }
    }

    /// <summary>绑定本次状态源并立即刷新页面。</summary>
    /// <param name="userData">本次窗口打开参数。</param>
    public void OnOpen(object userData)
    {
        Unbind();
        m_IsBound = true;
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
        m_Save.Disabled = false;
        m_Save.MouseDefaultCursorShape = CursorShape.PointingHand;
        m_Save.SelfModulate = Colors.White;
        m_Save.TooltipText = "保存游戏";
        ResetLocationHover();
        PlayCurrentLocationHint();
        Visible = true;
    }

    /// <summary>释放本次状态源引用，保留可复用的窗口节点。</summary>
    /// <param name="isShutdown">是否框架关停。</param>
    /// <param name="userData">关闭参数，不保存。</param>
    public void OnClose(bool isShutdown, object userData)
    {
        TitleRequested = null;
        SaveRequested = null;
        LevelSelected = null;
        Unbind();
        if (IsInstanceValid(m_LocationAnimation))
            ResetLocationHover();
        if (!isShutdown && IsInstanceValid(this))
            Visible = false;
    }

    /// <summary>回池时清除本次打开的引用和框架编号。</summary>
    public void OnRecycle()
    {
        Unbind();
        if (IsInstanceValid(m_LocationAnimation))
            ResetLocationHover();
        m_SerialId = 0;
        m_DepthInUIGroup = 0;
        m_PauseCoveredUIForm = true;
        if (IsInstanceValid(this))
            Visible = false;
    }

    /// <summary>界面暂停；点击导航不依赖轮询。</summary>
    public void OnPause()
    {
    }

    /// <summary>恢复界面暂停。</summary>
    public void OnResume()
    {
    }

    /// <summary>窗口被覆盖。</summary>
    public void OnCover()
    {
    }

    /// <summary>窗口恢复显示。</summary>
    public void OnReveal()
    {
    }

    /// <summary>框架要求的更新入口；数值和导航通过事件更新。</summary>
    /// <param name="elapseSeconds">游戏帧间隔秒数。</param>
    /// <param name="realElapseSeconds">真实帧间隔秒数。</param>
    public void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
    }

    /// <summary>读取所属游戏流程的架构入口。</summary>
    /// <returns>当前流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();

    /// <summary>只在本周期仍拥有订阅时退订；回池后的重复清理不查询旧架构。</summary>
    private void Unbind()
    {
        if (!m_IsBound)
            return;
        m_IsBound = false;
        this.GetModel<SaveModel>().ManualSave.UnRegister(RefreshManualSave);
        this.GetModel<ProfileModel>().HighestClearedLevelId.UnRegister(RefreshLevelProgress);
    }

    /// <summary>重新绑定显式传入的状态源。</summary>
    /// <param name="userData">新打开参数；null 保持当前绑定。</param>
    public void OnRefocus(object userData)
    {
        if (userData != null)
            OnOpen(userData);
    }

    /// <summary>记录框架分配的窗口层级。</summary>
    /// <param name="uiGroupDepth">界面组深度。</param>
    /// <param name="depthInUIGroup">窗口在组内的深度。</param>
    public void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
    {
        m_DepthInUIGroup = depthInUIGroup;
        ZIndex = depthInUIGroup;
    }

    /// <summary>返回主菜单。</summary>
    private void OnBackClicked()
    {
        if (!GF.UI.HasUIForm(SerialId))
            return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        TitleRequested?.Invoke(SerialId);
    }

    /// <summary>请求流程提交当前档案，等待期间禁用重复点击。</summary>
    private void OnSaveClicked()
    {
        if (!GF.UI.HasUIForm(SerialId) || m_Save.Disabled)
            return;
        m_Save.Disabled = true;
        m_Save.MouseDefaultCursorShape = CursorShape.Arrow;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        SaveRequested?.Invoke(SerialId);
    }

    /// <summary>显示属于本窗口的保存阶段和结果。</summary>
    /// <param name="result">模型已提交的保存状态。</param>
    private void RefreshManualSave(ManualSaveState result)
    {
        if (!GF.UI.HasUIForm(SerialId) || result.FormSerialId != SerialId)
            return;
        m_Save.Disabled = result.Phase == SaveWritePhase.Writing;
        m_Save.MouseDefaultCursorShape = m_Save.Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
        if (m_Save.Disabled)
        {
            m_Save.TooltipText = "正在保存";
            return;
        }
        m_Save.SelfModulate = result.Phase == SaveWritePhase.Succeeded ? new Color(0.65f, 1f, 0.65f) : new Color(1f, 0.55f, 0.55f);
        m_Save.TooltipText = result.Phase == SaveWritePhase.Succeeded ? "游戏已保存" : "保存失败，点击重试";
    }

    /// <summary>提交花果山的配置主键。</summary>
    private void OnHuaguoshanClicked() => SelectLevel(1);

    /// <summary>提交水帘洞的配置主键。</summary>
    private void OnShuiliandongClicked() => SelectLevel(2);

    /// <summary>提交桃花源的配置主键。</summary>
    private void OnTaohuayuanClicked() => SelectLevel(3);

    /// <summary>由地图流程处理已有配置的关卡请求。</summary>
    /// <param name="levelId">地图地点对应的关卡编号。</param>
    private void SelectLevel(int levelId)
    {
        if (!GF.UI.HasUIForm(SerialId) || !this.GetSystem<ProfileSystem>().CanEnterLevel(levelId))
            return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        LevelSelected?.Invoke(SerialId, levelId);
    }

    /// <summary>悬停时轻微放大，并循环闪烁原图中的黄色光晕。</summary>
    private void OnLocationMouseEntered() => PlayLocationHover(1, "location/hover");

    /// <summary>播放水帘洞的原图轮廓悬停。</summary>
    private void OnShuiliandongMouseEntered() => PlayLocationHover(2, "shuiliandong/hover");

    /// <summary>播放桃花源的原图轮廓悬停。</summary>
    private void OnTaohuayuanMouseEntered() => PlayLocationHover(3, "taohuayuan/hover");

    /// <summary>只有当前地点放大，其他地点恢复原来的尺寸和命中轮廓。</summary>
    /// <param name="animation">原生地点悬停动画。</param>
    /// <param name="levelId">地点对应的关卡，锁定时忽略迟到信号。</param>
    private void PlayLocationHover(int levelId, string animation)
    {
        if (!GF.UI.HasUIForm(SerialId) || !this.GetSystem<ProfileSystem>().CanEnterLevel(levelId))
            return;
        ResetLocationHover();
        m_LocationAnimation.Play(animation);
        m_LocationAnimation.Advance(0);
    }

    /// <summary>鼠标离开后继续显示最新开放关卡的边缘闪烁。</summary>
    private void OnLocationMouseExited()
    {
        ResetLocationHover();
        if (GF.UI.HasUIForm(SerialId))
            PlayCurrentLocationHint();
        else
            ResetLocationHover();
    }

    /// <summary>只闪烁本档案最新开放且已实现的地点，历史关卡仍可悬停和重玩。</summary>
    private void PlayCurrentLocationHint()
    {
        string animation = this.GetSystem<ProfileSystem>().LatestLevelId switch
        {
            1 => "location/current",
            2 => "shuiliandong/current",
            3 => "taohuayuan/current",
            _ => throw new InvalidOperationException("最新开放关卡缺少地图动画。")
        };
        m_LocationAnimation.Play(animation);
        m_LocationAnimation.Advance(0);
    }

    /// <summary>进度改变或窗口重开时同步可交互地点，并清除旧提示。</summary>
    /// <param name="highestClearedLevelId">本次通知的最远通关记录。</param>
    private void RefreshLevelProgress(int highestClearedLevelId)
    {
        foreach (var (button, id) in new[] { (m_Huaguoshan, 1), (m_Shuiliandong, 2), (m_Taohuayuan, 3) })
        {
            button.Disabled = !this.GetSystem<ProfileSystem>().CanEnterLevel(id);
            button.MouseDefaultCursorShape = button.Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
            button.TooltipText = button.Disabled ? "通关前一关后开放" : string.Empty;
        }
        ResetLocationHover();
        PlayCurrentLocationHint();
    }

    /// <summary>清除悬停参数，窗口复用时从普通图像开始。</summary>
    private void ResetLocationHover()
    {
        foreach (string animation in new[] { "location/RESET", "shuiliandong/RESET", "taohuayuan/RESET" })
        {
            m_LocationAnimation.Play(animation);
            m_LocationAnimation.Advance(0);
        }
        m_LocationAnimation.Stop();
    }
}
