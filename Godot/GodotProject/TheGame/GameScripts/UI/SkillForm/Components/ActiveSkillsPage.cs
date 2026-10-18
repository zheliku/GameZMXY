using GameLogic.Architecture;
using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Skills;
using GameLogic.Config;
using GameLogic.Profile;
using GameLogic.Skills;
using Godot;
using GodotGameFramework;

namespace GameLogic.UI.Widgets;

/// <summary>主动技能页组合两名角色目录，负责装配和当前角色显示。</summary>
public partial class ActiveSkillsPage : Control, IController
{
    // ---- 字段 ----

    [Export] private Control m_WukongRows; // 悟空的五行目录。
    [Export] private Control m_TangsengRows; // 唐僧的五行目录。
    private (Label Name, TextureButton Icon, Label Soul, Label Key, Label Description, Texture2D Unlearned, Texture2D Learned)[][] m_HeroRows; // 两名角色的固定行引用，由本页统一刷新。

    // ---- 事件 ----

    /// <summary>玩家点击尚未学习的技能；购买由窗口提交给技能系统。</summary>
    public event Action<SkillId> LearnRequested;

    // ---- 节点生命周期 ----

    /// <summary>一次收集两个角色的网格内容，行容器保持纯布局节点。</summary>
    public override void _Ready()
    {
        m_HeroRows = [CollectRows(m_WukongRows), CollectRows(m_TangsengRows)];
        foreach (var (hero, rows) in new[] { (SkillHero.Wukong, m_HeroRows[0]), (SkillHero.Tangseng, m_HeroRows[1]) })
        {
            var definitions = this.GetModel<ConfigurationModel>().GetActive(hero);
            for (int i = 0; i < rows.Length; i++)
            {
                SkillId id = definitions[i].Id;
                rows[i].Icon.Pressed += () => LearnRequested?.Invoke(id);
            }
        }
    }

    /// <summary>借用所属主流程的共享数据访问入口。</summary>
    /// <returns>当前流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();

    // ---- 业务方法 ----

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

    // ---- 私有方法 ----

    /// <summary>从本页固定的五行收集展示节点，装配错误在初始化时报告。</summary>
    /// <param name="container">一个角色的行容器。</param>
    /// <returns>按场景顺序排列的五行引用。</returns>
    /// <exception cref="InvalidOperationException">行数不是五行。</exception>
    private static (Label Name, TextureButton Icon, Label Soul, Label Key, Label Description, Texture2D Unlearned, Texture2D Learned)[] CollectRows(Control container)
    {
        var rows = container.GetChildren().Cast<GridContainer>().ToArray();
        if (rows.Length != 5)
            throw new InvalidOperationException("角色技能目录必须为五行。");
        return rows.Select(row => (row.GetNode<Label>("Name"), row.GetNode<TextureButton>("IconCell/Icon"),
            row.GetNode<Label>("Soul"), row.GetNode<Label>("Key"), row.GetNode<Label>("DescriptionCell/Description"),
            row.GetNode<TextureButton>("IconCell/Icon").TextureNormal, row.GetNode<TextureButton>("IconCell/Icon").TextureDisabled)).ToArray();
    }

    /// <summary>用数值目录刷新学习信息，图标和键位由本场景提供。</summary>
    /// <param name="rows">已经收集的五行控件。</param>
    /// <param name="definitions">按面板顺序排列的五项主动定义。</param>
    private void BindRows((Label Name, TextureButton Icon, Label Soul, Label Key, Label Description, Texture2D Unlearned, Texture2D Learned)[] rows,
        IReadOnlyList<SkillConfig> definitions)
    {
        // 只装配表现，不写学习记录或消费灵魂。
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            var definition = definitions[i];
            row.Name.Text = definition.NameCn;
            bool available = this.GetSystem<SkillSystem>().IsAvailable(definition.Id);
            bool learned = available && this.GetSystem<SkillSystem>().IsLearned(definition.Id);
            row.Icon.TextureNormal = learned ? row.Learned : row.Unlearned;
            row.Icon.TextureDisabled = row.Icon.TextureNormal;
            row.Icon.Disabled = !available || learned;
            row.Icon.MouseDefaultCursorShape = row.Icon.Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
            row.Icon.TooltipText = learned ? "已学习" : available ? $"点击学习（{definition.LearnSoul} 灵魂）" : "暂未开放";
            row.Soul.Text = definition.LearnSoul.ToString();
            row.Description.Text = definition.Description;
        }
    }
}
