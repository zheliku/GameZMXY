using GameLogic.Startup.FlowData;
using GameLogic.Event.Navigation;
using System;
using GameConfig;
using GameConfig.Sound;
using GameFramework.Event;
using GameLogic.Session;
using GameLogic.Startup;
using GameLogic.UI;
using GodotGameFramework;
using GodotGameFramework.UI;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>地图流程：通过地点点击事件验证配置并创建关卡作用域。</summary>
public sealed class ProcedureMap : MenuProcedure
{

    /// <inheritdoc />
    protected override UIFormId FormId => UIFormId.MapForm;
    /// <inheritdoc />
    protected override SoundId MusicId => SoundId.MapTheme;
    /// <inheritdoc />
    protected override object CreateData(ProcedureOwner owner) => null;

    /// <summary>订阅本页面的点击事件后打开窗口。</summary>
    /// <param name="procedureOwner">流程状态机。</param>
    protected internal override void OnEnter(ProcedureOwner procedureOwner)
    {

        GF.Event.Subscribe(TitleRequestedEventArgs.EventId, OnTitleRequested);
        GF.Event.Subscribe(LevelSelectedEventArgs.EventId, OnLevelSelected);
        GF.Event.Subscribe(SaveRequestedEventArgs.EventId, OnSaveRequested);
        base.OnEnter(procedureOwner);
    }

    /// <summary>退出时解除点击事件和本周期窗口。</summary>
    /// <param name="procedureOwner">流程状态机。</param>
    /// <param name="isShutdown">是否框架关停。</param>
    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        GF.Event.Unsubscribe(TitleRequestedEventArgs.EventId, OnTitleRequested);
        GF.Event.Unsubscribe(LevelSelectedEventArgs.EventId, OnLevelSelected);
        GF.Event.Unsubscribe(SaveRequestedEventArgs.EventId, OnSaveRequested);

        base.OnLeave(procedureOwner, isShutdown);
    }

    /// <summary>返回标题前等待当前检查点。</summary>
    /// <param name="sender">地图窗口。</param>
    /// <param name="args">返回标题请求。</param>
    private async void OnTitleRequested(object sender, GameEventArgs args)
    {
        if (((TitleRequestedEventArgs)args).FormSerialId == FormSerialId)
            await ReturnToTitleAsync();
    }

    /// <summary>手动保存当前档案，等待后只反馈给本周期窗口。</summary>
    /// <param name="sender">地图窗口。</param>
    /// <param name="args">本次保存请求。</param>
    private async void OnSaveRequested(object sender, GameEventArgs args)
    {
        if (((SaveRequestedEventArgs)args).FormSerialId != FormSerialId || IsReturningToTitle) return;
        var session = GetPlayerSession(Owner);
        var token = CancellationToken;
        int serial = FormSerialId;
        bool succeeded;
        try { succeeded = await session.Save.SaveCheckpointAsync(session.Profile); }
        catch (Exception error)
        {
            Log.Error("[ProcedureMap] 手动保存失败：{0}", error);
            succeeded = false;
        }
        if (!token.IsCancellationRequested)
            GF.Event.Fire(this, ProfileSavedEventArgs.Create(serial, succeeded));
    }

    /// <summary>验证所选关卡并进入战斗流程。</summary>
    /// <param name="sender">地图窗口。</param>
    /// <param name="args">携带关卡配置主键的点击事件。</param>
    private void OnLevelSelected(object sender, GameEventArgs args)
    {
        var selection = (LevelSelectedEventArgs)args;
        if (selection.FormSerialId != FormSerialId || IsReturningToTitle) return;
        if (ConfigSystem.Instance.Tables.TbLevelConfig.GetOrDefault(selection.LevelId) == null)
        {
            Log.Error("[ProcedureMap] 地图请求了不存在的关卡：{0}", selection.LevelId);
            return;
        }
        Owner.SetData<VarInt32>(FlowKeys.SelectedLevelId, selection.LevelId);
        ChangeState<ProcedureLevel>(Owner);
    }

}
