using GameLogic.Enemies.Body;
using GameLogic.Body;
using GameFramework.Fsm;

namespace GameLogic.Enemies.Body
{
    /// <summary>
    /// 死亡（终态）：进入时由宿主做副作用（<see cref="IEnemyBody.BeginDeath"/>：普通怪物保留受击体、首领关闭受击盒、广播
    /// EnemyDiedEventArgs），作废出招请求并保留致死抛飞；按原生complete标记检查回收，存在air_loop时等待落地。
    /// 池复用由OnShow重建整台状态机。
    /// </summary>
    public sealed class EnemyDeathState : AbstractEnemyBodyState
    {
        private float m_Left; // 死亡动画结束前的剩余时间（秒）。

        private bool m_Recycled; // 是否已经向宿主发送回收请求。
        private bool m_Starting; // 新的致死/尸体击退需要一次积分校正。

        /// <summary>
        /// 死亡状态不再响应受击中断。
        /// </summary>
        protected override bool HurtInterrupts
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// 清除待处理请求，执行死亡副作用并播放死亡动画。
        /// </summary>
        /// <param name="fsm">当前宿主拥有的状态机。</param>
        /// <param name="body">当前状态借用的身体宿主。</param>
        protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
        {
            body.MoveIntent = 0;
            body.TakeAttackRequest();
            m_Starting = body.HasPendingHurt;
            if (m_Starting)
            {
                body.Velocity = body.TakePendingHurt();
            }

            body.BeginDeath();
            m_Left = body.Params.DeathTime;
            m_Recycled = false;
            body.RestartAnim(EnemyAnims.Death);
        }

        /// <summary>
        /// 维持尸体抛飞与原生循环；到达允许销毁的完成帧后只回收一次。
        /// </summary>
        /// <param name="fsm">当前宿主拥有的状态机。</param>
        /// <param name="body">当前状态借用的身体宿主。</param>
        /// <param name="dt">本次物理推进的秒数。</param>
        protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
        {
            if (body.HasPendingHurt)
            {
                body.Velocity = body.TakePendingHurt();
                m_Starting = true;
            }
            ApplyHitMotion(body, dt, m_Starting);
            m_Starting = false;

            if (m_Recycled)
            {
                return;
            }

            if (Elapsed(m_Left))
            {
                if (!body.OnFloor && body.Params.DeathAirLoopTime.HasValue)
                {
                    // Flash keeps the corpse animation in its post-impact loop until the body lands.
                    body.RestartAnim(EnemyAnims.Death, EnemyAnims.DeathAirLoopMarker);
                    m_Left = body.Params.DeathTime - body.Params.DeathAirLoopTime.Value;
                    return;
                }

                m_Recycled = true;
                body.RequestRecycle();
                return;
            }

            m_Left -= dt;
        }
    }
}
