using System;
using GameConfig.Sound;
using GameFramework.UI;
using GameLogic.Architecture;
using GameLogic.Audio;
using GameLogic.Level;
using Godot;
using GodotGameFramework;
using GodotGameFramework.UI;

namespace GameLogic.UI;

/// <summary>
/// 成功与失败共用的结算窗口；显示冻结成绩，只提交导航请求。
/// </summary>
public partial class SettlementForm : IController
{
    [Export] private Texture2D _successTexture; // 成功原画。
    [Export] private Texture2D _failureTexture; // 失败原画。
    private LevelResult _result; // 本次结局的不可变成绩。
    private bool _resumeGame; // 只恢复本窗口拥有的暂停。

    /// <summary>
    /// 由所属流程处理 MapRequested；参数为本次窗口编号。
    /// </summary>
    public event Action<int> MapRequested;

    /// <summary>
    /// 由所属流程处理 RetryRequested；参数为本次窗口编号。
    /// </summary>
    public event Action<int> RetryRequested;

    /// <summary>
    /// 初始化框架字段；新实例的按钮只绑定一次。
    /// </summary>
    /// <param name="serialId">本次窗口编号。</param>
    /// <param name="uiFormAssetName">窗口资源路径。</param>
    /// <param name="uiGroup">所属窗口组。</param>
    /// <param name="pauseCoveredUIForm">是否暂停被覆盖窗口。</param>
    /// <param name="isNewInstance">是否新建实例。</param>
    /// <param name="userData">本次打开参数。</param>
    public void OnInit(int serialId, string uiFormAssetName, IUIGroup uiGroup, bool pauseCoveredUIForm,
        bool isNewInstance, object userData)
    {
        _serialId = serialId;
        _uiFormAssetName = uiFormAssetName;
        _uiGroup = uiGroup;
        _depthInUIGroup = 0;
        _pauseCoveredUIForm = pauseCoveredUIForm;
        UIStringKeys.ForEach(key => key.SetLocalizationValue());
        if (isNewInstance)
        {
            _returnMap.Pressed += OnMapClicked;
            _retry.Pressed += OnRetryClicked;
        }
    }

    /// <summary>
    /// 关闭时释放状态源与本窗口拥有的暂停。
    /// </summary>
    /// <param name="isShutdown">是否框架关停。</param>
    /// <param name="userData">关闭参数，不使用。</param>
    public void OnClose(bool isShutdown, object userData)
    {
        MapRequested = null;
        RetryRequested = null;
        ReleaseResultAndPause();
        if (!isShutdown && IsInstanceValid(this))
        {
            Visible = false;
        }
    }

    /// <summary>
    /// 回池后清除引用与打开编号，重复释放安全。
    /// </summary>
    public void OnRecycle()
    {
        ReleaseResultAndPause();
        _serialId = 0;
        _depthInUIGroup = 0;
        _pauseCoveredUIForm = true;
        if (IsInstanceValid(this))
        {
            Visible = false;
        }
    }

    /// <summary>
    /// 界面暂停。
    /// </summary>
    public void OnPause()
    {
    }

    /// <summary>
    /// 恢复界面。
    /// </summary>
    public void OnResume()
    {
    }

    /// <summary>
    /// 被其他窗口覆盖。
    /// </summary>
    public void OnCover()
    {
    }

    /// <summary>
    /// 重新露出窗口。
    /// </summary>
    public void OnReveal()
    {
    }

    /// <summary>
    /// 显示已经结算的成绩，暂停背后的关卡。
    /// </summary>
    /// <param name="userData">包含成功或失败成绩的 SettlementFormData。</param>
    /// <exception cref="ArgumentException">不是已提交的关卡结局。</exception>
    public void OnOpen(object userData)
    {
        ReleaseResultAndPause();
        _result = (userData as SettlementFormData)?.Result;
        if (_result == null || _result.Outcome is not (LevelOutcome.Cleared or LevelOutcome.Defeated))
        {
            throw new ArgumentException("结算需要成功或失败的成绩。", nameof(userData));
        }

        bool success = _result.Outcome == LevelOutcome.Cleared;
        _outcomeArt.Texture = success ? _successTexture : _failureTexture;
        _returnMap.Position = new Vector2(success ? 285 : 305, 405);
        _retry.Visible = !success;
        _returnMap.Disabled = _retry.Disabled = false;
        var time = TimeSpan.FromSeconds(Math.Floor(_result.ElapsedSeconds));
        _time.Text = $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}";
        _health.Text = $"{_result.HealthPercent}%";
        _kills.Text = _result.DefeatedEnemies.ToString();
        _experience.Text = _result.Experience.ToString();
        _resumeGame = !GF.Base.IsGamePaused;
        if (_resumeGame)
        {
            GF.Base.PauseGame();
        }

        Visible = true;
    }

    /// <summary>
    /// 成绩已经冻结，不轮询战斗实体。
    /// </summary>
    /// <param name="elapseSeconds">游戏秒数。</param>
    /// <param name="realElapseSeconds">实际秒数。</param>
    public void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
    }

    /// <summary>
    /// 读取所属游戏流程的架构入口。
    /// </summary>
    /// <returns>当前流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();

    /// <summary>
    /// 显式参数可重新绑定本窗口。
    /// </summary>
    /// <param name="userData">新的打开参数；null 保持当前内容。</param>
    public void OnRefocus(object userData)
    {
        if (userData != null)
        {
            OnOpen(userData);
        }
    }

    /// <summary>
    /// 同步框架分配的窗口层级。
    /// </summary>
    /// <param name="uiGroupDepth">窗口组层级。</param>
    /// <param name="depthInUIGroup">窗口在组内的层级。</param>
    public void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
    {
        _depthInUIGroup = depthInUIGroup;
        ZIndex = depthInUIGroup;
    }

    /// <summary>
    /// 释放结果引用与暂停所有权。
    /// </summary>
    private void ReleaseResultAndPause()
    {
        _result = null;
        if (_resumeGame && IsInstanceValid(GF.Base))
        {
            GF.Base.ResumeGame();
        }

        _resumeGame = false;
    }

    /// <summary>
    /// 等待流程导航期间防止重复提交。
    /// </summary>
    private void DisableNavigation()
    {
        _returnMap.Disabled = _retry.Disabled = true;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }

    /// <summary>
    /// 请求返回地图，由流程等待检查点并清理关卡。
    /// </summary>
    private void OnMapClicked()
    {
        if (_result == null || _returnMap.Disabled)
        {
            return;
        }

        DisableNavigation();
        MapRequested?.Invoke(SerialId);
    }

    /// <summary>
    /// 失败时请求重新挑战。
    /// </summary>
    private void OnRetryClicked()
    {
        if (_result?.Outcome != LevelOutcome.Defeated || _retry.Disabled)
        {
            return;
        }

        DisableNavigation();
        RetryRequested?.Invoke(SerialId);
    }
}
