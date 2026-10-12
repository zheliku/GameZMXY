using Godot;

namespace GameLogic.UI.Widgets;

/// <summary>被动守关生命条：左侧白字黑边名称、右侧笔刷血条，不持有怪物或播放入场弹窗。</summary>
public partial class EncounterHealthBar : Control
{
    [Export] private ResourceBar m_Health; // 原素材构成的生命条，不叠加数值文字。
    [Export] private Label m_Name; // 血条左侧的怪物名称。

    /// <summary>显示一组完整生命数据；归零时隐藏。</summary>
    /// <param name="name">怪物显示名称。</param>
    /// <param name="current">当前生命。</param>
    /// <param name="maximum">生命上限。</param>
    public void SetValue(string name, int current, int maximum)
    {
        m_Name.Text = name;
        m_Health.SetValue(current, maximum);
        Visible = current > 0 && maximum > 0;
    }
}
