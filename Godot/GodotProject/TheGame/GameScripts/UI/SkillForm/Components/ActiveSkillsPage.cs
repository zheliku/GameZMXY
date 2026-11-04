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

namespace GameLogic.UI.Widgets
{
    /// <summary>
    /// 主动技能页组合两名角色目录，负责装配和当前角色显示。
    /// </summary>
    public partial class ActiveSkillsPage : Control, IController
    {
        // ---- 字段 ----

        [Export] private Control m_WukongRows; // 悟空的五行目录。
        [Export] private Control m_TangsengRows; // 唐僧的五行目录。
        [Export] private Label[] m_Names = []; // 悟空、唐僧各五行的名称，按面板顺序绑定。
        [Export] private TextureButton[] m_Icons = []; // 与名称同序的技能图标及学习按钮。
        [Export] private Label[] m_Souls = []; // 与名称同序的学习费用。
        [Export] private Label[] m_Descriptions = []; // 与名称同序的技能说明。
        private const int SkillsPerHero = 5; // 当前每个角色技能目录的固定行数。
        private (Label Name, TextureButton Icon, Label Soul, Label Description, Texture2D Unlearned, Texture2D Learned)[][] m_HeroRows; // 两名角色的固定行引用，由本页统一刷新。

        // ---- 事件 ----

        /// <summary>
        /// 玩家点击尚未学习的技能；购买由窗口提交给技能系统。
        /// </summary>
        public event Action<SkillId> LearnRequested;

        // ---- 节点生命周期 ----

        /// <summary>
        /// 一次收集两个角色的网格内容，行容器保持纯布局节点。
        /// </summary>
        /// <exception cref="InvalidOperationException">十行展示节点的数量不一致或存在未绑定项。</exception>
        public override void _Ready()
        {
            if (m_Names.Length != SkillsPerHero * 2 || m_Icons.Length != m_Names.Length ||
                m_Souls.Length != m_Names.Length || m_Descriptions.Length != m_Names.Length ||
                m_Names.Any(item => item == null) || m_Icons.Any(item => item == null) ||
                m_Souls.Any(item => item == null) || m_Descriptions.Any(item => item == null))
            {
                throw new InvalidOperationException("主动技能页必须绑定两名角色各五行的名称、图标、学习费用和说明。");
            }

            m_HeroRows = [CollectRows(0), CollectRows(SkillsPerHero)];
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

        /// <summary>
        /// 借用所属主流程的共享数据访问入口。
        /// </summary>
        /// <returns>当前流程 FSM 的架构。</returns>
        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GF.Fsm.GetArchitecture();
        }

        // ---- 业务方法 ----

        /// <summary>
        /// 把正式只读目录分配给对应角色。
        /// </summary>
        public void Refresh()
        {
            var configurationModel = this.GetModel<ConfigurationModel>();
            BindRows(m_HeroRows[0], configurationModel.GetActive(SkillHero.Wukong));
            BindRows(m_HeroRows[1], configurationModel.GetActive(SkillHero.Tangseng));
        }

        /// <summary>
        /// 在主动页显示选中的角色行。
        /// </summary>
        /// <param name="hero">当前查看角色。</param>
        public void SetHero(SkillHero hero)
        {
            m_WukongRows.Visible = hero == SkillHero.Wukong;
            m_TangsengRows.Visible = hero == SkillHero.Tangseng;
        }

        // ---- 私有方法 ----

        /// <summary>
        /// 从本页固定的五行收集展示节点，装配错误在初始化时报告。
        /// </summary>
        /// <param name="start">本角色在场景绑定数组中的起始索引。</param>
        /// <returns>按场景顺序排列的五行引用。</returns>
        private (Label Name, TextureButton Icon, Label Soul, Label Description, Texture2D Unlearned, Texture2D Learned)[] CollectRows(int start)
        {
            return Enumerable.Range(start, SkillsPerHero).Select(i =>
                (m_Names[i], m_Icons[i], m_Souls[i], m_Descriptions[i],
                    m_Icons[i].TextureNormal, m_Icons[i].TextureDisabled)).ToArray();
        }

        /// <summary>
        /// 用数值目录刷新学习信息，图标和键位由本场景提供。
        /// </summary>
        /// <param name="rows">已经收集的五行控件。</param>
        /// <param name="definitions">按面板顺序排列的五项主动定义。</param>
        private void BindRows((Label Name, TextureButton Icon, Label Soul, Label Description, Texture2D Unlearned, Texture2D Learned)[] rows,
            IReadOnlyList<SkillConfig> definitions)
        {
            // 只装配表现，不写学习记录或消费灵魂。
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                var definition = definitions[i];
                row.Name.Text = definition.NameCn;
                bool available = this.GetSystem<SkillSystem>().IsLearnable(definition.Id);
                bool learned = available && this.GetSystem<SkillSystem>().IsLearned(definition.Id);
                row.Icon.TextureNormal = learned ? row.Learned : row.Unlearned;
                row.Icon.TextureDisabled = row.Icon.TextureNormal;
                row.Icon.Disabled = !available || learned;
                row.Icon.MouseDefaultCursorShape = row.Icon.Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
                row.Soul.Text = definition.LearnSoul.ToString();
                row.Description.Text = definition.Description;
            }
        }
    }
}
