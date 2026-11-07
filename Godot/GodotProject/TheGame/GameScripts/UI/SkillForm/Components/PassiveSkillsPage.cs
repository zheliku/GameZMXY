using GodotGameFramework;
using GameLogic.Config;
using GameLogic.Architecture;
using GameLogic.Profile;
using GameLogic.Skills;
using GameConfig.Skills;
using System;
using System.Linq;
using Godot;

namespace GameLogic.UI.Widgets
{
    /// <summary>
    /// 共用被动页显示当前/下一级效果并提交升级意图，等级由同一份技能模型提供。
    /// </summary>
    public partial class PassiveSkillsPage : Control, IController
    {
        [Export] private Label[] m_Names = []; // 按场景行顺序绑定的技能名称。
        [Export] private Label[] m_Levels = []; // 已学等级。
        [Export] private Label[] m_CurrentEffects = []; // 当前等级总效果。
        [Export] private Label[] m_NextEffects = []; // 与名称同序的下一级效果。
        [Export] private Label[] m_Souls = []; // 与名称同序的学习费用。
        [Export] private TextureButton[] m_UpgradeButtons = []; // 原版彩色升级字图及三态，满级隐藏。

        /// <summary>
        /// 玩家请求购买下一级，表单负责提交Command。
        /// </summary>
        public event Action<EPassiveId> UpgradeRequested;

        /// <summary>
        /// 收集本页行并验证固定行数。
        /// </summary>
        /// <exception cref="InvalidOperationException">五行展示节点的数量不一致或存在未绑定项。</exception>
        public override void _Ready()
        {
            if (m_Names.Length != 5 || m_Levels.Length != 5 || m_CurrentEffects.Length != 5 ||
                m_NextEffects.Length != 5 || m_Souls.Length != 5 || m_UpgradeButtons.Length != 5 ||
                m_Names.Any(item => item == null) || m_Levels.Any(item => item == null) ||
                m_CurrentEffects.Any(item => item == null) || m_NextEffects.Any(item => item == null) ||
                m_Souls.Any(item => item == null) || m_UpgradeButtons.Any(item => item == null))
            {
                throw new InvalidOperationException("被动页必须完整绑定五行的名称、等级、当前/下级效果、升级按钮和费用。");
            }

            var definitions = this.GetModel<ConfigurationModel>().Tables.TbPassiveConfig.DataList;
            for (int i = 0; i < m_UpgradeButtons.Length; i++)
            {
                EPassiveId id = definitions[i].PassiveId;
                m_UpgradeButtons[i].Pressed += () => UpgradeRequested?.Invoke(id);
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

        /// <summary>
        /// 显示当前等级和下一等级费用，满级后停止交互。
        /// </summary>
        public void Refresh()
        {
            var definitions = this.GetModel<ConfigurationModel>().Tables.TbPassiveConfig.DataList;
            var levels = this.GetModel<SkillModel>().GetPassiveLevels(this.GetModel<ProfileModel>().ActiveHeroId);
            for (int i = 0; i < m_Names.Length; i++)
            {
                var definition = definitions[i];
                int level = levels[definition.PassiveId];
                bool maximum = level == definition.Levels.Count;
                m_Names[i].Text = definition.NameCn;
                m_Levels[i].Text = level.ToString();
                m_CurrentEffects[i].Text = level == 0 ? "----" : definition.Levels[level - 1].Description;
                m_NextEffects[i].Text = maximum ? "----" : definition.Levels[level].Description;
                m_Souls[i].Text = maximum ? "----" : definition.Levels[level].LearnSoul.ToString();
                var button = m_UpgradeButtons[i];
                button.Disabled = maximum;
                button.Visible = !maximum;
                button.MouseDefaultCursorShape = maximum ? CursorShape.Arrow : CursorShape.PointingHand;
            }
        }
    }
}
