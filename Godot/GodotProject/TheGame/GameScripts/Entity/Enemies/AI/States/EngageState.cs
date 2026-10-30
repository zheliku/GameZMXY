using System;
using GameLogic.Architecture;
using GameFramework.Fsm;

namespace GameLogic.Enemies.AI
{
    /// <summary>
    /// 普通交战：目标高度超限则巡逻，范围外追击，范围内在决策节拍提交攻击；范围只读场景。
    /// </summary>
    public class EngageState : AbstractEnemyAiState
    {
        /// <inheritdoc />
        protected override void Tick(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds)
        {
            if (agent.IsAttacking)
            {
                return;
            }

            if (!agent.HasTarget)
            {
                ChangeState(fsm, agent.AiPreset.IdleStateType);
                return;
            }
            var bounds = agent.EngageBounds;
            var target = agent.TargetOffset;
            if (target.Y < bounds.Position.Y || target.Y > bounds.End.Y)
            {
                agent.GetSystem<EnemyAiSystem>().Patrol(agent, elapseSeconds);
            }
            else if (!agent.IsInEngageRange)
            {
                agent.Move(Math.Sign(target.X));
            }
            else if (agent.GetSystem<EnemyAiSystem>().IsDecisionDue(agent.AiData, agent.Config.AttackInterval))
            {
                Attack(agent);
            }
        }

        /// <summary>
        /// 普通物种按欲望发起第一招；多招物种在自己的状态中决定优先顺序。
        /// </summary>
        /// <param name="agent">当前实例的请求与数值。</param>
        protected virtual void Attack(IEnemyAiAgent agent)
        {
            if (Chance(agent, agent.Config.AttackDesire))
            {
                agent.RequestAttack(0);
            }
            else
            {
                agent.Move(0);
            }
        }
    }
}
