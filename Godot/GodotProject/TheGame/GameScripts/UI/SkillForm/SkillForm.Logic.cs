using GameLogic.Profile;
using GameLogic.Skills;
using System.Collections.Generic;
using System.Linq;
using GameLogic.Architecture;
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

/// <summary>造一技能面板：观察当前角色的技能书与钱包，提交主动技能购买请求；被动页暂为目录。</summary>
public partial class SkillForm : IController
{
    // ---- 字段 ----

    private bool m_ShowPassive; // 当前是否展示五项被动。
    private bool m_ResumeGame; // 只恢复本窗口发起的暂停。

    // ---- 窗口生命周期 ----

    /// <summary>初始化框架字段；按钮只在实例创建时绑定一次。</summary>
    /// <param name="serialId">本次窗口编号。</param>
    /// <param name="uiFormAssetName">窗口资源路径。</param>
    /// <param name="uiGroup">所属界面组。</param>
    /// <param name="pauseCoveredUIForm">是否暂停被覆盖界面。</param>
    /// <param name="isNewInstance">是否新建实例。</param>
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
        if (!isNewInstance)
            return;
        m_Back.Pressed += OnBackClicked;
        m_Wukong.Pressed += OnWukongClicked;
        m_Tangseng.Pressed += OnTangsengClicked;
        m_Active.Pressed += OnActiveClicked;
        m_Passive.Pressed += OnPassiveClicked;
        m_ActivePage.LearnRequested += OnLearnRequested;
    }

    /// <summary>绑定目录、观察灵魂并暂停本关，初始技能均展示为尚未学习。</summary>
    /// <param name="userData">未使用；状态从所属 Model 读取。</param>
    /// <exception cref="InvalidOperationException">所属关卡或出战角色无效。</exception>
    public void OnOpen(object userData)
    {
        ReleaseBindingsAndPause();
        Visible = false;
        var profile = this.GetModel<ProfileModel>();
        var levelModel = this.GetModel<LevelModel>();
        if (!levelModel.Initialized || levelModel.Outcome.Value != LevelOutcome.Running ||
            !Enum.IsDefined((SkillHero)profile.ActiveHeroId))
            throw new InvalidOperationException("技能面板需要正在运行的关卡与有效出战角色。");
        try
        {
            this.GetModel<ProfileModel>().Soul.RegisterWithInitValue(RefreshSoul);
            this.GetModel<SkillModel>().GetLearnedSkills(profile.ActiveHeroId).RegisterWithInitValue(RefreshLearnedSkills);
            levelModel.Outcome.Register(OnLevelEnded);
            m_ResumeGame = !GF.Base.IsGamePaused;
            if (m_ResumeGame)
                GF.Base.PauseGame();
            m_ShowPassive = false;
            m_ActivePage.Refresh();
            m_PassivePage.Refresh();
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
        m_SerialId = 0;
        m_DepthInUIGroup = 0;
        m_PauseCoveredUIForm = true;
        if (IsInstanceValid(this))
            Visible = false;
    }

    /// <summary>暂停期间接受技能开关键或 Escape 返回关卡；忽略按住产生的重复事件。</summary>
    /// <param name="inputEvent">Godot 分发的输入。</param>
    public override void _Input(InputEvent inputEvent)
    {
        if (!GF.UI.HasUIForm(SerialId) || !Visible ||
            (!inputEvent.IsActionPressed(Input.InputSystem.UiCancel) &&
             !inputEvent.IsActionPressed(Input.InputSystem.SkillToggle)))
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

    /// <summary>读取所属游戏流程的架构入口。</summary>
    /// <returns>当前流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();

    /// <summary>重绑、关闭或装配失败时退订并恢复暂停；调用时 Model 尚未移除。</summary>
    private void ReleaseBindingsAndPause()
    {
        this.GetModel<ProfileModel>().Soul.UnRegister(RefreshSoul);
        this.GetModel<SkillModel>().GetLearnedSkills(this.GetModel<ProfileModel>().ActiveHeroId).UnRegister(RefreshLearnedSkills);
        this.GetModel<LevelModel>().Outcome.UnRegister(OnLevelEnded);

        if (m_ResumeGame && IsInstanceValid(GF.Base))
            GF.Base.ResumeGame();
        m_ResumeGame = false;
    }

    /// <summary>显示当前钱包余额；切页不会消费灵魂。</summary>
    /// <param name="soul">已经提交的余额。</param>
    private void RefreshSoul(int soul) => m_Soul.Text = soul.ToString();

    /// <summary>学习、读档或关卡回滚后重绘同一份技能记录。</summary>
    /// <param name="skills">当前英雄的权威学习列表。</param>
    private void RefreshLearnedSkills(IReadOnlyList<SkillId> skills) => m_ActivePage.Refresh();

    /// <summary>只在本窗口有效周期内提交购买，余额不足复用已有提示动画。</summary>
    /// <param name="skill">点击的技能。</param>
    private void OnLearnRequested(SkillId skill)
    {
        if (!Visible || !GF.UI.HasUIForm(SerialId))
            return;
        var result = this.GetSystem<SkillSystem>().Learn(skill);
        if (result == SkillLearnResult.InsufficientSoul)
            GF.UI.GetAllLoadedUIForms().OfType<SaveNoticeForm>().Single().ShowMessage("灵魂不足！");
        else if (result == SkillLearnResult.Learned)
            GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }

    /// <summary>只显示出战角色的目录，其他角色页签没有交互。</summary>
    private void RefreshPage()
    {
        SkillHero hero = (SkillHero)this.GetModel<ProfileModel>().ActiveHeroId;
        SetHeroButton(m_Wukong, hero == SkillHero.Wukong);
        SetHeroButton(m_Tangseng, hero == SkillHero.Tangseng);
        m_ActivePage.Visible = !m_ShowPassive;
        m_PassivePage.Visible = m_ShowPassive;
        m_ActivePage.SetHero(hero);
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
        if (GF.UI.HasUIForm(SerialId) && Visible)
            SelectPage((SkillHero)this.GetModel<ProfileModel>().ActiveHeroId, false);
    }

    /// <summary>切换共用被动目录。</summary>
    private void OnPassiveClicked()
    {
        if (GF.UI.HasUIForm(SerialId) && Visible)
            SelectPage((SkillHero)this.GetModel<ProfileModel>().ActiveHeroId, true);
    }

    /// <summary>提交一次仅改变显示的切页。</summary>
    /// <param name="hero">当前查看角色。</param>
    /// <param name="passive">是否查看被动。</param>
    private void SelectPage(SkillHero hero, bool passive)
    {
        if (!GF.UI.HasUIForm(SerialId) || !Visible || hero != (SkillHero)this.GetModel<ProfileModel>().ActiveHeroId)
            return;
        m_ShowPassive = passive;
        RefreshPage();
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }

    /// <summary>返回关卡并对称恢复暂停。</summary>
    private void OnBackClicked()
    {
        if (!GF.UI.HasUIForm(SerialId) || !Visible)
            return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        GF.UI.CloseUIForm(SerialId);
    }

    /// <summary>所属关卡结束时关闭窗口。</summary>
    /// <param name="outcome">已经提交的关卡结局。</param>
    private void OnLevelEnded(LevelOutcome outcome)
    {
        if (outcome != LevelOutcome.Running)
            GF.UI.CloseUIForm(SerialId);
    }

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
