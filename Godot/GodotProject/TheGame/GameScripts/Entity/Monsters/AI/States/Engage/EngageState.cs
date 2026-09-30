using System;
using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI;

namespace GameLogic.Entity.Monsters.AI.States
{
	/// <summary>
	/// 交战类行为的基类（适合 Chase / Attack / Hold 槽）：
	///  * 出招中（含已请求、收招硬直）不做任何决策——不移动、不转身、不离开本状态；
	///  * 目标失效/丢失 → Patrol；
	///  * 否则先试优先招（就绪且够得着就放，追击/守候途中也放），再交给 <see cref="Engage"/>，并给出"面向目标"的方向 dir。
	/// 新的交战行为（冲锋、拉开射击、悬浮接近……）从这里派生，只写"怎么接近 / 怎么出手普攻"。
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
				ChangeRole(fsm, MonsterAiRole.Patrol);
				return;
			}

			int dir = Math.Sign(agent.TargetBox.CenterX);
			int priority = agent.Attacks.SelectPriority(agent.TargetBox, dir, agent.NextRandom());
			if (priority >= 0 && agent.RequestAttack(priority))
			{
				return;
			}

			Engage(fsm, agent, dir, elapseSeconds);
		}

		/// <summary>有目标、不在出招中、没有优先招可放时的决策。dir = 面向目标的方向（-1/0/1）。</summary>
		protected abstract void Engage(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, int dir, float elapseSeconds);
	}
}
