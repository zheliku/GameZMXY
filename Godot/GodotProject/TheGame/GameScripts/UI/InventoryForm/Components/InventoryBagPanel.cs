using GameLogic.Architecture;
using System;
using System.Linq;
using GameConfig.Equipment;
using GameConfig.Sound;
using GameLogic.Audio;
using GameLogic.Equipment;
using GameLogic.Profile;
using Godot;
using GodotGameFramework;

namespace GameLogic.UI.Widgets;

/// <summary>背包面板统一管理分类、格子、分页、钱包和品质出售选择，不执行交易。</summary>
public partial class InventoryBagPanel : Control, IController
{
    private const int PageSize = 25; // 每页固定 5×5 格。
    private const int MaximumPages = 3; // 当前场景支持的装备页数。
    [Export] private ItemAppearancesResource m_Appearances; // 与地面掉落共用的物品图标资源。
    [Export] private Control m_Tabs; // 四个分类按钮的容器。
    [Export] private GridContainer m_Grid; // 当前页格子容器。
    [Export] private Label m_SoulText; // 钱包灵魂。
    [Export] private TextureButton m_PreviousPage; // 上一页。
    [Export] private TextureButton m_NextPage; // 下一页。
    [Export] private Label m_PageNumber; // 当前页及总页数。
    [Export] private Button m_SellQuality; // 品质出售入口。
    [Export] private PopupMenu m_QualityOptions; // 品质上拉选项。
    private TextureButton[] m_TabButtons; // 本面板的四个分类按钮。
    private EquipmentCell[] m_Cells; // 当前页的固定格子。
    private readonly EquipmentData[] m_EquipmentDatas = new EquipmentData[PageSize]; // 与当前页格子一一对应，关闭后清空。
    private bool m_IsBound; // 窗口明确启用/清理本面板，关闭后不处理交互。
    private int m_SelectedTab; // 当前分类；零为装备。
    private int m_PageIndex; // 当前分类内从零开始的页号。

    /// <summary>分类或页面变化时通知窗口关闭旧操作和确认。</summary>
    public event Action ViewChanged;

    /// <summary>当前可见装备与格子锚点。</summary>
    public event Action<EquipmentData, Rect2> ItemSelected;

    /// <summary>品质出售选择及未提交交易的摘要。</summary>
    public event Action<EquipmentQuality, int, long> QualitySelected;

    /// <summary>收集固定的分类和格子，交互直接由背包面板处理。</summary>
    /// <exception cref="InvalidOperationException">分类不是四项或格子不是 5×5。</exception>
    public override void _Ready()
    {
        m_TabButtons = m_Tabs.GetChildren().Cast<TextureButton>().ToArray();
        m_Cells = m_Grid.GetChildren().Cast<EquipmentCell>().ToArray();
        if (m_TabButtons.Length != 4 || m_Cells.Length != PageSize)
            throw new InvalidOperationException("背包必须为四个分类和 5×5 格子。");
        // 固定列表的索引只在初始化时捕获，池复用不重复订阅。
        for (int i = 0; i < m_TabButtons.Length; i++)
        {
            int index = i;
            m_TabButtons[i].Pressed += () => OnTabSelected(index);
        }

        foreach (var cell in m_Cells)
            cell.Clicked += OnCellClicked;
        m_PreviousPage.Pressed += OnPreviousPageClicked;
        m_NextPage.Pressed += OnNextPageClicked;
        m_SellQuality.Pressed += OnSellQualityClicked;
        m_QualityOptions.IdPressed += OnQualitySelected;
    }

    /// <summary>借用所属主流程的共享数据访问入口。</summary>
    /// <returns>当前流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();

    /// <summary>更新只读显示数据；窗口打开时明确重置到装备第一页。</summary>
    /// <param name="resetPage">是否开始新的窗口周期。</param>
    /// <exception cref="InvalidOperationException">容量超过当前三页的显示能力。</exception>
    public void Refresh(bool resetPage = false)
    {
        var inventory = this.GetModel<ProfileModel>();
        if (inventory.Capacity > PageSize * MaximumPages)
            throw new InvalidOperationException("背包容量超过当前三页界面的显示上限。");
        m_IsBound = true;
        if (resetPage)
        {
            m_SelectedTab = 0;
            m_PageIndex = 0;
        }

        RefreshView();
    }

    /// <summary>关闭后停止提交交互，清除当前格子、弹出窗口和数据借用。</summary>
    public void Clear()
    {
        m_IsBound = false;
        Array.Clear(m_EquipmentDatas);
        foreach (var cell in m_Cells)
            cell.Bind(null, null);
        m_QualityOptions.Hide();
        m_SellQuality.Disabled = true;
        m_SellQuality.MouseDefaultCursorShape = CursorShape.Arrow;
    }

    /// <summary>切换分类，旧页号和交互意图一起失效。</summary>
    /// <param name="index">当前分类编号。</param>
    private void OnTabSelected(int index)
    {
        if (!m_IsBound)
            return;
        m_SelectedTab = index;
        m_PageIndex = 0;
        ViewChanged?.Invoke();
        RefreshView();
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }

    /// <summary>按本分类的范围钳制新页号。</summary>
    /// <param name="offset">翻页方向。</param>
    private void OnPageRequested(int offset)
    {
        if (!m_IsBound)
            return;
        m_PageIndex = Math.Clamp(m_PageIndex + offset, 0, PageCount() - 1);
        ViewChanged?.Invoke();
        RefreshView();
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }

    /// <summary>请求上一页。</summary>
    private void OnPreviousPageClicked() => OnPageRequested(-1);

    /// <summary>请求下一页。</summary>
    private void OnNextPageClicked() => OnPageRequested(1);

    /// <summary>装备按容量分页，尚无数据的分类只显示一页。</summary>
    /// <returns>当前分类的总页数。</returns>
    private int PageCount() => m_SelectedTab == 0
        ? (this.GetModel<ProfileModel>().Capacity + PageSize - 1) / PageSize
        : 1;

    /// <summary>按当前分类和页号刷新全部控件，并关闭过期出售选项。</summary>
    private void RefreshView()
    {
        var items = m_SelectedTab == 0 ? this.GetModel<ProfileModel>().EquipmentDatas : null;
        for (int i = 0; i < m_TabButtons.Length; i++)
            m_TabButtons[i].SetPressedNoSignal(i == m_SelectedTab);
        // 隐藏期间更新整页，避免相邻格出现新旧输入混合。
        m_Grid.Hide();
        for (int i = 0; i < m_Cells.Length; i++)
        {
            int index = m_PageIndex * PageSize + i;
            var item = items != null && index < items.Count ? items[index] : null;
            m_EquipmentDatas[i] = item;
            m_Cells[i].Bind(item,
                item == null ? null : m_Appearances.Get(item.EquipmentId).InventoryIcon);
        }

        m_Grid.Show();
        m_SoulText.Text = this.GetModel<ProfileModel>().Soul.Value.ToString();
        m_PageNumber.Text = $"{m_PageIndex + 1} / {PageCount()}";
        SetPageButton(m_PreviousPage, m_PageIndex > 0);
        SetPageButton(m_NextPage, m_PageIndex + 1 < PageCount());
        m_QualityOptions.Hide();
        m_SellQuality.Disabled = items == null;
        m_SellQuality.MouseDefaultCursorShape = items == null ? CursorShape.Arrow : CursorShape.PointingHand;
    }

    /// <summary>同步翻页按钮的可用状态、鼠标和颜色。</summary>
    /// <param name="button">翻页按钮。</param>
    /// <param name="enabled">当前是否可以翻页。</param>
    private static void SetPageButton(TextureButton button, bool enabled)
    {
        button.Disabled = !enabled;
        button.MouseDefaultCursorShape = enabled ? CursorShape.PointingHand : CursorShape.Arrow;
        button.SelfModulate = enabled ? Colors.White : new Color(.55f, .55f, .55f);
    }

    /// <summary>只提交当前页仍然可见的装备及其点击锚点。</summary>
    /// <param name="instanceId">格子提交的装备编号。</param>
    private void OnCellClicked(Guid instanceId)
    {
        if (!m_IsBound || m_SelectedTab != 0)
            return;
        for (int i = 0; i < m_EquipmentDatas.Length; i++)
        {
            var item = m_EquipmentDatas[i];
            if (item?.InstanceId != instanceId)
                continue;
            ItemSelected?.Invoke(item, m_Cells[i].GetGlobalRect());
            return;
        }
    }

    /// <summary>统计完整未穿戴背包，在出售按钮上方显示品质选项。</summary>
    private void OnSellQualityClicked()
    {
        if (!m_IsBound || m_SelectedTab != 0)
            return;
        m_QualityOptions.Clear();
        foreach (EquipmentQuality quality in Enum.GetValues<EquipmentQuality>())
        {
            var (count, price) = this.SendQuery(new EquipmentSaleSummaryQuery(quality));
            m_QualityOptions.AddItem($"出售{quality.DisplayName()}装备（{count}）", (int)quality);
            int index = m_QualityOptions.ItemCount - 1;
            bool overflow = price > int.MaxValue - this.GetModel<ProfileModel>().Soul.Value;
            m_QualityOptions.SetItemDisabled(index, count == 0 || overflow);
            m_QualityOptions.SetItemTooltip(index, overflow ? "出售后灵魂将超过上限" : $"获得 {price} 灵魂");
        }

        // 用按钮自身的屏幕变换定位，缩放窗口时也保持右对齐。
        Vector2I size = (Vector2I)m_QualityOptions.GetContentsMinimumSize().Ceil();
        Rect2 button = m_SellQuality.GetScreenTransform() * new Rect2(Vector2.Zero, m_SellQuality.Size);
        m_QualityOptions.Popup(new Rect2I(new Vector2I((int)button.End.X - size.X, (int)button.Position.Y - size.Y - 4),
            size));
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }

    /// <summary>重读当前背包以拒绝过期选择，交易确认交给窗口。</summary>
    /// <param name="id">菜单保存的品质枚举编号。</param>
    private void OnQualitySelected(long id)
    {
        if (!m_IsBound || m_SelectedTab != 0 || id < 0 || id > int.MaxValue ||
            !Enum.IsDefined((EquipmentQuality)id))
            return;
        var quality = (EquipmentQuality)id;
        var (count, price) = this.SendQuery(new EquipmentSaleSummaryQuery(quality));
        if (count == 0 || price > int.MaxValue - this.GetModel<ProfileModel>().Soul.Value)
            return;
        m_QualityOptions.Hide();
        QualitySelected?.Invoke(quality, count, price);
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }
}
