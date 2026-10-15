using GameConfig.Constant;
using GameLogic.Save;
using GameLogic.Architecture;
using GameLogic.Audio;
using GameFramework.Procedure;
using System.Threading;
using GameLogic.Event.Navigation;
using System;
using GameConfig;
using GameConfig.Sound;
using GameFramework.Event;
using GameLogic;
using GodotGameFramework;
using GodotGameFramework.UI;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>选角流程：确认进入地图，返回标题前等待最近一次存档完成。</summary>
public sealed class ProcedureCharacter : ProcedureBase
{
    private int m_FormSerialId; // 本周期窗口，含在途加载请求。
    private int m_MusicSerialId; // 本周期 GGF 音乐请求。
    private ProcedureOwner m_Owner; // 仅当前流程周期借用主 FSM。
    private CancellationTokenSource m_Cancellation; // 离开时使旧异步结果失效。
    private bool m_IsReturningToTitle; // 等待写盘期间拒绝重复导航。

    /// <summary>订阅本页面的点击事件后打开窗口。</summary>
    /// <param name="procedureOwner">流程状态机。</param>
    protected internal override void OnEnter(ProcedureOwner procedureOwner)
    {
        GF.Event.Subscribe(CharacterConfirmedEventArgs.EventId, OnCharacterConfirmed);
        GF.Event.Subscribe(TitleRequestedEventArgs.EventId, OnTitleRequested);
        base.OnEnter(procedureOwner);
        m_Owner = procedureOwner;
        m_IsReturningToTitle = false;
        m_Cancellation = new CancellationTokenSource();
        GF.Event.Subscribe(OpenUIFormSuccessEventArgs.EventId, OnFormOpened);
        GF.Event.Subscribe(OpenUIFormFailureEventArgs.EventId, OnFormFailed);
        m_FormSerialId = GF.UI.OpenUIForm(UIFormId.CharacterForm);
        m_MusicSerialId = GF.Sound.PlayConfigured(SoundId.MenuTheme, 0.3f);
    }

    /// <summary>退出时解除点击事件和本周期窗口。</summary>
    /// <param name="procedureOwner">流程状态机。</param>
    /// <param name="isShutdown">是否框架关停。</param>
    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        GF.Event.Unsubscribe(CharacterConfirmedEventArgs.EventId, OnCharacterConfirmed);
        GF.Event.Unsubscribe(TitleRequestedEventArgs.EventId, OnTitleRequested);

        m_Cancellation?.Cancel();
        m_Cancellation?.Dispose();
        m_Cancellation = null;
        GF.Event.Unsubscribe(OpenUIFormSuccessEventArgs.EventId, OnFormOpened);
        GF.Event.Unsubscribe(OpenUIFormFailureEventArgs.EventId, OnFormFailed);
        if (!isShutdown)
        {
            if (m_FormSerialId != 0 && (GF.UI.HasUIForm(m_FormSerialId) || GF.UI.IsLoadingUIForm(m_FormSerialId)))
                GF.UI.CloseUIForm(m_FormSerialId);
            if (m_MusicSerialId != 0)
                GF.Sound.StopSound(m_MusicSerialId, 0.2f);
        }
        m_FormSerialId = m_MusicSerialId = 0;
        m_Owner = null;
        base.OnLeave(procedureOwner, isShutdown);
    }

    /// <summary>确认当前出战角色后进入地图。</summary>
    /// <param name="sender">选角窗口。</param>
    /// <param name="args">本次确认请求。</param>
    private void OnCharacterConfirmed(object sender, GameEventArgs args)
    {
        if (!m_IsReturningToTitle && ((CharacterConfirmedEventArgs)args).FormSerialId == m_FormSerialId)
            ChangeState<ProcedureMap>(m_Owner);
    }

    /// <summary>保存完成后释放档案作用域；失败保留页面，允许重试。</summary>
    /// <param name="sender">选角窗口。</param>
    /// <param name="args">返回标题请求，仅在等待前读取。</param>
    private async void OnTitleRequested(object sender, GameEventArgs args)
    {
        if (((TitleRequestedEventArgs)args).FormSerialId != m_FormSerialId || m_IsReturningToTitle)
            return;
        m_IsReturningToTitle = true;
        var token = m_Cancellation.Token;
        var owner = m_Owner;
        var saveSystem = owner.GetSystem<SaveSystem>();
        try
        {
            // 捕获本周期引用，等待后不得访问已经换周期的流程字段。
            bool saved = await saveSystem.PendingWrite;
            token.ThrowIfCancellationRequested();
            if (saved)
                ChangeState<ProcedureTitle>(owner);
            else
                Log.Error("[{0}] 存档写入失败，已保留档案，暂不能返回标题。", GetType().Name);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            Log.Error("[{0}] 返回标题失败：{1}", GetType().Name, error);
        }
        finally
        {
            if (!token.IsCancellationRequested)
                m_IsReturningToTitle = false;
        }
    }
    /// <summary>本周期界面可见后关闭加载页。</summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="args">只在本次分发内读取的池化参数。</param>
    private void OnFormOpened(object sender, GameEventArgs args)
    {
        if (args is not OpenUIFormSuccessEventArgs success || success.UIForm.SerialId != m_FormSerialId)
            return;
        (GF.UI.GetUIForm(ResourcesCollectionConstant.UI_LoadingForm) as LoadingForm)?.CloseLoading();
    }

    /// <summary>报告当前窗口请求的装配错误。</summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="args">只在本次分发内读取的池化参数。</param>
    private void OnFormFailed(object sender, GameEventArgs args)
    {
        if (args is OpenUIFormFailureEventArgs failure && failure.SerialId == m_FormSerialId)
            Log.Error("[ProcedureCharacter] 界面加载失败：{0}", failure.ErrorMessage);
    }


}
