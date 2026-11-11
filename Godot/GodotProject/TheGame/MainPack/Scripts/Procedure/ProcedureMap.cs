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
using GameLogic.Config;
using GameLogic.Procedure.FlowData;
using GameLogic.Profile;
using GameLogic.Save;
using GameLogic.UI;
using GodotGameFramework;
using GodotGameFramework.UI;
using IUIForm = GameFramework.UI.IUIForm;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>
/// 地图流程：通过地点点击事件验证配置并创建关卡作用域。
/// </summary>
public sealed class ProcedureMap : ProcedureBase
{
    private int _formSerialId; // 本周期窗口，含在途加载请求。
    private int _settingsSerialId; // 当前地图拥有的设置窗口，包括在途请求。
    private int _skillSerialId; // 当前地图拥有的技能窗口，包括在途请求。
    private int _musicSerialId; // 本周期 GGF 音乐请求。
    private ProcedureOwner _owner; // 仅当前流程周期借用主 FSM。
    private CancellationTokenSource _cancellation; // 离开时使旧异步结果失效。
    private bool _isReturningToTitle; // 等待写盘期间拒绝重复导航。

    /// <summary>
    /// 订阅本页面的点击事件后打开窗口。
    /// </summary>
    /// <param name="procedureOwner">流程状态机。</param>
    protected internal override void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);
        _owner = procedureOwner;
        _isReturningToTitle = false;
        _cancellation = new CancellationTokenSource();
        GF.Event.Subscribe(OpenUIFormSuccessEventArgs.EventId, OnFormOpened);
        GF.Event.Subscribe(OpenUIFormFailureEventArgs.EventId, OnFormFailed);
        _formSerialId = GF.UI.OpenUIForm(UIFormId.MapForm);
        SetFormCallbacks(GF.UI.GetUIForm(_formSerialId), bind: true);
        _musicSerialId = GF.Sound.PlayConfigured(SoundId.MapTheme, 0.3f);
    }

    /// <summary>
    /// 退出时解除点击事件和本周期窗口。
    /// </summary>
    /// <param name="procedureOwner">流程状态机。</param>
    /// <param name="isShutdown">是否框架关停。</param>
    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        SetFormCallbacks(GF.UI.GetUIForm(_formSerialId), bind: false);
        SetFormCallbacks(GF.UI.GetUIForm(_settingsSerialId), bind: false);
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
        GF.Event.Unsubscribe(OpenUIFormSuccessEventArgs.EventId, OnFormOpened);
        GF.Event.Unsubscribe(OpenUIFormFailureEventArgs.EventId, OnFormFailed);
        // 地图绑定的模型归流程FSM所有，关停时同样先关窗再销毁流程。
        if (_settingsSerialId != 0 && (GF.UI.HasUIForm(_settingsSerialId) || GF.UI.IsLoadingUIForm(_settingsSerialId)))
        {
            GF.UI.CloseUIForm(_settingsSerialId);
        }

        if (_skillSerialId != 0 && (GF.UI.HasUIForm(_skillSerialId) || GF.UI.IsLoadingUIForm(_skillSerialId)))
        {
            GF.UI.CloseUIForm(_skillSerialId);
        }

        if (_formSerialId != 0 && (GF.UI.HasUIForm(_formSerialId) || GF.UI.IsLoadingUIForm(_formSerialId)))
        {
            GF.UI.CloseUIForm(_formSerialId);
        }

        if (_musicSerialId != 0)
        {
            GF.Sound.StopSound(_musicSerialId, isShutdown ? 0f : 0.2f);
        }
        _skillSerialId = _settingsSerialId = _formSerialId = _musicSerialId = 0;
        _owner = null;
        base.OnLeave(procedureOwner, isShutdown);
    }

    /// <summary>
    /// 返回标题前保存当前档案；地图与关卡设置复用同一存档入口。
    /// </summary>
    /// <param name="formSerialId">发起操作的当前窗口编号。</param>
    private async void OnTitleRequested(int formSerialId)
    {
        if (formSerialId != _settingsSerialId || _isReturningToTitle)
        {
            return;
        }

        _isReturningToTitle = true;
        CancellationToken token = _cancellation.Token;
        ProcedureOwner owner = _owner;
        SaveSystem saveSystem = owner.GetSystem<SaveSystem>();
        try
        {
            // 捕获本周期引用，等待后不得访问已经换周期的流程字段。
            bool saved = await saveSystem.SaveManuallyAsync(formSerialId, token);
            token.ThrowIfCancellationRequested();
            if (saved)
            {
                ChangeState<ProcedureTitle>(owner);
            }
            else
            {
                Log.Error("[{0}] 存档写入失败，已保留档案，暂不能返回标题。", GetType().Name);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            Log.Error("[{0}] 返回标题失败：{1}", GetType().Name, error);
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                _isReturningToTitle = false;
            }
        }
    }

    /// <summary>
    /// 手动保存当前档案，等待后只反馈给本周期窗口。
    /// </summary>
    /// <param name="formSerialId">发起操作的当前窗口编号。</param>
    private async void OnSaveRequested(int formSerialId)
    {
        if (formSerialId != _formSerialId || _isReturningToTitle)
        {
            return;
        }

        await _owner.GetSystem<SaveSystem>().SaveManuallyAsync(formSerialId, _cancellation.Token);
    }

    /// <summary>
    /// 验证所选关卡并进入战斗流程。
    /// </summary>
    /// <param name="formSerialId">发起操作的当前窗口编号。</param>
    /// <param name="selectionId">选中的存档格或关卡编号。</param>
    private void OnLevelSelected(int formSerialId, int selectionId)
    {
        if (formSerialId != _formSerialId || _isReturningToTitle)
        {
            return;
        }

        if (!_owner.GetSystem<ProfileSystem>().CanEnterLevel(selectionId))
        {
            Log.Error("[ProcedureMap] 地图请求了不存在的关卡：{0}", selectionId);
            return;
        }
        _owner.SetData<VarInt32>(FlowKeys.SelectedLevelId, selectionId);
        ChangeState<ProcedureLevel>(_owner);
    }

    /// <summary>
    /// 地图使用现有设置窗口；已打开或加载中不重复提交。
    /// </summary>
    /// <param name="formSerialId">发起操作的地图窗口编号。</param>
    private void OnSettingsRequested(int formSerialId)
    {
        if (formSerialId != _formSerialId || _isReturningToTitle)
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
    /// 地图复用现有技能窗；已打开或加载中的窗口不重复提交。
    /// </summary>
    /// <param name="formSerialId">发起操作的地图窗口编号。</param>
    private void OnSkillsRequested(int formSerialId)
    {
        if (formSerialId != _formSerialId || _isReturningToTitle)
        {
            return;
        }

        if (_skillSerialId != 0 && (GF.UI.HasUIForm(_skillSerialId) || GF.UI.IsLoadingUIForm(_skillSerialId)))
        {
            return;
        }

        _skillSerialId = GF.UI.OpenUIForm(UIFormId.SkillForm,
            new SkillFormData(_owner.GetModel<ProfileModel>().HeroOf(PlayerSlot.P1)));
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
        if (args is OpenUIFormFailureEventArgs failure &&
            (failure.SerialId == _formSerialId || failure.SerialId == _settingsSerialId || failure.SerialId == _skillSerialId))
        {
            Log.Error("[ProcedureMap] 界面加载失败：{0}", failure.ErrorMessage);
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
            case MapForm mapForm when mapForm.SerialId == _formSerialId:
                mapForm.SettingsRequested -= OnSettingsRequested;
                if (bind)
                {
                    mapForm.SettingsRequested += OnSettingsRequested;
                }

                mapForm.SkillsRequested -= OnSkillsRequested;
                if (bind)
                {
                    mapForm.SkillsRequested += OnSkillsRequested;
                }

                mapForm.SaveRequested -= OnSaveRequested;
                if (bind)
                {
                    mapForm.SaveRequested += OnSaveRequested;
                }

                mapForm.LevelSelected -= OnLevelSelected;
                if (bind)
                {
                    mapForm.LevelSelected += OnLevelSelected;
                }

                break;
            case SettingsForm settingsForm when settingsForm.SerialId == _settingsSerialId:
                settingsForm.TitleRequested -= OnTitleRequested;
                if (bind)
                {
                    settingsForm.TitleRequested += OnTitleRequested;
                }
                break;
        }
    }
}
