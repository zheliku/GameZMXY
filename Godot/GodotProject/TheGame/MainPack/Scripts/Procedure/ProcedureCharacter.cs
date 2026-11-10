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
using GameLogic.Procedure.FlowData;
using GameLogic.Save;
using GameLogic.Save.Slots;
using GameLogic.UI;
using GodotGameFramework;
using GodotGameFramework.UI;
using IUIForm = GameFramework.UI.IUIForm;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 新档选角与选格：先确定有序阵容，再交给读档流程创建档案。
/// </summary>
public sealed class ProcedureCharacter : ProcedureBase
{
    private int _formSerialId; // 本周期窗口，含在途加载请求。
    private int _musicSerialId; // 本周期 GGF 音乐请求。
    private ProcedureOwner _owner; // 仅当前流程周期借用主 FSM。
    private CancellationTokenSource _cancellation; // 离开时使存档目录读取与删除失效。
    private int _saveSerialId; // 本流程打开的保存模式存档页。

    /// <summary>
    /// 订阅本页面的点击事件后打开窗口。
    /// </summary>
    /// <param name="procedureOwner">流程状态机。</param>
    protected internal override void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);
        _owner = procedureOwner;
        _cancellation = new CancellationTokenSource();
        GF.Event.Subscribe(OpenUIFormSuccessEventArgs.EventId, OnFormOpened);
        GF.Event.Subscribe(OpenUIFormFailureEventArgs.EventId, OnFormFailed);
        int count = procedureOwner.GetData<SaveSelectionVariable>(FlowKeys.SaveSelection).Value.HeroIds.Length;
        _formSerialId = GF.UI.OpenUIForm(UIFormId.CharacterForm, new CharacterFormData(count));
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
        _cancellation.Cancel();
        _cancellation.Dispose();
        _cancellation = null;
        if (_saveSerialId != 0 && (GF.UI.HasUIForm(_saveSerialId) || GF.UI.IsLoadingUIForm(_saveSerialId)))
        {
            GF.UI.CloseUIForm(_saveSerialId);
        }
        _saveSerialId = 0;

        GF.Event.Unsubscribe(OpenUIFormSuccessEventArgs.EventId, OnFormOpened);
        GF.Event.Unsubscribe(OpenUIFormFailureEventArgs.EventId, OnFormFailed);
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

    /// <summary>选满席位后在本流程内打开保存模式存档页。</summary>
    /// <param name="formSerialId">发起操作的当前选角窗口编号。</param>
    /// <param name="heroIds">按席位排列的英雄编号。</param>
    private async void OnCharacterConfirmed(int formSerialId, int[] heroIds)
    {
        if (formSerialId != _formSerialId ||
            (_saveSerialId != 0 && (GF.UI.HasUIForm(_saveSerialId) || GF.UI.IsLoadingUIForm(_saveSerialId))))
        {
            return;
        }

        // 保存页由本流程持有，保留选角阵容并让选角页停在下层。
        SaveSelectionVariable selection = _owner.GetData<SaveSelectionVariable>(FlowKeys.SaveSelection);
        selection.Value = (0, 0, heroIds);
        CancellationToken token = _cancellation.Token;
        SaveSystem saves = _owner.GetSystem<SaveSystem>();
        _saveSerialId = GF.UI.OpenUIForm(UIFormId.SaveForm, new SaveFormData(SaveFormMode.Save));
        SetFormCallbacks(GF.UI.GetUIForm(_saveSerialId), bind: true);
        await saves.RefreshDirectoryAsync(token);
    }

    /// <summary>确认保存格；覆盖时先复用删除流程，再以刷新后的空格建档。</summary>
    /// <param name="formSerialId">发起操作的保存窗口编号。</param>
    /// <param name="slotNumber">从 1 开始的目标格号。</param>
    private async void OnSaveSlotSelected(int formSerialId, int slotNumber)
    {
        SaveModel model = _owner.GetModel<SaveModel>();
        if (formSerialId != _saveSerialId || !GF.UI.HasUIForm(_saveSerialId) ||
            model.Phase != SaveListState.Ready || slotNumber < 1 || slotNumber > model.Slots.Count)
        {
            return;
        }

        SaveSlotInfo slot = model.Slots[slotNumber - 1];
        if (slot.State == SaveSlotState.Unreadable)
        {
            return;
        }

        CancellationToken token = _cancellation.Token;
        SaveSystem saves = _owner.GetSystem<SaveSystem>();
        try
        {
            if (slot.State == SaveSlotState.Occupied)
            {
                // 删除后目录会重新读取，不能继续使用旧编号或旧摘要。
                bool deleted = await saves.DeleteSlotAsync(slot.SlotNumber, slot.UnitId, token);
                if (!deleted || token.IsCancellationRequested)
                {
                    return;
                }
                slot = model.Slots[slotNumber - 1];
            }

            SaveSelectionVariable selection = _owner.GetData<SaveSelectionVariable>(FlowKeys.SaveSelection);
            // 将刷新后的空槽身份与已选阵容一次交给读档建档流程。
            selection.Value = (slot.SlotNumber, slot.UnitId, selection.Value.HeroIds);
            ChangeState<ProcedureLoadGame>(_owner);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    /// <summary>放弃选角，回到会清除流程选择的标题。</summary>
    /// <param name="formSerialId">发起操作的当前选角窗口编号。</param>
    private void OnCharacterCancelled(int formSerialId)
    {
        if (formSerialId == _formSerialId)
        {
            ChangeState<ProcedureTitle>(_owner);
        }
    }

    /// <summary>保存页关闭即放弃本次新档，不写入目录。</summary>
    /// <param name="formSerialId">发起操作的当前保存窗口编号。</param>
    private void OnSaveFormClosed(int formSerialId)
    {
        if (formSerialId == _saveSerialId)
        {
            ChangeState<ProcedureTitle>(_owner);
        }
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

        (GF.UI.GetUIForm(ResourcesCollectionConstant.UI_LoadingForm) as LoadingForm)?.CloseLoading();
    }

    /// <summary>
    /// 报告当前窗口请求的装配错误。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="args">只在本次分发内读取的池化参数。</param>
    private void OnFormFailed(object sender, GameEventArgs args)
    {
        if (args is OpenUIFormFailureEventArgs failure && (failure.SerialId == _formSerialId || failure.SerialId == _saveSerialId))
        {
            Log.Error("[ProcedureCharacter] 界面加载失败：{0}", failure.ErrorMessage);
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
            case CharacterForm characterForm when characterForm.SerialId == _formSerialId:
                characterForm.CharacterConfirmed -= OnCharacterConfirmed;
                characterForm.CharacterCancelled -= OnCharacterCancelled;
                if (bind)
                {
                    characterForm.CharacterConfirmed += OnCharacterConfirmed;
                    characterForm.CharacterCancelled += OnCharacterCancelled;
                }

                break;
            case SaveForm saveForm when saveForm.SerialId == _saveSerialId:
                saveForm.SaveSlotSelected -= OnSaveSlotSelected;
                saveForm.SaveFormClosed -= OnSaveFormClosed;
                if (bind)
                {
                    saveForm.SaveSlotSelected += OnSaveSlotSelected;
                    saveForm.SaveFormClosed += OnSaveFormClosed;
                }

                break;
        }
    }
}
