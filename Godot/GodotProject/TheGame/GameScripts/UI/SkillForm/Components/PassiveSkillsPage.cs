using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Skills;
using Godot;

namespace GameLogic.UI.Widgets;

/// <summary>共用被动页管理自己的五行，初期只展示未学习和一级效果。</summary>
public partial class PassiveSkillsPage : Control
{
    [Export] private Control m_Rows; // 本页的五行容器。
    private (Label Name, Label NextEffect, Label Soul)[] m_Items; // 本页统一刷新的五行内容，行不挂脚本。

    /// <summary>收集本页行并验证固定行数。</summary>
    /// <exception cref="InvalidOperationException">场景不是五行共用被动。</exception>
    public override void _Ready()
    {
        var rows = m_Rows.GetChildren().Cast<GridContainer>().ToArray();
        if (rows.Length != 5)
            throw new InvalidOperationException("共用被动目录必须为五行。");
        m_Items = rows.Select(row => (row.GetNode<Label>("Name"), row.GetNode<Label>("NextEffect"), row.GetNode<Label>("Soul"))).ToArray();
    }

    /// <summary>刷新尚未学习的五项被动定义。</summary>
    /// <param name="definitions">按原版顺序排列的五项一级定义。</param>
    public void Bind(IReadOnlyList<PassiveConfig> definitions)
    {
        for (int i = 0; i < m_Items.Length; i++)
        {
            var (name, nextEffect, soul) = m_Items[i];
            name.Text = definitions[i].NameCn;
            nextEffect.Text = definitions[i].Description;
            soul.Text = definitions[i].LearnSoul.ToString();
        }
    }
}
