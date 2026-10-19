using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Skills;
using Godot;

namespace GameLogic.UI.Widgets;

/// <summary>左下角菜单和无双蓄力表现，按钮只报告选择，不发布跨模块业务事件。</summary>
public partial class BattleMenuPanel : Sprite2D
{
    // ---- 字段 ----

    [Export] private ResourceBar m_WsBar; // 无双蓄力条。
    [Export] private AnimatedSprite2D m_WsMax; // 满值循环闪烁。
    [Export] private TextureButton m_SettingsButton; // 设置入口。
    [Export] private TextureButton m_InventoryButton; // 背包入口。
    [Export] private TextureButton m_SkillsButton; // 技能入口。
    [Export] private Control m_SkillBar; // 五个主动技能图标，按配置顺序显示。
    [Export] private SpriteFrames m_SkillIcons; // 官方战斗栏独立的未学习/已学习图标，不使用学习面板图片。
    private (Control Cell, TextureRect Icon)[] m_SkillCells; // 固定节点只在装配时收集，刷新时按场景顺序取用。

    // ---- 事件 ----

    /// <summary>玩家选择设置菜单。</summary>
    public event Action SettingsSelected;

    /// <summary>玩家选择背包。</summary>
    public event Action InventorySelected;

    /// <summary>玩家选择技能。</summary>
    public event Action SkillsSelected;

    // ---- 节点生命周期 ----

    /// <summary>子按钮只绑定一次，初始不播放满值动画。</summary>
    public override void _Ready()
    {
        m_SkillCells = m_SkillBar.GetChildren().Cast<Control>()
            .Select(cell => (cell, cell.GetNode<TextureRect>("Icon"))).ToArray();
        m_SettingsButton.Pressed += OnSettingsPressed;
        m_InventoryButton.Pressed += OnInventoryPressed;
        m_SkillsButton.Pressed += OnSkillsPressed;
        ResetMusou();
    }

    // ---- 业务方法 ----

    /// <summary>只在满值时循环闪烁，重复满值刷新保持动画进度。</summary>
    /// <param name="value">当前无双。</param>
    /// <param name="maximum">无双上限。</param>
    /// <param name="full">是否蓄满且未处于消耗阶段。</param>
    public void SetMusou(int value, int maximum, bool full)
    {
        m_WsBar.SetValue(value, maximum);
        if (!full)
        {
            ResetMusou();
            return;
        }
        m_WsMax.Show();
        if (!m_WsMax.IsPlaying())
            m_WsMax.Play();
    }

    /// <summary>关闭或改绑时停止并隐藏满值动画。</summary>
    public void ResetMusou()
    {
        m_WsMax.Stop();
        m_WsMax.Hide();
    }

    /// <summary>根据权威学习记录显示灰色或彩色图标，不复制学习状态。</summary>
    /// <param name="definitions">当前角色的有序技能定义。</param>
    /// <param name="learned">已学习技能列表。</param>
    /// <exception cref="InvalidOperationException">当前场景容纳不下角色技能。</exception>
    public void SetSkills(IReadOnlyList<SkillConfig> definitions, IReadOnlyList<SkillId> learned)
    {
        if (definitions.Count > m_SkillCells.Length)
            throw new InvalidOperationException("战斗技能栏的场景格子少于当前角色技能数。");
        for (int i = 0; i < m_SkillCells.Length; i++)
        {
            var (cell, icon) = m_SkillCells[i];
            if (i >= definitions.Count)
            {
                icon.Texture = null;
                cell.TooltipText = "";
                continue;
            }
            var definition = definitions[i];
            bool unlocked = learned.Contains(definition.Id);
            icon.Texture = m_SkillIcons.GetFrameTexture(definition.Id.ToString(), unlocked ? 1 : 0);
            cell.TooltipText = $"{definition.NameCn}（{definition.ManaCost} MP）" + (unlocked ? "" : "\n尚未学习");
        }
    }

    // ---- 私有方法 ----

    /// <summary>报告本块的设置选择。</summary>
    private void OnSettingsPressed() => SettingsSelected?.Invoke();

    /// <summary>报告本块的背包选择。</summary>
    private void OnInventoryPressed() => InventorySelected?.Invoke();

    /// <summary>报告本块的技能选择。</summary>
    private void OnSkillsPressed() => SkillsSelected?.Invoke();
}
