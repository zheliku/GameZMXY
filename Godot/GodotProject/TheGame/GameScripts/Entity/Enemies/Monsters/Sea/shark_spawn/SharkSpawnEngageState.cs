using System;
using GameLogic.Architecture;
using GameFramework.Fsm;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 鲨鱼按原点追踪、固定折返间隔和一秒决策节拍选择撞击或旋风；撞击后退避三秒。
    /// </summary>
    /// <param name="trackingHeight">场景允许追踪的高差，像素。</param>
    /// <param name="altitude">读取当前世界高度。</param>
    /// <param name="horizontalSpeed">读取当前身体水平速度，退避沿用实际运动方向。</param>
    internal sealed class SharkSpawnEngageState(float trackingHeight, Func<float> altitude,
        Func<float> horizontalSpeed) : AbstractEnemyAiState
    {
        private float m_TurnInterval; // 本次显示固定的1～3秒折返间隔。
        private double m_TurnAt; // 下一次允许改变水平追踪方向的游戏时点。
        private double m_RetreatUntil; // 从普通撞击起手开始的三秒退避截止时点。
        private int m_TravelDirection; // 折返节流期间保留的水平移动方向。

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
            var flight = agent.AiData;
            flight.VerticalIntent = 0;
            flight.DirectVerticalMovement = false;
        }

        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds)
        {
            var flight = agent.AiData;
            double now = agent.AiData.ElapsedSeconds;
            if (agent.IsAttacking)
            {
                return;
            }

            // 退避与招式、受击共用游戏时钟；只升至150px，不继续飞出画面。
            flight.DirectVerticalMovement = now < m_RetreatUntil;
            if (flight.DirectVerticalMovement)
            {
                int direction = Math.Sign(horizontalSpeed());
                agent.Move(direction == 0 ? m_TravelDirection : direction);
                flight.VerticalIntent = altitude() > 150f ? -1 : 0;
                return;
            }

            if (!agent.HasTarget)
            {
                flight.VerticalIntent = 0;
                agent.GetSystem<EnemyAiSystem>().Patrol(agent, elapseSeconds);
                m_TravelDirection = agent.AiData.MoveIntent;
                return;
            }

            if (Math.Abs(agent.TargetOffset.Y) > trackingHeight)
            {
                flight.VerticalIntent = 0;
                agent.GetSystem<EnemyAiSystem>().Patrol(agent, elapseSeconds);
                m_TravelDirection = agent.AiData.MoveIntent;
                return;
            }

            // 追踪条件与450px攻击定义分开：超过250px或高差50px时追向目标上方60px。
            var offset = agent.TargetOffset;
            if (!agent.IsInEngageRange)
            {
                if (now >= m_TurnAt)
                {
                    m_TravelDirection = offset.X < 0f ? -1 : 1;
                    m_TurnAt = now + m_TurnInterval;
                }
                agent.Move(m_TravelDirection);
                flight.VerticalIntent = offset.Y > 60f ? 1 : -1;
                return;
            }

            flight.VerticalIntent = 0;
            agent.Move(m_TravelDirection);
            if (!agent.GetSystem<EnemyAiSystem>().IsDecisionDue(agent.AiData, agent.Config.AttackInterval))
            {
                return;
            }

            // 普攻不受旋风冷却限制；旋风只在未抽中普攻且自身冷却结束时触发。
            if (Chance(agent, agent.Config.AttackDesire))
            {
                if (agent.RequestAttack(0))
                {
                    m_TravelDirection = offset.X < 0f ? -1 : 1;
                    m_RetreatUntil = now + 3f;
                }
            }
            else if (agent.AiData.Cooldowns[1] <= 0f)
            {
                agent.RequestAttack(1);
            }
        }
    }
}
