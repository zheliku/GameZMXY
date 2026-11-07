using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Skills;
using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Profile;
using GameLogic.Skills;
using Godot;
using GodotGameFramework;

namespace GameLogic.UI.Widgets;

/// <summary>
/// 共用被动页显示当前/下一级效果并提交升级意图，等级由同一份技能模型提供。
/// </summary>
public partial class PassiveSkillsPage : Control, IController
{
    [Export] private Label[] _names = []; // 按场景行顺序绑定的技能名称。
    [Export] private Label[] _levels = []; // 已学等级。
    [Export] private Label[] _currentEffects = []; // 当前等级总效果。
    [Export] private Label[] _nextEffects = []; // 与名称同序的下一级效果。
    [Export] private Label[] _souls = []; // 与名称同序的学习费用。
    [Export] private TextureButton[] _upgradeButtons = []; // 原版彩色升级字图及三态，满级隐藏。

    /// <summary>
    /// 玩家请求购买下一级，表单负责提交Command。
    /// </summary>
    public event Action<PassiveId> UpgradeRequested;

    /// <summary>
    /// 收集本页行并验证固定行数。
    /// </summary>
    /// <exception cref="InvalidOperationException">五行展示节点的数量不一致或存在未绑定项。</exception>
    public override void _Ready()
    {
        if (_names.Length != 5 || _levels.Length != 5 || _currentEffects.Length != 5 ||
            _nextEffects.Length != 5 || _souls.Length != 5 || _upgradeButtons.Length != 5 ||
            _names.Any(item => item == null) || _levels.Any(item => item == null) ||
            _currentEffects.Any(item => item == null) || _nextEffects.Any(item => item == null) ||
            _souls.Any(item => item == null) || _upgradeButtons.Any(item => item == null))
        {
            throw new InvalidOperationException("被动页必须完整绑定五行的名称、等级、当前/下级效果、升级按钮和费用。");
        }

        IReadOnlyList<PassiveConfig> definitions = this.GetModel<ConfigurationModel>().Tables.TbPassiveConfig.DataList;
        for (int i = 0; i < _upgradeButtons.Length; i++)
        {
            PassiveId id = definitions[i].PassiveId;
            _upgradeButtons[i].Pressed += () => UpgradeRequested?.Invoke(id);
        }
    }

    /// <summary>
    /// 借用所属主流程的共享数据访问入口。
    /// </summary>
    /// <returns>当前流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();

    /// <summary>
    /// 显示当前等级和下一等级费用，满级后停止交互。
    /// </summary>
    public void Refresh()
    {
        IReadOnlyList<PassiveConfig> definitions = this.GetModel<ConfigurationModel>().Tables.TbPassiveConfig.DataList;
        BindableDictionary<PassiveId, int> levels = this.GetModel<SkillModel>().GetPassiveLevels(this.GetModel<ProfileModel>().ActiveHeroId);
        for (int i = 0; i < _names.Length; i++)
        {
            PassiveConfig definition = definitions[i];
            int level = levels[definition.PassiveId];
            bool maximum = level == definition.Levels.Count;
            _names[i].Text = definition.NameCn;
            _levels[i].Text = level.ToString();
            _currentEffects[i].Text = level == 0 ? "----" : definition.Levels[level - 1].Description;
            _nextEffects[i].Text = maximum ? "----" : definition.Levels[level].Description;
            _souls[i].Text = maximum ? "----" : definition.Levels[level].LearnSoul.ToString();
            TextureButton button = _upgradeButtons[i];
            button.Disabled = maximum;
            button.Visible = !maximum;
            button.MouseDefaultCursorShape = maximum ? CursorShape.Arrow : CursorShape.PointingHand;
        }
    }
}
