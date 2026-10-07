using System;
using GameLogic.Bindable;
using Godot;

namespace GameLogic.UI;

/// <summary>观察任意资源的当前值与上限，可选显示文字、损失残影和段位；不执行玩法结算。</summary>
public partial class ResourceBar : TextureProgressBar
{
    private const double DelaySeconds = 0.35; // 损失残影追平填充的表现秒数。

    [Export] private TextureProgressBar m_Delay; // 可选损失残影节点。
    [Export] private Label m_Text; // 可选当前值与上限文本。
    [Export] private Sprite2D m_Steps; // 可选段位帧图，例如无双。
    [Export] private bool m_FullAtZeroMaximum; // 零上限时是否显示满条，用于经验满级。
    [Export] private string m_ZeroMaximumText = "MAX"; // 零上限的特殊文字，仅满条模式使用。

    private BindableProperty<int> m_Current; // 本次绑定的当前资源属性。
    private BindableProperty<int> m_Maximum; // 本次绑定的上限属性。
    private Tween m_DelayTween; // 解绑或刷新时停止的残影补间。

    /// <summary>先解除旧订阅，再观察两个属性；首次显示直接对齐可选残影。</summary>
    /// <param name="current">当前资源值。</param>
    /// <param name="maximum">资源上限。</param>
    /// <exception cref="ArgumentNullException">任一属性为空。</exception>
    public void Bind(BindableProperty<int> current, BindableProperty<int> maximum)
    {
        Unbind();
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(maximum);

        // 保存具体实例，窗口关闭时对称解除同一订阅。
        m_Current = current;
        m_Maximum = maximum;
        current.Changed += OnCurrentChanged;
        maximum.Changed += OnMaximumChanged;
        Refresh(immediate: true);
    }

    /// <summary>解除属性订阅并停止残影，重复调用及框架关停时均可使用。</summary>
    public void Unbind()
    {
        if (m_Current != null)
        {
            m_Current.Changed -= OnCurrentChanged;
            m_Maximum.Changed -= OnMaximumChanged;
            m_Current = null;
            m_Maximum = null;
        }
        StopTween();
    }

    /// <summary>当前值变化时刷新填充，数值下降时保留损失残影。</summary>
    /// <param name="current">通知后的当前值；同时读取绑定的上限。</param>
    private void OnCurrentChanged(int current) => Refresh(immediate: false);

    /// <summary>上限改变时立即重新对齐比例，避免把等级变化当作资源损失。</summary>
    /// <param name="maximum">通知后的上限。</param>
    private void OnMaximumChanged(int maximum) => Refresh(immediate: true);

    /// <summary>刷新场景配置的填充与可选表现，不依赖资源的业务类型。</summary>
    /// <param name="immediate">是否立即对齐残影。</param>
    private void Refresh(bool immediate)
    {
        int maximum = m_Maximum.Value;
        int current = Math.Clamp(m_Current.Value, 0, Math.Max(0, maximum));
        double ratio = maximum <= 0 ? (m_FullAtZeroMaximum ? 1.0 : 0.0) : (double)current / maximum;
        MinValue = 0.0;
        MaxValue = 1.0;
        Step = 0.0;
        Value = ratio;
        if (m_Text != null)
        {
            m_Text.Text = maximum <= 0 && m_FullAtZeroMaximum ? m_ZeroMaximumText : $"{current}/{Math.Max(0, maximum)}";
        }
        if (m_Steps != null)
        {
            m_Steps.Frame = Math.Clamp((int)Math.Round(ratio * (m_Steps.Hframes - 1)), 0, m_Steps.Hframes - 1);
        }

        // 只对下降值播放残影；增益、重新绑定和上限变化立即显示。
        StopTween();
        if (m_Delay == null)
        {
            return;
        }
        m_Delay.MinValue = 0.0;
        m_Delay.MaxValue = 1.0;
        m_Delay.Step = 0.0;
        if (immediate || ratio >= m_Delay.Value)
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
