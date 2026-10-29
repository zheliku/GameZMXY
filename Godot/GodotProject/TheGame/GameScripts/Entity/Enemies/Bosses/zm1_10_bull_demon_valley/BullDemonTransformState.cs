using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using Godot;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 前两形态先结算死亡，再完成原生演出和后继登记，最后安全归还旧实体。
    /// </summary>
    internal sealed class BullDemonTransformState : AbstractEnemyBodyState
    {
        private bool m_AnimationCompleted; // 原生死亡片段与可选BossDead均已完成。
        private bool m_Requested; // 本次状态只提交一次后继显示。
        private bool m_Transformed; // 原服务完成所有权登记后才允许回收旧形态。

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
            m_AnimationCompleted = false;
            m_Requested = false;
            m_Transformed = false;
            body.MoveIntent = 0;
            body.TakeAttackRequest();
            body.TakePendingHurt();
            body.Velocity = Vector2.Zero;
            body.BeginDeath();
            body.RestartAnim(EnemyAnims.Death);
        }

        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
        {
            body.Velocity = Vector2.Zero;
            if (m_AnimationCompleted && !m_Requested)
            {
                m_Requested = true;
                ((AbstractBullDemonFormEntity)body).BeginTransformation(() => m_Transformed = true);
            }
            if (m_Transformed)
            {
                body.RequestRecycle();
            }
        }

        /// <summary>
        /// 原生方法键或独立死亡效果只登记完成，不在动画遍历中变身或销毁状态机。
        /// </summary>
        internal void CompleteAnimation()
        {
            m_AnimationCompleted = true;
        }
    }
}
