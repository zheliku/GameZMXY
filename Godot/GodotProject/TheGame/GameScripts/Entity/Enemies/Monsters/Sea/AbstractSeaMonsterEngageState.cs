using System;
using GameFramework.Fsm;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 海域地面怪的共同决策节拍；目标与受控归共享 AI，距离和选招归具体物种。
    /// </summary>
    internal abstract class AbstractSeaMonsterEngageState : AbstractEnemyAiState
    {
        private double m_NextDecision; // 下一次按源计数器作出概率/选招决策的游戏秒数。
        private double m_NextPatrolDecision; // 目标高度不可达时的下一次游荡决策。
        private int m_PatrolDirection; // 高度不可达期间保留的游荡方向，不追随空中目标。

        /// <inheritdoc />
        protected override void Enter(IEnemyAiAgent agent)
        {
            double interval = agent.Config.AttackInterval;
            m_NextDecision = (Math.Floor(agent.AiData.ElapsedSeconds / interval) + 1d) * interval;
            m_NextPatrolDecision = agent.AiData.ElapsedSeconds;
            m_PatrolDirection = 0;
        }

        /// <inheritdoc />
        protected sealed override void Tick(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds)
        {
            if (agent.IsAttacking)
            {
                return;
            }

            if (!agent.TargetBox.HasArea())
            {
                ChangeState<WanderState>(fsm);
                return;
            }

            // 出招及受控不重置实例时钟；错过的节拍不补发多次攻击。
            bool decide = agent.AiData.ElapsedSeconds + 1e-6 >= m_NextDecision;
            if (decide)
            {
                double interval = agent.Config.AttackInterval;
                m_NextDecision = (Math.Floor((agent.AiData.ElapsedSeconds + 1e-6) / interval) + 1d) * interval;
            }
            Engage(agent, Math.Sign(agent.TargetOffset.X), decide);
        }

        /// <summary>
        /// 执行本物种的距离和出招规则。
        /// </summary>
        /// <param name="agent">本实例的感知与意图。</param>
        /// <param name="direction">目标所在方向。</param>
        /// <param name="decide">是否抵达下一次配置决策时点。</param>
        protected abstract void Engage(IEnemyAiAgent agent, int direction, bool decide);

        /// <summary>
        /// 潮汐妖与龟丞相无法触及高处目标时继续原地段游荡，保留目标而非追向平台下方。
        /// </summary>
        /// <param name="agent">本实例的巡逻配置与移动意图。</param>
        protected void PatrolBelowTarget(IEnemyAiAgent agent)
        {
            if (agent.AiData.ElapsedSeconds >= m_NextPatrolDecision)
            {
                m_NextPatrolDecision = agent.AiData.ElapsedSeconds + agent.Config.PatrolInterval;
                m_PatrolDirection = Chance(agent, agent.Config.PatrolIdleChance) ? 0 : agent.NextRandom() < .5f ? -1 : 1;
            }
            if (agent.Config.PatrolRadius > 0f && MathF.Abs(agent.PatrolOffsetX) > agent.Config.PatrolRadius)
            {
                m_PatrolDirection = -Math.Sign(agent.PatrolOffsetX);
            }

            agent.Move(m_PatrolDirection);
        }
    }
}
