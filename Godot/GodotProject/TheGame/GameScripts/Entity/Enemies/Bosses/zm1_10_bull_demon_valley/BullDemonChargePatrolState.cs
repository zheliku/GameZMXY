using GameFramework.Fsm;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 兽形冲撞忽略目标距离，抵达相机两侧后停步，再向反方向冲刺。
    /// </summary>
    internal sealed class BullDemonChargePatrolState : AbstractEnemyAiState
    {
        private int m_Direction; // 当前一轮冲撞方向，首次固定朝左。
        private float m_RestLeft; // 首轮边界停一秒，之后每轮停三秒。
        private bool m_Resting; // 已抵达边界，不能随目标移动立即重新起步。

        /// <inheritdoc />
        protected override void Enter(IEnemyAiAgent agent)
        {
            m_Direction = -1;
            m_RestLeft = 1f;
            m_Resting = false;
            agent.Move(m_Direction);
        }

        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds)
        {
            if (!m_Resting && (m_Direction < 0 && agent.PatrolOffsetX < agent.PatrolBounds.X ||
                m_Direction > 0 && agent.PatrolOffsetX > agent.PatrolBounds.Y))
            {
                m_Resting = true;
                agent.Move(0);
            }
            if (!m_Resting)
            {
                return;
            }

            m_RestLeft -= elapseSeconds;
            if (m_RestLeft > 0f)
            {
                return;
            }

            m_RestLeft = 3f;
            m_Resting = false;
            m_Direction = -m_Direction;
            agent.Move(m_Direction);
        }
    }
}
