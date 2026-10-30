using System;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 彭魔王按地面/空中、原点距离和原版一秒节拍选择近身刺击、生火或火鸟冲刺。
    /// </summary>
    internal sealed class PengDemonKingEngageState : AbstractEnemyAiState
    {
        private long m_TurnSlot = -1; // 两秒决策格只消费一次，避免在整个偶数秒内每帧折返。

        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float delta)
        {
            if (agent.IsAttacking)
            {
                return;
            }
            if (!agent.HasTarget || Math.Abs(agent.TargetOffset.Y) > ((PengDemonKingEntity)agent).TrackingHeight)
            {
                agent.GetSystem<EnemyAiSystem>().Patrol(agent, delta);
                return;
            }
            Engage(agent, agent.GetSystem<EnemyAiSystem>().IsDecisionDue(agent.AiData, 1));
        }

        /// <summary>
        /// 按圆形距离和飞行状态执行本首领选招，追踪高度由独立原生范围拥有。
        /// </summary>
        /// <param name="agent">当前首领实例。</param>
        /// <param name="decision">是否跨过一秒选招节拍。</param>
        private void Engage(IEnemyAiAgent agent, bool decision)
        {
            var actor = (PengDemonKingEntity)agent;
            float distance = agent.TargetOffset.Length();
            if (!actor.Flying && !actor.OnFloor)
            {
                return;
            }

            if (!agent.IsInEngageRange)
            {
                if (!actor.Flying)
                {
                    agent.Move(Math.Sign(agent.TargetOffset.X));
                }
                else if (decision && agent.NextRandom() < .5f)
                {
                    agent.RequestAttack(0);
                }

                return;
            }
            if (!decision)
            {
                // 飞行时只每两秒修正一次朝向，仍持续朝150px高度运动。
                if (!actor.Flying)
                {
                    agent.Move(Math.Sign(agent.TargetOffset.X));
                }
                else
                {
                    long slot = (long)(agent.AiData.ElapsedSeconds / 2);
                    if (slot != m_TurnSlot)
                    {
                        m_TurnSlot = slot;
                        if (Math.Abs(agent.TargetOffset.X) > actor.MeleeDistance)
                        {
                            agent.Move(Math.Sign(agent.TargetOffset.X));
                        }
                    }
                }
                return;
            }
            agent.Move(0);
            if (distance <= actor.MeleeDistance)
            {
                if (!actor.Flying)
                {
                    agent.RequestAttack(3);
                }

                return;
            }
            if (agent.NextRandom() < .3f)
            {
                if (agent.AiData.Cooldowns[1] <= 0f)
                {
                    agent.RequestAttack(1);
                }
            }
            else
            {
                int fire = actor.Flying ? 0 : 2;
                if (agent.AiData.Cooldowns[fire] <= 0f)
                {
                    agent.RequestAttack(fire);
                }
            }
        }
    }
}
