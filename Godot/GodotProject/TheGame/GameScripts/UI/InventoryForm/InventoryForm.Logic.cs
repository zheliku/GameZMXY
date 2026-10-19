using GameLogic.Battle;
using GameLogic.Profile;
using GameLogic.Config;
using GameLogic.Architecture;
using System;
using GameConfig.Attributes;
using GameFramework.UI;
using GameLogic.Audio;
using GameLogic.Level;
using GameConfig.Sound;
using Godot;
using GodotGameFramework;
using GameConfig.Equipment;
using GameLogic.Equipment;
using System.Collections.Generic;

namespace GameLogic.UI;

/// <summary>观察战斗个人资料与背包，通过命令提交穿戴与出售请求。</summary>
public partial class InventoryForm : IController
{
    private InventoryFormData m_InventoryData; // 本次打开借用的状态源。
    private bool m_ResumeGame; // 只对称恢复本窗口发起的暂停。

    /// <summary>初始化框架字段；新实例的按钮只绑定一次。</summary>
    /// <param name="serialId">本次窗口编号。</param>
    /// <param name="uiFormAssetName">窗口资源路径。</param>
    /// <param name="uiGroup">所属窗口组。</param>
    /// <param name="pauseCoveredUIForm">是否暂停被覆盖窗口。</param>
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
        if (isNewInstance)
        {
            m_Close.Pressed += OnCloseClicked;
            m_Actions.EquipRequested += OnEquipRequested;
            m_Actions.SellRequested += OnSellRequested;
            m_Character.UnequipRequested += OnSlotClicked;
            m_Bag.ItemSelected += OnBagClicked;
            m_Bag.ViewChanged += CloseTransientSelection;
            m_Bag.QualitySelected += OnQualitySaleSelected;
            m_SaleConfirmation.Confirmed += OnQualitySaleConfirmed;
        }
    }

    /// <summary>关闭时释放状态源与本窗口拥有的暂停。</summary>
    /// <param name="isShutdown">是否框架关停。</param>
    /// <param name="userData">关闭参数，不使用。</param>
    public void OnClose(bool isShutdown, object userData)
    {
        ReleaseBindingsAndPause();
        if (!isShutdown && IsInstanceValid(this))
            Visible = false;
    }

    /// <summary>回池后清除引用与打开编号，重复释放安全。</summary>
    public void OnRecycle()
    {
        ReleaseBindingsAndPause();
        m_SerialId = 0;
        m_DepthInUIGroup = 0;
        m_PauseCoveredUIForm = true;
        if (IsInstanceValid(this))
            Visible = false;
    }

    /// <summary>界面暂停。</summary>
    public void OnPause()
    {
    }

    /// <summary>恢复界面。</summary>
    public void OnResume()
    {
    }

    /// <summary>被其他窗口覆盖。</summary>
    public void OnCover()
    {
    }

    /// <summary>重新露出窗口。</summary>
    public void OnReveal()
    {
    }

    /// <summary>绑定当前角色、成长和灵魂，并暂停战斗。</summary>
    /// <param name="userData">当前关卡的背包打开参数。</param>
    /// <exception cref="ArgumentException">参数类型错误或不是当前出战角色。</exception>
    public void OnOpen(object userData)
    {
        ReleaseBindingsAndPause();
        Visible = false;
        var profile = this.GetModel<ProfileModel>();
        var levelModel = this.GetModel<LevelModel>();
        if (userData is not InventoryFormData data || !IsInstanceValid(data.Hero) || !data.Hero.IsAlive ||
            levelModel == null || !levelModel.Initialized ||
            levelModel.Outcome.Value != LevelOutcome.Running || data.Hero.HeroId != profile.ActiveHeroId)
            throw new ArgumentException("背包需要本关存活的出战英雄与档案。", nameof(userData));
        m_InventoryData = data;
        // 先记录借用引用，装配失败时也能完整解除订阅与暂停。
        try
        {
            this.GetModel<HeroModel>().Hp.Register(RefreshResources);
            this.GetModel<HeroModel>().Mp.Register(RefreshResources);
            foreach (AttributeType attribute in Enum.GetValues<AttributeType>())
                this.GetModel<HeroModel>().GetAttribute(attribute).Register(RefreshResources);
            this.GetModel<HeroModel>().Level.Register(RefreshResources);
            profile.Progress.Register(RefreshProgression);
            this.GetModel<ProfileModel>().Soul.Register(RefreshSoul);
            this.GetModel<ProfileModel>().EquipmentDatas.Register(RefreshItems);
            this.GetModel<ProfileModel>().Slots.Register(RefreshEquipment);
            levelModel.Outcome.Register(OnLevelEnded);
            m_ResumeGame = !GF.Base.IsGamePaused;
            if (m_ResumeGame)
                GF.Base.PauseGame();
            m_Character.ResetPreview();
            m_Bag.Refresh(resetPage: true);
            RefreshInventoryPanels();
            Visible = true;
        }
        catch
        {
            ReleaseBindingsAndPause();
            throw;
        }
    }

    /// <summary>暂停战斗时，角色预览仍按真实时间播放。</summary>
    /// <param name="elapseSeconds">游戏秒数。</param>
    /// <param name="realElapseSeconds">真实秒数。</param>
    public void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        if (m_InventoryData != null)
            m_Character.AdvancePreview();
    }

    /// <summary>背包暂停期间仍接收关闭动作，拒绝按键自动重复。</summary>
    /// <param name="inputEvent">已映射的输入事件。</param>
    public override void _Input(InputEvent inputEvent)
    {
        if (m_InventoryData != null && Visible && m_SaleConfirmation.Visible && inputEvent.IsActionPressed(GameLogic.Input.InputSystem.UiCancel))
        {
            GetViewport().SetInputAsHandled();
            m_SaleConfirmation.Cancel();
            return;
        }

        if (m_InventoryData == null || !Visible ||
            !inputEvent.IsActionPressed(GameLogic.Input.InputSystem.InventoryToggle))
            return;
        GetViewport().SetInputAsHandled();
        OnCloseClicked();
    }

    /// <summary>读取所属游戏流程的架构入口。</summary>
    /// <returns>当前流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();

    /// <summary>显式参数可重新绑定本窗口。</summary>
    /// <param name="userData">新的打开参数；null 保持当前内容。</param>
    public void OnRefocus(object userData)
    {
        if (userData != null)
            OnOpen(userData);
    }

    /// <summary>同步框架分配的窗口层级。</summary>
    /// <param name="uiGroupDepth">窗口组层级。</param>
    /// <param name="depthInUIGroup">窗口在组内的层级。</param>
    public void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
    {
        m_DepthInUIGroup = depthInUIGroup;
        ZIndex = depthInUIGroup;
    }

    /// <summary>关闭后解除全部借用与订阅。</summary>
    private void ReleaseBindingsAndPause()
    {
        m_Actions.Close();
        m_Bag.Clear();
        m_SaleConfirmation.Cancel();
        var data = m_InventoryData;
        m_InventoryData = null;
        if (data != null)
        {
            // 流程先关闭窗口再移除 Model，本次订阅源在关闭前保持同一实例。
            var profile = this.GetModel<ProfileModel>();
            this.GetModel<HeroModel>().Hp.UnRegister(RefreshResources);
            this.GetModel<HeroModel>().Mp.UnRegister(RefreshResources);
            foreach (AttributeType attribute in Enum.GetValues<AttributeType>())
                this.GetModel<HeroModel>().GetAttribute(attribute).UnRegister(RefreshResources);
            this.GetModel<HeroModel>().Level.UnRegister(RefreshResources);
            profile.Progress.UnRegister(RefreshProgression);
            this.GetModel<ProfileModel>().Soul.UnRegister(RefreshSoul);
            this.GetModel<ProfileModel>().EquipmentDatas.UnRegister(RefreshItems);
            this.GetModel<ProfileModel>().Slots.UnRegister(RefreshEquipment);
            this.GetModel<LevelModel>().Outcome.UnRegister(OnLevelEnded);
        }

        if (m_ResumeGame && IsInstanceValid(GF.Base))
            GF.Base.ResumeGame();
        m_ResumeGame = false;
    }

    /// <summary>关卡结局后不保留旧英雄窗口。</summary>
    /// <param name="outcome">已提交的结局。</param>
    private void OnLevelEnded(LevelOutcome outcome)
    {
        if (outcome != LevelOutcome.Running)
            GF.UI.CloseUIForm(SerialId);
    }

    /// <summary>关闭个人资料，继续本关。</summary>
    private void OnCloseClicked()
    {
        if (m_InventoryData == null)
            return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        GF.UI.CloseUIForm(SerialId);
    }

    /// <summary>拉取一致状态；百分比按同等级、无穿透对手显示基础概率和减伤。</summary>
    private void RefreshInventoryPanels()
    {
        CloseTransientSelection();
        m_Character.Refresh(m_InventoryData.Hero);
        m_Bag.Refresh();
    }

    /// <summary>生命、魔法变化只刷新个人资料。</summary>
    /// <param name="value">已提交的资源值。</param>
    private void RefreshResources(int value) => m_Character.Refresh(m_InventoryData.Hero);

    /// <summary>成长变化只刷新个人资料。</summary>
    /// <param name="progress">已经提交的成长快照。</param>
    private void RefreshProgression(HeroProgressionState progress) => m_Character.Refresh(m_InventoryData.Hero);

    /// <summary>余额变化刷新背包与出售选项，清除已过期的确认。</summary>
    /// <param name="soul">已经提交的余额。</param>
    private void RefreshSoul(int soul)
    {
        CloseTransientSelection();
        m_Bag.Refresh();
    }

    /// <summary>背包集合变化后重新分页和绘制格子。</summary>
    /// <param name="datas">已领取装备的实时只读视图。</param>
    private void RefreshItems(IReadOnlyList<EquipmentData> datas)
    {
        CloseTransientSelection();
        m_Bag.Refresh();
    }

    /// <summary>穿戴提交后刷新槽位、预览与角色属性。</summary>
    /// <param name="slots">完整的穿戴快照。</param>
    private void RefreshEquipment(EquippedItems slots)
    {
        CloseTransientSelection();
        m_Character.Refresh(m_InventoryData.Hero);
    }

    /// <summary>打开所选背包格的操作菜单；点击本身不改变穿戴。</summary>
    /// <param name="data">当前可见格提交的装备实例。</param>
    /// <param name="anchor">当前可见格的全局区域。</param>
    private void OnBagClicked(EquipmentData data, Rect2 anchor)
    {
        if (m_InventoryData == null || m_SaleConfirmation.Visible)
            return;
        var definition = this.GetModel<ConfigurationModel>().Tables.TbEquipmentConfig.Get(data.EquipmentId);
        m_Actions.Open(data.InstanceId, anchor, (int)definition.Owner == this.GetModel<ProfileModel>().ActiveHeroId);
    }

    /// <summary>所选菜单确认后提交穿戴请求。</summary>
    /// <param name="instanceId">菜单捕获的背包实例。</param>
    private void OnEquipRequested(Guid instanceId)
    {
        if (m_InventoryData != null && this.SendCommand(new EquipEquipmentCommand(instanceId)))
            GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }

    /// <summary>按配置售价出售背包装备，过期选择不会重复入账。</summary>
    /// <param name="instanceId">菜单捕获的背包实例。</param>
    private void OnSellRequested(Guid instanceId)
    {
        if (m_InventoryData != null && this.SendCommand(new SellEquipmentCommand(instanceId)))
            GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }

    /// <summary>请求卸下当前部位；背包满时保持原状态。</summary>
    /// <param name="slot">被点击的穿戴部位。</param>
    private void OnSlotClicked(EquipmentSlot slot)
    {
        if (m_InventoryData != null && this.SendCommand(new UnequipEquipmentCommand(slot)))
            GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }

    /// <summary>状态刷新或页面切换时使旧操作和待确认交易失效。</summary>
    private void CloseTransientSelection()
    {
        m_Actions.Close();
        m_SaleConfirmation.Cancel();
    }

    /// <summary>品质选择只打开确认层，不改变物品和钱包。</summary>
    /// <param name="quality">子组件当前选择的品质。</param>
    /// <param name="count">完整未穿戴背包中的件数。</param>
    /// <param name="price">未截断收益。</param>
    private void OnQualitySaleSelected(EquipmentQuality quality, int count, long price)
    {
        if (m_InventoryData == null || m_SaleConfirmation.Visible)
            return;
        m_Actions.Close();
        m_SaleConfirmation.Open(quality, count, price);
    }

    /// <summary>确认层已清除一次性意图，窗口通过命令提交已确认的交易请求。</summary>
    /// <param name="quality">本次明确确认的品质。</param>
    private void OnQualitySaleConfirmed(EquipmentQuality quality)
    {
        if (m_InventoryData != null && this.SendCommand(new SellEquipmentByQualityCommand(quality)) > 0)
            GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }
}
