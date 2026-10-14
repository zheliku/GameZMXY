using System;
using GameConfig.Skills;
using GameConfig.Sound;
using GameFramework.UI;
using GameLogic.Audio;
using GameLogic.Level;
using Godot;
using GodotGameFramework;
using GodotGameFramework.UI;

namespace GameLogic.UI;

/// <summary>造一技能面板：查看当前出战角色的主动/被动目录，观察钱包；购买与实际释放以后接入。</summary>
public partial class SkillForm
{
    private SkillFormData m_Data; // 本次窗口借用的状态源。
    private bool m_ShowPassive; // 当前是否展示五项被动。
    private bool m_ResumeGame; // 只恢复本窗口发起的暂停。

    /// <summary>初始化框架字段；按钮只在实例创建时绑定一次。</summary>
    /// <param name="serialId">本次窗口编号。</param>
    /// <param name="uiFormAssetName">窗口资源路径。</param>
    /// <param name="uiGroup">所属界面组。</param>
    /// <param name="pauseCoveredUIForm">是否暂停被覆盖界面。</param>
    /// <param name="isNewInstance">是否新建实例。</param>
    /// <param name="userData">本次打开参数。</param>
    public void OnInit(int serialId, string uiFormAssetName, IUIGroup uiGroup, bool pauseCoveredUIForm, bool isNewInstance, object userData)
    {
        m_SerialId = serialId;
        m_UIFormAssetName = uiFormAssetName;
        m_UIGroup = uiGroup;
        m_DepthInUIGroup = 0;
        m_PauseCoveredUIForm = pauseCoveredUIForm;
        UIStringKeys.ForEach(key => key.SetLocalizationValue());
        if (!isNewInstance)
            return;
        m_Back.Pressed += OnBackClicked;
        m_Wukong.Pressed += OnWukongClicked;
        m_Tangseng.Pressed += OnTangsengClicked;
        m_Active.Pressed += OnActiveClicked;
        m_Passive.Pressed += OnPassiveClicked;
    }

    /// <summary>绑定目录、观察灵魂并暂停本关，初始技能均展示为尚未学习。</summary>
    /// <param name="userData">当前关卡的技能展示参数。</param>
    /// <exception cref="ArgumentException">参数或所属关卡无效。</exception>
    public void OnOpen(object userData)
    {
        ReleaseBindingsAndPause();
        Visible = false;
        if (userData is not SkillFormData data || !IsInstanceValid(data.Level) ||
            data.Level.Outcome != LevelOutcome.Running || !Enum.IsDefined(data.Hero))
            throw new ArgumentException("技能面板需要正在运行的关卡与有效角色目录。", nameof(userData));
        m_Data = data;
        try
        {
            data.Wallet.Changed += RefreshSoul;
            data.Level.Ended += OnLevelEnded;
            m_ResumeGame = !GF.Base.IsGamePaused;
            if (m_ResumeGame)
                GF.Base.PauseGame();
            m_ShowPassive = false;
            m_ActivePage.Bind(data.Catalog);
            m_PassivePage.Bind(data.Catalog.GetPassives(1));
            RefreshSoul();
            RefreshPage();
            Visible = true;
        }
        catch
        {
            ReleaseBindingsAndPause();
            throw;
        }
    }

    /// <summary>关闭后退订并恢复本窗口拥有的暂停。</summary>
    /// <param name="isShutdown">是否框架关停。</param>
    /// <param name="userData">关闭参数。</param>
    public void OnClose(bool isShutdown, object userData)
    {
        ReleaseBindingsAndPause();
        if (!isShutdown && IsInstanceValid(this))
            Visible = false;
    }

    /// <summary>池复用时清除编号与本周期引用。</summary>
    public void OnRecycle()
    {
        ReleaseBindingsAndPause();
        m_SerialId = 0;
        m_DepthInUIGroup = 0;
        m_PauseCoveredUIForm = true;
        if (IsInstanceValid(this))
            Visible = false;
    }

    /// <summary>对称释放订阅；重复调用安全。</summary>
    private void ReleaseBindingsAndPause()
    {
        if (m_Data != null)
        {
            m_Data.Wallet.Changed -= RefreshSoul;
            m_Data.Level.Ended -= OnLevelEnded;
            m_Data = null;
        }
        if (m_ResumeGame && IsInstanceValid(GF.Base))
            GF.Base.ResumeGame();
        m_ResumeGame = false;
    }

    /// <summary>显示当前钱包余额；切页不会消费灵魂。</summary>
    private void RefreshSoul() => m_Soul.Text = m_Data.Wallet.Soul.ToString();

    /// <summary>只显示出战角色的目录，其他角色页签没有交互。</summary>
    private void RefreshPage()
    {
        SetHeroButton(m_Wukong, m_Data.Hero == SkillHero.Wukong);
        SetHeroButton(m_Tangseng, m_Data.Hero == SkillHero.Tangseng);
        m_ActivePage.Visible = !m_ShowPassive;
        m_PassivePage.Visible = m_ShowPassive;
        m_ActivePage.SetHero(m_Data.Hero);
    }

    /// <summary>同步角色页签的选择、禁用和鼠标状态。</summary>
    /// <param name="button">角色页签。</param>
    /// <param name="active">是否为当前出战角色。</param>
    private static void SetHeroButton(TextureButton button, bool active)
    {
        button.SetPressedNoSignal(active);
        button.Disabled = !active;
        button.MouseDefaultCursorShape = active ? CursorShape.PointingHand : CursorShape.Arrow;
    }

    /// <summary>切换到悟空主动目录。</summary>
    private void OnWukongClicked() => SelectPage(SkillHero.Wukong, false);
    /// <summary>切换到唐僧主动目录。</summary>
    private void OnTangsengClicked() => SelectPage(SkillHero.Tangseng, false);
    /// <summary>切换当前角色的主动目录。</summary>
    private void OnActiveClicked()
    {
        if (m_Data != null)
            SelectPage(m_Data.Hero, false);
    }
    /// <summary>切换共用被动目录。</summary>
    private void OnPassiveClicked()
    {
        if (m_Data != null)
            SelectPage(m_Data.Hero, true);
    }

    /// <summary>提交一次仅改变显示的切页。</summary>
    /// <param name="hero">当前查看角色。</param>
    /// <param name="passive">是否查看被动。</param>
    private void SelectPage(SkillHero hero, bool passive)
    {
        if (m_Data == null || hero != m_Data.Hero)
            return;
        m_ShowPassive = passive;
        RefreshPage();
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }

    /// <summary>返回关卡并对称恢复暂停。</summary>
    private void OnBackClicked()
    {
        if (m_Data == null)
            return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        GF.UI.CloseUIForm(SerialId);
    }

    /// <summary>所属关卡结束时关闭窗口。</summary>
    /// <param name="outcome">已经提交的关卡结局。</param>
    private void OnLevelEnded(LevelOutcome outcome) => GF.UI.CloseUIForm(SerialId);

    /// <summary>暂停期间接受 Escape 返回关卡。</summary>
    /// <param name="inputEvent">Godot 分发的输入。</param>
    public override void _Input(InputEvent inputEvent)
    {
        if (m_Data == null || !Visible || !inputEvent.IsActionPressed("ui_cancel"))
            return;
        GetViewport().SetInputAsHandled();
        OnBackClicked();
    }

    /// <summary>窗口暂停回调。</summary>
    public void OnPause() { }
    /// <summary>窗口恢复回调。</summary>
    public void OnResume() { }
    /// <summary>窗口被覆盖回调。</summary>
    public void OnCover() { }
    /// <summary>窗口重新露出回调。</summary>
    public void OnReveal() { }
    /// <summary>静态目录不需要逐帧刷新。</summary>
    /// <param name="elapseSeconds">游戏秒数。</param>
    /// <param name="realElapseSeconds">真实秒数。</param>
    public void OnUpdate(float elapseSeconds, float realElapseSeconds) { }
    /// <summary>显式参数重新绑定窗口。</summary>
    /// <param name="userData">新的打开参数。</param>
    public void OnRefocus(object userData)
    {
        if (userData != null)
            OnOpen(userData);
    }
    /// <summary>同步 GGF 分配的组内层级。</summary>
    /// <param name="uiGroupDepth">界面组层级。</param>
    /// <param name="depthInUIGroup">组内深度。</param>
    public void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
    {
        m_DepthInUIGroup = depthInUIGroup;
        ZIndex = depthInUIGroup;
    }
}
