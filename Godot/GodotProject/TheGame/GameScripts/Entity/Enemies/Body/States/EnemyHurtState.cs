using GameLogic.Enemies.Body;
using GameFramework.Fsm;

namespace GameLogic.Enemies.Body
{
    /// <summary>
    /// 受击硬直：施加击退并重播hurt，按原生结束标记计时；受击声音归ReceiveHit。
    /// 横速按官方衰减至移动速度、作废AI出招请求。
    /// 硬直中再受击：重新生效（重置击退、计时，从头重播动画）。结束回 Move。
    /// </summary>
    public sealed class EnemyHurtState : AbstractEnemyBodyState
    {
        private float m_Left; // 受击硬直剩余时间（秒）。
        private bool m_Starting; // 本次击退的首个物理步尚未完成重力积分校正。

        /// <summary>
        /// 连续受击由本状态自己处理（见 Tick），不走基类打断
        /// </summary>
        protected override bool HurtInterrupts
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// 进入受击状态时清除移动意图并应用击退。
        /// </summary>
        /// <param name="fsm">当前宿主拥有的状态机。</param>
        /// <param name="body">当前状态借用的身体宿主。</param>
        protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
        {
            body.MoveIntent = 0;
            ApplyHurt(body);
        }

        /// <summary>
        /// 处理连续受击、受击时长和重力。
        /// </summary>
        /// <param name="fsm">当前宿主拥有的状态机。</param>
        /// <param name="body">当前状态借用的身体宿主。</param>
        /// <param name="dt">本次物理推进的秒数。</param>
        protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
        {
            body.TakeAttackRequest();
            if (body.HasPendingHurt)
            {
                ApplyHurt(body);
            }

            if (Elapsed(m_Left))
            {
                ChangeState(fsm, body.MovementStateType);
                return;
            }

            m_Left -= dt;
            ApplyHitMotion(body, dt, m_Starting);
            m_Starting = false;
        }

        /// <summary>
        /// 消费击退请求并重新开始受击硬直。
        /// </summary>
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
