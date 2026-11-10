using System;
using GameConfig.Sound;
using GameFramework.UI;
using GameLogic;
using GameLogic.Architecture;
using GameLogic.Audio;
using GameLogic.Level;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Localization;
using GodotGameFramework.Sound;
using GodotGameFramework.UI;

namespace GameLogic.UI;

/// <summary>
/// 开始界面：沿用旧项目字体与分隔图片，通过具名点击事件通知流程。
/// </summary>
public partial class StartForm : IController
{
    /// <summary>
    /// 玩家选定人数开始新游戏；参数为本次窗口编号与参战人数（1 或 2）。
    /// </summary>
    public event Action<int, int> NewGameRequested;

    /// <summary>玩家请求读取已有存档；参数为本次窗口编号。</summary>
    public event Action<int> LoadGameRequested;

    /// <summary>
    /// 由所属流程处理 SettingsRequested；参数为本次窗口编号。
    /// </summary>
    public event Action<int> SettingsRequested;

    /// <summary>
    /// 设置框架字段；按钮事件只在新建实例时绑定一次。
    /// </summary>
    /// <param name="serialId">窗口打开编号。</param>
    /// <param name="uiFormAssetName">窗口资源路径。</param>
    /// <param name="uiGroup">所属界面组。</param>
    /// <param name="pauseCoveredUIForm">是否暂停被覆盖的窗口。</param>
    /// <param name="isNewInstance">是否首次创建节点。</param>
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
            _newGame.Pressed += ShowPlayerCountMenu;
            _loadGame.Pressed += OnLoadGameClicked;
            _single.Pressed += () => OnPlayerCountChosen(1);
            _double.Pressed += () => OnPlayerCountChosen(2);
            _back.Pressed += () => ShowMainMenu(playSound: true);
            _settings.Pressed += OnSettingsClicked;
            _help.Pressed += OnHelpClicked;
            _notice.Pressed += OnNoticeClicked;
        }
    }

    /// <summary>
    /// 显示开始页并清除上次的帮助说明。
    /// </summary>
    /// <param name="userData">不使用，无需空的 Data 包装。</param>
    public void OnOpen(object userData)
    {
        _status.Text = "";
        ShowMainMenu(playSound: false);
        Visible = true;
    }

    /// <summary>
    /// 释放本次状态源引用，保留可复用的窗口节点。
    /// </summary>
    /// <param name="isShutdown">是否框架关停。</param>
    /// <param name="userData">关闭参数，不保存。</param>
    public void OnClose(bool isShutdown, object userData)
    {
        NewGameRequested = null;
        LoadGameRequested = null;
        SettingsRequested = null;
        if (!isShutdown && IsInstanceValid(this))
        {
            Visible = false;
        }
    }

    /// <summary>
    /// 回池时清除本次打开的引用和框架编号。
    /// </summary>
    public void OnRecycle()
    {
        _serialId = 0;
        _depthInUIGroup = 0;
        _pauseCoveredUIForm = true;
        if (IsInstanceValid(this))
        {
            Visible = false;
        }
    }

    /// <summary>
    /// 界面暂停；点击导航不依赖轮询。
    /// </summary>
    public void OnPause()
    {
    }

    /// <summary>
    /// 恢复界面暂停。
    /// </summary>
    public void OnResume()
    {
    }

    /// <summary>
    /// 窗口被覆盖。
    /// </summary>
    public void OnCover()
    {
    }

    /// <summary>
    /// 窗口恢复显示。
    /// </summary>
    public void OnReveal()
    {
    }

    /// <summary>
    /// 框架要求的更新入口；数值和导航通过事件更新。
    /// </summary>
    /// <param name="elapseSeconds">游戏帧间隔秒数。</param>
    /// <param name="realElapseSeconds">真实帧间隔秒数。</param>
    public void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
    }

    /// <summary>
    /// 读取所属游戏流程的架构入口。
    /// </summary>
    /// <returns>当前流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();

    /// <summary>
    /// 重新绑定显式传入的状态源。
    /// </summary>
    /// <param name="userData">新打开参数；null 保持当前绑定。</param>
    public void OnRefocus(object userData)
    {
        if (userData != null)
        {
            OnOpen(userData);
        }
    }

    /// <summary>
    /// 记录框架分配的窗口层级。
    /// </summary>
    /// <param name="uiGroupDepth">界面组深度。</param>
    /// <param name="depthInUIGroup">窗口在组内的深度。</param>
    public void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
    {
        _depthInUIGroup = depthInUIGroup;
        ZIndex = depthInUIGroup;
    }

    /// <summary>展开本次新游戏的人数选择。</summary>
    private void ShowPlayerCountMenu()
    {
        if (!GF.UI.HasUIForm(SerialId))
        {
            return;
        }

        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        _mainMenu.Visible = false;
        _playerCountMenu.Visible = true;
        _status.Text = "";
    }

    /// <summary>恢复标题主菜单并清除说明。</summary>
    /// <param name="playSound">返回操作播放音效，首次打开不播放。</param>
    private void ShowMainMenu(bool playSound)
    {
        if (!GF.UI.HasUIForm(SerialId))
        {
            return;
        }

        if (playSound)
        {
            GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        }
        _mainMenu.Visible = true;
        _playerCountMenu.Visible = false;
        _status.Text = "";
    }

    /// <summary>将所选人数交给流程开始选角。</summary>
    /// <param name="count">参战人数（1 或 2）。</param>
    private void OnPlayerCountChosen(int count)
    {
        if (!GF.UI.HasUIForm(SerialId))
        {
            return;
        }

        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        NewGameRequested?.Invoke(SerialId, count);
    }

    /// <summary>
    /// 请求读取已有档案。
    /// </summary>
    private void OnLoadGameClicked()
    {
        if (!GF.UI.HasUIForm(SerialId))
        {
            return;
        }

        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        LoadGameRequested?.Invoke(SerialId);
    }

    /// <summary>
    /// 请当前流程打开声音设置。
    /// </summary>
    private void OnSettingsClicked()
    {
        if (!GF.UI.HasUIForm(SerialId))
        {
            return;
        }

        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        SettingsRequested?.Invoke(SerialId);
    }

    /// <summary>
    /// 显示本项目已经实现的操作说明。
    /// </summary>
    private void OnHelpClicked()
    {
        if (!GF.UI.HasUIForm(SerialId))
        {
            return;
        }

        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        _status.GetParent<ScrollContainer>().ScrollVertical = 0;
        _status.Text = "P1：A/D 移动 · J 攻击 · K 跳跃 · 空格 无双\nP2：←/→ 移动 · 小键盘1 攻击 · 2 跳跃 · 0 无双\n左下角设置中返回地图";
    }

    /// <summary>
    /// 说明目前可玩的内容。
    /// </summary>
    private void OnNoticeClicked()
    {
        if (!GF.UI.HasUIForm(SerialId))
        {
            return;
        }

        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        _status.GetParent<ScrollContainer>().ScrollVertical = 0;
        _status.Text = "当前开放悟空、唐僧与造一篇章。\n仅供个人学习，禁止商用。";
    }
}
