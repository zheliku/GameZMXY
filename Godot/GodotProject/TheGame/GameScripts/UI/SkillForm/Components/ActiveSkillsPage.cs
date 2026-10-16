using GameLogic.Architecture;
using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Skills;
using GameLogic.Config;
using Godot;
using GodotGameFramework;

namespace GameLogic.UI.Widgets;

/// <summary>主动技能页组合两名角色目录，负责装配和当前角色显示。</summary>
public partial class ActiveSkillsPage : Control, IController
{
    [Export] private Control m_WukongRows; // 悟空的五行目录。
    [Export] private Control m_TangsengRows; // 唐僧的五行目录。
    private (Label Name, TextureRect Icon, Label Soul, Label Key, Label Description)[][] m_HeroRows; // 两名角色的固定行引用，由本页统一刷新。

    /// <summary>一次收集两个角色的网格内容，行容器保持纯布局节点。</summary>
    public override void _Ready() => m_HeroRows = [CollectRows(m_WukongRows), CollectRows(m_TangsengRows)];

    /// <summary>借用所属主流程的共享数据访问入口。</summary>
    /// <returns>当前流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();

    /// <summary>把正式只读目录分配给对应角色。</summary>
    public void Refresh()
    {
        var configurationModel = this.GetModel<ConfigurationModel>();
        BindRows(m_HeroRows[0], configurationModel.GetActive(SkillHero.Wukong));
        BindRows(m_HeroRows[1], configurationModel.GetActive(SkillHero.Tangseng));
    }

    /// <summary>在主动页显示选中的角色行。</summary>
    /// <param name="hero">当前查看角色。</param>
    public void SetHero(SkillHero hero)
    {
        m_WukongRows.Visible = hero == SkillHero.Wukong;
        m_TangsengRows.Visible = hero == SkillHero.Tangseng;
    }

    /// <summary>从本页固定的五行收集展示节点，装配错误在初始化时报告。</summary>
    /// <param name="container">一个角色的行容器。</param>
    /// <returns>按场景顺序排列的五行引用。</returns>
    /// <exception cref="InvalidOperationException">行数不是五行。</exception>
    private static (Label Name, TextureRect Icon, Label Soul, Label Key, Label Description)[] CollectRows(Control container)
    {
        var rows = container.GetChildren().Cast<GridContainer>().ToArray();
        if (rows.Length != 5)
            throw new InvalidOperationException("角色技能目录必须为五行。");
        return rows.Select(row => (row.GetNode<Label>("Name"), row.GetNode<TextureRect>("IconCell/Icon"),
            row.GetNode<Label>("Soul"), row.GetNode<Label>("Key"), row.GetNode<Label>("DescriptionCell/Description"))).ToArray();
    }

    /// <summary>用正式配置覆盖一名角色的全部预览文字与图标。</summary>
    /// <param name="rows">已经收集的五行控件。</param>
    /// <param name="definitions">按面板顺序排列的五项主动定义。</param>
    private static void BindRows((Label Name, TextureRect Icon, Label Soul, Label Key, Label Description)[] rows,
        IReadOnlyList<SkillConfig> definitions)
    {
        // 只装配表现，不写学习记录或消费灵魂。
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            var definition = definitions[i];
            row.Name.Text = definition.NameCn;
            row.Icon.Texture = GF.Resource.LoadAsset<Texture2D>(definition.UnlearnedIconPath);
            row.Soul.Text = definition.LearnSoul.ToString();
            row.Key.Text = definition.KeyHint;
            row.Description.Text = definition.Description;
        }
    }
}
