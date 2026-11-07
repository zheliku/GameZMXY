using System;
using GameConfig.Attributes;
using GameConfig.Battle;
using GameConfig.Equipment;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Battle.Damage;
using GameLogic.Config;
using GameLogic.Equipment;
using GameLogic.Heroes;
using GameLogic.Profile;
using Godot;
using GodotGameFramework;

namespace GameLogic.UI.Widgets;

/// <summary>
/// 个人资料面板统一显示身份、穿戴、最终属性和经验，不修改角色状态。
/// </summary>
public partial class InventoryCharacterPanel : Control, IController
{
    [Export] private ItemAppearancesResource _appearances; // 与地面掉落共用的物品图标资源。
    [Export] private Label _name; // 角色昵称。
    [Export] private Label _level; // 当前等级。
    [Export] private HeroPreviewNode _preview; // 独立角色预览。
    [Export] private EquipmentCell _weaponSlot; // 当前武器槽。
    [Export] private EquipmentCell _armorSlot; // 当前防具槽。
    [Export] private EquipmentCell _accessorySlot; // 当前饰品槽。
    [Export] private EquipmentCell _treasureSlot; // 当前法宝槽。
    [Export] private Label _hp; // 当前生命与上限。
    [Export] private Label _mp; // 当前魔法与上限。
    [Export] private Label _power; // 最终攻击。
    [Export] private Label _def; // 最终防御。
    [Export] private Label _lucky; // 最终幸运。
    [Export] private Label _mdef; // 魔法减伤比例。
    [Export] private Label _crit; // 暴击概率。
    [Export] private Label _miss; // 闪避概率。
    [Export] private Label _hpRegen; // 每次恢复生命。
    [Export] private Label _mpRegen; // 每次恢复魔法。
    [Export] private TextureProgressBar _experience; // 当前等级经验比例。
    [Export] private Label _experienceText; // 当前经验与升级需求。

    /// <summary>
    /// 玩家请求卸下当前穿戴部位，由窗口执行事务。
    /// </summary>
    public event Action<EquipmentSlot> UnequipRequested;

    /// <summary>
    /// 穿戴槽直接向所属面板提交卸装请求。
    /// </summary>
    public override void _Ready()
    {
        _weaponSlot.Clicked += OnWeaponClicked;
        _armorSlot.Clicked += OnArmorClicked;
        _accessorySlot.Clicked += OnAccessoryClicked;
        _treasureSlot.Clicked += OnTreasureClicked;
    }

    /// <summary>
    /// 借用所属主流程的共享数据访问入口。
    /// </summary>
    /// <returns>当前流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();

    /// <summary>
    /// 每次窗口打开从预览待机起点开始。
    /// </summary>
    /// <param name="appearance">当前英雄共用的外观资源。</param>
    public void ResetPreview(HeroAppearanceResource appearance) => _preview.Reset(appearance);

    /// <summary>
    /// 战斗暂停时由窗口驱动真实时间预览。
    /// </summary>
    public void AdvancePreview() => _preview.Advance();

    /// <summary>
    /// 从同一组状态刷新个人资料与预览，不拥有或修改状态。
    /// </summary>
    /// <param name="hero">当前存活的出战角色。</param>
    public void Refresh(AbstractHeroEntity hero)
    {
        ProfileModel profile = this.GetModel<ProfileModel>();
        _name.Text = hero.Config.NameCn;
        _level.Text = profile.Level.ToString();
        HeroModel heroModel = this.GetModel<HeroModel>();
        TbBattleConfig battle = this.GetModel<ConfigurationModel>().Tables.TbBattleConfig;
        _hp.Text = $"{heroModel.Hp.Value} / {heroModel.MaxHp.Value}";
        _mp.Text = $"{heroModel.Mp.Value} / {heroModel.MaxMp.Value}";
        _power.Text = heroModel.Power.Value.ToString();
        _def.Text = heroModel.Def.Value.ToString();
        _lucky.Text = heroModel.Lucky.Value.ToString();
        _mdef.Text = Percent(heroModel.Mdef.Value, battle.MdefKHero);
        _crit.Text = $"{heroModel.Crit.Value * 100:0.#}%";
        _miss.Text = $"{heroModel.Miss.Value * 100:0.#}%";
        _hpRegen.Text = heroModel.HpRegen.Value.ToString();
        _mpRegen.Text = heroModel.MpRegen.Value.ToString();
        _experience.Value = profile.MaxExperience == 0 ? 100 : 100.0 * profile.Experience / profile.MaxExperience;
        _experienceText.Text = profile.MaxExperience == 0 ? "MAX" : $"{profile.Experience}/{profile.MaxExperience}";
        BindSlot(_weaponSlot, profile.Weapon);
        BindSlot(_armorSlot, profile.Armor);
        BindSlot(_accessorySlot, profile.Accessory);
        BindSlot(_treasureSlot, profile.Treasure);
        _preview.ApplyAppearance(profile.Weapon?.EquipmentId ?? EquipmentId.None, profile.Armor?.EquipmentId ?? EquipmentId.None);
    }

    /// <summary>
    /// 装配穿戴槽的图标及详情输入。
    /// </summary>
    /// <param name="cell">本面板的穿戴槽。</param>
    /// <param name="data">当前装备；null 恢复为空槽。</param>
    private void BindSlot(EquipmentCell cell, EquipmentData data) => cell.Bind(data, data == null ? null : _appearances.Get(data.EquipmentId).InventoryIcon);

    /// <summary>
    /// 格式化战斗系统计算的百分比，不在界面维护第二份属性公式。
    /// </summary>
    /// <param name="value">已汇总属性点。</param>
    /// <param name="coefficient">对应战斗系数。</param>
    /// <returns>同等级、无穿透对手下的概率或减伤文字。</returns>
    private static string Percent(float value, float coefficient) => $"{CombatSystem.CalculateAttributeRatio(value, coefficient) * 100:0.#}%";

    /// <summary>
    /// 提交武器卸装请求。
    /// </summary>
    /// <param name="instanceId">当前槽的装备实例编号。</param>
    private void OnWeaponClicked(Guid instanceId) => UnequipRequested?.Invoke(EquipmentSlot.Weapon);

    /// <summary>
    /// 提交防具卸装请求。
    /// </summary>
    /// <param name="instanceId">当前槽的装备实例编号。</param>
    private void OnArmorClicked(Guid instanceId) => UnequipRequested?.Invoke(EquipmentSlot.Armor);

    /// <summary>
    /// 提交饰品卸装请求。
    /// </summary>
    /// <param name="instanceId">当前槽的装备实例编号。</param>
    private void OnAccessoryClicked(Guid instanceId) => UnequipRequested?.Invoke(EquipmentSlot.Accessory);

    /// <summary>
    /// 提交法宝卸装请求。
    /// </summary>
    /// <param name="instanceId">当前槽的装备实例编号。</param>
    private void OnTreasureClicked(Guid instanceId) => UnequipRequested?.Invoke(EquipmentSlot.Treasure);
}
