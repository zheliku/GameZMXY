using System;
using Godot;

namespace GameLogic.UI.Widgets;

/// <summary>
/// 被动资源条：即时显示传入的当前值与上限，可选文字；不订阅状态源、不执行玩法结算。
/// </summary>
public partial class ResourceBar : TextureProgressBar
{
    [Export] private Label m_Text; // 可选当前值与上限文本。
    [Export] private bool m_FullAtZeroMaximum; // 零上限时是否显示满条，用于经验满级。
    [Export] private string m_ZeroMaximumText = "MAX"; // 零上限的特殊文字，仅满条模式使用。

    /// <summary>立即显示一组新数值及可选文字。</summary>
    /// <param name="current">当前值，显示前钳到 [0, 上限]。</param>
    /// <param name="maximum">上限；零或负数按"空条"或"满条 + 特殊文字"显示。</param>
    public void SetValue(int current, int maximum)
    {
        int max = Math.Max(0, maximum);
        int value = Math.Clamp(current, 0, max);
        double ratio = max <= 0 ? (m_FullAtZeroMaximum ? 1.0 : 0.0) : (double)value / max;

        // 主填充与文字立即反映新值。
        MinValue = 0.0;
        MaxValue = 1.0;
        Step = 0.0;
        Value = ratio;
        if (m_Text != null)
        {
            m_Text.Text = max <= 0 && m_FullAtZeroMaximum ? m_ZeroMaximumText : $"{value}/{max}";
        }
    }
}
