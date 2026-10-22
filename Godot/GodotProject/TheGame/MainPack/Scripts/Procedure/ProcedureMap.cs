using IUIForm = GameFramework.UI.IUIForm;
using GameLogic.UI;
using GameConfig.Constant;
using GameLogic.Audio;
using GameFramework.Procedure;
using System.Threading;
using GameLogic.Config;
using GameLogic.Architecture;
using GameLogic.Save;
using GameLogic.Profile;
using GameLogic.Procedure.FlowData;
using System;
using GameConfig;
using GameConfig.Sound;
using GameFramework.Event;
using GameLogic;
using GodotGameFramework;
using GodotGameFramework.UI;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>地图流程：通过地点点击事件验证配置并创建关卡作用域。</summary>
public sealed class ProcedureMap : ProcedureBase
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
        base.OnEnter(procedureOwner);
        m_Owner = procedureOwner;
        m_IsReturningToTitle = false;
        m_Cancellation = new CancellationTokenSource();
        GF.Event.Subscribe(OpenUIFormSuccessEventArgs.EventId, OnFormOpened);
        GF.Event.Subscribe(OpenUIFormFailureEventArgs.EventId, OnFormFailed);
        m_FormSerialId = GF.UI.OpenUIForm(UIFormId.MapForm);
        SetFormCallbacks(GF.UI.GetUIForm(m_FormSerialId), bind: true);
        m_MusicSerialId = GF.Sound.PlayConfigured(SoundId.MapTheme, 0.3f);
    }

    /// <summary>退出时解除点击事件和本周期窗口。</summary>
    /// <param name="procedureOwner">流程状态机。</param>
    /// <param name="isShutdown">是否框架关停。</param>
    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        SetFormCallbacks(GF.UI.GetUIForm(m_FormSerialId), bind: false);


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

    /// <summary>返回标题前等待当前检查点。</summary>
    /// <param name="formSerialId">发起操作的当前窗口编号。</param>
    private async void OnTitleRequested(int formSerialId)
    {
        if (formSerialId != m_FormSerialId || m_IsReturningToTitle)
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

    /// <summary>手动保存当前档案，等待后只反馈给本周期窗口。</summary>
    /// <param name="formSerialId">发起操作的当前窗口编号。</param>
    private async void OnSaveRequested(int formSerialId)
    {
        if (formSerialId != m_FormSerialId || m_IsReturningToTitle)
            return;
        await m_Owner.GetSystem<SaveSystem>().SaveManuallyAsync(formSerialId, m_Cancellation.Token);
    }

    /// <summary>验证所选关卡并进入战斗流程。</summary>
    /// <param name="formSerialId">发起操作的当前窗口编号。</param>
    /// <param name="selectionId">选中的存档格或关卡编号。</param>
    private void OnLevelSelected(int formSerialId, int selectionId)
    {
        if (formSerialId != m_FormSerialId || m_IsReturningToTitle)
            return;
        if (!m_Owner.GetSystem<ProfileSystem>().CanEnterLevel(selectionId))
        {
            Log.Error("[ProcedureMap] 地图请求了不存在的关卡：{0}", selectionId);
            return;
        }
        m_Owner.SetData<VarInt32>(FlowKeys.SelectedLevelId, selectionId);
        ChangeState<ProcedureLevel>(m_Owner);
    }

    /// <summary>本周期界面可见后关闭加载页。</summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="args">只在本次分发内读取的池化参数。</param>
    private void OnFormOpened(object sender, GameEventArgs args)
    {
        if (args is OpenUIFormSuccessEventArgs opened)
            SetFormCallbacks(opened.UIForm, bind: true);
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
            Log.Error("[ProcedureMap] 界面加载失败：{0}", failure.ErrorMessage);
    }


    /// <summary>按实际窗口类型绑定本流程回调；窗口编号拒绝旧作用域和无关实例。</summary>
    /// <param name="form">已打开窗口；在途时为空。</param>
    /// <param name="bind">绑定或解除。</param>
    private void SetFormCallbacks(IUIForm form, bool bind)
    {
        if (bind && (form == null || !GF.UI.IsValidUIForm(form)))
            return;
        switch (form)
        {
            case MapForm mapForm when mapForm.SerialId == m_FormSerialId:
                mapForm.TitleRequested -= OnTitleRequested;
                if (bind)
                    mapForm.TitleRequested += OnTitleRequested;
                mapForm.SaveRequested -= OnSaveRequested;
                if (bind)
                    mapForm.SaveRequested += OnSaveRequested;
                mapForm.LevelSelected -= OnLevelSelected;
                if (bind)
                    mapForm.LevelSelected += OnLevelSelected;
                break;
        }
    }
}
