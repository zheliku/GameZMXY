using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using Godot;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 龙王死亡完成25帧闪烁后才掉落并开放传送门，与原版destroy时点一致。
    /// </summary>
    internal sealed class DragonKingDeathState : AbstractEnemyBodyState
    {
        private float m_Left; // 正式死亡动画complete标记之前的剩余秒数。
        private bool m_Completed; // 奖励和回收只提交一次。

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
            body.TakeAttackRequest();
            body.TakePendingHurt();
            body.Velocity = Vector2.Zero;
            ((DragonKingEntity)body).HurtBox.Monitorable = false;
            m_Left = body.Params.DeathTime;
            m_Completed = false;
            body.RestartAnim(EnemyAnims.Death);
        }

        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
        {
            body.Velocity = Vector2.Zero;
            if (m_Completed)
            {
                return;
            }

            if (Elapsed(m_Left))
            {
                m_Completed = true;
                body.BeginDeath();
                body.RequestRecycle();
                return;
            }
            m_Left -= dt;
        }
    }
}
