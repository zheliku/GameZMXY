using Godot;
using GodotGameFramework;

namespace GameLogic.UI.Widgets;

/// <summary>
/// 屏幕空间无双演出；原生动画完成时释放自己拥有的暂停，不另配表现秒数。
/// </summary>
public partial class MusouActivationNode : Control
{
    private static readonly StringName s_activationAnimation = "activation"; // 播放与完成信号共用的外置动画键。
    [Export] private AnimatedSprite2D _animation; // 官方完整画面帧，帧号由外置动画轨道编辑。
    [Export] private AnimationPlayer _player; // 原生动画轨道是唯一演出时钟。
    [Export] private Godot.Collections.Dictionary<int, SpriteFrames> _heroFrames = new(); // 按当前英雄身份绑定官方全屏演出。
    private ulong _previousTick; // 真实时间上次采样点，游戏暂停时仍能推进。
    private bool _ownsPause; // 只恢复本演出自己申请的 GGF 暂停。

    /// <summary>
    /// 连接原生完成信号，默认隐藏并停止。
    /// </summary>
    public override void _Ready()
    {
        _player.AnimationFinished += OnAnimationFinished;
        Reset();
    }

    /// <summary>
    /// 用真实秒推进原生动画，绕过游戏暂停产生的零 delta。
    /// </summary>
    /// <param name="delta">游戏帧秒数；演出时钟由单调真实时间提供。</param>
    public override void _Process(double delta)
    {
        ulong now = Time.GetTicksMsec();
        double elapsed = (now - _previousTick) / 1000d;
        _previousTick = now;
        _player.Advance(elapsed);
    }

    /// <summary>
    /// 从首帧播放，暂停时间完全由这段动画的实际长度决定。
    /// </summary>
    /// <param name="heroId">当前HUD绑定的出战英雄身份。</param>
    public void ShowActivation(int heroId)
    {
        Reset();
        _animation.SpriteFrames = _heroFrames[heroId];
        _ownsPause = !GF.Base.IsGamePaused;
        if (_ownsPause)
        {
            GF.Base.PauseGame();
        }

        _previousTick = Time.GetTicksMsec();
        _animation.Stop();
        _player.Play(s_activationAnimation);
        _player.Advance(0);
        Visible = true;
        SetProcess(true);
    }

    /// <summary>
    /// 关闭、改绑、完成或复用时停止演出并对称释放暂停。
    /// </summary>
    public void Reset()
    {
        SetProcess(false);
        _player?.Stop();
        _animation?.Stop();
        if (_animation != null)
        {
            _animation.Frame = 0;
        }

        Visible = false;
        if (!_ownsPause)
        {
            return;
        }

        _ownsPause = false;
        if (GF.Base.IsGamePaused)
        {
            GF.Base.ResumeGame();
        }
    }

    /// <summary>
    /// 完整演出结束后隐藏并恢复游戏。
    /// </summary>
    /// <param name="animation">原生播放器实际完成的动画名。</param>
    private void OnAnimationFinished(StringName animation)
    {
        if (animation == s_activationAnimation)
        {
            Reset();
        }
    }
}
