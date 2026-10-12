using GameLogic.Event.Navigation;
using System;
using GameConfig;
using GameConfig.Sound;
using GameFramework.Event;
using GameLogic;
using GameLogic.Level;
using GameLogic.Startup;
using GameLogic.UI;
using GodotGameFramework;
using GodotGameFramework.UI;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

/// <summary>选角流程：确认进入地图，返回标题前等待最近一次存档完成。</summary>
public sealed class ProcedureCharacter : MenuProcedure
{
    /// <inheritdoc />
    protected override UIFormId FormId => UIFormId.CharacterForm;
    /// <inheritdoc />
    protected override SoundId MusicId => SoundId.MenuTheme;
    /// <inheritdoc />
    protected override object CreateData(ProcedureOwner owner) => null;

    /// <summary>订阅本页面的点击事件后打开窗口。</summary>
    /// <param name="procedureOwner">流程状态机。</param>
    protected internal override void OnEnter(ProcedureOwner procedureOwner)
    {
        GF.Event.Subscribe(CharacterConfirmedEventArgs.EventId, OnCharacterConfirmed);
        GF.Event.Subscribe(TitleRequestedEventArgs.EventId, OnTitleRequested);
        base.OnEnter(procedureOwner);
    }

    /// <summary>退出时解除点击事件和本周期窗口。</summary>
    /// <param name="procedureOwner">流程状态机。</param>
    /// <param name="isShutdown">是否框架关停。</param>
    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        GF.Event.Unsubscribe(CharacterConfirmedEventArgs.EventId, OnCharacterConfirmed);
        GF.Event.Unsubscribe(TitleRequestedEventArgs.EventId, OnTitleRequested);

        base.OnLeave(procedureOwner, isShutdown);
    }

    /// <summary>确认当前出战角色后进入地图。</summary>
    /// <param name="sender">选角窗口。</param>
    /// <param name="args">本次确认请求。</param>
    private void OnCharacterConfirmed(object sender, GameEventArgs args)
    {
        if (!IsReturningToTitle && ((CharacterConfirmedEventArgs)args).FormSerialId == FormSerialId)
            ChangeState<ProcedureMap>(Owner);
    }

    /// <summary>保存完成后释放档案作用域；失败保留页面，允许重试。</summary>
    /// <param name="sender">选角窗口。</param>
    /// <param name="args">返回标题请求，仅在等待前读取。</param>
    private async void OnTitleRequested(object sender, GameEventArgs args)
    {
        if (((TitleRequestedEventArgs)args).FormSerialId == FormSerialId)
            await ReturnToTitleAsync();
    }
}
