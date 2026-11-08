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
/// 主动技能页组合两名角色目录，负责装配和当前角色显示。
/// </summary>
public partial class ActiveSkillsPage : Control, IController
{
    // ---- 字段 ----

    [Export] private Control _wukongRows; // 悟空的五行目录。
    [Export] private Control _tangsengRows; // 唐僧的五行目录。
    [Export] private Label[] _names = []; // 悟空、唐僧各五行的名称，按面板顺序绑定。
    [Export] private TextureButton[] _icons = []; // 与名称同序的技能图标及学习按钮。
    [Export] private Label[] _souls = []; // 与名称同序的学习费用。
    [Export] private Label[] _descriptions = []; // 与名称同序的技能说明。
    private const int SkillsPerHero = 5; // 当前每个角色技能目录的固定行数。
    private (Label Name, TextureButton Icon, Label Soul, Label Description, Texture2D Unlearned, Texture2D Learned)[][] _heroRows; // 两名角色的固定行引用，由本页统一刷新。

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
        if (_names.Length != SkillsPerHero * 2 || _icons.Length != _names.Length ||
            _souls.Length != _names.Length || _descriptions.Length != _names.Length ||
            _names.Any(item => item == null) || _icons.Any(item => item == null) ||
            _souls.Any(item => item == null) || _descriptions.Any(item => item == null))
        {
            throw new InvalidOperationException("主动技能页必须绑定两名角色各五行的名称、图标、学习费用和说明。");
        }

        _heroRows = [CollectRows(0), CollectRows(SkillsPerHero)];
        foreach ((SkillHero hero, (Label Name, TextureButton Icon, Label Soul, Label Description, Texture2D Unlearned, Texture2D Learned)[] rows) in new[] { (SkillHero.Wukong, _heroRows[0]), (SkillHero.Tangseng, _heroRows[1]) })
        {
            IReadOnlyList<SkillConfig> definitions = this.GetModel<ConfigurationModel>().GetActive(hero);
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
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();

    // ---- 业务方法 ----

    /// <summary>
    /// 把正式只读目录分配给对应角色。
    /// </summary>
    /// <param name="heroId">本窗口当前显示的参战英雄编号。</param>
    public void Refresh(int heroId)
    {
        ConfigurationModel configurationModel = this.GetModel<ConfigurationModel>();
        BindRows(heroId, _heroRows[0], configurationModel.GetActive(SkillHero.Wukong));
        BindRows(heroId, _heroRows[1], configurationModel.GetActive(SkillHero.Tangseng));
    }

    /// <summary>
    /// 在主动页显示选中的角色行。
    /// </summary>
    /// <param name="hero">当前查看角色。</param>
    public void SetHero(SkillHero hero)
    {
        _wukongRows.Visible = hero == SkillHero.Wukong;
        _tangsengRows.Visible = hero == SkillHero.Tangseng;
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
            (_names[i], _icons[i], _souls[i], _descriptions[i],
                _icons[i].TextureNormal, _icons[i].TextureDisabled)).ToArray();
    }

    /// <summary>
    /// 用数值目录刷新学习信息，图标和键位由本场景提供。
    /// </summary>
    /// <param name="heroId">当前查看的英雄编号，用于判断学习资格。</param>
    /// <param name="rows">已经收集的五行控件。</param>
    /// <param name="definitions">按面板顺序排列的五项主动定义。</param>
    private void BindRows(int heroId, (Label Name, TextureButton Icon, Label Soul, Label Description, Texture2D Unlearned, Texture2D Learned)[] rows,
        IReadOnlyList<SkillConfig> definitions)
    {
        // 只装配表现，不写学习记录或消费灵魂。
        for (int i = 0; i < rows.Length; i++)
        {
            (Label Name, TextureButton Icon, Label Soul, Label Description, Texture2D Unlearned, Texture2D Learned) row = rows[i];
            SkillConfig definition = definitions[i];
            row.Name.Text = definition.NameCn;
            bool available = this.GetSystem<SkillSystem>().IsLearnable(heroId, definition.Id);
            bool learned = available && this.GetSystem<SkillSystem>().IsLearned(heroId, definition.Id);
            row.Icon.TextureNormal = learned ? row.Learned : row.Unlearned;
            row.Icon.TextureDisabled = row.Icon.TextureNormal;
            row.Icon.Disabled = !available || learned;
            row.Icon.MouseDefaultCursorShape = row.Icon.Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
            row.Soul.Text = definition.LearnSoul.ToString();
            row.Description.Text = definition.Description;
        }
    }
}
