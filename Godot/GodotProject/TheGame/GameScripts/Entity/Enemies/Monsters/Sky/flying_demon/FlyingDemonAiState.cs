using System;
using GameFramework.Fsm;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// Monster15追踪英雄上方60px，近身每36个源帧尝试出招，并按原版退避三秒。
    /// </summary>
    internal sealed class FlyingDemonAiState : AbstractEnemyAiState
    {
        private float m_TurnInterval; // 本次显示保持不变的1～3秒折返间隔。
        private double m_TurnAt; // 全局AI游戏时钟中的下次折返时点，受击重入不重抽。
        private double m_AttackAt; // 近身攻击的下次判定时点。
        private double m_RetreatUntil; // 包含出招和受击时间的三秒退避截止时点。
        private double m_PatrolAt; // 无目标时的下次游荡时点。
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
            if (!agent.TargetBox.HasArea())
            {
                flight.VerticalIntent = 0;
                if (now >= m_PatrolAt)
                {
                    agent.Move(Math.Abs(agent.PatrolOffsetX) > 400f ? -Math.Sign(agent.PatrolOffsetX) :
                        agent.NextRandom() < .5f ? -1 : 1);
                    m_PatrolAt = now + (agent.NextRandom() < .5f ? 3f : 4f);
                }
                return;
            }

            var offset = agent.TargetOffset;
            if (Math.Abs(offset.X) > 150f || Math.Abs(offset.Y) > 100f)
            {
                if (now >= m_TurnAt)
                {
                    agent.Move(Math.Sign(offset.X));
                    m_TurnAt = now + m_TurnInterval;
                }
                flight.VerticalIntent = offset.Y > 60f ? 1 : -1;
                return;
            }

            agent.Move(0);
            flight.VerticalIntent = 0;
            if (now >= m_AttackAt)
            {
                m_AttackAt = now + agent.Config.AttackInterval;
                if (Chance(agent, agent.Config.AttackDesire) && agent.RequestAttack(0))
                {
                    m_RetreatUntil = now + 3f;
                    m_RetreatDirection = Math.Sign(offset.X) is 0 ? 1 : Math.Sign(offset.X);
                }
            }
        }
    }
}
