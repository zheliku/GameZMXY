using Godot;

namespace GameLogic.UI.Widgets;

/// <summary>战斗头像区的生命、魔法、经验和等级组合，只接收一致的显示值。</summary>
public partial class BattleStatusPanel : Sprite2D
{
    [Export] private ResourceBar m_HpBar; // 生命资源条。
    [Export] private ResourceBar m_MpBar; // 魔法资源条。
    [Export] private ResourceBar m_ExpBar; // 当前等级经验条。
    [Export] private Label m_LevelLabel; // 等级数字。

    /// <summary>同步生命和魔法现值与上限。</summary>
    /// <param name="hp">当前生命。</param>
    /// <param name="maxHp">生命上限。</param>
    /// <param name="mp">当前魔法。</param>
    /// <param name="maxMp">魔法上限。</param>
    public void SetVitals(int hp, int maxHp, int mp, int maxMp)
    {
        m_HpBar.SetValue(hp, maxHp);
        m_MpBar.SetValue(mp, maxMp);
    }

    /// <summary>同步等级与本级经验。</summary>
    /// <param name="level">当前等级。</param>
    /// <param name="experience">当前等级内经验。</param>
    /// <param name="maximum">本级升级需求。</param>
    public void SetProgression(int level, int experience, int maximum)
    {
        m_LevelLabel.Text = level.ToString();
        m_ExpBar.SetValue(experience, maximum);
    }
}
