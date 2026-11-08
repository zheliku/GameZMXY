using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Equipment;
using GameConfig.Sound;
using GameLogic.Architecture;
using GameLogic.Audio;
using GameLogic.Equipment;
using Godot;
using GodotGameFramework;

namespace GameLogic.UI.Widgets;

/// <summary>
/// 背包面板统一管理分类、格子、分页、钱包和品质出售选择，不执行交易。
/// </summary>
public partial class InventoryBagPanel : Control, IController
{
    private const int PageSize = 25; // 每页固定 5×5 格。
    private const int MaximumPages = 3; // 当前场景支持的装备页数。
    [Export] private ItemAppearancesResource _appearances; // 与地面掉落共用的物品图标资源。
    [Export] private Control _tabs; // 四个分类按钮的容器。
    [Export] private GridContainer _grid; // 当前页格子容器。
    [Export] private Label _soulText; // 钱包灵魂。
    [Export] private TextureButton _previousPage; // 上一页。
    [Export] private TextureButton _nextPage; // 下一页。
    [Export] private Label _pageNumber; // 当前页及总页数。
    [Export] private Button _sellQuality; // 品质出售入口。
    [Export] private PopupMenu _qualityOptions; // 品质上拉选项。
    private TextureButton[] _tabButtons; // 本面板的四个分类按钮。
    private EquipmentCell[] _cells; // 当前页的固定格子。
    private readonly EquipmentData[] _equipmentDatas = new EquipmentData[PageSize]; // 与当前页格子一一对应，关闭后清空。
    private int _heroId; // 本周期显示背包的英雄编号，供出售查询使用。
    private int _capacity; // 所属窗口传入的个人背包容量。
    private int _soul; // 最近一次刷新时的个人余额，只用于显示和交互提示。
    private IReadOnlyList<EquipmentData> _items; // 借用本人背包的只读视图，关闭时释放。
    private bool _isBound; // 窗口明确启用/清理本面板，关闭后不处理交互。
    private int _selectedTab; // 当前分类；零为装备。
    private int _pageIndex; // 当前分类内从零开始的页号。

    /// <summary>
    /// 分类或页面变化时通知窗口关闭旧操作和确认。
    /// </summary>
    public event Action ViewChanged;

    /// <summary>
    /// 当前可见装备与格子锚点。
    /// </summary>
    public event Action<EquipmentData, Rect2> ItemSelected;

    /// <summary>
    /// 品质出售选择及未提交交易的摘要。
    /// </summary>
    public event Action<EquipmentQuality, int, long> QualitySelected;

    /// <summary>
    /// 收集固定的分类和格子，交互直接由背包面板处理。
    /// </summary>
    /// <exception cref="InvalidOperationException">分类不是四项或格子不是 5×5。</exception>
    public override void _Ready()
    {
        _tabButtons = _tabs.GetChildren().Cast<TextureButton>().ToArray();
        _cells = _grid.GetChildren().Cast<EquipmentCell>().ToArray();
        if (_tabButtons.Length != 4 || _cells.Length != PageSize)
        {
            throw new InvalidOperationException("背包必须为四个分类和 5×5 格子。");
        }
        // 固定列表的索引只在初始化时捕获，池复用不重复订阅。
        for (int i = 0; i < _tabButtons.Length; i++)
        {
            int index = i;
            _tabButtons[i].Pressed += () => OnTabSelected(index);
        }

        foreach (EquipmentCell cell in _cells)
        {
            cell.Clicked += OnCellClicked;
        }

        _previousPage.Pressed += OnPreviousPageClicked;
        _nextPage.Pressed += OnNextPageClicked;
        _sellQuality.Pressed += OnSellQualityClicked;
        _qualityOptions.IdPressed += OnQualitySelected;
    }

    /// <summary>
    /// 借用所属主流程的共享数据访问入口。
    /// </summary>
    /// <returns>当前流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();

    /// <summary>
    /// 更新只读显示数据；窗口打开时明确重置到装备第一页。
    /// </summary>
    /// <param name="heroId">本窗口所属的参战英雄编号。</param>
    /// <param name="capacity">该英雄的背包容量。</param>
    /// <param name="items">该英雄背包的实时只读视图。</param>
    /// <param name="soul">该英雄已经提交的灵魂余额。</param>
    /// <param name="resetPage">是否开始新的窗口周期。</param>
    /// <exception cref="InvalidOperationException">容量超过当前三页的显示能力。</exception>
    public void Refresh(int heroId, int capacity, IReadOnlyList<EquipmentData> items, int soul, bool resetPage = false)
    {
        if (capacity > PageSize * MaximumPages)
        {
            throw new InvalidOperationException("背包容量超过当前三页界面的显示上限。");
        }

        _heroId = heroId;
        _capacity = capacity;
        _items = items;
        _soul = soul;
        _isBound = true;
        if (resetPage)
        {
            _selectedTab = 0;
            _pageIndex = 0;
        }

        RefreshView();
    }

    /// <summary>
    /// 关闭后停止提交交互，清除当前格子、弹出窗口和数据借用。
    /// </summary>
    public void Clear()
    {
        _isBound = false;
        _items = null;
        Array.Clear(_equipmentDatas);
        foreach (EquipmentCell cell in _cells)
        {
            cell.Bind(null, null);
        }

        _qualityOptions.Hide();
        _sellQuality.Disabled = true;
        _sellQuality.MouseDefaultCursorShape = CursorShape.Arrow;
    }

    /// <summary>
    /// 切换分类，旧页号和交互意图一起失效。
    /// </summary>
    /// <param name="index">当前分类编号。</param>
    private void OnTabSelected(int index)
    {
        if (!_isBound)
        {
            return;
        }

        _selectedTab = index;
        _pageIndex = 0;
        ViewChanged?.Invoke();
        RefreshView();
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }

    /// <summary>
    /// 按本分类的范围钳制新页号。
    /// </summary>
    /// <param name="offset">翻页方向。</param>
    private void OnPageRequested(int offset)
    {
        if (!_isBound)
        {
            return;
        }

        _pageIndex = Math.Clamp(_pageIndex + offset, 0, PageCount() - 1);
        ViewChanged?.Invoke();
        RefreshView();
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }

    /// <summary>
    /// 请求上一页。
    /// </summary>
    private void OnPreviousPageClicked() => OnPageRequested(-1);

    /// <summary>
    /// 请求下一页。
    /// </summary>
    private void OnNextPageClicked() => OnPageRequested(1);

    /// <summary>
    /// 装备按容量分页，尚无数据的分类只显示一页。
    /// </summary>
    /// <returns>当前分类的总页数。</returns>
    private int PageCount()
    {
        return _selectedTab == 0
        ? (_capacity + PageSize - 1) / PageSize
        : 1;
    }

    /// <summary>
    /// 按当前分类和页号刷新全部控件，并关闭过期出售选项。
    /// </summary>
    private void RefreshView()
    {
        IReadOnlyList<EquipmentData> items = _selectedTab == 0 ? _items : null;
        for (int i = 0; i < _tabButtons.Length; i++)
        {
            _tabButtons[i].SetPressedNoSignal(i == _selectedTab);
        }
        // 隐藏期间更新整页，避免相邻格出现新旧输入混合。
        _grid.Hide();
        for (int i = 0; i < _cells.Length; i++)
        {
            int index = _pageIndex * PageSize + i;
            EquipmentData item = items != null && index < items.Count ? items[index] : null;
            _equipmentDatas[i] = item;
            _cells[i].Bind(item,
                item == null ? null : _appearances.Get(item.EquipmentId).InventoryIcon);
        }

        _grid.Show();
        _soulText.Text = _soul.ToString();
        _pageNumber.Text = $"{_pageIndex + 1} / {PageCount()}";
        SetPageButton(_previousPage, _pageIndex > 0);
        SetPageButton(_nextPage, _pageIndex + 1 < PageCount());
        _qualityOptions.Hide();
        _sellQuality.Disabled = items == null;
        _sellQuality.MouseDefaultCursorShape = items == null ? CursorShape.Arrow : CursorShape.PointingHand;
    }

    /// <summary>
    /// 同步翻页按钮的可用状态、鼠标和颜色。
    /// </summary>
    /// <param name="button">翻页按钮。</param>
    /// <param name="enabled">当前是否可以翻页。</param>
    private static void SetPageButton(TextureButton button, bool enabled)
    {
        button.Disabled = !enabled;
        button.MouseDefaultCursorShape = enabled ? CursorShape.PointingHand : CursorShape.Arrow;
        button.SelfModulate = enabled ? Colors.White : new Color(.55f, .55f, .55f);
    }

    /// <summary>
    /// 只提交当前页仍然可见的装备及其点击锚点。
    /// </summary>
    /// <param name="instanceId">格子提交的装备编号。</param>
    private void OnCellClicked(Guid instanceId)
    {
        if (!_isBound || _selectedTab != 0)
        {
            return;
        }

        for (int i = 0; i < _equipmentDatas.Length; i++)
        {
            EquipmentData item = _equipmentDatas[i];
            if (item?.InstanceId != instanceId)
            {
                continue;
            }

            ItemSelected?.Invoke(item, _cells[i].GetGlobalRect());
            return;
        }
    }

    /// <summary>
    /// 统计完整未穿戴背包，在出售按钮上方显示品质选项。
    /// </summary>
    private void OnSellQualityClicked()
    {
        if (!_isBound || _selectedTab != 0)
        {
            return;
        }

        _qualityOptions.Clear();
        foreach (EquipmentQuality quality in Enum.GetValues<EquipmentQuality>())
        {
            (int count, long price) = this.SendQuery(new EquipmentSaleSummaryQuery(_heroId, quality));
            _qualityOptions.AddItem($"出售{quality.DisplayName()}装备（{count}）", (int)quality);
            int index = _qualityOptions.ItemCount - 1;
            bool overflow = price > int.MaxValue - _soul;
            _qualityOptions.SetItemDisabled(index, count == 0 || overflow);
        }

        // 用按钮自身的屏幕变换定位，缩放窗口时也保持右对齐。
        var size = (Vector2I)_qualityOptions.GetContentsMinimumSize().Ceil();
        Rect2 button = _sellQuality.GetScreenTransform() * new Rect2(Vector2.Zero, _sellQuality.Size);
        _qualityOptions.Popup(new Rect2I(new Vector2I((int)button.End.X - size.X, (int)button.Position.Y - size.Y - 4),
            size));
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }

    /// <summary>
    /// 重读当前背包以拒绝过期选择，交易确认交给窗口。
    /// </summary>
    /// <param name="id">菜单保存的品质枚举编号。</param>
    private void OnQualitySelected(long id)
    {
        if (!_isBound || _selectedTab != 0 || id < 0 || id > int.MaxValue ||
            !Enum.IsDefined((EquipmentQuality)id))
        {
            return;
        }

        var quality = (EquipmentQuality)id;
        (int count, long price) = this.SendQuery(new EquipmentSaleSummaryQuery(_heroId, quality));
        if (count == 0 || price > int.MaxValue - _soul)
        {
            return;
        }

        _qualityOptions.Hide();
        QualitySelected?.Invoke(quality, count, price);
        GF.Sound.PlayConfigured(SoundId.MenuConfirm);
    }
}
