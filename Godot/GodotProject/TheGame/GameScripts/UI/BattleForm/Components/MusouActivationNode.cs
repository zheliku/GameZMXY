using Godot;
using GodotGameFramework;

namespace GameLogic.UI.Widgets;

/// <summary>屏幕空间无双演出；原生动画完成时释放自己拥有的暂停，不另配表现秒数。</summary>
public partial class MusouActivationNode : Control
{
    [Export] private AnimatedSprite2D m_Animation; // 官方完整画面帧，帧号由外置动画轨道编辑。
    [Export] private AnimationPlayer m_Player; // 原生动画轨道是唯一演出时钟。
    private ulong m_PreviousTick; // 真实时间上次采样点，游戏暂停时仍能推进。
    private bool m_OwnsPause; // 只恢复本演出自己申请的 GGF 暂停。

    /// <summary>连接原生完成信号，默认隐藏并停止。</summary>
    public override void _Ready()
    {
        m_Player.AnimationFinished += OnAnimationFinished;
        Reset();
    }

    /// <summary>用真实秒推进原生动画，绕过游戏暂停产生的零 delta。</summary>
    /// <param name="delta">游戏帧秒数；演出时钟由单调真实时间提供。</param>
    public override void _Process(double delta)
    {
        ulong now = Time.GetTicksMsec();
        double elapsed = (now - m_PreviousTick) / 1000d;
        m_PreviousTick = now;
        m_Player.Advance(elapsed);
    }

    /// <summary>从首帧播放，暂停时间完全由这段动画的实际长度决定。</summary>
    public void ShowActivation()
    {
        Reset();
        m_OwnsPause = !GF.Base.IsGamePaused;
        if (m_OwnsPause)
            GF.Base.PauseGame();
        m_PreviousTick = Time.GetTicksMsec();
        m_Animation.Stop();
        m_Player.Play("activation");
        m_Player.Advance(0);
        Visible = true;
        SetProcess(true);
    }

    /// <summary>关闭、改绑、完成或复用时停止演出并对称释放暂停。</summary>
    public void Reset()
    {
        SetProcess(false);
        m_Player?.Stop();
        m_Animation?.Stop();
        if (m_Animation != null)
            m_Animation.Frame = 0;
        Visible = false;
        if (!m_OwnsPause)
            return;
        m_OwnsPause = false;
        if (GF.Base.IsGamePaused)
            GF.Base.ResumeGame();
    }

    /// <summary>完整演出结束后隐藏并恢复游戏。</summary>
    /// <param name="animation">原生播放器实际完成的动画名。</param>
    private void OnAnimationFinished(StringName animation)
    {
        if (animation == "activation")
            Reset();
    }
}
