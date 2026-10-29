using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using Godot;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 破蛋后播放身体闪烁和死亡旋涡，旋涡真正结束后才归还首领及其效果。
    /// </summary>
    internal sealed class PengDemonKingDeathState : PengDemonKingBodyState
    {
        private bool m_Completed; // 原生死亡旋涡已经完成，下一物理步可以回收。

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
        /// 效果回调只登记事实，避免动画方法遍历中销毁身体状态机。
        /// </summary>
        internal void CompleteEffect()
        {
            m_Completed = true;
        }
    }
}
