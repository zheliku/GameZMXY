using GameConfig.Equipment;
using GameConfig.Attributes;
using GameLogic.Equipment;
using Godot;
using GameLogic.UI.Widgets;

namespace GameLogic.UI;

/// <summary>按造梦三 AttributeCon 排列名称、品质、类型、实际属性与灵魂价值。</summary>
public partial class EquipmentTooltip : PanelContainer
{
    [Export] private Label m_Name; // 品质色的装备名称。
    [Export] private Label m_Quality; // 品质值，标题由场景中的独立 Label 绘制。
    [Export] private Label m_Type; // 部位与可用角色。
    [Export] private VBoxContainer m_Attributes; // 只显示非零实际属性，每行 25 像素。
    [Export] private Label m_Description; // 官方说明，空说明不占行。
    [Export] private Label m_Value; // 出售获得的灵魂。

    /// <summary>注入装备并立即建立可测量的文本布局，避免首次悬停出现空白细条。</summary>
    /// <param name="item">不可变的实际装备。</param>
    /// <param name="definition">装备名称、类型、品质与售价。</param>
    public void Bind(EquipmentInstance item, EquipmentConfig definition)
    {
        Color qualityColor = definition.Quality.DisplayColor();
        m_Name.Text = definition.NameCn;
        m_Name.AddThemeColorOverride("font_color", qualityColor);
        m_Quality.Text = string.Join(" ", definition.Quality.DisplayName().ToCharArray());
        m_Quality.AddThemeColorOverride("font_color", qualityColor);
        m_Type.Text = (definition.Slot == EquipmentSlot.Weapon ? "武器" : "防具") + "·" +
            (definition.Owner == EquipmentOwner.Wukong ? "悟空" : "唐僧");
        foreach (Node child in m_Attributes.GetChildren())
        {
            m_Attributes.RemoveChild(child);
            child.QueueFree();
        }
        foreach (var modifier in item.Attributes.EnumerateModifiers())
        {
            Label line = new() { Text = $"{AttributeName(modifier.Attribute)}： {modifier.Value:0.##}", CustomMinimumSize = new Vector2(0, 25), MouseFilter = MouseFilterEnum.Ignore };
            line.AddThemeColorOverride("font_color", new Color("ff9933"));
            m_Attributes.AddChild(line);
        }
        m_Description.Text = definition.Description;
        m_Description.Visible = !string.IsNullOrEmpty(definition.Description);
        m_Value.Text = $"价值 : {definition.SellPrice} 灵魂";
    }

    /// <summary>把战斗属性映射为玩家使用的中文名称。</summary>
    /// <param name="attribute">属性类型。</param>
    /// <returns>界面名称。</returns>
    private static string AttributeName(AttributeType attribute) => attribute switch
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
