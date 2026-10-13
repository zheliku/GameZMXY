using System;
using GameConfig.Equipment;
using GameConfig.Sound;
using GameLogic.Audio;
using Godot;
using GameLogic.UI.Widgets;
using GodotGameFramework;

namespace GameLogic.UI;

/// <summary>背包分类、分页与品质出售入口；物品和灵魂仍由档案事务持有。</summary>
public partial class InventoryForm
{
    private const int BagPageSize = 25; // 场景中固定的 5×5 格布局。
    private const int MaximumBagPages = 3; // 当前界面支持的最多页数。
    private int m_SelectedTab; // 0 装备、1 道具、2 时装、3 经书。
    private int m_PageIndex; // 当前分类内从零开始的页号。
    private EquipmentQuality? m_PendingSaleQuality; // 确认框当前选择；刷新或关闭窗口即失效。

    /// <summary>新窗口只绑定一次按钮与下拉菜单，不在刷新时累积订阅。</summary>
    private void BindBagControls()
    {
        for (int i = 0; i < m_Tabs.GetChildCount(); i++)
        {
            int index = i;
            m_Tabs.GetChild<TextureButton>(i).Pressed += () => SelectBagTab(index);
        }
        m_PreviousPage.Pressed += () => ChangeBagPage(-1);
        m_NextPage.Pressed += () => ChangeBagPage(1);
        m_SellQuality.Pressed += OnSellQualityPressed;
        m_QualityOptions.IdPressed += OnQualitySaleSelected;
        m_SaleCancel.Pressed += CancelQualitySale;
        m_SaleConfirm.Pressed += OnQualitySaleConfirmed;
    }

    /// <summary>每次打开回到装备第一页；超出当前显示能力的配置明确报错。</summary>
    private void ResetBagView()
    {
        if (m_Data.Profile.Inventory.Capacity > BagPageSize * MaximumBagPages)
            throw new InvalidOperationException("背包容量超过当前三页界面的显示上限。");
        m_SelectedTab = 0;
        m_PageIndex = 0;
    }

    /// <summary>切换分类，同时关闭旧装备操作和悬停提示。</summary>
    /// <param name="index">被点击的分类下标。</param>
    private void SelectBagTab(int index)
    {
        if (m_Data == null) return;
        m_SelectedTab = index;
        m_PageIndex = 0;
        RefreshBag();
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }

    /// <summary>在当前分类可用的页号范围内翻页。</summary>
    /// <param name="offset">上一页 -1、下一页 1。</param>
    private void ChangeBagPage(int offset)
    {
        if (m_Data == null) return;
        m_PageIndex = Math.Clamp(m_PageIndex + offset, 0, BagPageCount() - 1);
        RefreshBag();
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }

    /// <summary>装备页数由背包容量决定，其他尚无物品存储的分类显示一页空格。</summary>
    /// <returns>当前分类总页数。</returns>
    private int BagPageCount() => m_SelectedTab == 0
        ? (m_Data.Profile.Inventory.Capacity + BagPageSize - 1) / BagPageSize : 1;

    /// <summary>只绑定当前页格子；异类页不借用装备内容或保留旧图标。</summary>
    private void RefreshBag()
    {
        m_Actions.Close();
        m_QualityOptions.Hide();
        CancelQualitySale();
        m_BagGrid.Hide();
        for (int i = 0; i < m_Tabs.GetChildCount(); i++)
            m_Tabs.GetChild<TextureButton>(i).SetPressedNoSignal(i == m_SelectedTab);
        var items = m_Data.Profile.Inventory.Items;
        var definitions = ConfigSystem.Instance.Tables.TbEquipmentConfig;
        for (int i = 0; i < m_BagGrid.GetChildCount(); i++)
        {
            int index = m_PageIndex * BagPageSize + i;
            var item = m_SelectedTab == 0 && index < items.Count ? items[index] : null;
            var definition = item == null ? null : definitions.Get(item.EquipmentId);
            m_BagGrid.GetChild<EquipmentCell>(i).Bind(item, definition,
                definition == null ? null : GF.Resource.LoadAsset<Texture2D>(definition.IconPath));
        }
        m_PageNumber.Text = $"{m_PageIndex + 1} / {BagPageCount()}";
        m_PreviousPage.Disabled = m_PageIndex == 0;
        m_NextPage.Disabled = m_PageIndex + 1 == BagPageCount();
        m_PreviousPage.MouseDefaultCursorShape = m_PreviousPage.Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
        m_NextPage.MouseDefaultCursorShape = m_NextPage.Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
        m_PreviousPage.SelfModulate = m_PreviousPage.Disabled ? new Color(.55f, .55f, .55f) : Colors.White;
        m_NextPage.SelfModulate = m_NextPage.Disabled ? new Color(.55f, .55f, .55f) : Colors.White;
        m_SellQuality.Disabled = m_SelectedTab != 0;
        m_SellQuality.MouseDefaultCursorShape = m_SellQuality.Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
        m_BagGrid.Show();
    }

    /// <summary>在按钮上方展开品质菜单，右边界与按钮对齐。</summary>
    private void OnSellQualityPressed()
    {
        if (m_Data == null || m_SelectedTab != 0 || m_SaleConfirmation.Visible) return;
        RefreshSaleOptions();
        Vector2I size = (Vector2I)m_QualityOptions.GetContentsMinimumSize().Ceil();
        Rect2 button = m_SellQuality.GetScreenTransform() * new Rect2(Vector2.Zero, m_SellQuality.Size);
        Vector2I position = new((int)button.End.X - size.X, (int)button.Position.Y - size.Y - 4);
        m_QualityOptions.Popup(new Rect2I(position, size));
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }

    /// <summary>展开时按最新完整背包统计各品质，包含未显示的第二、三页。</summary>
    private void RefreshSaleOptions()
    {
        var popup = m_QualityOptions;
        popup.Clear();
        foreach (EquipmentQuality quality in Enum.GetValues<EquipmentQuality>())
        {
            var (count, price) = GetQualitySaleSummary(quality);
            string name = quality.DisplayName();
            popup.AddItem($"出售{name}装备（{count}）", (int)quality);
            int index = popup.ItemCount - 1;
            bool overflow = price > int.MaxValue - m_Data.Profile.Wallet.Soul;
            popup.SetItemDisabled(index, count == 0 || overflow);
            popup.SetItemTooltip(index, overflow ? "出售后灵魂将超过上限" : $"获得 {price} 灵魂");
        }
    }

    /// <summary>统计完整背包内指定品质的出售数量和收益，排除已穿戴装备。</summary>
    /// <param name="quality">需要统计的品质。</param>
    /// <returns>件数与尚未截断的灵魂收益。</returns>
    private (int Count, long Price) GetQualitySaleSummary(EquipmentQuality quality)
    {
        int count = 0;
        long price = 0;
        foreach (var item in m_Data.Profile.Inventory.Items)
        {
            var definition = ConfigSystem.Instance.Tables.TbEquipmentConfig.Get(item.EquipmentId);
            if (definition.Quality != quality) continue;
            count++;
            price += definition.SellPrice;
        }
        return (count, price);
    }

    /// <summary>选中品质只展示交易摘要，明确确认前不改变档案。</summary>
    /// <param name="id">菜单保存的装备品质枚举值。</param>
    private void OnQualitySaleSelected(long id)
    {
        if (m_Data == null || m_SelectedTab != 0 || id < 0 || id > int.MaxValue ||
            !Enum.IsDefined((EquipmentQuality)id)) return;
        EquipmentQuality quality = (EquipmentQuality)id;
        var (count, price) = GetQualitySaleSummary(quality);
        if (count == 0 || price > int.MaxValue - m_Data.Profile.Wallet.Soul) return;
        m_Actions.Close();
        m_QualityOptions.Hide();
        m_PendingSaleQuality = quality;
        m_SaleSummary.Text = $"出售全部「{quality.DisplayName()}」装备？\n共 {count} 件，可获得 {price} 灵魂";
        m_SaleConfirmation.Show();
        m_SaleCancel.GrabFocus();
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }

    /// <summary>取消交易意图；状态变化、窗口关闭与池复用同样清除旧确认。</summary>
    private void CancelQualitySale()
    {
        m_PendingSaleQuality = null;
        m_SaleConfirmation.Hide();
        m_SaleCancel.ReleaseFocus();
        m_SaleConfirm.ReleaseFocus();
    }

    /// <summary>明确确认后提交关卡交易；先清理意图以拒绝重复点击。</summary>
    private void OnQualitySaleConfirmed()
    {
        if (m_Data == null || m_PendingSaleQuality is not EquipmentQuality quality) return;
        CancelQualitySale();
        if (m_Data.Level.SellByQuality(quality) > 0)
            GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }
}
