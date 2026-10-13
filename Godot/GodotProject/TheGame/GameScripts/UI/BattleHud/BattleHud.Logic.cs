using GameLogic.Battle.Resources;
using GameLogic.Event.Navigation;
using System;
using GameFramework.UI;
using Godot;
using GodotGameFramework;
using GameLogic.Audio;
using GameConfig.Sound;
using GameLogic.Monsters;

namespace GameLogic.UI;

/// <summary>
/// 战斗 HUD：订阅状态源的聚合变更事件，变化时拉取一组一致数值刷新被动控件；由 GGF 管理打开、关闭和复用。
/// 不写任何状态，不执行玩法结算。
/// </summary>
public partial class BattleHud
{
    /// <summary>战斗界面通过统一动作请求背包，暂停或覆盖时不响应。</summary>
    /// <param name="inputEvent">输入映射处理的事件。</param>
    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (m_Data == null || !Visible || GF.Base.IsGamePaused ||
            !inputEvent.IsActionPressed(GameLogic.Input.InputActions.InventoryToggle)) return;
        GetViewport().SetInputAsHandled();
        OnInventoryClicked();
    }
    private BattleHudData m_Data; // 本次打开的状态源引用，关闭后释放。
    private MonsterEntity m_Opponent; // 本次观察的守关怪，实体所有权仍归关卡。
    private bool m_WasMusouActive; // 当前绑定观察到的激活边沿，避免每次扣点重播。

    /// <summary>初始化框架字段；业务订阅只在 OnOpen 建立。</summary>
    /// <param name="serialId">界面请求编号。</param>
    /// <param name="uiFormAssetName">界面资源路径。</param>
    /// <param name="uiGroup">所属界面组。</param>
    /// <param name="pauseCoveredUIForm">是否暂停被覆盖界面。</param>
    /// <param name="isNewInstance">是否首次创建实例。</param>
    /// <param name="userData">本次打开参数。</param>
    public void OnInit(int serialId, string uiFormAssetName, IUIGroup uiGroup, bool pauseCoveredUIForm,
        bool isNewInstance, object userData)
    {
        m_SerialId = serialId;
        m_UIFormAssetName = uiFormAssetName;
        m_UIGroup = uiGroup;
        m_DepthInUIGroup = 0;
        m_PauseCoveredUIForm = pauseCoveredUIForm;
        UIStringKeys.ForEach(key => key.SetLocalizationValue());
        if (isNewInstance)
        {
            m_SettingsButton.Pressed += OnSettingsClicked;
            m_InventoryButton.Pressed += OnInventoryClicked;
        }
    }

    /// <summary>回池时重复执行本地解绑，清空框架字段。</summary>
    public void OnRecycle()
    {
        Unbind();
        m_SerialId = 0;
        m_DepthInUIGroup = 0;
        m_PauseCoveredUIForm = true;
        if (IsInstanceValid(this))
        {
            Visible = false;
        }
    }

    /// <summary>验证装配，订阅各状态源的变更事件，并用当前值一次性初始化显示。</summary>
    /// <param name="userData">携带英雄、成长与关卡的 <see cref="BattleHudData"/>。</param>
    /// <exception cref="ArgumentException">打开参数类型错误。</exception>
    /// <exception cref="InvalidOperationException">必需节点未绑定或英雄未显示。</exception>
    public void OnOpen(object userData)
    {
        Unbind();
        Visible = false;
        if (userData is not BattleHudData data)
        {
            throw new ArgumentException("BattleHud 必须接收 BattleHudData。", nameof(userData));
        }

        if (!IsInstanceValid(data.Hero) || !data.Hero.IsShown || !IsInstanceValid(data.Level))
        {
            throw new InvalidOperationException("BattleHud 绑定的英雄或关卡已经失效。");
        }

        if (m_HpBar == null || m_MpBar == null || m_ExpBar == null || m_LevelLabel == null ||
            m_WsBar == null || m_WsMax == null || m_Go == null || m_EncounterHealthBar == null || m_MusouActivation == null)
        {
            throw new InvalidOperationException("BattleHud 必须绑定资源条、无双满值提示、等级、Go 和守关怪提示节点。");
        }

        // 先登记本次引用再订阅，部分订阅失败时也能完整退订。
        m_Data = data;
        m_WasMusouActive = data.Hero.Musou.IsActive;
        try
        {
            data.Hero.Vitals.Changed += RefreshVitals;
            data.Hero.Musou.Changed += RefreshMusou;
            data.Progression.Changed += RefreshProgression;
            data.Level.TravelAvailableChanged += RefreshTravel;
            data.Level.FeaturedOpponentChanged += RefreshEncounter;
            RefreshAll();
            Visible = true;
        }
        catch
        {
            Unbind();
            Visible = false;
            throw;
        }
    }

    /// <summary>关闭时退订，即使窗口仍在场景树内也不再观察旧对象。</summary>
    /// <param name="isShutdown">是否框架关停；此时节点可能已经释放。</param>
    /// <param name="userData">关闭参数，不使用。</param>
    public void OnClose(bool isShutdown, object userData)
    {
        Unbind();
        if (!isShutdown && IsInstanceValid(this))
        {
            Visible = false;
        }
    }

    /// <summary>界面暂停，数据仍保持订阅。</summary>
    public void OnPause() { }

    /// <summary>恢复界面暂停。</summary>
    public void OnResume() { }

    /// <summary>界面被覆盖。</summary>
    public void OnCover() { }

    /// <summary>恢复被覆盖界面。</summary>
    public void OnReveal() { }

    /// <summary>重新绑定显式传入的打开参数。</summary>
    /// <param name="userData">新绑定参数；null 保持当前绑定。</param>
    public void OnRefocus(object userData)
    {
        if (userData != null)
        {
            OnOpen(userData);
        }
    }

    /// <summary>界面轮询；数值显示由状态源变更事件驱动，计时类显示（冷却、Buff 剩余）以后在这里读取。</summary>
    /// <param name="elapseSeconds">游戏帧间隔秒数。</param>
    /// <param name="realElapseSeconds">实际帧间隔秒数。</param>
    public void OnUpdate(float elapseSeconds, float realElapseSeconds) { }

    /// <summary>记录框架分配的界面深度。</summary>
    /// <param name="uiGroupDepth">界面组深度。</param>
    /// <param name="depthInGroup">界面在组内的深度。</param>
    public void OnDepthChanged(int uiGroupDepth, int depthInGroup)
    {
        m_DepthInUIGroup = depthInGroup;
        ZIndex = depthInGroup;
    }

    /// <summary>请关卡流程打开左下角设置菜单。</summary>
    private void OnSettingsClicked()
    {
        if (m_Data == null) return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        GF.Event.Fire(this, SettingsRequestedEventArgs.Create(SerialId));
    }

    /// <summary>请关卡流程打开战斗背包。</summary>
    private void OnInventoryClicked()
    {
        if (m_Data == null) return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        GF.Event.Fire(this, InventoryRequestedEventArgs.Create(SerialId));
    }

    /// <summary>释放本次订阅与表现任务，重复调用安全。</summary>
    private void Unbind()
    {
        if (m_Data != null)
        {
            m_Data.Hero.Vitals.Changed -= RefreshVitals;
            m_Data.Hero.Musou.Changed -= RefreshMusou;
            m_Data.Progression.Changed -= RefreshProgression;
            m_Data.Level.TravelAvailableChanged -= RefreshTravel;
            m_Data.Level.FeaturedOpponentChanged -= RefreshEncounter;
            m_Data = null;
        }

        ResetMusouAnimation();
        if (IsInstanceValid(m_MusouActivation)) m_MusouActivation.Reset();
        m_WasMusouActive = false;
        BindEncounter(null);
        if (IsInstanceValid(m_Go))
        {
            m_Go.Stop();
            m_Go.Visible = false;
        }
    }

    /// <summary>按当前状态立即刷新全部显示。</summary>
    private void RefreshAll()
    {
        RefreshVitals();
        RefreshMusou();
        RefreshProgression();
        RefreshTravel();
        RefreshEncounter();
    }

    /// <summary>在 HUD 绑定关卡指定的守关对手；该怪物仍由实体系统管理。</summary>
    private void RefreshEncounter() => BindEncounter(m_Data.Level.FeaturedOpponent);

    /// <summary>只在对手改变时重绑生命事件；控件本身只接收完整数值。</summary>
    /// <param name="opponent">关卡指定的怪物；空值解除显示。</param>
    private void BindEncounter(MonsterEntity opponent)
    {
        if (m_Opponent != opponent)
        {
            if (m_Opponent != null) m_Opponent.Vitals.Changed -= RefreshEncounterHealth;
            m_Opponent = opponent;
            if (opponent != null) opponent.Vitals.Changed += RefreshEncounterHealth;
        }
        RefreshEncounterHealth();
    }

    /// <summary>从唯一生命状态源读取并更新左名右条；关闭或死亡后隐藏。</summary>
    private void RefreshEncounterHealth()
    {
        if (!IsInstanceValid(m_EncounterHealthBar)) return;
        if (m_Opponent == null) m_EncounterHealthBar.Visible = false;
        else m_EncounterHealthBar.SetValue(m_Opponent.Config.NameCn, m_Opponent.Vitals.Hp, m_Opponent.Vitals.MaxHp);
    }

    /// <summary>生命与魔法变化：一次读取当前值与上限，不会看到中间态。</summary>
    private void RefreshVitals()
    {
        var vitals = m_Data.Hero.Vitals;
        m_HpBar.SetValue(vitals.Hp, vitals.MaxHp);
        m_MpBar.SetValue(vitals.Mp, vitals.MaxMp);
    }

    /// <summary>刷新无双条，并按蓄满状态控制 HUD 专属提示；重复满值通知保持动画进度。</summary>
    private void RefreshMusou()
    {
        var musou = m_Data.Hero.Musou;
        if (musou.IsActive && !m_WasMusouActive) m_MusouActivation.ShowActivation();
        else if (!musou.IsActive) m_MusouActivation.Reset();
        m_WasMusouActive = musou.IsActive;
        m_WsBar.SetValue(musou.Value, musou.Max);
        if (!musou.IsFull)
        {
            ResetMusouAnimation();
            return;
        }

        m_WsMax.Visible = true;
        if (!m_WsMax.IsPlaying())
        {
            m_WsMax.Play();
        }
    }

    /// <summary>停止并复位无双满值提示，关闭、改绑或消耗无双后不残留播放状态。</summary>
    private void ResetMusouAnimation()
    {
        if (IsInstanceValid(m_WsMax))
        {
            m_WsMax.Stop();
            m_WsMax.Visible = false;
        }
    }

    /// <summary>刷新等级与经验条（等级、本级经验、上限来自同一次派生）。</summary>
    private void RefreshProgression()
    {
        var progression = m_Data.Progression;
        m_LevelLabel.Text = progression.Level.ToString();
        m_ExpBar.SetValue(progression.Experience, progression.MaxExperience);
    }

    /// <summary>显示或隐藏前进提示，并同步控制动画。</summary>
    private void RefreshTravel()
    {
        bool visible = m_Data.Level.TravelAvailable;
        m_Go.Visible = visible;
        if (visible)
        {
            m_Go.Play();
        }
        else
        {
            m_Go.Stop();
        }
    }
}
