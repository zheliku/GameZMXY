using GameLogic.Heroes.Body;
using GameFramework.Fsm;

namespace GameLogic.Heroes.Body
{
    /// <summary>
    /// 受击硬直：生效时施加击退速度、从头播 hurt 动画、播受击语音；**硬直时长 = hurt 动画长度**
    ///（OnInit 读进 Params，状态计时）。期间保持击退速度，未消费的动作仅在仍按住时等待硬直结束。
    /// 硬直中再受击：重新生效（重置击退、计时，从头重播动画）。结束回 Ground/Air，移动逻辑接管
    ///（与旧项目一致：击退只在 hurt 动画期间生效）。
    /// </summary>
    public sealed class HeroHurtState : HeroBodyState
    {
        private float m_Left; // 受击硬直剩余时长（秒）。

        /// <summary>连续受击由本状态自己处理（见 Tick）</summary>
        protected override bool HurtInterrupts => false;

        /// <summary>进入受击状态时取消奔跑并立即应用击退，不制造跨松键的输入缓冲。</summary>
        protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
        {
            body.Input.StopRunning();
            ApplyHurt(body);
        }

        /// <summary>处理连续受击、硬直计时和受击动画。</summary>
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

            if (Elapsed(m_Left))
            {
                ChangeToLocomotion(fsm, body);
                return;
            }

            m_Left -= dt;
            ApplyGravity(body, dt);
        }

        /// <summary>消费待生效击退并重置受击动画计时。</summary>
        /// <param name="body">英雄宿主。</param>
        private void ApplyHurt(IHeroBody body)
        {
            // 连续受击重置同一状态，不排队保存旧击退请求。
            body.Velocity = body.TakePendingHurt();
            m_Left = body.Params.HurtTime;
            body.RestartAnim(HeroAnims.Hurt);
            body.PlayHurtSound();
        }
    }
}
