using System;
using GameFramework.UI;
using GameLogic.Audio;
using GameLogic.Event.Navigation;
using GameLogic.Level;
using GameConfig.Sound;
using Godot;
using GodotGameFramework;
using GodotGameFramework.UI;
using GameConfig.Attributes;
using GameConfig.Equipment;
using GameLogic.Equipment;

namespace GameLogic.UI;

/// <summary>观察战斗个人资料与背包，向关卡提交穿脱装备请求。</summary>
public partial class InventoryForm
{
    private InventoryFormData m_Data; // 本次打开借用的状态源。
    private bool m_ResumeGame; // 只对称恢复本窗口发起的暂停。
    /// <summary>初始化框架字段；新实例的按钮只绑定一次。</summary>
    /// <param name="serialId">本次窗口编号。</param>
    /// <param name="uiFormAssetName">窗口资源路径。</param>
    /// <param name="uiGroup">所属窗口组。</param>
    /// <param name="pauseCoveredUIForm">是否暂停被覆盖窗口。</param>
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
        if (isNewInstance)
        {
            m_Close.Pressed += OnCloseClicked;
            m_Actions.EquipRequested += OnEquipRequested;
            m_Actions.SellRequested += OnSellRequested;
            foreach (EquipmentCell cell in m_BagGrid.GetChildren()) cell.Clicked += OnBagClicked;
            m_WeaponSlot.Clicked += _ => OnSlotClicked(EquipmentSlot.Weapon);
            m_ArmorSlot.Clicked += _ => OnSlotClicked(EquipmentSlot.Armor);
            BindBagControls();
        }
    }
    /// <summary>关闭时释放状态源与本窗口拥有的暂停。</summary>
    /// <param name="isShutdown">是否框架关停。</param>
    /// <param name="userData">关闭参数，不使用。</param>
    public void OnClose(bool isShutdown, object userData)
    {
        Release();
        if (!isShutdown && IsInstanceValid(this)) Visible = false;
    }
    /// <summary>回池后清除引用与打开编号，重复释放安全。</summary>
    public void OnRecycle()
    {
        Release();
        m_SerialId = 0;
        m_DepthInUIGroup = 0;
        m_PauseCoveredUIForm = true;
        if (IsInstanceValid(this)) Visible = false;
    }
    /// <summary>界面暂停。</summary>
    public void OnPause() { }
    /// <summary>恢复界面。</summary>
    public void OnResume() { }
    /// <summary>被其他窗口覆盖。</summary>
    public void OnCover() { }
    /// <summary>重新露出窗口。</summary>
    public void OnReveal() { }
    /// <summary>显式参数可重新绑定本窗口。</summary>
    /// <param name="userData">新的打开参数；null 保持当前内容。</param>
    public void OnRefocus(object userData) { if (userData != null) OnOpen(userData); }
    /// <summary>同步框架分配的窗口层级。</summary>
    /// <param name="uiGroupDepth">窗口组层级。</param>
    /// <param name="depthInUIGroup">窗口在组内的层级。</param>
    public void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
    {
        m_DepthInUIGroup = depthInUIGroup;
        ZIndex = depthInUIGroup;
    }

    /// <summary>绑定当前角色、成长和灵魂，并暂停战斗。</summary>
    /// <param name="userData">当前关卡的背包打开参数。</param>
    /// <exception cref="ArgumentException">参数类型错误或不是当前出战角色。</exception>
    public void OnOpen(object userData)
    {
        Release();
        Visible = false;
        if (userData is not InventoryFormData data || !IsInstanceValid(data.Hero) || !data.Hero.IsAlive || !IsInstanceValid(data.Level) ||
            data.Level.Outcome != LevelOutcome.Running || data.Hero.HeroId != data.Profile.ActiveHeroId)
            throw new ArgumentException("背包需要本关存活的出战英雄与档案。", nameof(userData));
        m_Data = data;
        // 先记录借用引用，装配失败时也能完整解除订阅与暂停。
        try
        {
            data.Hero.Vitals.Changed += Refresh;
            data.Profile.ActiveHero.Changed += Refresh;
            data.Profile.Wallet.Changed += Refresh;
            data.Profile.Inventory.Changed += Refresh;
            data.Profile.ActiveEquipment.Changed += Refresh;
            data.Level.Ended += OnLevelEnded;
            m_ResumeGame = !GF.Base.IsGamePaused;
            if (m_ResumeGame) GF.Base.PauseGame();
            m_Preview.Reset();
            ResetBagView();
            Refresh();
            Visible = true;
        }
        catch
        {
            Release();
            throw;
        }
    }

    /// <summary>暂停战斗时，角色预览仍按真实时间播放。</summary>
    /// <param name="elapseSeconds">游戏秒数。</param>
    /// <param name="realElapseSeconds">真实秒数。</param>
    public void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        if (m_Data != null) m_Preview.Advance();
    }

    /// <summary>关闭后解除全部借用与订阅。</summary>
    private void Release()
    {
        m_Actions.Close();
        m_QualityOptions.Hide();
        CancelQualitySale();
        if (m_Data != null)
        {
            m_Data.Hero.Vitals.Changed -= Refresh;
            m_Data.Profile.ActiveHero.Changed -= Refresh;
            m_Data.Profile.Wallet.Changed -= Refresh;
            m_Data.Profile.Inventory.Changed -= Refresh;
            m_Data.Profile.ActiveEquipment.Changed -= Refresh;
            m_Data.Level.Ended -= OnLevelEnded;
            m_Data = null;
        }
        if (m_ResumeGame && IsInstanceValid(GF.Base)) GF.Base.ResumeGame();
        m_ResumeGame = false;
    }

    /// <summary>关卡结局后不保留旧英雄窗口。</summary>
    /// <param name="outcome">已提交的结局。</param>
    private void OnLevelEnded(LevelOutcome outcome) => GF.UI.CloseUIForm(SerialId);

    /// <summary>关闭个人资料，继续本关。</summary>
    private void OnCloseClicked()
    {
        if (m_Data == null) return;
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
        GF.UI.CloseUIForm(SerialId);
    }

    /// <summary>拉取一致状态；百分比按同等级、无穿透对手显示基础概率和减伤。</summary>
    private void Refresh()
    {
        m_Actions.Close();
        var hero = m_Data.Hero;
        var progression = m_Data.Profile.ActiveHero;
        var attributes = hero.Attributes;
        var vitals = hero.Vitals;
        var battle = ConfigSystem.Instance.Tables.TbBattleConfig;
        m_Name.Text = hero.Config.NameCn;
        m_Level.Text = progression.Level.ToString();
        m_Hp.Text = $"{vitals.Hp} / {vitals.MaxHp}";
        m_Mp.Text = $"{vitals.Mp} / {vitals.MaxMp}";
        m_Attack.Text = attributes.Power.ToString();
        m_Defense.Text = attributes.Def.ToString();
        m_Lucky.Text = attributes.Lucky.ToString();
        m_MagicResistance.Text = Percent(attributes.Get(AttributeType.Mdef), battle.MdefKHero);
        m_Crit.Text = Percent(attributes.Get(AttributeType.Crit), battle.CritK);
        m_Dodge.Text = Percent(attributes.Get(AttributeType.Miss), battle.MissKHero);
        m_HpRegen.Text = attributes.HpRegen.ToString();
        m_MpRegen.Text = attributes.MpRegen.ToString();
        m_Experience.Value = progression.MaxExperience == 0 ? 100 : 100.0 * progression.Experience / progression.MaxExperience;
        m_ExperienceText.Text = progression.MaxExperience == 0 ? "MAX" : $"{progression.Experience}/{progression.MaxExperience}";
        m_Soul.Text = m_Data.Profile.Wallet.Soul.ToString();
        BindSlot(m_WeaponSlot, m_Data.Profile.ActiveEquipment.Weapon);
        BindSlot(m_ArmorSlot, m_Data.Profile.ActiveEquipment.Armor);
        m_Preview.ApplyAppearance(m_Data.Profile.ActiveEquipment.Weapon != null, m_Data.Profile.ActiveEquipment.Armor != null);
        RefreshBag();
    }

    /// <summary>打开所选背包格的操作菜单；点击本身不改变穿戴。</summary>
    /// <param name="instanceId">被点击的背包实例。</param>
    private void OnBagClicked(Guid instanceId)
    {
        if (m_Data == null || m_SelectedTab != 0) return;
        var items = m_Data.Profile.Inventory.Items;
        for (int i = 0; i < m_BagGrid.GetChildCount(); i++)
        {
            int index = m_PageIndex * BagPageSize + i;
            if (index >= items.Count) break;
            if (items[index].InstanceId != instanceId) continue;
            var definition = ConfigSystem.Instance.Tables.TbEquipmentConfig.Get(items[index].EquipmentId);
            m_Actions.Open(instanceId, m_BagGrid.GetChild<EquipmentCell>(i).GetGlobalRect(),
                (int)definition.Owner == m_Data.Profile.ActiveHeroId);
            return;
        }
    }

    /// <summary>所选菜单确认后提交穿戴请求。</summary>
    /// <param name="instanceId">菜单捕获的背包实例。</param>
    private void OnEquipRequested(Guid instanceId)
    {
        if (m_Data?.Level.TryEquip(instanceId) == true) GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }

    /// <summary>按配置售价出售背包装备，过期选择不会重复入账。</summary>
    /// <param name="instanceId">菜单捕获的背包实例。</param>
    private void OnSellRequested(Guid instanceId)
    {
        if (m_Data?.Level.TrySell(instanceId) == true) GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }

    /// <summary>背包暂停期间仍接收关闭动作，拒绝按键自动重复。</summary>
    /// <param name="inputEvent">已映射的输入事件。</param>
    public override void _Input(InputEvent inputEvent)
    {
        if (m_Data != null && Visible && m_SaleConfirmation.Visible && inputEvent.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            CancelQualitySale();
            return;
        }
        if (m_Data == null || !Visible || !inputEvent.IsActionPressed(GameLogic.Input.InputActions.InventoryToggle)) return;
        GetViewport().SetInputAsHandled();
        OnCloseClicked();
    }

    /// <summary>请求卸下当前部位；背包满时保持原状态。</summary>
    /// <param name="slot">被点击的穿戴部位。</param>
    private void OnSlotClicked(EquipmentSlot slot)
    {
        if (m_Data?.Level.TryUnequip(slot) == true) GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }

    /// <summary>显示穿戴实例及实际属性，空槽使用场景的部位底图。</summary>
    /// <param name="cell">武器或防具格。</param>
    /// <param name="item">当前穿戴实例或空。</param>
    private static void BindSlot(EquipmentCell cell, EquipmentInstance item)
    {
        var definition = item == null ? null : ConfigSystem.Instance.Tables.TbEquipmentConfig.Get(item.EquipmentId);
        cell.Bind(item, definition, definition == null ? null : GF.Resource.LoadAsset<Texture2D>(definition.IconPath));
    }

    /// <summary>将战斗的 x/(x+K) 显示为百分比，不把属性点当作百分数。</summary>
    /// <param name="value">已汇总的非负属性点。</param>
    /// <param name="coefficient">战斗表系数。</param>
    /// <returns>基础概率或减伤比例。</returns>
    private static string Percent(float value, float coefficient) =>
        $"{(value <= 0 ? 0 : 100.0 * value / (value + coefficient)):0.#}%";
}
