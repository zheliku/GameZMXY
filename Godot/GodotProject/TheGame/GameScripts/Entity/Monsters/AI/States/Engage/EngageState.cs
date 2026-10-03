using System;
using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI;

namespace GameLogic.Entity.Monsters.AI.States
{
	/// <summary>
	/// 交战状态的共同规则：
	///  * 出招中（含已请求、收招硬直）不做任何决策——不移动、不转身、不离开本状态；
	///  * 目标失效/丢失 → Wander；
	///  * 否则交给 <see cref="Engage"/>，并给出"面向目标"的方向 dir。
	/// </summary>
	public abstract class EngageState : MonsterAiState
	{
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
			if (priority > 0 && agent.RequestAttack(agent.Attacks.SelectPriority(
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
