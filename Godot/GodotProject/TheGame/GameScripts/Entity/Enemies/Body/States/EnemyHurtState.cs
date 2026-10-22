using GameLogic.Enemies.Body;
using GameFramework.Fsm;
using System;
using Godot;

namespace GameLogic.Enemies.Body
{
    /// <summary>
    /// 受击硬直：生效时施加击退速度、从头播 hurt 动画、播受击语音；**硬直时长 = hurt 动画长度**
    ///（OnInit 读进 Params，状态计时）。横速按官方衰减至移动速度、作废 AI 出招请求。
    /// 硬直中再受击：重新生效（重置击退、计时，从头重播动画）。结束回 Move。
    /// </summary>
    public sealed class EnemyHurtState : AbstractEnemyBodyState
    {
        private const float SourceStepSeconds = 1f / 24f; // 官方先位移后加重力的源步长，只用于积分顺序换算。
        private float m_Left; // 受击硬直剩余时间（秒）。
        private bool m_Starting; // 本次击退的首个物理步尚未完成重力积分校正。

        /// <summary>连续受击由本状态自己处理（见 Tick），不走基类打断</summary>
        protected override bool HurtInterrupts => false;

        /// <summary>进入受击状态时清除移动意图并应用击退。</summary>
        protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
        {
            body.MoveIntent = 0;
            ApplyHurt(body);
        }

        /// <summary>处理连续受击、受击时长和重力。</summary>
        protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
        {
            body.TakeAttackRequest();
            if (body.HasPendingHurt)
            {
                ApplyHurt(body);
            }

            if (Elapsed(m_Left))
            {
                ChangeState<EnemyMoveState>(fsm);
                return;
            }

            m_Left -= dt;
            if (m_Starting)
            {
                // 源位移 v*t + g*t*(t-h)/2；Godot 半隐式积分多出 g*t*dt/2。
                // 只在起步扣掉 g*(h+dt)/2，后续仍按当前物理秒数推进，不运行24Hz子循环。
                body.Velocity -= new Vector2(0f, body.Gravity * (SourceStepSeconds + dt) * 0.5f);
                m_Starting = false;
            }
            float speed = body.Velocity.X;
            if (MathF.Abs(speed) > body.Params.MoveSpeed)
            {
                float magnitude = MathF.Max(body.Params.MoveSpeed,
                    MathF.Abs(speed) * MathF.Pow(body.Params.KnockbackRetentionPerSecond, dt));
                body.Velocity = new Vector2(MathF.CopySign(magnitude, speed), body.Velocity.Y);
            }
            ApplyGravity(body, dt);
        }

        /// <summary>消费击退请求并重新开始受击硬直。</summary>
        /// <param name="body">受击怪物。</param>
        private void ApplyHurt(IEnemyBody body)
        {
            // 连续受击覆盖当前击退和时钟，保持同一受击状态。
            body.Velocity = body.TakePendingHurt();
            m_Starting = true;
            m_Left = body.Params.HurtTime;
            body.RestartAnim(EnemyAnims.Hurt);
        }
    }
}
