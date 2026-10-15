using GameLogic.Architecture;
using GameLogic.Event.Navigation;
using System;
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

/// <summary>开始界面：沿用旧项目字体与分隔图片，通过具名点击事件通知流程。</summary>
public partial class StartForm : IController
{
    /// <summary>读取所属游戏流程的架构入口。</summary>
    /// <returns>当前流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();


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
            m_Start.Pressed += OnStartClicked;
            m_Settings.Pressed += OnSettingsClicked;
            m_Help.Pressed += OnHelpClicked;
            m_Notice.Pressed += OnNoticeClicked;
        }
    }

    /// <summary>显示开始页并清除上次的帮助说明。</summary>
    /// <param name="userData">不使用，无需空的 Data 包装。</param>
    public void OnOpen(object userData)
    {
        m_Status.Text = "";
        Visible = true;
    }

    /// <summary>释放本次状态源引用，保留可复用的窗口节点。</summary>
    /// <param name="isShutdown">是否框架关停。</param>
    /// <param name="userData">关闭参数，不保存。</param>
    public void OnClose(bool isShutdown, object userData)
    {
        if (!isShutdown && IsInstanceValid(this))
            Visible = false;
    }

    /// <summary>回池时清除本次打开的引用和框架编号。</summary>
    public void OnRecycle()
    {
        m_SerialId = 0;
        m_DepthInUIGroup = 0;
        m_PauseCoveredUIForm = true;
        if (IsInstanceValid(this))
            Visible = false;
    }

    /// <summary>界面暂停；点击导航不依赖轮询。</summary>
    public void OnPause() { }

    /// <summary>恢复界面暂停。</summary>
    public void OnResume() { }

    /// <summary>窗口被覆盖。</summary>
    public void OnCover() { }

    /// <summary>窗口恢复显示。</summary>
    public void OnReveal() { }

    /// <summary>框架要求的更新入口；数值和导航通过事件更新。</summary>
    /// <param name="elapseSeconds">游戏帧间隔秒数。</param>
    /// <param name="realElapseSeconds">真实帧间隔秒数。</param>
    public void OnUpdate(float elapseSeconds, float realElapseSeconds) { }

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

    /// <summary>请求进入读档与选角流程。</summary>
    private void OnStartClicked()
    {
        if (!GF.UI.HasUIForm(SerialId))
            return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        GF.Event.Fire(this, StartGameRequestedEventArgs.Create(SerialId));
    }

    /// <summary>请当前流程打开声音设置。</summary>
    private void OnSettingsClicked()
    {
        if (!GF.UI.HasUIForm(SerialId))
            return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        GF.Event.Fire(this, SettingsRequestedEventArgs.Create(SerialId));
    }

    /// <summary>显示本项目已经实现的操作说明。</summary>
    private void OnHelpClicked()
    {
        if (!GF.UI.HasUIForm(SerialId))
            return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        m_Status.Text = "A / D 移动 · 连按奔跑\nJ 攻击 · K 跳跃 · 空格 无双\n左下角设置中返回地图";
    }

    /// <summary>说明目前可玩的内容。</summary>
    private void OnNoticeClicked()
    {
        if (!GF.UI.HasUIForm(SerialId))
            return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        m_Status.Text = "当前开放悟空与花果山。\n仅供个人学习，禁止商用。";
    }
}
