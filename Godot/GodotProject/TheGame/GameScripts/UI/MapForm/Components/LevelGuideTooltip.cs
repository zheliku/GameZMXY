using GameConfig.Enemy;
using GameConfig.Entity;
using GameConfig.Equipment;
using GameLogic.Equipment;
using GameLogic.Level;
using Godot;

namespace GameLogic.UI;

/// <summary>
/// 地图共用的小型关卡浮层；移入装备图标后由原生tooltip显示背包式详情。
/// </summary>
public partial class LevelGuideTooltip : PanelContainer
{
    private const float CloseDelaySeconds = .18f; // 允许鼠标跨越地点与浮层的空隙。
    [Export] private Label _title; // 关卡名称。
    [Export] private Label _description; // 简短表内介绍。
    [Export] private ItemList _enemies; // 同尺寸怪物图与名称，守关对手排在前面。
    [Export] private Godot.Collections.Dictionary<EntityId, Texture2D> _enemyTextures = new(); // 按实体身份装配的正式待机图像。
    [Export] private GridContainer _drops; // 只显示透明装备图标。
    [Export] private ItemAppearancesResource _appearances; // 背包共用物品图像。
    [Export] private PackedScene _equipmentCellScene; // 复用背包图标与装备详情。
    private Control _anchor; // 当前地点，地图关闭后解除。
    private float _closeDelay; // 跨越地点与浮层间8px空隙的宽限。
    private Vector2 _anchorPoint; // 本次鼠标进入地点的位置，避免东海等宽透明按钮将浮层推到远处。

    /// <summary>
    /// 注册可见性清理，并使初始浮层保持隐藏。
    /// </summary>
    public override void _Ready()
    {
        VisibilityChanged += OnVisibilityChanged;
        Hide();
    }

    /// <summary>
    /// 鼠标在地点或浮层内时保持，离开两者后延迟关闭。
    /// </summary>
    /// <param name="delta">真实帧秒数。</param>
    public override void _Process(double delta)
    {
        if (!Visible || _anchor == null)
        {
            return;
        }
        Control hovered = GetViewport().GuiGetHoveredControl();
        if (_anchor.IsVisibleInTree() && hovered != null && (hovered == _anchor ||
            _anchor.IsAncestorOf(hovered) || hovered == this || IsAncestorOf(hovered)))
        {
            _closeDelay = CloseDelaySeconds;
        }
        else
        {
            _closeDelay -= (float)delta;
            if (_closeDelay <= 0)
            {
                Close();
            }
        }
    }

    /// <summary>
    /// 更新当前地点并重算位置，保持字体和图标原比例。
    /// </summary>
    /// <param name="anchor">本次悬停地点。</param>
    /// <param name="data">本次组合查询。</param>
    /// <param name="point">本次进入地点的视口位置。</param>
    public void ShowFor(Control anchor, LevelGuideData data, Vector2 point)
    {
        _anchor = anchor;
        _anchorPoint = point;
        Bind(data);
        _closeDelay = CloseDelaySeconds;
        Show();
        CallDeferred(MethodName.PlaceBesideAnchor);
    }

    /// <summary>
    /// 关闭并解除旧地点引用。
    /// </summary>
    private void Close()
    {
        _anchor = null;
        Hide();
    }

    /// <summary>
    /// 填充短介绍、守关和装备图标，不随机创建实际装备。
    /// </summary>
    /// <param name="data">当前关卡查询结果。</param>
    private void Bind(LevelGuideData data)
    {
        _title.Text = data.Level.NameCn;
        _description.Text = data.Level.Desc.Split('。')[0] + "。";
        _enemies.Clear();
        foreach (EnemyConfig enemy in data.Enemies)
        {
            int index = _enemies.AddItem(enemy.NameCn, _enemyTextures[enemy.EntityId], false);
            _enemies.SetItemTooltipEnabled(index, false);
            if (index == 0)
            {
                _enemies.SetItemCustomFgColor(index, new Color("ffdf9d"));
            }
        }
        foreach (Node child in _drops.GetChildren())
        {
            _drops.RemoveChild(child);
            child.QueueFree();
        }
        foreach (EquipmentConfig item in data.Drops)
        {
            EquipmentCell cell = _equipmentCellScene.Instantiate<EquipmentCell>();
            cell.BindDefinition(item, _appearances.Get(item.Id).InventoryIcon);
            _drops.AddChild(cell);
        }
        ResetSize();
    }

    /// <summary>
    /// 右侧空间不足时放左侧，保持浮层在设计视口边界内。
    /// </summary>
    private void PlaceBesideAnchor()
    {
        if (_anchor == null || !Visible)
        {
            return;
        }
        ResetSize();
        Vector2 viewport = GetViewportRect().Size;
        float x = _anchorPoint.X + 16;
        if (x + Size.X > viewport.X - 8)
        {
            x = _anchorPoint.X - Size.X - 16;
        }
        GlobalPosition = new Vector2(Mathf.Clamp(x, 8, viewport.X - Size.X - 8),
            Mathf.Clamp(_anchorPoint.Y - 30, 8, viewport.Y - Size.Y - 8));
    }

    /// <summary>
    /// 地图隐藏时立即清理，重开不闪现旧卡片。
    /// </summary>
    private void OnVisibilityChanged()
    {
        if (!IsVisibleInTree())
        {
            _anchor = null;
            Hide();
        }
    }
}
