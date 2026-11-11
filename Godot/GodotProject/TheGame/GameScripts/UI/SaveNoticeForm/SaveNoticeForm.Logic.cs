using GameFramework.Event;
using GameFramework.UI;
using GameLogic;
using GameLogic.Event.Equipment;
using GameLogic.Event.Save;
using Godot;
using GodotGameFramework;

namespace GameLogic.UI;

/// <summary>
/// 应用级非交互提示，跨地图与关卡流程保持，消费保存成功和直接装备奖励结果。
/// </summary>
public partial class SaveNoticeForm
{
    private static readonly StringName s_showAnimation = "feedback/show"; // 外置保存提示的播放轨道。
    private static readonly StringName s_resetAnimation = "feedback/RESET"; // 外置提示的初始位置与透明度。
    private ulong _lastTick; // 提示按真实时间结束，背包暂停时也不滞留。

    /// <summary>
    /// 初始化框架身份与一次性动画回调。
    /// </summary>
    /// <param name="serialId">窗口编号。</param>
    /// <param name="uiFormAssetName">场景路径。</param>
    /// <param name="uiGroup">提示组。</param>
    /// <param name="pauseCoveredUIForm">不暂停下层窗口。</param>
    /// <param name="isNewInstance">是否首次创建。</param>
    /// <param name="userData">没有打开参数。</param>
    public void OnInit(int serialId, string uiFormAssetName, IUIGroup uiGroup, bool pauseCoveredUIForm,
        bool isNewInstance, object userData)
    {
        _serialId = serialId;
        _uiFormAssetName = uiFormAssetName;
        _uiGroup = uiGroup;
        _pauseCoveredUIForm = pauseCoveredUIForm;
        if (isNewInstance)
        {
            _saveFeedbackAnimation.AnimationFinished += OnAnimationFinished;
        }
    }

    /// <summary>
    /// 在应用预加载后开放监听，文字默认隐藏。
    /// </summary>
    /// <param name="userData">没有打开参数。</param>
    public void OnOpen(object userData)
    {
        if (GF.Event.Check(SaveSucceededEventArgs.EventId, OnSaveSucceeded))
        {
            GF.Event.Unsubscribe(SaveSucceededEventArgs.EventId, OnSaveSucceeded);
        }

        GF.Event.Subscribe(SaveSucceededEventArgs.EventId, OnSaveSucceeded);
        if (GF.Event.Check(EquipmentRewardEventArgs.EventId, OnEquipmentReward))
        {
            GF.Event.Unsubscribe(EquipmentRewardEventArgs.EventId, OnEquipmentReward);
        }

        GF.Event.Subscribe(EquipmentRewardEventArgs.EventId, OnEquipmentReward);
        Visible = true;
        ResetFeedback();
    }

    /// <summary>
    /// 关闭时对称解除监听，框架关停不再访问节点。
    /// </summary>
    /// <param name="isShutdown">是否框架关停。</param>
    /// <param name="userData">关闭参数。</param>
    public void OnClose(bool isShutdown, object userData)
    {
        if (GF.Event.Check(SaveSucceededEventArgs.EventId, OnSaveSucceeded))
        {
            GF.Event.Unsubscribe(SaveSucceededEventArgs.EventId, OnSaveSucceeded);
        }

        if (GF.Event.Check(EquipmentRewardEventArgs.EventId, OnEquipmentReward))
        {
            GF.Event.Unsubscribe(EquipmentRewardEventArgs.EventId, OnEquipmentReward);
        }

        if (!isShutdown)
        {
            ResetFeedback();
        }
    }

    /// <summary>
    /// 回池清除框架编号与订阅。
    /// </summary>
    public void OnRecycle()
    {
        _serialId = _depthInUIGroup = 0;
    }

    /// <summary>
    /// 手动按真实时间推进提示，不随战斗暂停冻结在其他窗口上。
    /// </summary>
    /// <param name="elapseSeconds">游戏秒数。</param>
    /// <param name="realElapseSeconds">真实秒数。</param>
    public void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        if (!GF.UI.HasUIForm(SerialId) || !_saveFeedbackAnimation.IsPlaying())
        {
            return;
        }

        ulong now = Time.GetTicksUsec();
        _saveFeedbackAnimation.Advance((now - _lastTick) / 1_000_000d);
        _lastTick = now;
    }

    /// <summary>
    /// 提示不会主动暂停。
    /// </summary>
    public void OnPause()
    {
    }

    /// <summary>
    /// 恢复时没有额外状态。
    /// </summary>
    public void OnResume()
    {
    }

    /// <summary>
    /// 覆盖不改变正在播放的提示。
    /// </summary>
    public void OnCover()
    {
    }

    /// <summary>
    /// 重新显示不重播。
    /// </summary>
    public void OnReveal()
    {
    }

    /// <summary>
    /// 写盘成功立即重播提示，重复保存从完整透明度开始。
    /// </summary>
    /// <param name="sender">保存系统。</param>
    /// <param name="args">当帧有效的成功事实。</param>
    private void OnSaveSucceeded(object sender, GameEventArgs args)
    {
        ShowMessage("保存成功！");
    }

    /// <summary>
    /// 以当前配置名称显示直接奖励结果，不持有池化事件或发奖实体。
    /// </summary>
    /// <param name="sender">提交奖励的实体。</param>
    /// <param name="args">仅在本次分发期间有效的奖励结果。</param>
    private void OnEquipmentReward(object sender, GameEventArgs args)
    {
        var reward = (EquipmentRewardEventArgs)args;
        string name = ConfigSystem.Instance.Tables.TbEquipmentConfig.Get(reward.EquipmentId).NameCn;
        ShowMessage(reward.Collected ? $"获得 {name}" : $"背包已满，无法获得 {name}");
    }

    /// <summary>
    /// 复用居中淡出提示；暂停中的技能窗口也按真实时间播放。
    /// </summary>
    /// <param name="message">本次提示文本。</param>
    public void ShowMessage(string message)
    {
        _saveFeedback.Text = message;
        _saveFeedback.Visible = true;
        _saveFeedbackAnimation.Stop();
        _saveFeedbackAnimation.Play(s_showAnimation);
        _saveFeedbackAnimation.Advance(0);
        _lastTick = Time.GetTicksUsec();
    }

    /// <summary>
    /// 提示结束后恢复位置、透明度并隐藏。
    /// </summary>
    /// <param name="name">结束的动画名称。</param>
    private void OnAnimationFinished(StringName name)
    {
        if (name == s_showAnimation)
        {
            ResetFeedback();
        }
    }

    /// <summary>
    /// 复位外置动画，避免下一次提示继承淡出状态。
    /// </summary>
    private void ResetFeedback()
    {
        _saveFeedbackAnimation.Play(s_resetAnimation);
        _saveFeedbackAnimation.Advance(0);
        _saveFeedbackAnimation.Stop();
        _saveFeedback.Visible = false;
    }

    /// <summary>
    /// 复用同一应用级监听。
    /// </summary>
    /// <param name="userData">没有打开参数。</param>
    public void OnRefocus(object userData)
    {
    }

    /// <summary>
    /// 应用提示组拥有层级，窗口记录组内深度。
    /// </summary>
    /// <param name="uiGroupDepth">组层级。</param>
    /// <param name="depthInUIGroup">组内深度。</param>
    public void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
    {
        _depthInUIGroup = depthInUIGroup;
        ZIndex = depthInUIGroup;
    }
}
