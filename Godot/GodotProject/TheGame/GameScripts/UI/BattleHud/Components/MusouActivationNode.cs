using GameLogic.Heroes.Body;
using Godot;

namespace GameLogic.UI.Widgets;

/// <summary>屏幕空间无双开启动画；只播放原帧，真实时间驱动以跨越开启时的短暂停顿。</summary>
public partial class MusouActivationNode : Control
{
    [Export] private AnimatedSprite2D m_Animation; // 原版 940×590 完整动画帧。
    private ulong m_Started; // 本次提示的真实时间原点；暂停不阻止动画。

    /// <summary>默认隐藏，等待 HUD 观察到真正的激活状态变化。</summary>
    public override void _Ready() => Reset();

    /// <summary>从首帧开始一次提示，多次通知不应由调用方重复播放。</summary>
    public void ShowActivation()
    {
        m_Started = Time.GetTicksMsec();
        // 只用下方的真实时间推进，避免子精灵同时自动前进一遍。
        m_Animation.Stop();
        m_Animation.Animation = HeroAnims.MusouActivation;
        m_Animation.SetFrameAndProgress(0, 0);
        Visible = true;
        SetProcess(true);
    }

    /// <summary>按原资源帧率显示，正常结束后隐藏；不暂停或恢复游戏。</summary>
    /// <param name="delta">引擎游戏时间，本表现使用真实时间而不依赖它。</param>
    public override void _Process(double delta)
    {
        double frames = (Time.GetTicksMsec() - m_Started) / 1000d *
            m_Animation.SpriteFrames.GetAnimationSpeed(HeroAnims.MusouActivation);
        if (frames >= m_Animation.SpriteFrames.GetFrameCount(HeroAnims.MusouActivation))
            Reset();
        else
            m_Animation.SetFrameAndProgress((int)frames, (float)(frames % 1));
    }

    /// <summary>关闭、改绑或复用时清除旧动画。</summary>
    public void Reset()
    {
        SetProcess(false);
        m_Animation?.Stop();
        Visible = false;
    }
}
