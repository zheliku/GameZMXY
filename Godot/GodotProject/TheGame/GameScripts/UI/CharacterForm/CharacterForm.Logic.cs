using GameLogic.Event.Navigation;
using System;
using GameFramework.UI;
using GameLogic.Audio;
using GameConfig.Sound;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Localization;
using GodotGameFramework.Sound;
using GodotGameFramework.UI;

namespace GameLogic.UI;

/// <summary>四栏选角：已开放角色响应悬停和确认；未开放角色只作静态展示。</summary>
public partial class CharacterForm
{
    private bool m_IsOpen; // 静态选角页只在本次打开期间提交导航请求。

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
            m_Wukong.Pressed += OnConfirmClicked;
            m_Wukong.MouseEntered += OnWukongMouseEntered;
            m_Wukong.MouseExited += ResetRoleHover;
        }
    }

    /// <summary>开放当前窗口的角色确认请求。</summary>
    /// <param name="userData">本次窗口打开参数。</param>
    public void OnOpen(object userData)
    {
        ResetRoleHover();
        m_IsOpen = true;
        Visible = true;
    }

    /// <summary>释放本次状态源引用，保留可复用的窗口节点。</summary>
    /// <param name="isShutdown">是否框架关停。</param>
    /// <param name="userData">关闭参数，不保存。</param>
    public void OnClose(bool isShutdown, object userData)
    {
        m_IsOpen = false;
        if (!isShutdown) ResetRoleHover();
        if (!isShutdown && IsInstanceValid(this)) Visible = false;
    }

    /// <summary>回池时清除本次打开的引用和框架编号。</summary>
    public void OnRecycle()
    {
        m_IsOpen = false;
        ResetRoleHover();
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

    /// <summary>确认出战角色，进入地图。</summary>
    private void OnConfirmClicked()
    {
        if (!m_IsOpen) return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        GF.Event.Fire(this, CharacterConfirmedEventArgs.Create(SerialId));
    }

    /// <summary>预览已开放的悟空，将 1P 放到卡片顶部中央。</summary>
    private void OnWukongMouseEntered()
    {
        if (!m_IsOpen) return;
        ((ShaderMaterial)m_Wukong.Material).SetShaderParameter("grayscale", 0f);
        m_PlayerOne.GlobalPosition = new Vector2(m_Wukong.GetGlobalRect().GetCenter().X - m_PlayerOne.Size.X / 2,
            m_PlayerOne.GlobalPosition.Y);
        m_PlayerOne.Visible = true;
    }

    /// <summary>离开卡片或关闭窗口时恢复灰度并隐藏玩家标记。</summary>
    private void ResetRoleHover()
    {
        ((ShaderMaterial)m_Wukong.Material).SetShaderParameter("grayscale", 1f);
        m_PlayerOne.Visible = false;
    }
}
