using System;
using GameFramework.Fsm;

namespace GameLogic.Enemies.AI
{
    /// <summary>
    /// 左右随机游荡（旧 normal_state）。每 PatrolInterval 决策一次：PatrolIdleChance 概率停下，否则左右各半；
    /// 离巡逻中心超出 PatrolRadius 时立刻往回走、期间不停留。
    /// </summary>
    public class WanderState : AbstractRoamState
    {
        private float m_DecideTimer; // 距下一次巡逻停留/转向决策的剩余时间（秒）。

        private int m_Dir; // 当前游荡方向；-1 向左，1 向右。

        /// <summary>初始化巡逻决策计时，并选择初始方向。</summary>
        protected override void Enter(IEnemyAiAgent agent)
        {
            // 进入时只选方向，不掷停留（避免进入即切走）
            m_DecideTimer = agent.Config.PatrolInterval;
            int returnDirection = GetPatrolReturnDirection(agent);
            m_Dir = returnDirection != 0 ? returnDirection : ChooseWanderDirection(agent);
            agent.Move(m_Dir);
        }

        /// <summary>按巡逻范围与停留概率更新方向或切换暂停状态。</summary>
        protected override void Roam(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds)
        {
            m_DecideTimer -= elapseSeconds;
            int returnDirection = GetPatrolReturnDirection(agent);
            if (returnDirection != 0)
            {
                m_Dir = returnDirection;
            }
            else if (m_DecideTimer <= 0f)
            {
                m_DecideTimer = agent.Config.PatrolInterval;
                if (Chance(agent, agent.Config.PatrolIdleChance))
                {
                    ChangeState<PauseState>(fsm);
                    return;
                }

                m_Dir = ChooseWanderDirection(agent);
            }

            agent.Move(m_Dir);
        }

        /// <summary>计算超出巡逻半径后的折返方向。</summary>
        /// <param name="agent">提供巡逻中心偏移与半径的怪物。</param>
        /// <returns>左为 -1，右为 1；仍在范围内或未限制半径时为 0。</returns>
        private static int GetPatrolReturnDirection(IEnemyAiAgent agent)
        {
            float home = agent.PatrolOffsetX;
            float radius = agent.Config.PatrolRadius;
            return radius > 0f && Math.Abs(home) > radius ? -Math.Sign(home) : 0;
        }

        /// <summary>使用怪物随机源等概率选择左右巡逻方向。</summary>
        /// <param name="agent">提供本怪物随机源的 AI 宿主。</param>
        /// <returns>左为 -1，右为 1。</returns>
        private static int ChooseWanderDirection(IEnemyAiAgent agent)
        {
            return agent.NextRandom() < 0.5f ? -1 : 1;
        }
    }
}
