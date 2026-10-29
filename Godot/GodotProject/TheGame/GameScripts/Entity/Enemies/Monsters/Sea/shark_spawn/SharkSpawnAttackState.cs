using GameFramework.Fsm;
using GameLogic.Enemies.Body;
using Godot;
using GodotGameFramework;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 鲨鱼hit1保留飞行冲刺并在接触后收招；hit2等待地面警示完成后恢复行动。
    /// </summary>
    internal sealed class SharkSpawnAttackState : AbstractEnemyBodyState
    {
        private float m_Left; // 原生动作完成标记的剩余时间。
        private bool m_ContactCompleted; // 身体已接触目标，下一身体帧释放斩击并收招。
        private Vector2 m_ContactPosition; // 接触回调捕获的世界落点，下一身体帧才创建物理效果。
        private bool m_WaitingReturn; // 原生第49帧的警示尚未完成，身体保持隐藏。

        /// <inheritdoc />
        protected override void Enter(IFsm<IEnemyBody> fsm, IEnemyBody body)
        {
            int index = fsm.GetData<VarInt32>(AttackIndexKey).Value;
            fsm.RemoveData(AttackIndexKey);
            m_ContactCompleted = false;
            m_WaitingReturn = false;
            body.BeginAttack(index);
            m_Left = body.Params.AttackTimes[index];
            body.RestartAnim(body.Params.AttackAnims[index]);
        }

        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyBody> fsm, IEnemyBody body, float dt)
        {
            if (m_WaitingReturn)
            {
                body.Velocity = Vector2.Zero;
                return;
            }
            if (m_ContactCompleted)
            {
                // AreaEntered仍在物理查询中；创建带Area的效果必须回到正常身体更新。
                ((SharkSpawnEntity)body).SpawnContactSlash(m_ContactPosition);
                ChangeState(fsm, body.MovementStateType);
                return;
            }
            if (Elapsed(m_Left))
            {
                ChangeState(fsm, body.MovementStateType);
                return;
            }
            m_Left -= dt;
        }

        /// <inheritdoc />
        protected override void Leave(IFsm<IEnemyBody> fsm, IEnemyBody body, bool isShutdown)
        {
            body.EndAttack();
            body.Velocity = Vector2.Zero;
            if (!isShutdown)
            {
                ((SharkSpawnEntity)body).RestoreSharkBody();
            }
        }

        /// <summary>
        /// 真实接触命中只记录落点，不在物理查询回调中创建判定或销毁状态。
        /// </summary>
        /// <param name="position">接触时刻按目标根坐标计算的斩击世界原点。</param>
        internal void CompleteContact(Vector2 position)
        {
            m_ContactPosition = position;
            m_ContactCompleted = true;
        }

        /// <summary>
        /// 原生帧请求等候返回警示，本状态仍是攻击忙碌源。
        /// </summary>
        internal void WaitForReturn()
        {
            m_WaitingReturn = true;
        }

        /// <summary>
        /// 警示完成后在下一身体帧恢复飞行。
        /// </summary>
        internal void CompleteReturn()
        {
            m_WaitingReturn = false;
            m_Left = 0f;
        }
    }
}
