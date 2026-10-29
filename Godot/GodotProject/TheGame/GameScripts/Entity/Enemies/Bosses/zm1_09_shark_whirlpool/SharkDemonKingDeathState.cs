using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using Godot;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 鲨魔王死亡先完成身体闪烁和BossDead，再回收首领及其召唤鲨鱼。
    /// </summary>
    internal sealed class SharkDemonKingDeathState : AbstractEnemyBodyState
    {
        private bool m_Completed; // 独立死亡旋涡完成后由下一身体帧安全消费。

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
        /// 方法回调只设置完成事实，避免播放器执行中销毁身体状态机。
        /// </summary>
        internal void CompleteEffect()
        {
            m_Completed = true;
        }
    }
}
