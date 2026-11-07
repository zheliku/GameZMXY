using System;
using System.Threading;
using System.Threading.Tasks;
using GameFramework.Event;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Scene;
using GodotGameFramework.UI;
namespace GameLogic;

/// <summary>
/// 共享加载窗口，拥有进度补间与实际关闭任务。
/// </summary>
public partial class LoadingForm
{
    private const double ProgressTransitionSeconds = 0.25; // 加载进度的界面补间时长。
    private const double CompletionHoldSeconds = 0.1; // 达到 100% 后保留完成状态的显示时间。
    private Tween _progressTween; // 驱动进度条平滑变化的补间。
    private Tween _closeTween; // 加载完成后延迟关闭界面的补间。

    private bool _isCloseRequested; // 场景和界面加载事件可能重复触发关闭请求，用于防止重入。
    private TaskCompletionSource _closeCompletion; // 当前打开周期的实际关闭通知，先于关闭请求创建。

    /// <summary>
    /// 初始化界面。
    /// </summary>
    /// <param name="serialId">界面序列编号。</param>
    /// <param name="uiFormAssetName">界面资源名称。</param>
    /// <param name="uiGroup">界面所处的界面组。</param>
    /// <param name="pauseCoveredUIForm">是否暂停被覆盖的界面。</param>
    /// <param name="isNewInstance">是否是新实例。</param>
    /// <param name="userData">用户自定义数据。</param>
    public void OnInit(int serialId, string uiFormAssetName, GameFramework.UI.IUIGroup uiGroup, bool pauseCoveredUIForm, bool isNewInstance, object userData)
    {
        _serialId = serialId;
        _uiFormAssetName = uiFormAssetName;
        _uiGroup = uiGroup;
        _depthInUIGroup = 0;
        _pauseCoveredUIForm = pauseCoveredUIForm;
        _isCloseRequested = false;
        UIStringKeys.ForEach(key => key.SetLocalizationValue());
    }

    /// <summary>
    /// 界面回收。
    ///
    /// </summary>
    public void OnRecycle()
    {
        _serialId = 0;
        _depthInUIGroup = 0;
        _pauseCoveredUIForm = true;
        Visible = false;

        _isCloseRequested = false;
        _progressTween?.Kill();
        _progressTween = null;
        _closeTween?.Kill();
        _closeTween = null;
        _closeCompletion?.TrySetCanceled();
        _closeCompletion = null;
    }

    /// <summary>
    /// 界面打开。
    /// </summary>
    /// <param name="userData">本次框架调用的附加数据。</param>
    public void OnOpen(object userData)
    {
        _isCloseRequested = false;
        _closeCompletion = new TaskCompletionSource();

        Visible = true;

        if (_hSlider != null)
        {
            _hSlider.Value = 0;
        }
        GF.Event.Subscribe(OpenUIFormUpdateEventArgs.EventId, OnLoadingUpdate);
        GF.Event.Subscribe(LoadSceneUpdateEventArgs.EventId, OnLoadingUpdate);
    }

    /// <summary>
    /// 界面关闭。
    /// </summary>
    /// <param name="isShutdown">是否因所属服务或状态机关闭而离开。</param>
    /// <param name="userData">本次框架调用的附加数据。</param>
    public void OnClose(bool isShutdown, object userData)
    {
        Visible = false;

        GF.Event.Unsubscribe(OpenUIFormUpdateEventArgs.EventId, OnLoadingUpdate);
        GF.Event.Unsubscribe(LoadSceneUpdateEventArgs.EventId, OnLoadingUpdate);

        // 终止所有 Tween，避免旧实例的延迟关闭回调在实例复用后误关新实例
        _progressTween?.Kill();
        _progressTween = null;
        _closeTween?.Kill();
        _closeTween = null;
        _closeCompletion?.TrySetResult();
    }

    /// <summary>
    /// 界面暂停。
    /// </summary>
    public void OnPause()
    {
    }

    /// <summary>
    /// 界面暂停恢复。
    /// </summary>
    public void OnResume()
    {
    }

    /// <summary>
    /// 界面遮挡。
    /// </summary>
    public void OnCover()
    {
    }

    /// <summary>
    /// 界面遮挡恢复。
    /// </summary>
    public void OnReveal()
    {
    }

    /// <summary>
    /// 界面轮询。
    /// </summary>
    /// <param name="elapseSeconds">逻辑流逝时间，单位为秒。</param>
    /// <param name="realElapseSeconds">真实流逝时间，单位为秒。</param>
    public void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
    }

    /// <summary>
    /// 界面重新获得焦点。
    /// </summary>
    /// <param name="userData">本次框架调用的附加数据。</param>
    public void OnRefocus(object userData)
    {
    }

    /// <summary>
    /// 界面深度改变。
    /// </summary>
    /// <param name="uiGroupDepth">窗口所属界面组的深度。</param>
    /// <param name="depthInUIGroup">窗口在所属组内的深度。</param>
    public void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
    {
        _depthInUIGroup = depthInUIGroup;
        ZIndex = depthInUIGroup;
    }

    /// <summary>
    /// 更新加载状态文本和进度值。
    /// </summary>
    /// <param name="logState">要显示的状态文本。</param>
    /// <param name="progress">进度百分比，范围会限制为 0 到 100。</param>
    public void SetLogState(string logState, float progress)
    {
        if (_hSlider == null)
        {
            return;
        }
        float clamped = Mathf.Clamp(progress, 0f, 100f);

        // 平滑过渡，避免进度条跳跃显得生硬
        _progressTween?.Kill();
        _progressTween = CreateTween();
        _progressTween.SetIgnoreTimeScale(true);
        _progressTween.TweenProperty(_hSlider, "value", clamped, ProgressTransitionSeconds)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        if (_state != null)
        {
            _state.Text = (logState ?? string.Empty) + $" {clamped:F0}%";
        }
    }

    /// <summary>
    /// 显式关闭加载界面（由加载发起方在 finally 中调用）。
    /// 先让进度达到 100%，再短暂展示完成状态后关闭。
    /// </summary>
    public void CloseLoading()
    {
        if (_isCloseRequested)
        {
            return;
        }
        _isCloseRequested = true;
        SetLogState("加载完成", 100);

        // 延迟关闭：tween 绑定在节点上，并校验 serialId/Current，
        // 避免旧实例的延迟回调误关复用后的新实例或已关闭的实例。
        int closeSerialId = _serialId;
        _closeTween?.Kill();
        _closeTween = CreateTween();
        _closeTween.SetIgnoreTimeScale(true); // 延迟不受游戏暂停/TimeScale 影响
        _closeTween.TweenInterval(ProgressTransitionSeconds + CompletionHoldSeconds);
        _closeTween.TweenCallback(Callable.From(() =>
        {
            if (_serialId != closeSerialId)
            {
                return;
            }
            GF.UI.CloseUIForm(this);
        }));
    }

    /// <summary>
    /// 请求关闭加载界面，并等待实际关闭；取消只终止等待，不保存跨周期回调。
    /// </summary>
    /// <param name="cancellationToken">所属流程的取消令牌。</param>
    /// <returns>窗口实际关闭后的任务。</returns>
    public Task CloseLoadingAsync(CancellationToken cancellationToken)
    {
        Task closed = _closeCompletion.Task;
        CloseLoading();
        return closed.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// 接收界面或场景加载事件并更新进度。
    /// </summary>
    /// <param name="sender">事件发送者。</param>
    /// <param name="e">加载进度事件参数。</param>
    private void OnLoadingUpdate(object sender, GameEventArgs e)
    {
        float progress = 0;
        if (e is OpenUIFormUpdateEventArgs ui)
        {
            progress = ui.Progress;
        }
        else if (e is LoadSceneUpdateEventArgs scene)
        {
            progress = scene.Progress;
        }
        SetLogState("加载中...", progress * 100);
    }
}
