using System;
using GameFramework.UI;
using Godot;
using GodotGameFramework;

namespace GameLogic.UI;

/// <summary>
/// 战斗 HUD：订阅状态源的聚合变更事件，变化时拉取一组一致数值刷新被动控件；由 GGF 管理打开、关闭和复用。
/// 不写任何状态，不执行玩法结算。
/// </summary>
public partial class BattleHud
{
    private BattleHudData m_Data; // 本次打开的状态源引用，关闭后释放。

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
            m_WsBar == null || m_Go == null)
        {
            throw new InvalidOperationException("BattleHud 必须绑定生命、魔法、经验、无双资源条、等级和 Go 节点。");
        }

        // 先登记本次引用再订阅，部分订阅失败时也能完整退订。
        m_Data = data;
        try
        {
            data.Hero.Vitals.Changed += RefreshVitals;
            data.Hero.Musou.Changed += RefreshMusou;
            data.Progression.Changed += RefreshProgression;
            data.Level.TravelAvailableChanged += RefreshTravel;
            RefreshAll(immediate: true);
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
    public void OnDepthChanged(int uiGroupDepth, int depthInGroup) => m_DepthInUIGroup = depthInGroup;

    /// <summary>释放本次订阅与表现任务，重复调用安全。</summary>
    private void Unbind()
    {
        if (m_Data != null)
        {
            m_Data.Hero.Vitals.Changed -= RefreshVitals;
            m_Data.Hero.Musou.Changed -= RefreshMusou;
            m_Data.Progression.Changed -= RefreshProgression;
            m_Data.Level.TravelAvailableChanged -= RefreshTravel;
            m_Data = null;
        }

        m_HpBar?.ResetPresentation();
        m_MpBar?.ResetPresentation();
        m_ExpBar?.ResetPresentation();
        m_WsBar?.ResetPresentation();
        if (IsInstanceValid(m_Go))
        {
            m_Go.Stop();
            m_Go.Visible = false;
        }
    }

    /// <summary>按当前状态刷新全部显示（打开时立即对齐残影）。</summary>
    /// <param name="immediate">是否立即对齐残影。</param>
    private void RefreshAll(bool immediate)
    {
        RefreshVitals(immediate);
        RefreshMusou();
        RefreshProgression();
        RefreshTravel();
    }

    /// <summary>生命与魔法变化：一次读取当前值与上限，不会看到中间态。</summary>
    private void RefreshVitals() => RefreshVitals(immediate: false);

    /// <summary>刷新生命与魔法条。</summary>
    /// <param name="immediate">是否立即对齐残影。</param>
    private void RefreshVitals(bool immediate)
    {
        var vitals = m_Data.Hero.Vitals;
        m_HpBar.SetValue(vitals.Hp, vitals.MaxHp, immediate);
        m_MpBar.SetValue(vitals.Mp, vitals.MaxMp, immediate);
    }

    /// <summary>刷新无双条。</summary>
    private void RefreshMusou() => m_WsBar.SetValue(m_Data.Hero.Musou.Value, m_Data.Hero.Musou.Max);

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
