using System;
using Godot;

namespace GameLogic.UI;

/// <summary>
/// 被动资源条：只接收当前值与上限并显示，可选文字、损失残影和段位；不订阅任何状态源、不执行玩法结算。
/// 窗口在状态变化时调用 <see cref="SetValue"/>；残影由本控件比较前后两次的比例自行决定。
/// </summary>
public partial class ResourceBar : TextureProgressBar
{
    private const double DelaySeconds = 0.35; // 损失残影追平填充的表现秒数。

    [Export] private TextureProgressBar m_Delay; // 可选损失残影节点。
    [Export] private Label m_Text; // 可选当前值与上限文本。
    [Export] private Sprite2D m_Steps; // 可选段位帧图，例如无双。
    [Export] private bool m_FullAtZeroMaximum; // 零上限时是否显示满条，用于经验满级。
    [Export] private string m_ZeroMaximumText = "MAX"; // 零上限的特殊文字，仅满条模式使用。

    private int m_LastMaximum = -1; // 上一次显示的上限；变化时视为重新对齐，不播放损失残影。
    private Tween m_DelayTween; // 刷新或复位时停止的残影补间。

    /// <summary>显示一组新数值；比例下降且上限不变时播放损失残影，其余情况立即对齐。</summary>
    /// <param name="current">当前值，显示前钳到 [0, 上限]。</param>
    /// <param name="maximum">上限；零或负数按"空条"或"满条 + 特殊文字"显示。</param>
    /// <param name="immediate">是否强制立即对齐残影（窗口打开、重新绑定时）。</param>
    public void SetValue(int current, int maximum, bool immediate = false)
    {
        int max = Math.Max(0, maximum);
        int value = Math.Clamp(current, 0, max);
        double ratio = max <= 0 ? (m_FullAtZeroMaximum ? 1.0 : 0.0) : (double)value / max;
        bool realign = immediate || max != m_LastMaximum;
        m_LastMaximum = max;

        // 主填充、文字与段位立即反映新值。
        MinValue = 0.0;
        MaxValue = 1.0;
        Step = 0.0;
        Value = ratio;
        if (m_Text != null)
        {
            m_Text.Text = max <= 0 && m_FullAtZeroMaximum ? m_ZeroMaximumText : $"{value}/{max}";
        }

        if (m_Steps != null)
        {
            m_Steps.Frame = Math.Clamp((int)Math.Round(ratio * (m_Steps.Hframes - 1)), 0, m_Steps.Hframes - 1);
        }

        // 只对同一上限下的下降播放残影；增益、上限变化和强制对齐立即显示。
        StopTween();
        if (m_Delay == null)
        {
            return;
        }

        m_Delay.MinValue = 0.0;
        m_Delay.MaxValue = 1.0;
        m_Delay.Step = 0.0;
        if (realign || ratio >= m_Delay.Value)
        {
            m_Delay.Value = ratio;
        }
        else
        {
            m_DelayTween = CreateTween();
            m_DelayTween.TweenProperty(m_Delay, "value", ratio, DelaySeconds)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        }
    }

    /// <summary>停止残影并忘记上一次的上限（窗口关闭时调用），下次显示必然立即对齐。</summary>
    public void ResetPresentation()
    {
        m_LastMaximum = -1;
        StopTween();
    }

    /// <summary>仅在补间仍有效时停止它，避免关停时访问已释放的引擎对象。</summary>
    private void StopTween()
    {
        if (IsInstanceValid(m_DelayTween))
        {
            m_DelayTween.Kill();
        }

        m_DelayTween = null;
    }
}
