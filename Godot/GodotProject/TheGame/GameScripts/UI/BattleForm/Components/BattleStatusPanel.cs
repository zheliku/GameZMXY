using Godot;

namespace GameLogic.UI.Widgets;

/// <summary>
/// 战斗头像区的生命、魔法、经验和等级组合，只接收一致的显示值。
/// </summary>
public partial class BattleStatusPanel : Sprite2D
{
    [Export] private ResourceBar _hpBar; // 生命资源条。
    [Export] private ResourceBar _mpBar; // 魔法资源条。
    [Export] private ResourceBar _expBar; // 当前等级经验条。
    [Export] private Label _levelLabel; // 等级数字。
    [Export] private Sprite2D _portrait; // 当前角色头像。
    [Export] private Godot.Collections.Dictionary<int, Texture2D> _portraits = new(); // 本场景登记的角色头像。

    /// <summary>
    /// 每次绑定战斗英雄时切换对应头像，池复用不保留上一角色。
    /// </summary>
    /// <param name="heroId">当前出战英雄身份。</param>
    public void SetHero(int heroId) => _portrait.Texture = _portraits[heroId];

    /// <summary>
    /// 同步生命和魔法现值与上限。
    /// </summary>
    /// <param name="hp">当前生命。</param>
    /// <param name="maxHp">生命上限。</param>
    /// <param name="mp">当前魔法。</param>
    /// <param name="maxMp">魔法上限。</param>
    public void SetResources(int hp, int maxHp, int mp, int maxMp)
    {
        _hpBar.SetValue(hp, maxHp);
        _mpBar.SetValue(mp, maxMp);
    }

    /// <summary>
    /// 同步等级与本级经验。
    /// </summary>
    /// <param name="level">当前等级。</param>
    /// <param name="experience">当前等级内经验。</param>
    /// <param name="maximum">本级升级需求。</param>
    public void SetProgression(int level, int experience, int maximum)
    {
        _levelLabel.Text = level.ToString();
        _expBar.SetValue(experience, maximum);
    }
}
