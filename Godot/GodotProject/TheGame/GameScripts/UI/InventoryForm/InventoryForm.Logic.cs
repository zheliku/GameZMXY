using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Attributes;
using GameConfig.Constant;
using GameConfig.Equipment;
using GameConfig.Sound;
using GameFramework.UI;
using GameLogic.Architecture;
using GameLogic.Audio;
using GameLogic.Battle;
using GameLogic.Config;
using GameLogic.Equipment;
using GameLogic.Level;
using GameLogic.Profile;
using Godot;
using GodotGameFramework;

namespace GameLogic.UI;

/// <summary>
/// 观察战斗个人资料与背包，通过命令提交穿戴、出售与赠送请求。
/// </summary>
public partial class InventoryForm : IController
{
    private InventoryFormData _inventoryData; // 本次打开借用的状态源。
    private bool _resumeGame; // 只对称恢复本窗口发起的暂停。

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
            _close.Pressed += OnCloseClicked;
            _actions.EquipRequested += OnEquipRequested;
            _actions.SellRequested += OnSellRequested;
            _actions.GiftRequested += OnGiftRequested;
            _character.UnequipRequested += OnSlotClicked;
            _bag.ItemSelected += OnBagClicked;
            _bag.ViewChanged += CloseTransientSelection;
            _bag.QualitySelected += OnQualitySaleSelected;
            _saleConfirmation.Confirmed += OnQualitySaleConfirmed;
        }
    }

    /// <summary>
    /// 关闭时释放状态源与本窗口拥有的暂停。
    /// </summary>
    /// <param name="isShutdown">是否框架关停。</param>
    /// <param name="userData">关闭参数，不使用。</param>
    public void OnClose(bool isShutdown, object userData)
    {
        ReleaseBindingsAndPause();
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
        ReleaseBindingsAndPause();
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
    /// 绑定当前角色、成长和灵魂，并暂停战斗。
    /// </summary>
    /// <param name="userData">当前关卡的背包打开参数。</param>
    /// <exception cref="ArgumentException">参数类型错误或不是当前出战角色。</exception>
    public void OnOpen(object userData)
    {
        ReleaseBindingsAndPause();
        Visible = false;
        ProfileModel profile = this.GetModel<ProfileModel>();
        LevelModel levelModel = this.GetModel<LevelModel>();
        if (userData is not InventoryFormData data || !IsInstanceValid(data.Hero) || !data.Hero.IsAlive ||
            levelModel == null || !levelModel.Initialized ||
            levelModel.Outcome.Value != LevelOutcome.Running || !profile.HeroIds.Contains(data.Hero.HeroId))
        {
            throw new ArgumentException("背包需要本关存活的出战英雄与档案。", nameof(userData));
        }

        _inventoryData = data;
        // 先记录借用引用，装配失败时也能完整解除订阅与暂停。
        try
        {
            HeroState heroModel = this.GetModel<HeroModel>().GetState(_inventoryData.Hero.HeroId);
            heroModel.Hp.Register(RefreshResources);
            heroModel.Mp.Register(RefreshResources);
            foreach (BindableProperty<int> property in new[]
            {
                heroModel.MaxHp, heroModel.MaxMp,
                heroModel.Power, heroModel.Def, heroModel.Mdef, heroModel.Lucky,
                heroModel.HpRegen, heroModel.MpRegen
            })
            {
                property.Register(RefreshResources);
            }

            heroModel.Crit.Register(RefreshProbability);
            heroModel.Miss.Register(RefreshProbability);
            heroModel.Level.Register(RefreshResources);
            profile.GetProgress(data.Hero.HeroId).Register(RefreshProgression);
            profile.GetSoul(data.Hero.HeroId).Register(RefreshSoul);
            profile.GetBag(data.Hero.HeroId).Register(RefreshItems);
            profile.GetSlots(data.Hero.HeroId).Register(RefreshEquipment);
            levelModel.Outcome.Register(OnLevelEnded);
            _resumeGame = !GF.Base.IsGamePaused;
            if (_resumeGame)
            {
                GF.Base.PauseGame();
            }

            _character.ResetPreview(data.Hero.Appearance);
            RefreshBag(resetPage: true);
            RefreshInventoryPanels();
            Visible = true;
        }
        catch
        {
            ReleaseBindingsAndPause();
            throw;
        }
    }

    /// <summary>
    /// 暂停战斗时，角色预览仍按真实时间播放。
    /// </summary>
    /// <param name="elapseSeconds">游戏秒数。</param>
    /// <param name="realElapseSeconds">真实秒数。</param>
    public void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        if (_inventoryData != null)
        {
            _character.AdvancePreview();
        }
    }

    /// <summary>
    /// 背包暂停期间仍接收关闭动作，拒绝按键自动重复。
    /// </summary>
    /// <param name="inputEvent">已映射的输入事件。</param>
    public override void _Input(InputEvent inputEvent)
    {
        if (_inventoryData != null && Visible && _saleConfirmation.Visible && inputEvent.IsActionPressed(GameLogic.Input.InputSystem.UiCancel))
        {
            GetViewport().SetInputAsHandled();
            _saleConfirmation.Cancel();
            return;
        }

        if (_inventoryData == null || !Visible ||
            !inputEvent.IsActionPressed(GameLogic.Input.InputSystem.ActionOf(GameLogic.Input.InputSystem.InventoryToggle, _inventoryData.Hero.Slot)))
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        OnCloseClicked();
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
    /// 关闭后解除全部借用与订阅。
    /// </summary>
    private void ReleaseBindingsAndPause()
    {
        _actions.Close();
        _bag.Clear();
        _saleConfirmation.Cancel();
        InventoryFormData data = _inventoryData;
        _inventoryData = null;
        if (data != null)
        {
            // 流程先关闭窗口再移除 Model，本次订阅源在关闭前保持同一实例。
            ProfileModel profile = this.GetModel<ProfileModel>();
            HeroState heroModel = this.GetModel<HeroModel>().GetState(data.Hero.HeroId);
            heroModel.Hp.UnRegister(RefreshResources);
            heroModel.Mp.UnRegister(RefreshResources);
            foreach (BindableProperty<int> property in new[]
            {
                heroModel.MaxHp, heroModel.MaxMp,
                heroModel.Power, heroModel.Def, heroModel.Mdef, heroModel.Lucky,
                heroModel.HpRegen, heroModel.MpRegen
            })
            {
                property.UnRegister(RefreshResources);
            }

            heroModel.Crit.UnRegister(RefreshProbability);
            heroModel.Miss.UnRegister(RefreshProbability);
            heroModel.Level.UnRegister(RefreshResources);
            profile.GetProgress(data.Hero.HeroId).UnRegister(RefreshProgression);
            profile.GetSoul(data.Hero.HeroId).UnRegister(RefreshSoul);
            profile.GetBag(data.Hero.HeroId).UnRegister(RefreshItems);
            profile.GetSlots(data.Hero.HeroId).UnRegister(RefreshEquipment);
            this.GetModel<LevelModel>().Outcome.UnRegister(OnLevelEnded);
        }

        if (_resumeGame && IsInstanceValid(GF.Base))
        {
            GF.Base.ResumeGame();
        }

        _resumeGame = false;
    }

    /// <summary>
    /// 关卡结局后不保留旧英雄窗口。
    /// </summary>
    /// <param name="outcome">已提交的结局。</param>
    private void OnLevelEnded(LevelOutcome outcome)
    {
        if (outcome != LevelOutcome.Running)
        {
            GF.UI.CloseUIForm(SerialId);
        }
    }

    /// <summary>
    /// 关闭个人资料，继续本关。
    /// </summary>
    private void OnCloseClicked()
    {
        if (_inventoryData == null)
        {
            return;
        }

        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        GF.UI.CloseUIForm(SerialId);
    }

    /// <summary>
    /// 拉取一致状态；百分比按同等级、无穿透对手显示基础概率和减伤。
    /// </summary>
    private void RefreshInventoryPanels()
    {
        CloseTransientSelection();
        RefreshCharacter();
        RefreshBag();
    }

    /// <summary>
    /// 把本窗口英雄的完整成长与穿戴值交给个人资料面板。
    /// </summary>
    private void RefreshCharacter()
    {
        ProfileModel profile = this.GetModel<ProfileModel>();
        int heroId = _inventoryData.Hero.HeroId;
        _character.Refresh(_inventoryData.Hero, profile.GetProgress(heroId).Value, profile.GetSlots(heroId).Value);
    }

    /// <summary>
    /// 把本窗口英雄的背包、容量与余额交给背包面板。
    /// </summary>
    /// <param name="resetPage">是否开始新的窗口周期并回到第一页。</param>
    private void RefreshBag(bool resetPage = false)
    {
        ProfileModel profile = this.GetModel<ProfileModel>();
        int heroId = _inventoryData.Hero.HeroId;
        _bag.Refresh(heroId, profile.Capacity, profile.GetBag(heroId), profile.GetSoul(heroId).Value, resetPage);
    }

    /// <summary>
    /// 生命、魔法变化只刷新个人资料。
    /// </summary>
    /// <param name="value">已提交的资源值。</param>
    private void RefreshResources(int value) => RefreshCharacter();

    /// <summary>
    /// 概率变化时读取同一模型，保留小数概率而不转成属性点。
    /// </summary>
    /// <param name="value">已提交的 0..1 概率。</param>
    private void RefreshProbability(float value) => RefreshCharacter();

    /// <summary>
    /// 成长变化只刷新个人资料。
    /// </summary>
    /// <param name="progress">已经提交的成长快照。</param>
    private void RefreshProgression(HeroProgressionState progress) => RefreshCharacter();

    /// <summary>
    /// 余额变化刷新背包与出售选项，清除已过期的确认。
    /// </summary>
    /// <param name="soul">已经提交的余额。</param>
    private void RefreshSoul(int soul)
    {
        CloseTransientSelection();
        RefreshBag();
    }

    /// <summary>
    /// 背包集合变化后重新分页和绘制格子。
    /// </summary>
    /// <param name="datas">已领取装备的实时只读视图。</param>
    private void RefreshItems(IReadOnlyList<EquipmentData> datas)
    {
        CloseTransientSelection();
        RefreshBag();
    }

    /// <summary>
    /// 穿戴提交后刷新槽位、预览与角色属性。
    /// </summary>
    /// <param name="slots">完整的穿戴快照。</param>
    private void RefreshEquipment(EquippedItems slots)
    {
        CloseTransientSelection();
        RefreshCharacter();
    }

    /// <summary>
    /// 打开所选背包格的操作菜单；点击本身不改变穿戴。
    /// </summary>
    /// <param name="data">当前可见格提交的装备实例。</param>
    /// <param name="anchor">当前可见格的全局区域。</param>
    private void OnBagClicked(EquipmentData data, Rect2 anchor)
    {
        if (_inventoryData == null || _saleConfirmation.Visible)
        {
            return;
        }

        EquipmentConfig definition = this.GetModel<ConfigurationModel>().Tables.TbEquipmentConfig.Get(data.EquipmentId);
        _actions.Open(data.InstanceId, anchor,
            definition.Owner == EquipmentOwner.Universal ||
             (int)definition.Owner == _inventoryData.Hero.HeroId,
            this.GetModel<ProfileModel>().HeroIds.Count == 2);
    }

    /// <summary>
    /// 所选菜单确认后提交穿戴请求。
    /// </summary>
    /// <param name="instanceId">菜单捕获的背包实例。</param>
    private void OnEquipRequested(Guid instanceId)
    {
        if (_inventoryData != null && this.SendCommand(new EquipEquipmentCommand(_inventoryData.Hero.HeroId, instanceId)))
        {
            GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        }
    }

    /// <summary>
    /// 按配置售价出售背包装备，过期选择不会重复入账。
    /// </summary>
    /// <param name="instanceId">菜单捕获的背包实例。</param>
    private void OnSellRequested(Guid instanceId)
    {
        if (_inventoryData != null && this.SendCommand(new SellEquipmentCommand(_inventoryData.Hero.HeroId, instanceId)))
        {
            GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        }
    }

    /// <summary>
    /// 将背包装备交给阵容中另一名英雄，容量不足复用已有提示。
    /// </summary>
    /// <param name="instanceId">菜单捕获的背包实例。</param>
    private void OnGiftRequested(Guid instanceId)
    {
        if (_inventoryData == null)
        {
            return;
        }

        int fromHeroId = _inventoryData.Hero.HeroId;
        int toHeroId = this.GetModel<ProfileModel>().HeroIds.Single(id => id != fromHeroId);
        if (this.SendCommand(new GiftEquipmentCommand(fromHeroId, toHeroId, instanceId)))
        {
            GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        }
        else
        {
            (GF.UI.GetUIForm(ResourcesCollectionConstant.UIs_save_notice_form) as SaveNoticeForm)?.ShowMessage("对方背包已满！");
        }
    }

    /// <summary>
    /// 请求卸下当前部位；背包满时保持原状态。
    /// </summary>
    /// <param name="slot">被点击的穿戴部位。</param>
    private void OnSlotClicked(EquipmentSlot slot)
    {
        if (_inventoryData != null && this.SendCommand(new UnequipEquipmentCommand(_inventoryData.Hero.HeroId, slot)))
        {
            GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        }
    }

    /// <summary>
    /// 状态刷新或页面切换时使旧操作和待确认交易失效。
    /// </summary>
    private void CloseTransientSelection()
    {
        _actions.Close();
        _saleConfirmation.Cancel();
    }

    /// <summary>
    /// 品质选择只打开确认层，不改变物品和钱包。
    /// </summary>
    /// <param name="quality">子组件当前选择的品质。</param>
    /// <param name="count">完整未穿戴背包中的件数。</param>
    /// <param name="price">未截断收益。</param>
    private void OnQualitySaleSelected(EquipmentQuality quality, int count, long price)
    {
        if (_inventoryData == null || _saleConfirmation.Visible)
        {
            return;
        }

        _actions.Close();
        _saleConfirmation.Open(quality, count, price);
    }

    /// <summary>
    /// 确认层已清除一次性意图，窗口通过命令提交已确认的交易请求。
    /// </summary>
    /// <param name="quality">本次明确确认的品质。</param>
    private void OnQualitySaleConfirmed(EquipmentQuality quality)
    {
        if (_inventoryData != null && this.SendCommand(new SellEquipmentByQualityCommand(_inventoryData.Hero.HeroId, quality)) > 0)
        {
            GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        }
    }
}
