using GameLogic.Startup.FlowData;
using System;
using System.Threading;
using System.Threading.Tasks;
using GameConfig;
using GameConfig.Constant;
using GameConfig.Sound;
using GameFramework.Event;
using GameFramework.Procedure;
using GameLogic.Audio;
using GameLogic.Session;
using GodotGameFramework;
using GodotGameFramework.UI;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace GameLogic.Startup;

/// <summary>菜单流程的窗口与音乐所有者；具体页面通过具名事件切换流程，离开时取消加载并释放订阅。</summary>
public abstract class MenuProcedure : ProcedureBase
{
    private int m_FormSerialId; // 本周期窗口请求，包含仍在加载中的请求。
    private int m_MusicSerialId; // 本周期背景音乐请求，退出时按编号取消或停止。
    private CancellationTokenSource m_Cancellation; // 离开时作废仍在等待的导航结果。

    /// <summary>当前流程状态机；离开后清空。</summary>
    protected ProcedureOwner Owner { get; private set; }
    /// <summary>当前窗口打开编号。</summary>
    protected int FormSerialId => m_FormSerialId;
    /// <summary>本周期异步操作的取消令牌。</summary>
    protected CancellationToken CancellationToken => m_Cancellation.Token;

    /// <summary>正在等待保存并返回标题，期间拒绝其他导航。</summary>
    protected bool IsReturningToTitle { get; private set; }

    /// <summary>本流程的界面配置。</summary>
    protected abstract UIFormId FormId { get; }

    /// <summary>本流程的音乐配置。</summary>
    protected abstract SoundId MusicId { get; }

    /// <summary>建立本周期窗口和 GGF 音乐请求。</summary>
    /// <param name="procedureOwner">流程状态机。</param>
    protected internal override void OnEnter(ProcedureOwner procedureOwner)
    {
        base.OnEnter(procedureOwner);
        Owner = procedureOwner;
        IsReturningToTitle = false;
        m_Cancellation = new CancellationTokenSource();
        // 注册在请求之前；资源缓存命中时也能接收打开结果。
        GF.Event.Subscribe(OpenUIFormSuccessEventArgs.EventId, OnFormOpened);
        GF.Event.Subscribe(OpenUIFormFailureEventArgs.EventId, OnFormFailed);
        m_FormSerialId = GF.UI.OpenUIForm(FormId, CreateData(procedureOwner));
        m_MusicSerialId = GF.Sound.PlayConfigured(MusicId, 0.3f);
    }

    /// <summary>关闭本周期的窗口与音乐；在途请求也必须取消。</summary>
    /// <param name="procedureOwner">流程状态机。</param>
    /// <param name="isShutdown">是否框架关停，此时不访问已释放节点。</param>
    protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
    {
        m_Cancellation?.Cancel();
        m_Cancellation?.Dispose();
        m_Cancellation = null;
        GF.Event.Unsubscribe(OpenUIFormSuccessEventArgs.EventId, OnFormOpened);
        GF.Event.Unsubscribe(OpenUIFormFailureEventArgs.EventId, OnFormFailed);
        if (!isShutdown)
        {
            if (m_FormSerialId != 0 && (GF.UI.HasUIForm(m_FormSerialId) || GF.UI.IsLoadingUIForm(m_FormSerialId)))
                GF.UI.CloseUIForm(m_FormSerialId);
            if (m_MusicSerialId != 0) GF.Sound.StopSound(m_MusicSerialId, 0.2f);
        }
        m_FormSerialId = m_MusicSerialId = 0;
        Owner = null;
        base.OnLeave(procedureOwner, isShutdown);
    }

    /// <summary>创建只包含当前状态源引用的窗口参数。</summary>
    /// <param name="owner">流程状态机。</param>
    /// <returns>窗口打开参数。</returns>
    protected abstract object CreateData(ProcedureOwner owner);

    /// <summary>本流程窗口实际打开成功后执行流程专属收尾。</summary>
    protected virtual void OnFormReady() { }

    /// <summary>读取已经建立的档案作用域；缺失视为流程装配错误。</summary>
    /// <param name="owner">流程状态机。</param>
    /// <returns>当前档案作用域。</returns>
    /// <exception cref="InvalidOperationException">未先完成读档流程。</exception>
    protected static GameContext GetGameContext(ProcedureOwner owner) =>
        owner.GetData<GameContextVariable>(FlowKeys.GameContext)?.Value
        ?? throw new InvalidOperationException("菜单流程缺少档案作用域。");

    /// <summary>等待当前档案的检查点，再释放档案作用域；失败保留当前页面。</summary>
    /// <returns>本周期的返回任务。</returns>
    protected async Task ReturnToTitleAsync()
    {
        if (IsReturningToTitle) return;
        IsReturningToTitle = true;
        var token = CancellationToken;
        var owner = Owner;
        var save = GetGameContext(owner).Save;
        try
        {
            // 捕获本周期引用，等待后不得访问已经换周期的流程字段。
            bool saved = await save.PendingWrite;
            token.ThrowIfCancellationRequested();
            if (saved) ChangeState<ProcedureTitle>(owner);
            else Log.Error("[{0}] 存档写入失败，已保留档案，暂不能返回标题。", GetType().Name);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { Log.Error("[{0}] 返回标题失败：{1}", GetType().Name, error); }
        finally { if (!token.IsCancellationRequested) IsReturningToTitle = false; }
    }

    /// <summary>目标窗口可见后关闭启动加载界面。</summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="args">池化事件参数，仅在分发期间读取。</param>
    private void OnFormOpened(object sender, GameEventArgs args)
    {
        if (args is OpenUIFormSuccessEventArgs success && success.UIForm.SerialId == m_FormSerialId)
        {
            OnFormReady();
            (GF.UI.GetUIForm(ResourcesCollectionConstant.UI_LoadingForm) as LoadingForm)?.CloseLoading();
        }
    }

    /// <summary>报告当前请求的资源或装配错误。</summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="args">池化事件参数，仅在分发期间读取。</param>
    private void OnFormFailed(object sender, GameEventArgs args)
    {
        if (args is OpenUIFormFailureEventArgs failure && failure.SerialId == m_FormSerialId)
            Log.Error("[{0}] 界面加载失败：{1}", GetType().Name, failure.ErrorMessage);
    }
}
