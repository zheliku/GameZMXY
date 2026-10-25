using GodotGameFramework;
using GameLogic.Config;
using GameLogic.Architecture;
using System;
using System.Linq;
using Godot;

namespace GameLogic.UI.Widgets;

/// <summary>
/// 共用被动页管理自己的五行，初期只展示未学习和一级效果。
/// </summary>
public partial class PassiveSkillsPage : Control, IController
{
    [Export] private Label[] m_Names = []; // 按场景行顺序绑定的技能名称。
    [Export] private Label[] m_NextEffects = []; // 与名称同序的下一级效果。
    [Export] private Label[] m_Souls = []; // 与名称同序的学习费用。
    private (Label Name, Label NextEffect, Label Soul)[] m_Items; // 本页统一刷新的五行内容，行不挂脚本。

    /// <summary>
    /// 收集本页行并验证固定行数。
    /// </summary>
    /// <exception cref="InvalidOperationException">五行展示节点的数量不一致或存在未绑定项。</exception>
    public override void _Ready()
    {
        if (m_Names.Length != 5 || m_NextEffects.Length != m_Names.Length || m_Souls.Length != m_Names.Length ||
            m_Names.Any(item => item == null) || m_NextEffects.Any(item => item == null) || m_Souls.Any(item => item == null))
            throw new InvalidOperationException("共用被动页必须绑定五行名称、下一级效果和学习费用。");
        m_Items = m_Names.Select((name, i) => (name, m_NextEffects[i], m_Souls[i])).ToArray();
    }

    /// <summary>
    /// 借用所属主流程的共享数据访问入口。
    /// </summary>
    /// <returns>当前流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture()
    {
        return GF.Fsm.GetArchitecture();
    }

    /// <summary>
    /// 刷新尚未学习的五项被动定义。
    /// </summary>
    public void Refresh()
    {
        var definitions = this.GetModel<ConfigurationModel>().Tables.TbPassiveConfig.DataList;
        for (int i = 0; i < m_Items.Length; i++)
        {
            var (name, nextEffect, soul) = m_Items[i];
            var level = definitions[i].Levels[0];
            name.Text = definitions[i].NameCn;
            nextEffect.Text = level.Description;
            soul.Text = level.LearnSoul.ToString();
        }
    }
}
