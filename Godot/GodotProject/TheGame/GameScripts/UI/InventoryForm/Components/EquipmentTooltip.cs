using GameConfig.Attributes;
using GameConfig.Equipment;
using GameLogic.Battle.Attributes;
using GameLogic.Equipment;
using GameLogic.UI.Widgets;
using Godot;

namespace GameLogic.UI;

/// <summary>
/// 按造梦三 AttributeCon 排列名称、品质、类型、实际属性与灵魂价值。
/// </summary>
public partial class EquipmentTooltip : PanelContainer
{
    [Export] private Label _name; // 品质色的装备名称。
    [Export] private Label _quality; // 品质值，标题由场景中的独立 Label 绘制。
    [Export] private Label _type; // 部位与可用角色。
    [Export] private VBoxContainer _attributes; // 只显示非零实际属性，每行 25 像素。
    [Export] private Label _description; // 官方说明，空说明不占行。
    [Export] private Label _value; // 出售获得的灵魂。

    /// <summary>
    /// 注入装备并立即建立可测量的文本布局，避免首次悬停出现空白细条。
    /// </summary>
    /// <param name="data">不可变的实际装备。</param>
    /// <param name="definition">装备名称、类型、品质与售价。</param>
    public void Bind(EquipmentData data, EquipmentConfig definition)
    {
        BindHeader(definition);
        foreach (AttributeModifier modifier in data.Attributes.EnumerateModifiers())
        {
            AddAttribute(modifier.Attribute, FormatValue(modifier.Attribute, modifier.Value));
        }
    }

    /// <summary>
    /// 图鉴显示生成属性范围，不创建或随机抽取实际装备。
    /// </summary>
    /// <param name="definition">共享装备定义。</param>
    public void BindDefinition(EquipmentConfig definition)
    {
        BindHeader(definition);
        foreach (EquipmentAttributeRange range in definition.AttributeRanges)
        {
            string value = FormatValue(range.Attribute, range.Minimum);
            if (range.Minimum != range.Maximum)
            {
                value += "～" + FormatValue(range.Attribute, range.Maximum);
            }
            AddAttribute(range.Attribute, value);
        }
    }

    /// <summary>
    /// 填充实例详情与图鉴共用的固定头尾并清理上一份属性。
    /// </summary>
    /// <param name="definition">本次装备定义。</param>
    private void BindHeader(EquipmentConfig definition)
    {
        // Tooltip 在入树前测量；先给换行文本真实列宽，避免按 1px 计算出多行高度后被弹窗保留。
        float contentWidth = _description.CustomMinimumSize.X;
        _name.Size = new Vector2(contentWidth, 0);
        _description.Size = new Vector2(contentWidth, 0);
        Color qualityColor = definition.Quality.DisplayColor();
        _name.Text = definition.NameCn;
        _name.AddThemeColorOverride("font_color", qualityColor);
        _quality.Text = string.Join(" ", definition.Quality.DisplayName().ToCharArray());
        _quality.AddThemeColorOverride("font_color", qualityColor);
        string slotName = definition.Slot switch
        {
            EquipmentSlot.Weapon => "武器",
            EquipmentSlot.Armor => "防具",
            EquipmentSlot.Accessory => "饰品",
            EquipmentSlot.Treasure => "法宝",
            _ => throw new System.ArgumentOutOfRangeException(nameof(definition.Slot))
        };
        string ownerName = definition.Owner switch
        {
            EquipmentOwner.Universal => "通用",
            EquipmentOwner.Wukong => "悟空",
            EquipmentOwner.Tangseng => "唐僧",
            _ => throw new System.ArgumentOutOfRangeException(nameof(definition.Owner))
        };
        _type.Text = $"{slotName}·{ownerName}";
        foreach (Node child in _attributes.GetChildren())
        {
            _attributes.RemoveChild(child);
            child.QueueFree();
        }
        _description.Text = definition.Description;
        _description.Visible = !string.IsNullOrEmpty(_description.Text);
        _value.Text = $"价值：{definition.SellPrice} 灵魂";
    }

    /// <summary>
    /// 按属性单位格式化数值。
    /// </summary>
    /// <param name="attribute">属性身份。</param>
    /// <param name="value">值或范围端点。</param>
    /// <returns>显示数值。</returns>
    private static string FormatValue(AttributeType attribute, float value) => attribute.IsProbability ? $"{value * 100:0.##}%" : $"{value:0.##}";

    /// <summary>
    /// 添加实际属性或配置范围的一行。
    /// </summary>
    /// <param name="attribute">属性身份。</param>
    /// <param name="value">已格式化的显示值。</param>
    private void AddAttribute(AttributeType attribute, string value)
    {
        Label line = new()
        {
            Text = $"{AttributeName(attribute)}：{value}",
            CustomMinimumSize = new Vector2(0, 25),
            HorizontalAlignment = HorizontalAlignment.Left,
            MouseFilter = MouseFilterEnum.Ignore
        };
        line.AddThemeColorOverride("font_color", new Color("ff9933"));
        _attributes.AddChild(line);
    }

    /// <summary>
    /// 把战斗属性映射为玩家使用的中文名称。
    /// </summary>
    /// <param name="attribute">属性类型。</param>
    /// <returns>界面名称。</returns>
    private static string AttributeName(AttributeType attribute)
    {
        return attribute switch
        {
            AttributeType.MaxHp => "生命",
            AttributeType.MaxMp => "魔法",
            AttributeType.Power => "攻击",
            AttributeType.Def => "防御",
            AttributeType.Mdef => "魔抗",
            AttributeType.Crit => "暴击",
            AttributeType.Miss => "闪避",
            AttributeType.Lucky => "幸运",
            AttributeType.Toughness => "韧性",
            AttributeType.Htarget => "命中",
            AttributeType.CritReduce => "抗暴",
            AttributeType.Ar => "破甲",
            AttributeType.Sp => "破魔",
            AttributeType.Vampirism => "吸血",
            AttributeType.HpRegen => "回血",
            AttributeType.MpRegen => "回蓝",
            _ => attribute.ToString()
        };
    }
}
