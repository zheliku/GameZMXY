using GameFramework.Event;
using GameFramework.UI;
using GameLogic.Event.Save;
using Godot;
using GodotGameFramework;

namespace GameLogic.UI;

/// <summary>应用级非交互保存提示，跨地图与关卡流程保持，由真实写盘成功事件触发。</summary>
public partial class SaveNoticeForm
{
    private bool m_IsOpen; // 当前是否已订阅保存事实。
    private ulong m_LastTick; // 提示按真实时间结束，背包暂停时也不滞留。

    /// <summary>初始化框架身份与一次性动画回调。</summary>
    /// <param name="serialId">窗口编号。</param>
    /// <param name="uiFormAssetName">场景路径。</param>
    /// <param name="uiGroup">提示组。</param>
    /// <param name="pauseCoveredUIForm">不暂停下层窗口。</param>
    /// <param name="isNewInstance">是否首次创建。</param>
    /// <param name="userData">没有打开参数。</param>
    public void OnInit(int serialId, string uiFormAssetName, IUIGroup uiGroup, bool pauseCoveredUIForm,
        bool isNewInstance, object userData)
    {
        m_SerialId = serialId;
        m_UIFormAssetName = uiFormAssetName;
        m_UIGroup = uiGroup;
        m_PauseCoveredUIForm = pauseCoveredUIForm;
        if (isNewInstance)
            m_SaveFeedbackAnimation.AnimationFinished += OnAnimationFinished;
    }

    /// <summary>在应用预加载后开放监听，文字默认隐藏。</summary>
    /// <param name="userData">没有打开参数。</param>
    public void OnOpen(object userData)
    {
        ReleaseSubscription();
        m_IsOpen = true;
        GF.Event.Subscribe(SaveSucceededEventArgs.EventId, OnSaveSucceeded);
        Visible = true;
        ResetFeedback();
    }

    /// <summary>写盘成功立即重播提示，重复保存从完整透明度开始。</summary>
    /// <param name="sender">保存模块。</param>
    /// <param name="args">当帧有效的成功事实。</param>
    private void OnSaveSucceeded(object sender, GameEventArgs args)
    {
        m_SaveFeedback.Text = "保存成功！";
        m_SaveFeedback.Visible = true;
        m_SaveFeedbackAnimation.Stop();
        m_SaveFeedbackAnimation.Play("feedback/show");
        m_SaveFeedbackAnimation.Advance(0);
        m_LastTick = Time.GetTicksUsec();
    }

    /// <summary>提示结束后恢复位置、透明度并隐藏。</summary>
    /// <param name="name">结束的动画名称。</param>
    private void OnAnimationFinished(StringName name)
    {
        if (name == "feedback/show")
            ResetFeedback();
    }

    /// <summary>复位外置动画，避免下一次提示继承淡出状态。</summary>
    private void ResetFeedback()
    {
        m_SaveFeedbackAnimation.Play("feedback/RESET");
        m_SaveFeedbackAnimation.Advance(0);
        m_SaveFeedbackAnimation.Stop();
        m_SaveFeedback.Visible = false;
    }

    /// <summary>关闭时对称解除监听，框架关停不再访问节点。</summary>
    /// <param name="isShutdown">是否框架关停。</param>
    /// <param name="userData">关闭参数。</param>
    public void OnClose(bool isShutdown, object userData)
    {
        ReleaseSubscription();
        if (!isShutdown)
            ResetFeedback();
    }

    /// <summary>回池清除框架编号与订阅。</summary>
    public void OnRecycle()
    {
        ReleaseSubscription();
        m_SerialId = m_DepthInUIGroup = 0;
    }

    /// <summary>解除本次显示期间的订阅。</summary>
    private void ReleaseSubscription()
    {
        if (m_IsOpen)
            GF.Event.Unsubscribe(SaveSucceededEventArgs.EventId, OnSaveSucceeded);
        m_IsOpen = false;
    }

    /// <summary>手动按真实时间推进提示，不随战斗暂停冻结在其他窗口上。</summary>
    /// <param name="elapseSeconds">游戏秒数。</param>
    /// <param name="realElapseSeconds">真实秒数。</param>
    public void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        if (!m_IsOpen || !m_SaveFeedbackAnimation.IsPlaying())
            return;
        ulong now = Time.GetTicksUsec();
        m_SaveFeedbackAnimation.Advance((now - m_LastTick) / 1_000_000d);
        m_LastTick = now;
    }
    /// <summary>提示不会主动暂停。</summary>
    public void OnPause() { }
    /// <summary>恢复时没有额外状态。</summary>
    public void OnResume() { }
    /// <summary>覆盖不改变正在播放的提示。</summary>
    public void OnCover() { }
    /// <summary>重新显示不重播。</summary>
    public void OnReveal() { }
    /// <summary>复用同一应用级监听。</summary>
    /// <param name="userData">没有打开参数。</param>
    public void OnRefocus(object userData) { }
    /// <summary>应用提示组拥有层级，窗口记录组内深度。</summary>
    /// <param name="uiGroupDepth">组层级。</param>
    /// <param name="depthInUIGroup">组内深度。</param>
    public void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
    {
        m_DepthInUIGroup = depthInUIGroup;
        ZIndex = depthInUIGroup;
    }
}
