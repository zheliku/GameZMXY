using System;
using GameFramework.UI;
using Godot;
using GodotGameFramework;

namespace GameLogic;

/// <summary>战斗 HUD 的绑定与显示逻辑，由 GGF 管理打开、关闭和复用。</summary>
public partial class BattleHud
{
    private BattleHudContext m_Context; // 本次打开的绑定对象，关闭后释放。

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

    /// <summary>验证装配，订阅具体属性实例，并用当前值一次性初始化显示。</summary>
    /// <param name="userData">携带英雄与关卡的 BattleHudContext。</param>
    /// <exception cref="ArgumentException">打开参数类型错误。</exception>
    /// <exception cref="InvalidOperationException">必需节点未绑定或英雄未显示。</exception>
    public void OnOpen(object userData)
    {
        Unbind();
        Visible = false;
        if (userData is not BattleHudContext context)
        {
            throw new ArgumentException("BattleHud 必须接收 BattleHudContext。", nameof(userData));
        }
        if (!IsInstanceValid(context.Hero) || !context.Hero.IsShown ||
            !IsInstanceValid(context.Level))
        {
            throw new InvalidOperationException("BattleHud 绑定的英雄或关卡已经失效。");
        }
        if (m_HpBar == null || m_MpBar == null || m_ExpBar == null || m_LevelLabel == null ||
            m_WsBar == null || m_Go == null)
        {
            throw new InvalidOperationException("BattleHud 必须绑定生命、魔法、经验、无双资源条、等级和 Go 节点。");
        }

        // 先登记本次引用，部分绑定失败时也能完整退订。
        m_Context = context;
        try
        {
            m_HpBar.Bind(context.Hero.Hp, context.Hero.MaxHp);
            m_MpBar.Bind(context.Hero.Mp, context.Hero.MaxMp);
            m_ExpBar.Bind(context.Hero.Experience, context.Hero.MaxExperience);
            m_WsBar.Bind(context.Hero.WsValue, context.Hero.WsMax);
            context.Hero.Level.Changed += RefreshLevel;
            context.Level.TravelAvailable.Changed += SetGo;
            RefreshLevel(context.Hero.Level.Value);
            SetGo(context.Level.TravelAvailable.Value);
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

    /// <summary>界面轮询；显示由属性通知驱动。</summary>
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
        if (m_Context != null)
        {
            m_Context.Hero.Level.Changed -= RefreshLevel;
            m_Context.Level.TravelAvailable.Changed -= SetGo;
            m_Context = null;
        }
        m_HpBar?.Unbind();
        m_MpBar?.Unbind();
        m_ExpBar?.Unbind();
        m_WsBar?.Unbind();
        if (IsInstanceValid(m_Go))
        {
            m_Go.Stop();
            m_Go.Visible = false;
        }
    }

    /// <summary>显示最新等级。</summary>
    /// <param name="level">最新等级。</param>
    private void RefreshLevel(int level) => m_LevelLabel.Text = level.ToString();

    /// <summary>显示或隐藏前进提示，并同步控制动画。</summary>
    /// <param name="visible">是否处于可前进窗口。</param>
    private void SetGo(bool visible)
    {
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
