using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI;

namespace GameLogic.Entity.Monsters.AI.States
{
	/// <summary>
	/// 无目标游荡状态的共同规则：一旦有目标立刻转追击；否则交给 <see cref="Roam"/>。
	/// </summary>
	public abstract class RoamState : MonsterAiState
	{
		protected sealed override void Tick(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds)
		{
			if (!agent.TargetBox.IsEmpty)
			{
				ChangeState<WalkToTargetState>(fsm);
				return;
			}

			Roam(fsm, agent, elapseSeconds);
		}

		/// <summary>无目标时的决策。</summary>
		protected abstract void Roam(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds);
	}
}
