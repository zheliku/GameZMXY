using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Profile;
using Godot;
using GodotGameFramework;

namespace GameLogic.UI.Widgets;

/// <summary>
/// 一名英雄的战斗头像区；绑定本人生命、法力和成长，订阅由窗口打开与关闭统一驱动。
/// </summary>
public partial class BattleStatusPanel : Sprite2D, IController
{
    [Export] private ResourceBar _hpBar; // 生命资源条。
    [Export] private ResourceBar _mpBar; // 魔法资源条。
    [Export] private ResourceBar _expBar; // 当前等级经验条。
    [Export] private Label _levelLabel; // 等级数字。
    [Export] private Sprite2D _portrait; // 当前角色头像。
    [Export] private Godot.Collections.Dictionary<int, Texture2D> _portraits = new(); // 本场景登记的角色头像。
    private HeroState _state; // 借用本次绑定英雄的唯一局内状态。
    private BindableProperty<HeroProgressionState> _progression; // 本次实际订阅的成长来源，解绑时释放。

    /// <summary>借用所属主流程的共享数据访问入口。</summary>
    /// <returns>当前流程 FSM 的架构。</returns>
    IArchitecture IBelongToArchitecture.GetArchitecture()
    {
        return GF.Fsm.GetArchitecture();
    }

    /// <summary>解除旧绑定，再观察指定英雄并立即刷新头像、资源和成长。</summary>
    /// <param name="heroId">本局已经建立状态的参战英雄编号。</param>
    public void BindHero(int heroId)
    {
        UnbindHero();
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        BindableProperty<HeroProgressionState> progression = this.GetModel<ProfileModel>().GetProgress(heroId);
        // 先保存实际来源，窗口的失败清理也能解除已经完成的部分订阅。
        _state = state;
        _progression = progression;
        _state.Hp.Register(RefreshResources);
        _state.MaxHp.Register(RefreshResources);
        _state.Mp.Register(RefreshResources);
        _state.MaxMp.Register(RefreshResources);
        _progression.RegisterWithInitValue(RefreshProgression);
        _portrait.Texture = _portraits[heroId];
        RefreshResources();
    }

    /// <summary>由窗口关闭、改绑或装配失败时调用，解除本面板的全部订阅。</summary>
    public void UnbindHero()
    {
        if (_state == null)
        {
            return;
        }

        // 具名方法的目标就是本面板，无需为每个席位保存闭包。
        _state.Hp.UnRegister(RefreshResources);
        _state.MaxHp.UnRegister(RefreshResources);
        _state.Mp.UnRegister(RefreshResources);
        _state.MaxMp.UnRegister(RefreshResources);
        _progression.UnRegister(RefreshProgression);
        _state = null;
        _progression = null;
    }

    /// <summary>读取本人的当前值与上限，同步生命和魔法显示。</summary>
    /// <param name="value">触发通知的已提交值；关联数值从绑定状态一起读取。</param>
    private void RefreshResources(int value = 0)
    {
        _hpBar.SetValue(_state.Hp.Value, _state.MaxHp.Value);
        _mpBar.SetValue(_state.Mp.Value, _state.MaxMp.Value);
    }

    /// <summary>用同一次成长快照刷新等级和经验。</summary>
    /// <param name="progression">当前英雄已经提交的成长值。</param>
    private void RefreshProgression(HeroProgressionState progression)
    {
        _levelLabel.Text = progression.Level.ToString();
        _expBar.SetValue(progression.Experience, progression.MaxExperience);
    }
}
