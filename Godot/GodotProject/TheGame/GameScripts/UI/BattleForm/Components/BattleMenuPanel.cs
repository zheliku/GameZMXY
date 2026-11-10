using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Skills;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Config;
using GameLogic.Skills;
using Godot;
using GodotGameFramework;

namespace GameLogic.UI.Widgets;

/// <summary>
/// 一名英雄的战斗菜单：绑定本人无双与技能，向窗口同步报告带身份的交互和演出变化。
/// </summary>
public partial class BattleMenuPanel : Sprite2D, IController
{
    // ---- 字段 ----

    [Export] private ResourceBar _wsBar; // 无双蓄力条。
    [Export] private AnimatedSprite2D _wsMax; // 满值循环闪烁。
    [Export] private TextureButton _settingsButton; // 设置入口。
    [Export] private TextureButton _inventoryButton; // 背包入口。
    [Export] private TextureButton _skillsButton; // 技能入口。
    [Export] private TextureRect[] _icons = []; // 按配置顺序绑定的主动技能图标，父控件承载提示。
    [Export] private SpriteFrames _skillIcons; // 官方战斗栏独立的未学习/已学习图标，不使用学习面板图片。
    private (Control Cell, TextureRect Icon)[] _skillCells; // 固定节点只在装配时收集，刷新时按场景顺序取用。

    private HeroState _state; // 借用本次绑定英雄的唯一局内状态。
    private BindableList<SkillId> _learnedSkills; // 本次实际订阅的技能来源，解绑时释放。
    private PlayerSlot _slot; // 本面板绑定的输入与菜单席位，不作为个人数据主键。

    // ---- 事件 ----

    /// <summary>
    /// 玩家选择设置菜单。
    /// </summary>
    public event Action<PlayerSlot> SettingsSelected;

    /// <summary>
    /// 玩家选择背包。
    /// </summary>
    public event Action<PlayerSlot> InventorySelected;

    /// <summary>
    /// 玩家选择技能。
    /// </summary>
    public event Action<PlayerSlot> SkillsSelected;

    /// <summary>本人无双开启状态发生变化；携带英雄编号并同步通知窗口，以保持立即暂停。</summary>
    public event Action<int, bool> MusouStateChanged;

    // ---- 节点生命周期 ----

    /// <summary>
    /// 子按钮只绑定一次，初始不播放满值动画。
    /// </summary>
    /// <exception cref="InvalidOperationException">图标未绑定，或其直属父节点不是提示控件。</exception>
    public override void _Ready()
    {
        if (_icons.Length == 0 || _icons.Any(icon => icon == null || icon.GetParent() is not Control))
        {
            throw new InvalidOperationException("战斗技能栏必须绑定图标，且图标直属控件承载提示。");
        }

        _skillCells = _icons.Select(icon => (icon.GetParent<Control>(), icon)).ToArray();
        _settingsButton.Pressed += OnSettingsPressed;
        _inventoryButton.Pressed += OnInventoryPressed;
        _skillsButton.Pressed += OnSkillsPressed;
        ResetMusou();
    }

    // ---- 业务方法 ----

    /// <summary>借用所属主流程的共享数据访问入口。</summary>
    /// <returns>当前流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture() => GF.Fsm.GetArchitecture();

    /// <summary>解除旧绑定，再观察本人的无双和技能；初次刷新不重播开启演出。</summary>
    /// <param name="heroId">本局已经建立状态的参战英雄编号。</param>
    /// <param name="slot">该英雄本局的输入与菜单席位。</param>
    public void BindHero(int heroId, PlayerSlot slot)
    {
        UnbindHero();
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        BindableList<SkillId> learned = this.GetModel<SkillModel>().GetLearnedSkills(heroId);
        // 保存真实来源供窗口失败清理，开启通知只订阅变化，不发送初值。
        _state = state;
        _learnedSkills = learned;
        _slot = slot;
        _state.Ws.Register(RefreshMusou);
        _state.MaxWs.Register(RefreshMusou);
        _state.WsActive.Register(OnMusouStateChanged);
        _learnedSkills.RegisterWithInitValue(RefreshSkills);
        RefreshMusou();
    }

    /// <summary>由窗口关闭、改绑或装配失败时调用，解除个人订阅并停止满值动画。</summary>
    public void UnbindHero()
    {
        if (_state != null)
        {
            _state.Ws.UnRegister(RefreshMusou);
            _state.MaxWs.UnRegister(RefreshMusou);
            _state.WsActive.UnRegister(OnMusouStateChanged);
            _learnedSkills.UnRegister(RefreshSkills);
            _state = null;
            _learnedSkills = null;
            _slot = default;
        }
        ResetMusou();
    }

    /// <summary>读取本人无双刷新条体与满值提示，重复刷新保持动画进度。</summary>
    /// <param name="value">触发通知的已提交值；关联值从绑定状态一起读取。</param>
    private void RefreshMusou(int value = 0)
    {
        _wsBar.SetValue(_state.Ws.Value, _state.MaxWs.Value);
        if (!_state.IsWsFull)
        {
            ResetMusou();
            return;
        }
        _wsMax.Show();
        if (!_wsMax.IsPlaying())
        {
            _wsMax.Play();
        }
    }

    /// <summary>同步报告无双变化，由窗口管理共享立绘与暂停，再刷新本人条体。</summary>
    /// <param name="active">已经提交的开启状态。</param>
    private void OnMusouStateChanged(bool active)
    {
        MusouStateChanged?.Invoke(_state.HeroId, active);
        RefreshMusou();
    }

    /// <summary>
    /// 关闭或改绑时停止并隐藏满值动画。
    /// </summary>
    private void ResetMusou()
    {
        _wsMax.Stop();
        _wsMax.Hide();
    }

    /// <summary>
    /// 根据权威学习记录显示灰色或彩色图标，不复制学习状态。
    /// </summary>
    /// <param name="learned">已学习技能列表。</param>
    /// <exception cref="InvalidOperationException">当前场景容纳不下角色技能。</exception>
    private void RefreshSkills(IReadOnlyList<SkillId> learned)
    {
        IReadOnlyList<SkillConfig> definitions = this.GetModel<ConfigurationModel>().GetActive((SkillHero)_state.HeroId);
        if (definitions.Count > _skillCells.Length)
        {
            throw new InvalidOperationException("战斗技能栏的场景格子少于当前角色技能数。");
        }

        for (int i = 0; i < _skillCells.Length; i++)
        {
            (Control cell, TextureRect icon) = _skillCells[i];
            if (i >= definitions.Count)
            {
                icon.Texture = null;
                continue;
            }
            SkillConfig definition = definitions[i];
            bool unlocked = learned.Contains(definition.Id);
            icon.Texture = _skillIcons.GetFrameTexture(definition.Id.ToString(), unlocked ? 1 : 0);
        }
    }

    // ---- 私有方法 ----

    /// <summary>
    /// 报告本块的设置选择。
    /// </summary>
    private void OnSettingsPressed() => SettingsSelected?.Invoke(_slot);

    /// <summary>
    /// 报告本块的背包选择。
    /// </summary>
    private void OnInventoryPressed() => InventorySelected?.Invoke(_slot);

    /// <summary>
    /// 报告本块的技能选择。
    /// </summary>
    private void OnSkillsPressed() => SkillsSelected?.Invoke(_slot);
}
