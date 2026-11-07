using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Heroes.Body;
using GameLogic.Input;

namespace GameLogic.Heroes.Body;

/// <summary>
/// 受击状态维护击退、重力与原生结束标记计时；动画方法键可以按角色源帧停止运动，声音归ReceiveHit。
/// 连续受击覆盖本次击退并重播，结束回Ground/Air；未消费动作仅在仍按住时等待硬直结束。
/// </summary>
public sealed class HeroHurtState : AbstractHeroBodyState
{
    private float _left; // 受击硬直剩余时长（秒）。

    /// <summary>
    /// 连续受击由本状态自己处理（见 Tick）
    /// </summary>
    protected override bool HurtInterrupts => false;

    /// <summary>
    /// 进入受击状态时取消奔跑并立即应用击退，不制造跨松键的输入缓冲。
    /// </summary>
    /// <param name="fsm">当前宿主拥有的状态机。</param>
    /// <param name="body">当前状态借用的身体宿主。</param>
    protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
    {
        body.GetSystem<InputSystem>().StopRunning(body.ActionData);
        ApplyHurt(body);
    }

    /// <summary>
    /// 处理连续受击、硬直计时和受击动画。
    /// </summary>
    /// <param name="fsm">当前宿主拥有的状态机。</param>
    /// <param name="body">当前状态借用的身体宿主。</param>
    /// <param name="dt">本次物理推进的秒数。</param>
    protected override void Tick(IFsm<IHeroBody> fsm, IHeroBody body, float dt)
    {
        if (body.SuperArmor)
        {
            body.Velocity = new Godot.Vector2(0f, body.Velocity.Y);
            ChangeToLocomotion(fsm, body);
            return;
        }

        if (body.HasPendingHurt)
        {
            ApplyHurt(body);
        }

        if (Elapsed(_left))
        {
            ChangeToLocomotion(fsm, body);
            return;
        }

        _left -= dt;
        ApplyGravity(body, dt);
    }

    /// <summary>
    /// 消费待生效击退并重置受击动画计时。
    /// </summary>
    /// <param name="body">英雄宿主。</param>
    private void ApplyHurt(IHeroBody body)
    {
        // 连续受击重置同一状态，不排队保存旧击退请求。
        body.Velocity = body.TakePendingHurt();
        _left = body.Params.HurtTime;
        body.RestartAnim(HeroAnims.Hurt);
    }
}
