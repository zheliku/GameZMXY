using GameFramework.Fsm;
using GameLogic.Enemies.Body;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 彭魔王普通硬直保留其飞行重力，并在首次生命耗尽时优先进入化卵。
    /// </summary>
    internal sealed class PengDemonKingHurtState : PengDemonKingBodyState
    {
        private float m_Left; // 本次六源帧硬直的剩余秒数。
        private bool m_Starting; // 击退初始积分校正尚未应用。

        /// <inheritdoc />
        protected override bool HurtInterrupts
        {
            get
            {
                return false;
            }
        }

        /// <inheritdoc />
        protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
        {
            body.MoveIntent = 0;
            ApplyHurt(body);
        }

        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
        {
            body.TakeAttackRequest();
            if (body.HasPendingHurt)
            {
                ApplyHurt(body);
            }

            if (Elapsed(m_Left))
            {
                ChangeState<PengDemonKingMoveState>(fsm);
                return;
            }
            m_Left -= dt;
            ApplyHitMotion(body, dt, m_Starting);
            m_Starting = false;
        }

        /// <summary>
        /// 连续受击更新同一硬直，不恢复到地面小怪的动作入口。
        /// </summary>
        /// <param name="body">本实例身体。</param>
        private void ApplyHurt(IEnemyBody body)
        {
            body.Velocity = body.TakePendingHurt();
            m_Left = body.Params.HurtTime;
            m_Starting = true;
            body.RestartAnim(EnemyAnims.Hurt);
        }
    }
}
