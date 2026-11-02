using System;
using GameFramework.Fsm;
using GameLogic.Architecture;

namespace GameLogic.Enemies.AI
{
    /// <summary>
    /// 飞行小怪追踪目标上方指定高度，按共同节拍出招并退避三秒；节点只提供当前高度事实。
    /// </summary>
    /// <param name="pursuitHeight">追踪目标上方的距离，像素。</param>
    /// <param name="trackingHeight">允许追踪的高差，像素。</param>
    /// <param name="altitude">读取当前世界高度，不复制节点位置。</param>
    public sealed class FlyingPursuitState(float pursuitHeight, float trackingHeight,
        Func<float> altitude) : AbstractEnemyAiState
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
            agent.AiData.VerticalIntent = 0;
            agent.AiData.DirectVerticalMovement = false;
        }

        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds)
        {
            var intent = agent.AiData;
            double now = intent.ElapsedSeconds;
            intent.DirectVerticalMovement = now < m_RetreatUntil;
            if (agent.IsAttacking)
            {
                return;
            }

            // 原版BACK_TIME从出招开始计时；结束后沿本次朝向升至150px，再恢复追踪。
            if (now < m_RetreatUntil)
            {
                agent.Move(m_RetreatDirection);
                intent.VerticalIntent = altitude() > 150f ? -1 : 0;
                return;
            }
            if (!agent.HasTarget || Math.Abs(agent.TargetOffset.Y) > trackingHeight)
            {
                intent.VerticalIntent = 0;
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
                intent.VerticalIntent = offset.Y > pursuitHeight ? 1 : -1;
                return;
            }

            agent.Move(0);
            intent.VerticalIntent = 0;
            if (agent.GetSystem<EnemyAiSystem>().IsDecisionDue(intent, agent.Config.AttackInterval) &&
                Chance(agent, agent.Config.AttackDesire) && agent.RequestAttack(0))
            {
                m_RetreatUntil = now + 3f;
                m_RetreatDirection = Math.Sign(offset.X) is 0 ? 1 : Math.Sign(offset.X);
            }
        }
    }
}
