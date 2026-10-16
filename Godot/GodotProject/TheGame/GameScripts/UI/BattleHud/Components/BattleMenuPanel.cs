using System;
using Godot;

namespace GameLogic.UI.Widgets;

/// <summary>左下角菜单和无双蓄力表现，按钮只报告选择，不发布跨模块业务事件。</summary>
public partial class BattleMenuPanel : Sprite2D
{
    [Export] private ResourceBar m_WsBar; // 无双蓄力条。
    [Export] private AnimatedSprite2D m_WsMax; // 满值循环闪烁。
    [Export] private TextureButton m_SettingsButton; // 设置入口。
    [Export] private TextureButton m_InventoryButton; // 背包入口。
    [Export] private TextureButton m_SkillsButton; // 技能入口。

    /// <summary>玩家选择设置菜单。</summary>
    public event Action SettingsSelected;

    /// <summary>玩家选择背包。</summary>
    public event Action InventorySelected;

    /// <summary>玩家选择技能。</summary>
    public event Action SkillsSelected;

    /// <summary>子按钮只绑定一次，初始不播放满值动画。</summary>
    public override void _Ready()
    {
        m_SettingsButton.Pressed += OnSettingsPressed;
        m_InventoryButton.Pressed += OnInventoryPressed;
        m_SkillsButton.Pressed += OnSkillsPressed;
        ResetMusou();
    }

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

    /// <summary>报告本块的设置选择。</summary>
    private void OnSettingsPressed() => SettingsSelected?.Invoke();

    /// <summary>报告本块的背包选择。</summary>
    private void OnInventoryPressed() => InventorySelected?.Invoke();

    /// <summary>报告本块的技能选择。</summary>
    private void OnSkillsPressed() => SkillsSelected?.Invoke();
}
