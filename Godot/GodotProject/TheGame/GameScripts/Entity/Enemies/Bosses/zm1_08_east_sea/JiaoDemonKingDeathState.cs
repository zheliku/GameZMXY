using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using Godot;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 鲛魔王死亡保留身体闪烁与共享旋涡，原生效果完成后再回池。
    /// </summary>
    internal sealed class JiaoDemonKingDeathState : AbstractEnemyBodyState
    {
        private bool m_Completed; // 原生死亡旋涡的完成事实，在下一物理步消费。

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
            m_Completed = false;
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
            if (m_Completed)
            {
                body.RequestRecycle();
            }
        }

        /// <summary>
        /// 动画回调只标记完成，不在播放器遍历中销毁状态机。
        /// </summary>
        internal void CompleteEffect()
        {
            m_Completed = true;
        }
    }
}
