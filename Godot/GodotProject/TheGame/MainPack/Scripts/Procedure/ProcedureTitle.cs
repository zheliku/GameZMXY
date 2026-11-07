using System;
using System.Threading;
using GameConfig;
using GameConfig.Constant;
using GameConfig.Sound;
using GameFramework.Event;
using GameFramework.Procedure;
using GameLogic;
using GameLogic.Architecture;
using GameLogic.Audio;
using GameLogic.Equipment;
using GameLogic.Procedure.FlowData;
using GameLogic.Profile;
using GameLogic.Save;
using GameLogic.Save.Slots;
using GameLogic.Skills;
using GameLogic.UI;
using GodotGameFramework;
using GodotGameFramework.UI;
using IUIForm = GameFramework.UI.IUIForm;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 开始界面：点击开始后选择存档，设置作为本流程拥有的独立 GGF 窗口。
/// </summary>
public sealed class ProcedureTitle : ProcedureBase
{
    private int _formSerialId; // 本周期窗口，含在途加载请求。
    private int _musicSerialId; // 本周期 GGF 音乐请求。
    private ProcedureOwner _owner; // 仅当前流程周期借用主 FSM。
    private CancellationTokenSource _cancellation; // 离开时使旧异步结果失效。

    private int _saveSerialId; // 本流程打开的六格存档窗口。
    private int _settingsSerialId; // 本流程打开的设置窗口，包括在途加载。

    /// <summary>
    /// 订阅本页面的点击事件后打开窗口。
    /// </summary>
    /// <param name="procedureOwner">流程状态机。</param>
    protected internal override void OnEnter(ProcedureOwner procedureOwner)
    {
        // 上个页面已结束借用；移除流程持有的档案状态和系统，继续借用应用存档系统。
        procedureOwner.RemoveSystem<EquipmentSystem>();
        procedureOwner.RemoveSystem<SkillSystem>();
        procedureOwner.RemoveModel<SkillModel>();
        procedureOwner.RemoveSystem<ProfileSystem>();
        procedureOwner.RemoveModel<ProfileModel>();
        procedureOwner.RemoveData(FlowKeys.SelectedLevelId);
        procedureOwner.RemoveData(FlowKeys.SaveSelection);

        base.OnEnter(procedureOwner);
        _owner = procedureOwner;
        _cancellation = new CancellationTokenSource();
        GF.Event.Subscribe(OpenUIFormSuccessEventArgs.EventId, OnFormOpened);
        GF.Event.Subscribe(OpenUIFormFailureEventArgs.EventId, OnFormFailed);
        _formSerialId = GF.UI.OpenUIForm(UIFormId.StartForm);
        SetFormCallbacks(GF.UI.GetUIForm(_formSerialId), bind: true);
        _musicSerialId = GF.Sound.PlayConfigured(SoundId.MenuTheme, 0.3f);
    }

    /// <summary>
    /// 退出时解除点击事件和本周期窗口。
    /// </summary>
    /// <param name="procedureOwner">流程状态机。</param>
    /// <param name="isShutdown">是否框架关停。</param>
    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        SetFormCallbacks(GF.UI.GetUIForm(_formSerialId), bind: false);
        SetFormCallbacks(GF.UI.GetUIForm(_saveSerialId), bind: false);


        if (_settingsSerialId != 0 && (GF.UI.HasUIForm(_settingsSerialId) || GF.UI.IsLoadingUIForm(_settingsSerialId)))
        {
            GF.UI.CloseUIForm(_settingsSerialId);
        }

        if (_saveSerialId != 0 && (GF.UI.HasUIForm(_saveSerialId) || GF.UI.IsLoadingUIForm(_saveSerialId)))
        {
            GF.UI.CloseUIForm(_saveSerialId);
        }

        _settingsSerialId = _saveSerialId = 0;
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
        GF.Event.Unsubscribe(OpenUIFormSuccessEventArgs.EventId, OnFormOpened);
        GF.Event.Unsubscribe(OpenUIFormFailureEventArgs.EventId, OnFormFailed);
        // 窗口在关停时也须先于流程销毁，解除档案和设置绑定。
        if (_formSerialId != 0 && (GF.UI.HasUIForm(_formSerialId) || GF.UI.IsLoadingUIForm(_formSerialId)))
        {
            GF.UI.CloseUIForm(_formSerialId);
        }

        if (_musicSerialId != 0)
        {
            GF.Sound.StopSound(_musicSerialId, isShutdown ? 0f : 0.2f);
        }
        _formSerialId = _musicSerialId = 0;
        _owner = null;
        base.OnLeave(procedureOwner, isShutdown);
    }

    /// <summary>
    /// 开始按钮打开存档选择页，读取目录期间显示加载反馈。
    /// </summary>
    /// <param name="formSerialId">发起操作的当前窗口编号。</param>
    private async void OnStartGameRequested(int formSerialId)
    {
        if (formSerialId != _formSerialId)
        {
            return;
        }

        if (_saveSerialId != 0 && (GF.UI.HasUIForm(_saveSerialId) || GF.UI.IsLoadingUIForm(_saveSerialId)))
        {
            return;
        }

        SaveSystem saveSystem = _owner.GetSystem<SaveSystem>();
        CancellationToken token = _cancellation.Token;
        _saveSerialId = GF.UI.OpenUIForm(UIFormId.SaveForm);
        SetFormCallbacks(GF.UI.GetUIForm(_saveSerialId), bind: true);
        await saveSystem.RefreshDirectoryAsync(token);
    }

    /// <summary>
    /// 只接受当前窗口中可读的格号，把稳定编号交给读档流程。
    /// </summary>
    /// <param name="formSerialId">发起操作的当前窗口编号。</param>
    /// <param name="selectionId">选中的存档格或关卡编号。</param>
    private void OnSaveSlotSelected(int formSerialId, int selectionId)
    {
        if (formSerialId != _saveSerialId || !GF.UI.HasUIForm(_saveSerialId) || GF.Fsm.GetModel<SaveModel>().Phase != SaveListState.Ready)
        {
            return;
        }

        if (selectionId < 1 || selectionId > GF.Fsm.GetModel<SaveModel>().Slots.Count)
        {
            return;
        }

        SaveSlotInfo slot = GF.Fsm.GetModel<SaveModel>().Slots[selectionId - 1];
        if (slot.State == SaveSlotState.Unreadable)
        {
            return;
        }

        _owner.SetData(FlowKeys.SaveSelection, SaveSelectionVariable.Create(slot.SlotNumber, slot.UnitId));
        if (slot.State == SaveSlotState.Empty)
        {
            ChangeState<ProcedureCharacter>(_owner);
        }
        else
        {
            ChangeState<ProcedureLoadGame>(_owner);
        }
    }

    /// <summary>
    /// 删除当前目录中指定的可读存档。
    /// </summary>
    /// <param name="formSerialId">发起操作的当前窗口编号。</param>
    /// <param name="slotNumber">要删除的格号。</param>
    private async void OnSaveSlotDeleteRequested(int formSerialId, int slotNumber)
    {
        SaveModel saveModel = GF.Fsm.GetModel<SaveModel>();
        if (formSerialId != _saveSerialId || !GF.UI.HasUIForm(_saveSerialId) ||
            saveModel.Phase != SaveListState.Ready || slotNumber < 1 || slotNumber > saveModel.Slots.Count)
        {
            return;
        }

        SaveSlotInfo slot = saveModel.Slots[slotNumber - 1];
        if (slot.State != SaveSlotState.Occupied)
        {
            return;
        }

        CancellationToken token = _cancellation.Token;
        SaveSystem saves = _owner.GetSystem<SaveSystem>();
        try
        {
            await saves.DeleteSlotAsync(slot.SlotNumber, slot.UnitId, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    /// <summary>
    /// 打开声音设置；加载中和已打开时不重复请求。
    /// </summary>
    /// <param name="formSerialId">发起操作的当前窗口编号。</param>
    private void OnSettingsRequested(int formSerialId)
    {
        if (formSerialId != _formSerialId)
        {
            return;
        }

        if (_settingsSerialId != 0 && (GF.UI.HasUIForm(_settingsSerialId) || GF.UI.IsLoadingUIForm(_settingsSerialId)))
        {
            return;
        }

        _settingsSerialId = GF.UI.OpenUIForm(UIFormId.SettingsForm);
        SetFormCallbacks(GF.UI.GetUIForm(_settingsSerialId), bind: true);
    }

    /// <summary>
    /// 本周期界面可见后关闭加载页。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="args">只在本次分发内读取的池化参数。</param>
    private void OnFormOpened(object sender, GameEventArgs args)
    {
        if (args is OpenUIFormSuccessEventArgs opened)
        {
            SetFormCallbacks(opened.UIForm, bind: true);
        }

        if (args is not OpenUIFormSuccessEventArgs success || success.UIForm.SerialId != _formSerialId)
        {
            return;
        }

        GodotGameFramework.HotUpdate.HotUpdateSafetyGuard.MarkStartupSuccess();
        (GF.UI.GetUIForm(ResourcesCollectionConstant.UI_LoadingForm) as LoadingForm)?.CloseLoading();
    }

    /// <summary>
    /// 报告当前窗口请求的装配错误。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="args">只在本次分发内读取的池化参数。</param>
    private void OnFormFailed(object sender, GameEventArgs args)
    {
        if (args is OpenUIFormFailureEventArgs failure && failure.SerialId == _formSerialId)
        {
            Log.Error("[ProcedureTitle] 界面加载失败：{0}", failure.ErrorMessage);
        }
    }

    /// <summary>
    /// 按实际窗口类型绑定本流程回调；窗口编号拒绝旧作用域和无关实例。
    /// </summary>
    /// <param name="form">已打开窗口；在途时为空。</param>
    /// <param name="bind">绑定或解除。</param>
    private void SetFormCallbacks(IUIForm form, bool bind)
    {
        if (bind && (form == null || !GF.UI.IsValidUIForm(form)))
        {
            return;
        }

        switch (form)
        {
            case StartForm startForm when startForm.SerialId == _formSerialId:
                startForm.StartGameRequested -= OnStartGameRequested;
                if (bind)
                {
                    startForm.StartGameRequested += OnStartGameRequested;
                }

                startForm.SettingsRequested -= OnSettingsRequested;
                if (bind)
                {
                    startForm.SettingsRequested += OnSettingsRequested;
                }

                break;
            case SaveForm saveForm when saveForm.SerialId == _saveSerialId:
                saveForm.SaveSlotSelected -= OnSaveSlotSelected;
                saveForm.SaveSlotDeleteRequested -= OnSaveSlotDeleteRequested;
                if (bind)
                {
                    saveForm.SaveSlotSelected += OnSaveSlotSelected;
                    saveForm.SaveSlotDeleteRequested += OnSaveSlotDeleteRequested;
                }

                break;
        }
    }
}
