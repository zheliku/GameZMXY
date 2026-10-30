using System;
using GameLogic.Architecture;
using GameFramework.Fsm;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 蝙蝠追踪英雄上方90px，按共同决策节拍出招，并在起手后三秒退避。
    /// </summary>
    internal sealed class BatEngageState : AbstractEnemyAiState
    {
        private float m_TurnInterval; // 本次显示保持不变的1～3秒折返间隔。
        private double m_TurnAt; // 全局AI游戏时钟中的下次折返时点，受击重入不重抽。
        private double m_RetreatUntil; // 包含出招和受击时间的三秒退避截止时点。
        private int m_RetreatDirection; // 本次攻击固定的退避朝向。

        /// <inheritdoc />
        protected internal override void OnInit(IFsm<IEnemyAiAgent> fsm)
        {
            base.OnInit(fsm);
            m_TurnInterval = 1f + 2f * fsm.Owner.NextRandom();
        }

        /// <inheritdoc />
        protected override void Enter(IEnemyAiAgent agent)
        {
            base.Enter(agent);
            var flight = (AbstractSkyMonsterEntity)agent;
            flight.VerticalIntent = 0;
            flight.DirectVerticalMovement = false;
        }

        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds)
        {
            var flight = (AbstractSkyMonsterEntity)agent;
            double now = agent.AiData.ElapsedSeconds;
            flight.DirectVerticalMovement = now < m_RetreatUntil;
            if (agent.IsAttacking)
            {
                return;
            }

            // 原版BACK_TIME从出招开始计时；结束后沿本次朝向升至150px，再恢复追踪。
            if (now < m_RetreatUntil)
            {
                agent.Move(m_RetreatDirection);
                flight.VerticalIntent = flight.Altitude > 150f ? -1 : 0;
                return;
            }
            if (!agent.HasTarget)
            {
                flight.VerticalIntent = 0;
                agent.GetSystem<EnemyAiSystem>().Patrol(agent, elapseSeconds);
                return;
            }

            // BaseMonster先检查两倍attackRange高度，再执行物种自己的近身条件。
            if (Math.Abs(agent.TargetOffset.Y) > flight.TrackingHeight)
            {
                flight.VerticalIntent = 0;
                agent.GetSystem<EnemyAiSystem>().Patrol(agent, elapseSeconds);
                return;
            }
            var offset = agent.TargetOffset;
            if (!agent.IsInEngageRange)
            {
                if (now >= m_TurnAt)
                {
                    agent.Move(Math.Sign(offset.X));
                    m_TurnAt = now + m_TurnInterval;
                }
                flight.VerticalIntent = offset.Y > 90f ? 1 : -1;
                return;
            }

            agent.Move(0);
            flight.VerticalIntent = 0;
            if (agent.GetSystem<EnemyAiSystem>().IsDecisionDue(agent.AiData, agent.Config.AttackInterval))
            {
                if (Chance(agent, agent.Config.AttackDesire) && agent.RequestAttack(0))
                {
                    m_RetreatUntil = now + 3f;
                    m_RetreatDirection = Math.Sign(offset.X) is 0 ? 1 : Math.Sign(offset.X);
                }
            }
        }
    }
}
