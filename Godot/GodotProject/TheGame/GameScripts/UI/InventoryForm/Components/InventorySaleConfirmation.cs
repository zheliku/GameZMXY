using System;
using GameConfig.Equipment;
using Godot;

namespace GameLogic.UI.Widgets;

/// <summary>品质出售确认拥有待确认意图和摘要；确认仅提交一次，实际交易由窗口执行。</summary>
public partial class InventorySaleConfirmation : Control
{
    [Export] private Label m_SaleSummary; // 待确认件数和收益。
    [Export] private Button m_SaleCancel; // 取消并清除意图。
    [Export] private Button m_SaleConfirm; // 明确确认。
    private EquipmentQuality? m_PendingQuality; // 当前未提交意图；隐藏、状态变化及复用前清除。

    /// <summary>玩家明确确认本次品质出售。</summary>
    public event Action<EquipmentQuality> Confirmed;

    /// <summary>绑定本弹层按钮并清除编辑器预览状态。</summary>
    public override void _Ready()
    {
        m_SaleCancel.Pressed += Cancel;
        m_SaleConfirm.Pressed += OnConfirmed;
        Cancel();
    }

    /// <summary>显示尚未提交的出售摘要并将默认焦点放在取消。</summary>
    /// <param name="quality">所选品质。</param>
    /// <param name="count">未穿戴装备件数。</param>
    /// <param name="price">未截断收益。</param>
    public void Open(EquipmentQuality quality, int count, long price)
    {
        m_PendingQuality = quality;
        m_SaleSummary.Text = $"出售全部「{quality.DisplayName()}」装备？\n共 {count} 件，可获得 {price} 灵魂";
        Show();
        m_SaleCancel.GrabFocus();
    }

    /// <summary>取消或使旧状态下的交易意图失效，可重复调用。</summary>
    public void Cancel()
    {
        m_PendingQuality = null;
        Hide();
        m_SaleCancel.ReleaseFocus();
        m_SaleConfirm.ReleaseFocus();
    }

    /// <summary>先清除意图，再提交一次确认，拒绝重复按钮事件。</summary>
    private void OnConfirmed()
    {
        if (m_PendingQuality is not EquipmentQuality quality)
            return;
        Cancel();
        Confirmed?.Invoke(quality);
    }
}
