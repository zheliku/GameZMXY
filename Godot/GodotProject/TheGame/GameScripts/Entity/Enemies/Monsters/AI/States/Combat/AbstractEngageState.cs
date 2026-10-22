using GameLogic.Architecture;
using GameLogic.Utility;
using System;
using GameFramework.Fsm;

namespace GameLogic.Enemies.AI
{
    /// <summary>
    /// 交战状态的共同规则：
    ///  * 出招中（含已请求、收招硬直）不做任何决策——不移动、不转身、不离开本状态；
    ///  * 目标失效/丢失 → Wander；
    ///  * 否则交给 <see cref="Engage"/>，并给出"面向目标"的方向 dir。
    /// </summary>
    public abstract class AbstractEngageState : AbstractMonsterAiState
    {
        private float m_ReactionLeft; // 每次进入交战状态的优先招反应时间，避免突进绕过起手等待。

        /// <summary>保留移动意图，并按配置安排优先招的首次反应时间。</summary>
        /// <param name="agent">本状态的怪物宿主。</param>
        protected override void Enter(IEnemyAiAgent agent)
        {
            // 状态进入只初始化计时；由具体决策确认停步，避免切换当帧先清零、下一帧又追击。
            m_ReactionLeft = (agent.Config.AttackFirstDelay.X, agent.Config.AttackFirstDelay.Y).Sample(agent.NextRandom());
        }

        /// <summary>处理忙碌与目标失效，再处理优先招和当前交战行为。</summary>
        protected sealed override void Tick(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, float elapseSeconds)
        {
            if (agent.IsAttacking)
            {
                return;
            }

            if (!agent.TargetBox.HasArea())
            {
                ChangeState(fsm, agent.AiPreset.IdleStateType);
                return;
            }

            int dir = Math.Sign(agent.TargetBox.GetCenter().X);
            int priority = agent.GetSystem<EnemyAiSystem>().GetHighestUsableAttackPriority(agent.AiData, agent.Params, agent.TargetBox, dir);
            m_ReactionLeft -= elapseSeconds;
            if (m_ReactionLeft <= 0f && priority > 0 && agent.RequestAttack(agent.GetSystem<EnemyAiSystem>().SelectAttackByPriority(agent.AiData, agent.Params,
                agent.TargetBox, dir, priority, agent.NextRandom())))
            {
                return;
            }

            Engage(fsm, agent, dir, elapseSeconds);
        }

        /// <summary>有目标且不在出招时的决策。dir = 面向目标的方向（-1/0/1）。</summary>
        protected abstract void Engage(IFsm<IEnemyAiAgent> fsm, IEnemyAiAgent agent, int dir, float elapseSeconds);
    }
}
