using GameLogic.Architecture;
using GameLogic.Event.Navigation;
using GameFramework.UI;
using GameLogic.Audio;
using GameLogic;
using GameLogic.Level;
using GameConfig.Sound;
using Godot;
using GodotGameFramework;
using GodotGameFramework.Localization;
using GodotGameFramework.Sound;
using GodotGameFramework.UI;

namespace GameLogic.UI;

/// <summary>声音和战斗设置：使用 GGF 保存音量，战斗中暂停并提供返回地图入口。</summary>
public partial class SettingsForm : IController
{
    /// <summary>读取所属游戏流程的架构入口。</summary>
    /// <returns>当前流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();
    private bool m_InBattle; // 本次打开时确定的显示模式，关闭后清除。
    private bool m_ShowingHelp; // 帮助复用当前卷轴，关闭后复位。
    private bool m_ResumeGame; // 只恢复由本窗口发起的暂停。
    /// <summary>设置框架字段；按钮事件只在新建实例时绑定一次。</summary>
    /// <param name="serialId">窗口打开编号。</param>
    /// <param name="uiFormAssetName">窗口资源路径。</param>
    /// <param name="uiGroup">所属界面组。</param>
    /// <param name="pauseCoveredUIForm">是否暂停被覆盖的窗口。</param>
    /// <param name="isNewInstance">是否首次创建节点。</param>
    /// <param name="userData">本次打开参数。</param>
    public void OnInit(int serialId, string uiFormAssetName, IUIGroup uiGroup, bool pauseCoveredUIForm, bool isNewInstance, object userData)
    {
        m_SerialId = serialId;
        m_UIFormAssetName = uiFormAssetName;
        m_UIGroup = uiGroup;
        m_DepthInUIGroup = 0;
        m_PauseCoveredUIForm = pauseCoveredUIForm;
        UIStringKeys.ForEach(key => key.SetLocalizationValue());
        if (isNewInstance)
        {
            m_Continue.Pressed += OnContinueClicked;
            m_Close.Pressed += OnCloseClicked;
            m_Sound.Pressed += OnSoundClicked;
            m_ReturnMap.Pressed += OnReturnMapClicked;
            m_ReturnTitle.Pressed += OnReturnTitleClicked;
            m_Help.Pressed += OnHelpClicked;
        }
    }

    /// <summary>绑定本次状态源并立即刷新页面。</summary>
    /// <param name="userData">未使用；是否处于关卡由当前 Model 判断。</param>
    public void OnOpen(object userData)
    {
        ReleaseTransaction();
        var levelModel = this.GetModel<LevelModel>();
        m_InBattle = levelModel?.SessionActive == true;
        if (m_InBattle)
        {
            levelModel.Ended += OnLevelEnded;
            m_ResumeGame = !GF.Base.IsGamePaused;
            if (m_ResumeGame)
                GF.Base.PauseGame();
        }
        m_ShowingHelp = false;
        RefreshOptions();
        RefreshSoundSettings();
        Visible = true;
    }

    /// <summary>释放本次状态源引用，保留可复用的窗口节点。</summary>
    /// <param name="isShutdown">是否框架关停。</param>
    /// <param name="userData">关闭参数，不保存。</param>
    public void OnClose(bool isShutdown, object userData)
    {
        ReleaseTransaction();
        if (!isShutdown && IsInstanceValid(this))
            Visible = false;
    }

    /// <summary>回池时清除本次打开的引用和框架编号。</summary>
    public void OnRecycle()
    {
        m_SerialId = 0;
        m_DepthInUIGroup = 0;
        m_PauseCoveredUIForm = true;
        if (IsInstanceValid(this))
            Visible = false;
    }

    /// <summary>界面暂停；点击导航不依赖轮询。</summary>
    public void OnPause() { }
    /// <summary>恢复界面暂停。</summary>
    public void OnResume() { }
    /// <summary>窗口被覆盖。</summary>
    public void OnCover() { }
    /// <summary>窗口恢复显示。</summary>
    public void OnReveal() { }
    /// <summary>框架要求的更新入口；数值和导航通过事件更新。</summary>
    /// <param name="elapseSeconds">游戏帧间隔秒数。</param>
    /// <param name="realElapseSeconds">真实帧间隔秒数。</param>
    public void OnUpdate(float elapseSeconds, float realElapseSeconds) { }
    /// <summary>重新绑定显式传入的状态源。</summary>
    /// <param name="userData">新打开参数；null 保持当前绑定。</param>
    public void OnRefocus(object userData)
    {
        if (userData != null)
            OnOpen(userData);
    }
    /// <summary>记录框架分配的窗口层级。</summary>
    /// <param name="uiGroupDepth">界面组深度。</param>
    /// <param name="depthInUIGroup">窗口在组内的深度。</param>
    public void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
    {
        m_DepthInUIGroup = depthInUIGroup;
        ZIndex = depthInUIGroup;
    }

    /// <summary>释放运行订阅并对称恢复时间缩放。</summary>
    private void ReleaseTransaction()
    {
        if (m_InBattle)
        {
            m_InBattle = false;
            this.GetModel<LevelModel>().Ended -= OnLevelEnded;
        }
        if (m_ResumeGame && IsInstanceValid(GF.Base))
            GF.Base.ResumeGame();
        m_ResumeGame = false;
    }
    /// <summary>运行结束时关闭设置，避免阻塞流程结局计时。</summary>
    /// <param name="outcome">已经确定的关卡结局。</param>
    private void OnLevelEnded(LevelOutcome outcome) => GF.UI.CloseUIForm(SerialId);
    /// <summary>继续游戏或从帮助回到设置选项。</summary>
    private void OnContinueClicked()
    {
        if (!GF.UI.HasUIForm(SerialId))
            return;
        if (!m_ShowingHelp)
        {
            OnCloseClicked();
            return;
        }
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        m_ShowingHelp = false;
        RefreshOptions();
    }
    /// <summary>右上角叉始终关闭整个设置窗口。</summary>
    private void OnCloseClicked()
    {
        if (!GF.UI.HasUIForm(SerialId))
            return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        GF.UI.CloseUIForm(SerialId);
    }
    /// <summary>按当前上下文排列官方五项，标题只保留三个可用选项。</summary>
    private void RefreshOptions()
    {
        bool battle = m_InBattle;
        m_HelpText.Visible = m_ShowingHelp;
        m_Sound.Visible = m_Help.Visible = !m_ShowingHelp;
        m_ReturnMap.Visible = m_ReturnTitle.Visible = battle && !m_ShowingHelp;
        m_Continue.Text = m_ShowingHelp ? "返回设置" : battle ? "继续游戏" : "返回游戏";
        m_Continue.Position = new Vector2(19, battle || m_ShowingHelp ? 62 : 96);
        m_Sound.Position = new Vector2(19, battle ? 114 : 156);
        m_Help.Position = new Vector2(19, battle ? 218 : 216);
    }
    /// <summary>显示 GGF 所有声音组的统一开关。</summary>
    private void RefreshSoundSettings() => m_Sound.Text = SoundEnabled() ? "关闭声音" : "打开声音";
    /// <summary>读取应用当前是否有任何一类声音开启。</summary>
    /// <returns>任一声音组音量大于零时为 true。</returns>
    private static bool SoundEnabled() => GF.Sound.GetSoundGroup("Music").Volume > 0 || GF.Sound.GetSoundGroup("SFX").Volume > 0 || GF.Sound.GetSoundGroup("UI").Volume > 0;
    /// <summary>统一开关音乐和效果音，并保留静音前的音量。</summary>
    private void OnSoundClicked()
    {
        if (!GF.UI.HasUIForm(SerialId))
            return;
        bool mute = SoundEnabled();
        SetMuted("Music", mute, 0.7f);
        SetMuted("SFX", mute, 1f);
        SetMuted("UI", mute, 1f);
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        RefreshSoundSettings();
    }
    /// <summary>通过 GGF 保存音量及静音前的恢复值。</summary>
    /// <param name="group">声音组。</param>
    /// <param name="mute">是否静音。</param>
    /// <param name="defaultVolume">没有历史恢复值时的默认音量。</param>
    private static void SetMuted(string group, bool mute, float defaultVolume)
    {
        string key = $"{group}.VolumeBeforeMute";
        float volume = GF.Sound.GetSoundGroup(group).Volume;
        if (mute && volume > 0)
            GF.Setting.SetFloat(key, volume);
        GF.Sound.SetVolume(group, mute ? 0 : GF.Setting.GetFloat(key, defaultVolume));
    }
    /// <summary>在当前卷轴显示实际可用的游戏操作。</summary>
    private void OnHelpClicked()
    {
        if (!GF.UI.HasUIForm(SerialId))
            return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        m_ShowingHelp = true;
        RefreshOptions();
    }
    /// <summary>请关卡流程回滚当前关卡并返回标题。</summary>
    private void OnReturnTitleClicked()
    {
        if (!GF.UI.HasUIForm(SerialId) || !m_InBattle)
            return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        GF.Event.Fire(this, TitleRequestedEventArgs.Create(SerialId));
    }
    /// <summary>请关卡流程关闭设置并回滚未完成的关卡。</summary>
    private void OnReturnMapClicked()
    {
        if (!GF.UI.HasUIForm(SerialId) || !m_InBattle)
            return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        GF.Event.Fire(this, MapRequestedEventArgs.Create(SerialId));
    }

}
