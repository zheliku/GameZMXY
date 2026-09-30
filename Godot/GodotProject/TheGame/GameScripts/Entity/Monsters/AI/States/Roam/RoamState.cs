using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI;

namespace GameLogic.Entity.Monsters.AI.States
{
	/// <summary>
	/// 无目标类行为的基类（适合放进 Idle / Patrol 槽）：一旦有目标立刻转 Chase；否则交给 <see cref="Roam"/>。
	/// 新的游荡行为（原地转圈、沿平台来回、悬浮漂移……）从这里派生，只写"怎么游荡"。
	/// </summary>
	public abstract class RoamState : MonsterAiState
	{
		protected sealed override void Tick(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds)
		{
			if (!agent.TargetBox.IsEmpty)
			{
				ChangeRole(fsm, MonsterAiRole.Chase);
				return;
			}

			Roam(fsm, agent, elapseSeconds);
		}

		/// <summary>无目标时的决策。</summary>
		protected abstract void Roam(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds);
	}
}
