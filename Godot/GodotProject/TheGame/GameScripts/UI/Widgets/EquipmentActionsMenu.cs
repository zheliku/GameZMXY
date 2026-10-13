using System;
using Godot;

namespace GameLogic.UI;

/// <summary>背包装备的选择面板；只提交所选实例，离开原格与面板后关闭。</summary>
public partial class EquipmentActionsMenu : Control
{
    [Export] private TextureRect m_Panel; // 官方三项面板及按钮容器。
    [Export] private TextureButton m_Equip; // 异角色装备时禁用。
    [Export] private TextureButton m_Sell; // 由窗口向关卡提交出售请求。
    private Guid m_InstanceId; // 当前选择；关闭后立即清空，拒绝过期操作。
    private Rect2 m_Anchor; // 当前装备格的全局区域，指针可在原格与面板间移动。

    /// <summary>玩家选择装备。</summary>
    public event Action<Guid> EquipRequested;
    /// <summary>玩家选择出售。</summary>
    public event Action<Guid> SellRequested;

    /// <summary>场景按钮只绑定一次。</summary>
    public override void _Ready()
    {
        m_Equip.Pressed += OnEquipPressed;
        m_Sell.Pressed += OnSellPressed;
        Close();
    }

    /// <summary>在所选格的中心偏右下显示面板，靠近屏幕边缘时保持完整可见。</summary>
    /// <param name="instanceId">窗口已验证的背包实例。</param>
    /// <param name="anchor">所选格的屏幕区域。</param>
    /// <param name="canEquip">当前出战角色是否可以使用。</param>
    public void Open(Guid instanceId, Rect2 anchor, bool canEquip)
    {
        m_InstanceId = instanceId;
        m_Anchor = anchor;
        m_Equip.Disabled = !canEquip;
        m_Equip.MouseDefaultCursorShape = canEquip ? CursorShape.PointingHand : CursorShape.Arrow;
        Vector2 position = GetGlobalTransform().AffineInverse() * anchor.GetCenter();
        m_Panel.Position = position.Clamp(Vector2.Zero, Size - m_Panel.Size);
        Visible = true;
        SetProcess(true);
    }

    /// <summary>清除选择并停止悬停检查，关闭或复用背包时也调用。</summary>
    public void Close()
    {
        Visible = false;
        m_InstanceId = Guid.Empty;
        SetProcess(false);
    }

    /// <summary>复现官方 rollOut：指针离开装备格及其菜单后关闭。</summary>
    /// <param name="delta">本帧秒数，不用于玩法计时。</param>
    public override void _Process(double delta)
    {
        Vector2 mouse = GetGlobalMousePosition();
        if (!m_Anchor.HasPoint(mouse) && !m_Panel.GetGlobalRect().HasPoint(mouse)) Close();
    }

    /// <summary>清除选择后提交一次装备请求，状态刷新不会重复使用旧选择。</summary>
    private void OnEquipPressed()
    {
        Guid id = m_InstanceId;
        Close();
        if (id != Guid.Empty) EquipRequested?.Invoke(id);
    }

    /// <summary>清除选择后提交一次出售请求，重复按下不重复入账。</summary>
    private void OnSellPressed()
    {
        Guid id = m_InstanceId;
        Close();
        if (id != Guid.Empty) SellRequested?.Invoke(id);
    }
}
