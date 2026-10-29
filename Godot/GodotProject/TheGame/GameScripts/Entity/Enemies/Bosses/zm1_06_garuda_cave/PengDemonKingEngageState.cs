using System;
using GameLogic.Enemies.AI;

namespace GameLogic.Enemies
{
    /// <summary>
    /// 彭魔王按地面/空中、原点距离和原版一秒节拍选择近身刺击、生火或火鸟冲刺。
    /// </summary>
    internal sealed class PengDemonKingEngageState : AbstractBossAiState
    {
        private long m_TurnSlot = -1; // 两秒决策格只消费一次，避免在整个偶数秒内每帧折返。

        /// <inheritdoc />
        protected override float HeightRange
        {
            get
            {
                return 800f;
            }
        }

        /// <inheritdoc />
        protected override void Engage(IEnemyAiAgent agent, bool decision, float delta)
        {
            var actor = (PengDemonKingEntity)agent;
            float distance = agent.TargetOffset.Length();
            if (!actor.Flying && !actor.OnFloor)
            {
                return;
            }

            if (distance > 400f)
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
                        if (Math.Abs(agent.TargetOffset.X) > 150f)
                        {
                            agent.Move(Math.Sign(agent.TargetOffset.X));
                        }
                    }
                }
                return;
            }
            agent.Move(0);
            if (distance <= 150f)
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
