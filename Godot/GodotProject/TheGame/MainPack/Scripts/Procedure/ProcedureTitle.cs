using GameLogic.Startup.FlowData;
using GameLogic.Save.Slots;
using GameLogic.Event.Navigation;
using System;
using GameConfig;
using GameConfig.Sound;
using GameFramework.Event;
using GameLogic;
using GameLogic.Level;
using GameLogic.Profile;
using GameLogic.Save;
using GameLogic.Startup;
using GameLogic.UI;
using GodotGameFramework;
using GodotGameFramework.UI;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>开始界面：点击开始后选择存档，设置作为本流程拥有的独立 GGF 窗口。</summary>
public sealed class ProcedureTitle : MenuProcedure
{
    private int m_SaveSerialId; // 本流程打开的六格存档窗口。
    private SaveModule m_Save; // 本标题周期唯一存档模块，拥有目录状态及读取任务。
    private int m_SettingsSerialId; // 本流程打开的设置窗口，包括在途加载。
    /// <inheritdoc />
    protected override UIFormId FormId => UIFormId.StartForm;
    /// <inheritdoc />
    protected override SoundId MusicId => SoundId.MenuTheme;
    /// <inheritdoc />
    protected override object CreateData(ProcedureOwner owner)
    {
        owner.RemoveData(FlowKeys.GameContext);
        owner.RemoveData(FlowKeys.SelectedLevelId);
        owner.RemoveData(FlowKeys.SaveSelection);
        var curve = owner.GetData<ExperienceCurveVariable>(FlowKeys.ExperienceCurve)?.Value
            ?? throw new InvalidOperationException("标题流程缺少经验曲线。");
        m_Save = new SaveModule(curve, ConfigSystem.Instance.Tables);
        return null;
    }

    /// <summary>订阅本页面的点击事件后打开窗口。</summary>
    /// <param name="procedureOwner">流程状态机。</param>
    protected internal override void OnEnter(ProcedureOwner procedureOwner)
    {

        GF.Event.Subscribe(StartGameRequestedEventArgs.EventId, OnStartGameRequested);
        GF.Event.Subscribe(SaveSlotSelectedEventArgs.EventId, OnSaveSlotSelected);
        GF.Event.Subscribe(SettingsRequestedEventArgs.EventId, OnSettingsRequested);
        base.OnEnter(procedureOwner);
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
        m_Save = null;
        base.OnLeave(procedureOwner, isShutdown);
    }

    /// <summary>开始窗口打开后确认启动成功。</summary>
    protected override void OnFormReady() => GodotGameFramework.HotUpdate.HotUpdateSafetyGuard.MarkStartupSuccess();

    /// <summary>开始按钮打开存档选择页，读取目录期间显示加载反馈。</summary>
    /// <param name="sender">发出请求的窗口。</param>
    /// <param name="args">本次开始请求。</param>
    private async void OnStartGameRequested(object sender, GameEventArgs args)
    {
        if (((StartGameRequestedEventArgs)args).FormSerialId != FormSerialId)
            return;
        if (m_SaveSerialId != 0 && (GF.UI.HasUIForm(m_SaveSerialId) || GF.UI.IsLoadingUIForm(m_SaveSerialId)))
            return;
        var save = m_Save;
        var token = CancellationToken;
        m_SaveSerialId = GF.UI.OpenUIForm(UIFormId.SaveForm, new SaveFormData(save.Directory));
        await save.RefreshDirectoryAsync(token);
    }

    /// <summary>只接受当前窗口中可读的格号，把稳定编号交给读档流程。</summary>
    /// <param name="sender">存档窗口。</param>
    /// <param name="args">本次存档选择，仅在当前分发期间读取。</param>
    private void OnSaveSlotSelected(object sender, GameEventArgs args)
    {
        var selection = (SaveSlotSelectedEventArgs)args;
        if (selection.FormSerialId != m_SaveSerialId || !GF.UI.HasUIForm(m_SaveSerialId) || m_Save.Directory.Phase != SaveDirectoryPhase.Ready)
            return;
        if (selection.SlotNumber < 1 || selection.SlotNumber > m_Save.Directory.Slots.Count)
            return;
        var slot = m_Save.Directory.Slots[selection.SlotNumber - 1];
        if (slot.State == SaveSlotState.Unreadable)
            return;
        Owner.SetData(FlowKeys.SaveSelection, SaveSelectionVariable.Create(slot.SlotNumber, slot.UnitId));
        ChangeState<ProcedureLoadProfile>(Owner);
    }

    /// <summary>打开声音设置；加载中和已打开时不重复请求。</summary>
    /// <param name="sender">发出请求的窗口。</param>
    /// <param name="args">本次设置请求。</param>
    private void OnSettingsRequested(object sender, GameEventArgs args)
    {
        if (((SettingsRequestedEventArgs)args).FormSerialId != FormSerialId)
            return;
        if (m_SettingsSerialId != 0 && (GF.UI.HasUIForm(m_SettingsSerialId) || GF.UI.IsLoadingUIForm(m_SettingsSerialId)))
            return;
        m_SettingsSerialId = GF.UI.OpenUIForm(UIFormId.SettingsForm, new SettingsFormData());
    }

}
