using GameLogic.Monsters.AI;
using System;
using GameFramework.Fsm;

namespace GameLogic.Monsters.AI
{
	/// <summary>
	/// 交战状态的共同规则：
	///  * 出招中（含已请求、收招硬直）不做任何决策——不移动、不转身、不离开本状态；
	///  * 目标失效/丢失 → Wander；
	///  * 否则交给 <see cref="Engage"/>，并给出"面向目标"的方向 dir。
	/// </summary>
	public abstract class EngageState : MonsterAiState
	{
		private float m_ReactionLeft; // 每次进入交战状态的优先招反应时间，避免突进绕过起手等待。

		/// <summary>停步并按配置安排优先招的首次反应时间。</summary>
		/// <param name="agent">本状态的怪物宿主。</param>
		protected override void Enter(IMonsterAiAgent agent)
		{
			base.Enter(agent);
			m_ReactionLeft = MonsterAttackSpec.Roll(agent.Params.AttackFirstDelay, agent.NextRandom());
		}

		/// <summary>处理忙碌与目标失效，再处理优先招和当前交战行为。</summary>
		protected sealed override void Tick(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds)
		{
			if (agent.IsAttacking)
			{
				return;
			}

			if (agent.TargetBox.IsEmpty)
			{
				ChangeState<WanderState>(fsm);
				return;
			}

			int dir = Math.Sign(agent.TargetBox.CenterX);
			int priority = agent.Attacks.HighestPriority(agent.TargetBox, dir);
			m_ReactionLeft -= elapseSeconds;
			if (m_ReactionLeft <= 0f && priority > 0 && agent.RequestAttack(agent.Attacks.SelectPriority(
				agent.TargetBox, dir, priority, agent.NextRandom())))
			{
				return;
			}

			Engage(fsm, agent, dir, elapseSeconds);
		}

		/// <summary>有目标且不在出招时的决策。dir = 面向目标的方向（-1/0/1）。</summary>
		protected abstract void Engage(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, int dir, float elapseSeconds);
	}
}
