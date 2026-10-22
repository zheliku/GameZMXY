using GameLogic.Battle;
using GameLogic.Profile;
using GameLogic.Level;
using GameLogic.Architecture;
using System;
using GameFramework.UI;
using Godot;
using GodotGameFramework;
using GameLogic.Audio;
using GameConfig.Sound;
using GameLogic.Enemies;
using GameConfig.Skills;
using GameLogic.Skills;
using GameLogic.Config;
using System.Collections.Generic;

namespace GameLogic.UI;

/// <summary>
/// 战斗 HUD：绑定模型属性并观察实体资源，变化时刷新被动控件；由 GGF 管理打开、关闭和复用。
/// 不写任何状态，不执行玩法结算。
/// </summary>
public partial class BattleForm : IController
{
    private BattleFormData m_FormData; // 本次打开的状态源引用，关闭后释放。
    private AbstractEnemyEntity m_Opponent; // 本次观察的守关怪，实体所有权仍归关卡。

    /// <summary>由所属流程处理 SettingsRequested；参数为本次窗口编号。</summary>
    public event Action<int> SettingsRequested;

    /// <summary>由所属流程处理 InventoryRequested；参数为本次窗口编号。</summary>
    public event Action<int> InventoryRequested;

    /// <summary>由所属流程处理 SkillsRequested；参数为本次窗口编号。</summary>
    public event Action<int> SkillsRequested;

    /// <summary>战斗界面通过输入映射请求背包或技能，暂停或覆盖时不响应。</summary>
    /// <param name="inputEvent">输入映射处理的事件。</param>
    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (m_FormData == null || !Visible || GF.Base.IsGamePaused)
            return;
        bool inventory = inputEvent.IsActionPressed(Input.InputSystem.InventoryToggle);
        bool skills = inputEvent.IsActionPressed(Input.InputSystem.SkillToggle);
        if (!inventory && !skills)
            return;
        GetViewport().SetInputAsHandled();
        if (inventory)
            OnInventoryClicked();
        else
            OnSkillsClicked();
    }

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
            m_Menu.SettingsSelected += OnSettingsClicked;
            m_Menu.InventorySelected += OnInventoryClicked;
            m_Menu.SkillsSelected += OnSkillsClicked;
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
    /// <param name="userData">携带本次英雄与场景的 <see cref="BattleFormData"/>。</param>
    /// <exception cref="ArgumentException">打开参数类型错误。</exception>
    /// <exception cref="InvalidOperationException">必需节点未绑定或英雄未显示。</exception>
    public void OnOpen(object userData)
    {
        Unbind();
        Visible = false;
        if (userData is not BattleFormData data)
        {
            throw new ArgumentException("BattleForm 必须接收 BattleFormData。", nameof(userData));
        }

        var levelModel = this.GetModel<LevelModel>();
        var progression = this.GetModel<ProfileModel>();
        if (!IsInstanceValid(data.Hero) || !data.Hero.IsShown || levelModel == null || !levelModel.Initialized)
        {
            throw new InvalidOperationException("BattleForm 绑定的英雄或关卡已经失效。");
        }

        if (m_Status == null || m_Menu == null || m_Go == null || m_EncounterHealthBar == null || m_MusouActivation == null)
        {
            throw new InvalidOperationException("BattleForm 必须绑定状态区、菜单区、Go、守关生命条和无双开启表现。");
        }

        // 先登记本次引用再订阅，部分订阅失败时也能完整退订。
        m_FormData = data;
        try
        {
            this.GetModel<HeroModel>().Hp.Register(RefreshResources);
            this.GetModel<HeroModel>().MaxHp.Register(RefreshResources);
            this.GetModel<HeroModel>().Mp.Register(RefreshResources);
            this.GetModel<HeroModel>().MaxMp.Register(RefreshResources);
            this.GetModel<HeroModel>().Ws.Register(RefreshMusou);
            this.GetModel<HeroModel>().MaxWs.Register(RefreshMusou);
            this.GetModel<HeroModel>().WsActive.Register(RefreshMusouActive);
            progression.Progress.RegisterWithInitValue(RefreshProgression);
            this.GetModel<SkillModel>().GetLearnedSkills(progression.ActiveHeroId).RegisterWithInitValue(RefreshSkills);
            levelModel.TravelAvailable.RegisterWithInitValue(RefreshTravel);
            data.ViewController.FeaturedOpponentChanged += RefreshEncounter;
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
        SettingsRequested = null;
        InventoryRequested = null;
        SkillsRequested = null;
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

    /// <summary>界面轮询；数值显示由状态源变更事件驱动，计时类显示（冷却、Buff 剩余）以后在这里读取。</summary>
    /// <param name="elapseSeconds">游戏帧间隔秒数。</param>
    /// <param name="realElapseSeconds">实际帧间隔秒数。</param>
    public void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        if (m_FormData != null)
            RefreshEncounterHealth();
    }

    /// <summary>读取所属游戏流程的架构入口。</summary>
    /// <returns>当前流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();

    /// <summary>重新绑定显式传入的打开参数。</summary>
    /// <param name="userData">新绑定参数；null 保持当前绑定。</param>
    public void OnRefocus(object userData)
    {
        if (userData != null)
        {
            OnOpen(userData);
        }
    }

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
        if (m_FormData == null)
            return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        SettingsRequested?.Invoke(SerialId);
    }

    /// <summary>请关卡流程打开战斗背包。</summary>
    private void OnInventoryClicked()
    {
        if (m_FormData == null)
            return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        InventoryRequested?.Invoke(SerialId);
    }

    /// <summary>请关卡流程打开技能目录。</summary>
    private void OnSkillsClicked()
    {
        if (m_FormData == null)
            return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        SkillsRequested?.Invoke(SerialId);
    }

    /// <summary>释放本次订阅与表现任务，重复调用安全。</summary>
    private void Unbind()
    {
        var data = m_FormData;
        m_FormData = null;
        if (data != null)
        {
            // 关窗先于模型移除和实体隐藏，直接解除本周期的对应订阅。
            this.GetModel<HeroModel>().Hp.UnRegister(RefreshResources);
            this.GetModel<HeroModel>().MaxHp.UnRegister(RefreshResources);
            this.GetModel<HeroModel>().Mp.UnRegister(RefreshResources);
            this.GetModel<HeroModel>().MaxMp.UnRegister(RefreshResources);
            this.GetModel<HeroModel>().Ws.UnRegister(RefreshMusou);
            this.GetModel<HeroModel>().MaxWs.UnRegister(RefreshMusou);
            this.GetModel<HeroModel>().WsActive.UnRegister(RefreshMusouActive);
            this.GetModel<ProfileModel>().Progress.UnRegister(RefreshProgression);
            this.GetModel<SkillModel>().GetLearnedSkills(this.GetModel<ProfileModel>().ActiveHeroId).UnRegister(RefreshSkills);
            this.GetModel<LevelModel>().TravelAvailable.UnRegister(RefreshTravel);
            data.ViewController.FeaturedOpponentChanged -= RefreshEncounter;
        }

        if (IsInstanceValid(m_Menu))
            m_Menu.ResetMusou();
        if (IsInstanceValid(m_MusouActivation))
            m_MusouActivation.Reset();
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
        RefreshResources();
        RefreshMusou();
        RefreshEncounter();
    }

    /// <summary>技能学习或关卡回滚后同步战斗栏颜色。</summary>
    /// <param name="learned">当前英雄的权威学习记录。</param>
    private void RefreshSkills(IReadOnlyList<SkillId> learned) => m_Menu.SetSkills(
        this.GetModel<ConfigurationModel>().GetActive((SkillHero)this.GetModel<ProfileModel>().ActiveHeroId), learned);

    /// <summary>在 HUD 绑定关卡指定的守关对手；该怪物仍由实体系统管理。</summary>
    private void RefreshEncounter() => BindEncounter(m_FormData.ViewController.FeaturedOpponent);

    /// <summary>只在对手改变时重绑生命事件；控件本身只接收完整数值。</summary>
    /// <param name="opponent">关卡指定的怪物；空值解除显示。</param>
    private void BindEncounter(AbstractEnemyEntity opponent)
    {
        m_Opponent = opponent;
        RefreshEncounterHealth();
    }

    /// <summary>从唯一生命状态源读取并更新左名右条；关闭或死亡后隐藏。</summary>
    /// <param name="value">已提交的属性值；同时读取关联上限。</param>
    private void RefreshEncounterHealth(int value = 0)
    {
        if (!IsInstanceValid(m_EncounterHealthBar))
            return;
        if (m_Opponent == null)
            m_EncounterHealthBar.Visible = false;
        else
            m_EncounterHealthBar.SetValue(m_Opponent.Config.NameCn, m_Opponent.Hp, m_Opponent.MaxHp);
    }

    /// <summary>生命与魔法变化：读取当前值与上限刷新显示。</summary>
    /// <param name="value">已提交的属性值；同时读取关联上限。</param>
    private void RefreshResources(int value = 0)
    {
        var vitals = this.GetModel<HeroModel>();
        m_Status.SetResources(vitals.Hp.Value, vitals.MaxHp.Value, vitals.Mp.Value, vitals.MaxMp.Value);
    }

    /// <summary>刷新无双条，并按蓄满状态控制 HUD 专属提示；重复满值通知保持动画进度。</summary>
    /// <param name="value">已提交的属性值；同时读取关联上限。</param>
    private void RefreshMusou(int value = 0)
    {
        var musou = this.GetModel<HeroModel>();
        m_Menu.SetMusou(musou.Ws.Value, musou.MaxWs.Value, musou.IsWsFull);
    }

    /// <summary>开启状态改变时刷新无双表现。</summary>
    /// <param name="active">已经提交的开启状态。</param>
    private void RefreshMusouActive(bool active)
    {
        if (active)
            m_MusouActivation.ShowActivation();
        else
            m_MusouActivation.Reset();
        RefreshMusou();
    }

    /// <summary>刷新等级与经验条（等级、本级经验、上限来自同一次派生）。</summary>
    /// <param name="progression">完整的成长快照。</param>
    private void RefreshProgression(HeroProgressionState progression)
    {
        m_Status.SetProgression(progression.Level, progression.Experience, progression.MaxExperience);
    }

    /// <summary>显示或隐藏前进提示，并同步控制动画。</summary>
    /// <param name="visible">本局已提交的前进窗口。</param>
    private void RefreshTravel(bool visible)
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
