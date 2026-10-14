using System;
using GameConfig.Attributes;
using GameConfig.Equipment;
using GameLogic.Equipment;
using GameLogic.Heroes;
using GameLogic.Profile;
using Godot;
using GodotGameFramework;

namespace GameLogic.UI.Widgets;

/// <summary>个人资料面板统一显示身份、穿戴、最终属性和经验，不修改角色状态。</summary>
public partial class InventoryCharacterPanel : Control
{
    [Export] private Label m_Name; // 角色昵称。
    [Export] private Label m_Level; // 当前等级。
    [Export] private HeroPreviewNode m_Preview; // 独立角色预览。
    [Export] private EquipmentCell m_WeaponSlot; // 当前武器槽。
    [Export] private EquipmentCell m_ArmorSlot; // 当前防具槽。
    [Export] private Label m_Hp; // 当前生命与上限。
    [Export] private Label m_Mp; // 当前魔法与上限。
    [Export] private Label m_Power; // 最终攻击。
    [Export] private Label m_Def; // 最终防御。
    [Export] private Label m_Lucky; // 最终幸运。
    [Export] private Label m_Mdef; // 魔法减伤比例。
    [Export] private Label m_Crit; // 暴击概率。
    [Export] private Label m_Miss; // 闪避概率。
    [Export] private Label m_HpRegen; // 每次恢复生命。
    [Export] private Label m_MpRegen; // 每次恢复魔法。
    [Export] private TextureProgressBar m_Experience; // 当前等级经验比例。
    [Export] private Label m_ExperienceText; // 当前经验与升级需求。

    /// <summary>玩家请求卸下当前穿戴部位，由窗口执行事务。</summary>
    public event Action<EquipmentSlot> UnequipRequested;

    /// <summary>穿戴槽直接向所属面板提交卸装请求。</summary>
    public override void _Ready()
    {
        m_WeaponSlot.Clicked += OnWeaponClicked;
        m_ArmorSlot.Clicked += OnArmorClicked;
    }

    /// <summary>每次窗口打开从预览待机起点开始。</summary>
    public void ResetPreview() => m_Preview.Reset();

    /// <summary>战斗暂停时由窗口驱动真实时间预览。</summary>
    public void AdvancePreview() => m_Preview.Advance();

    /// <summary>从同一组状态刷新个人资料与预览，不拥有或修改状态。</summary>
    /// <param name="hero">当前存活的出战角色。</param>
    /// <param name="progression">当前角色成长。</param>
    /// <param name="equipment">当前角色穿戴状态。</param>
    public void Bind(HeroEntity hero, HeroProgression progression, HeroEquipment equipment)
    {
        m_Name.Text = hero.Config.NameCn;
        m_Level.Text = progression.Level.ToString();
        var attributes = hero.Attributes;
        var vitals = hero.Vitals;
        var battle = ConfigSystem.Instance.Tables.TbBattleConfig;
        m_Hp.Text = $"{vitals.Hp} / {vitals.MaxHp}";
        m_Mp.Text = $"{vitals.Mp} / {vitals.MaxMp}";
        m_Power.Text = attributes.Power.ToString();
        m_Def.Text = attributes.Def.ToString();
        m_Lucky.Text = attributes.Lucky.ToString();
        m_Mdef.Text = Percent(attributes.Get(AttributeType.Mdef), battle.MdefKHero);
        m_Crit.Text = Percent(attributes.Get(AttributeType.Crit), battle.CritK);
        m_Miss.Text = Percent(attributes.Get(AttributeType.Miss), battle.MissKHero);
        m_HpRegen.Text = attributes.HpRegen.ToString();
        m_MpRegen.Text = attributes.MpRegen.ToString();
        m_Experience.Value = progression.MaxExperience == 0 ? 100 : 100.0 * progression.Experience / progression.MaxExperience;
        m_ExperienceText.Text = progression.MaxExperience == 0 ? "MAX" : $"{progression.Experience}/{progression.MaxExperience}";
        BindSlot(m_WeaponSlot, equipment.Weapon);
        BindSlot(m_ArmorSlot, equipment.Armor);
        m_Preview.ApplyAppearance(equipment.Weapon != null, equipment.Armor != null);
    }

    /// <summary>装配穿戴槽的图标及详情输入。</summary>
    /// <param name="cell">本面板的穿戴槽。</param>
    /// <param name="item">当前装备；null 恢复为空槽。</param>
    private static void BindSlot(EquipmentCell cell, EquipmentInstance item)
    {
        var definition = item == null ? null : ConfigSystem.Instance.Tables.TbEquipmentConfig.Get(item.EquipmentId);
        cell.Bind(item, definition, definition == null ? null : GF.Resource.LoadAsset<Texture2D>(definition.IconPath));
    }

    /// <summary>将属性点按 x/(x+K) 转换为显示百分比。</summary>
    /// <param name="value">已汇总属性点。</param>
    /// <param name="coefficient">对应战斗系数。</param>
    /// <returns>同等级、无穿透对手下的概率或减伤文字。</returns>
    private static string Percent(float value, float coefficient) =>
        $"{(value <= 0 ? 0 : 100.0 * value / (value + coefficient)):0.#}%";

    /// <summary>提交武器卸装请求。</summary>
    /// <param name="instanceId">当前槽的装备实例编号。</param>
    private void OnWeaponClicked(Guid instanceId) => UnequipRequested?.Invoke(EquipmentSlot.Weapon);

    /// <summary>提交防具卸装请求。</summary>
    /// <param name="instanceId">当前槽的装备实例编号。</param>
    private void OnArmorClicked(Guid instanceId) => UnequipRequested?.Invoke(EquipmentSlot.Armor);
}
