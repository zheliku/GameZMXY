using GameConfig.Constant;
using GameLogic.Audio;
using GameFramework.Procedure;
using System.Threading;
using GameLogic.Architecture;
using GameLogic.Equipment;
using GameLogic.Procedure.FlowData;
using GameLogic.Save.Slots;
using GameLogic.Event.Navigation;
using GameConfig;
using GameConfig.Sound;
using GameFramework.Event;
using GameLogic;
using GameLogic.Profile;
using GameLogic.Save;
using GodotGameFramework;
using GodotGameFramework.UI;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>开始界面：点击开始后选择存档，设置作为本流程拥有的独立 GGF 窗口。</summary>
public sealed class ProcedureTitle : ProcedureBase
{
    private int m_FormSerialId; // 本周期窗口，含在途加载请求。
    private int m_MusicSerialId; // 本周期 GGF 音乐请求。
    private ProcedureOwner m_Owner; // 仅当前流程周期借用主 FSM。
    private CancellationTokenSource m_Cancellation; // 离开时使旧异步结果失效。

    private int m_SaveSerialId; // 本流程打开的六格存档窗口。
    private int m_SettingsSerialId; // 本流程打开的设置窗口，包括在途加载。
    /// <summary>订阅本页面的点击事件后打开窗口。</summary>
    /// <param name="procedureOwner">流程状态机。</param>
    protected internal override void OnEnter(ProcedureOwner procedureOwner)
    {
        // 上个页面已结束借用；移除流程持有的档案状态和系统，继续借用应用存档系统。
        procedureOwner.RemoveSystem<EquipmentSystem>();
        procedureOwner.RemoveModel<PlayerModel>();
        procedureOwner.RemoveData(FlowKeys.SelectedLevelId);
        procedureOwner.RemoveData(FlowKeys.SaveSelection);

        GF.Event.Subscribe(StartGameRequestedEventArgs.EventId, OnStartGameRequested);
        GF.Event.Subscribe(SaveSlotSelectedEventArgs.EventId, OnSaveSlotSelected);
        GF.Event.Subscribe(SettingsRequestedEventArgs.EventId, OnSettingsRequested);
        base.OnEnter(procedureOwner);
        m_Owner = procedureOwner;
        m_Cancellation = new CancellationTokenSource();
        GF.Event.Subscribe(OpenUIFormSuccessEventArgs.EventId, OnFormOpened);
        GF.Event.Subscribe(OpenUIFormFailureEventArgs.EventId, OnFormFailed);
        m_FormSerialId = GF.UI.OpenUIForm(UIFormId.StartForm);
        m_MusicSerialId = GF.Sound.PlayConfigured(SoundId.MenuTheme, 0.3f);
    }

    /// <summary>退出时解除点击事件和本周期窗口。</summary>
    /// <param name="procedureOwner">流程状态机。</param>
    /// <param name="isShutdown">是否框架关停。</param>
    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        GF.Event.Unsubscribe(StartGameRequestedEventArgs.EventId, OnStartGameRequested);
        GF.Event.Unsubscribe(SaveSlotSelectedEventArgs.EventId, OnSaveSlotSelected);
        GF.Event.Unsubscribe(SettingsRequestedEventArgs.EventId, OnSettingsRequested);
        if (!isShutdown && m_SettingsSerialId != 0 && (GF.UI.HasUIForm(m_SettingsSerialId) || GF.UI.IsLoadingUIForm(m_SettingsSerialId)))
            GF.UI.CloseUIForm(m_SettingsSerialId);
        if (!isShutdown && m_SaveSerialId != 0 && (GF.UI.HasUIForm(m_SaveSerialId) || GF.UI.IsLoadingUIForm(m_SaveSerialId)))
            GF.UI.CloseUIForm(m_SaveSerialId);
        m_SettingsSerialId = m_SaveSerialId = 0;
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

    /// <summary>开始按钮打开存档选择页，读取目录期间显示加载反馈。</summary>
    /// <param name="sender">发出请求的窗口。</param>
    /// <param name="args">本次开始请求。</param>
    private async void OnStartGameRequested(object sender, GameEventArgs args)
    {
        if (((StartGameRequestedEventArgs)args).FormSerialId != m_FormSerialId)
            return;
        if (m_SaveSerialId != 0 && (GF.UI.HasUIForm(m_SaveSerialId) || GF.UI.IsLoadingUIForm(m_SaveSerialId)))
            return;
        var saveSystem = m_Owner.GetSystem<SaveSystem>();
        var token = m_Cancellation.Token;
        m_SaveSerialId = GF.UI.OpenUIForm(UIFormId.SaveForm);
        await saveSystem.RefreshDirectoryAsync(token);
    }

    /// <summary>只接受当前窗口中可读的格号，把稳定编号交给读档流程。</summary>
    /// <param name="sender">存档窗口。</param>
    /// <param name="args">本次存档选择，仅在当前分发期间读取。</param>
    private void OnSaveSlotSelected(object sender, GameEventArgs args)
    {
        var selection = (SaveSlotSelectedEventArgs)args;
        if (selection.FormSerialId != m_SaveSerialId || !GF.UI.HasUIForm(m_SaveSerialId) || GF.Fsm.GetModel<SaveModel>().Phase != SaveListState.Ready)
            return;
        if (selection.SlotNumber < 1 || selection.SlotNumber > GF.Fsm.GetModel<SaveModel>().Slots.Count)
            return;
        var slot = GF.Fsm.GetModel<SaveModel>().Slots[selection.SlotNumber - 1];
        if (slot.State == SaveSlotState.Unreadable)
            return;
        m_Owner.SetData(FlowKeys.SaveSelection, SaveSelectionVariable.Create(slot.SlotNumber, slot.UnitId));
        ChangeState<ProcedureLoadGame>(m_Owner);
    }

    /// <summary>打开声音设置；加载中和已打开时不重复请求。</summary>
    /// <param name="sender">发出请求的窗口。</param>
    /// <param name="args">本次设置请求。</param>
    private void OnSettingsRequested(object sender, GameEventArgs args)
    {
        if (((SettingsRequestedEventArgs)args).FormSerialId != m_FormSerialId)
            return;
        if (m_SettingsSerialId != 0 && (GF.UI.HasUIForm(m_SettingsSerialId) || GF.UI.IsLoadingUIForm(m_SettingsSerialId)))
            return;
        m_SettingsSerialId = GF.UI.OpenUIForm(UIFormId.SettingsForm);
    }

    /// <summary>本周期界面可见后关闭加载页。</summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="args">只在本次分发内读取的池化参数。</param>
    private void OnFormOpened(object sender, GameEventArgs args)
    {
        if (args is not OpenUIFormSuccessEventArgs success || success.UIForm.SerialId != m_FormSerialId)
            return;
        GodotGameFramework.HotUpdate.HotUpdateSafetyGuard.MarkStartupSuccess();
        (GF.UI.GetUIForm(ResourcesCollectionConstant.UI_LoadingForm) as LoadingForm)?.CloseLoading();
    }

    /// <summary>报告当前窗口请求的装配错误。</summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="args">只在本次分发内读取的池化参数。</param>
    private void OnFormFailed(object sender, GameEventArgs args)
    {
        if (args is OpenUIFormFailureEventArgs failure && failure.SerialId == m_FormSerialId)
            Log.Error("[ProcedureTitle] 界面加载失败：{0}", failure.ErrorMessage);
    }
}
