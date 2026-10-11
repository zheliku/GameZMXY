using System;
using GameFramework.UI;
using Godot;
using GodotGameFramework;
using GodotGameFramework.UI;

namespace GameLogic;

/// <summary>池化确认提示，关闭即释放回调；按钮订阅只建立一次。</summary>
public partial class QuestionTips : ITips
{
    private bool m_IsOpen; // 关闭的池实例不响应点击。
    private Action m_Cancel; // 本周期取消回调。
    private Action m_Confirm; // 本周期确认回调。

    /// <summary>设置框架字段；按钮事件只在新建实例时绑定一次。</summary>
    /// <param name="serialId">窗口打开编号。</param>
    /// <param name="uiFormAssetName">窗口资源路径。</param>
    /// <param name="uiGroup">所属界面组。</param>
    /// <param name="pauseCoveredUIForm">是否暂停被覆盖的窗口。</param>
    /// <param name="isNewInstance">是否首次创建节点。</param>
    /// <param name="userData">本次打开参数。</param>
    public void OnInit(int serialId, string uiFormAssetName, IUIGroup uiGroup, bool pauseCoveredUIForm, bool isNewInstance, object userData)
    {
        m_SerialId = serialId;
        m_UIFormAssetName = uiFormAssetName;
        m_UIGroup = uiGroup;
        m_DepthInUIGroup = 0;
        m_PauseCoveredUIForm = pauseCoveredUIForm;
        UIStringKeys.ForEach(key => key.SetLocalizationValue());
        if (!isNewInstance) return;
        m_Btn_Cancle.Pressed += OnCancelClicked;
        m_Btn_Confirm.Pressed += OnConfirmClicked;
    }

    /// <summary>设置正文、按钮及本周期回调。</summary>
    /// <param name="content">提示正文。</param>
    /// <param name="cancelTxt">取消按钮文本。</param>
    /// <param name="confirmTxt">确认按钮文本。</param>
    /// <param name="cancel">取消回调。</param>
    /// <param name="confirm">确认回调。</param>
    public void SetAction(string content, string cancelTxt, string confirmTxt, Action cancel, Action confirm)
    {
        m_Label.Text = content;
        m_Btn_Cancle.Text = cancelTxt;
        m_Btn_Confirm.Text = confirmTxt;
        m_Cancel = cancel;
        m_Confirm = confirm;
    }

    /// <summary>显示提示，回调由调用者随后通过 SetAction 绑定。</summary>
    /// <param name="userData">不使用的打开参数。</param>
    public void OnOpen(object userData)
    {
        m_IsOpen = true;
        Visible = true;
    }

    /// <summary>关闭即释放本周期回调。</summary>
    /// <param name="isShutdown">是否框架关停。</param>
    /// <param name="userData">不使用的关闭参数。</param>
    public void OnClose(bool isShutdown, object userData)
    {
        ReleaseCallbacks();
        if (!isShutdown && IsInstanceValid(this)) Visible = false;
    }

    /// <summary>回池清除编号与回调。</summary>
    public void OnRecycle()
    {
        ReleaseCallbacks();
        m_SerialId = 0;
        m_DepthInUIGroup = 0;
        m_PauseCoveredUIForm = true;
        if (IsInstanceValid(this)) Visible = false;
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
    /// <summary>提示参数通过 SetAction 显式设置。</summary>
    /// <param name="userData">不使用的重新聚焦参数。</param>
    public void OnRefocus(object userData) { }
    /// <summary>记录框架分配的窗口层级。</summary>
    /// <param name="uiGroupDepth">界面组深度。</param>
    /// <param name="depthInUIGroup">窗口在组内的深度。</param>
    public void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
    {
        m_DepthInUIGroup = depthInUIGroup;
        ZIndex = depthInUIGroup;
    }

        /// <summary>取消前捕获回调，关闭会同步释放窗口引用。</summary>
        private void OnCancelClicked()
        {
            if (!m_IsOpen) return;
            Action callback = m_Cancel;
            GF.UI.CloseUIForm(this);
            callback?.Invoke();
        }

        /// <summary>确认前捕获回调，允许回调安全打开下一提示。</summary>
        private void OnConfirmClicked()
        {
            if (!m_IsOpen) return;
            Action callback = m_Confirm;
            GF.UI.CloseUIForm(this);
            callback?.Invoke();
        }

        /// <summary>关闭或回池时清除本周期回调。</summary>
        private void ReleaseCallbacks()
        {
            m_IsOpen = false;
            m_Cancel = m_Confirm = null;
        }

}
