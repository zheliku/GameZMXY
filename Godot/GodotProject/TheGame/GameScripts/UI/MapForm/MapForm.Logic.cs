using GameLogic.Event.Navigation;
using System;
using GameFramework.Event;
using GameFramework.UI;
using GameLogic.Audio;
using GameLogic;
using GameLogic.Level;
using GameConfig.Sound;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Localization;
using GodotGameFramework.Sound;
using GodotGameFramework.UI;

namespace GameLogic.UI;

/// <summary>地图界面：显示旧项目地点图片，以点击事件提交所选关卡。</summary>
public partial class MapForm
{
    private const string SaveFeedbackAnimation = "feedback/show"; // 外置保存提示动画：停留后向上淡出。
    private bool m_IsOpen; // 地图只提交请求，无需持有档案。

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
            m_SaveFeedbackAnimation.AnimationFinished += OnSaveFeedbackFinished;
            m_Huaguoshan.Pressed += OnHuaguoshanClicked;
            m_Huaguoshan.MouseEntered += OnLocationMouseEntered;
            m_Huaguoshan.MouseExited += OnLocationMouseExited;
        }
    }

    /// <summary>绑定本次状态源并立即刷新页面。</summary>
    /// <param name="userData">本次窗口打开参数。</param>
    public void OnOpen(object userData)
    {
        if (m_IsOpen) GF.Event.Unsubscribe(ProfileSavedEventArgs.EventId, OnProfileSaved);
        m_IsOpen = true;
        GF.Event.Subscribe(ProfileSavedEventArgs.EventId, OnProfileSaved);
        m_Save.Disabled = false;
        m_Save.MouseDefaultCursorShape = CursorShape.PointingHand;
        m_Save.SelfModulate = Colors.White;
        m_Save.TooltipText = "保存游戏";
        ResetSaveFeedback();
        ResetLocationHover();
        Visible = true;
    }

    /// <summary>释放本次状态源引用，保留可复用的窗口节点。</summary>
    /// <param name="isShutdown">是否框架关停。</param>
    /// <param name="userData">关闭参数，不保存。</param>
    public void OnClose(bool isShutdown, object userData)
    {
        ReleaseFeedback();
        if (IsInstanceValid(m_LocationAnimation)) ResetLocationHover();
        if (!isShutdown && IsInstanceValid(this)) Visible = false;
    }

    /// <summary>回池时清除本次打开的引用和框架编号。</summary>
    public void OnRecycle()
    {
        ReleaseFeedback();
        if (IsInstanceValid(m_LocationAnimation)) ResetLocationHover();
        m_SerialId = 0;
        m_DepthInUIGroup = 0;
        m_PauseCoveredUIForm = true;
        if (IsInstanceValid(this)) Visible = false;
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

    /// <summary>重新绑定显式传入的状态源。</summary>
    /// <param name="userData">新打开参数；null 保持当前绑定。</param>
    public void OnRefocus(object userData)
    {
        if (userData != null) OnOpen(userData);
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
        if (!m_IsOpen) return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        GF.Event.Fire(this, TitleRequestedEventArgs.Create(SerialId));
    }

    /// <summary>请求流程提交当前档案，等待期间禁用重复点击。</summary>
    private void OnSaveClicked()
    {
        if (!m_IsOpen || m_Save.Disabled) return;
        m_Save.Disabled = true;
        m_Save.MouseDefaultCursorShape = CursorShape.Arrow;
        ResetSaveFeedback();
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        GF.Event.Fire(this, SaveRequestedEventArgs.Create(SerialId));
    }
    /// <summary>显示本窗口的保存结果，不保留池化参数。</summary>
    /// <param name="sender">当前地图流程。</param>
    /// <param name="args">本次写入结果。</param>
    private void OnProfileSaved(object sender, GameEventArgs args)
    {
        var result = (ProfileSavedEventArgs)args;
        if (!m_IsOpen || result.FormSerialId != SerialId) return;
        m_Save.Disabled = false;
        m_Save.MouseDefaultCursorShape = CursorShape.PointingHand;
        m_Save.SelfModulate = result.Succeeded ? new Color(0.65f, 1f, 0.65f) : new Color(1f, 0.55f, 0.55f);
        m_Save.TooltipText = result.Succeeded ? "游戏已保存" : "保存失败，点击重试";
        // 只有流程返回实际写盘结果后才展示，不把点击当作保存成功。
        m_SaveFeedback.Text = result.Succeeded ? "保存成功！" : "保存失败，请重试";
        m_SaveFeedback.SelfModulate = result.Succeeded ? Colors.White : new Color(1f, 0.55f, 0.55f);
        m_SaveFeedback.Visible = true;
        m_SaveFeedbackAnimation.Stop();
        m_SaveFeedbackAnimation.Play(SaveFeedbackAnimation);
        m_SaveFeedbackAnimation.Advance(0);
    }

    /// <summary>提示淡出后复位，下一次保存从完整文字开始。</summary>
    /// <param name="animationName">已播放完毕的动画名。</param>
    private void OnSaveFeedbackFinished(StringName animationName)
    {
        if (animationName == SaveFeedbackAnimation) ResetSaveFeedback();
    }

    /// <summary>隐藏提示并恢复位置和透明度；打开、保存开始及关闭时调用。</summary>
    private void ResetSaveFeedback()
    {
        m_SaveFeedbackAnimation.Play("feedback/RESET");
        m_SaveFeedbackAnimation.Advance(0);
        m_SaveFeedbackAnimation.Stop();
        m_SaveFeedback.Visible = false;
        m_SaveFeedback.Text = string.Empty;
    }

    /// <summary>关闭即解除保存反馈订阅并清理本次提示。</summary>
    private void ReleaseFeedback()
    {
        if (m_IsOpen) GF.Event.Unsubscribe(ProfileSavedEventArgs.EventId, OnProfileSaved);
        m_IsOpen = false;
        if (IsInstanceValid(m_SaveFeedbackAnimation) && IsInstanceValid(m_SaveFeedback)) ResetSaveFeedback();
    }
    /// <summary>提交花果山的配置主键。</summary>
    private void OnHuaguoshanClicked()
    {
        if (!m_IsOpen) return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        GF.Event.Fire(this, LevelSelectedEventArgs.Create(SerialId, 1));
    }

    /// <summary>悬停时轻微放大，并循环闪烁原图中的黄色光晕。</summary>
    private void OnLocationMouseEntered()
    {
        if (!m_IsOpen) return;
        m_LocationAnimation.Play("location/hover");
        m_LocationAnimation.Advance(0);
    }

    /// <summary>鼠标离开时恢复原图和比例。</summary>
    private void OnLocationMouseExited() => ResetLocationHover();

    /// <summary>清除悬停参数，窗口复用时从普通图像开始。</summary>
    private void ResetLocationHover()
    {
        m_LocationAnimation.Play("location/RESET");
        m_LocationAnimation.Advance(0);
        m_LocationAnimation.Stop();
    }
}
