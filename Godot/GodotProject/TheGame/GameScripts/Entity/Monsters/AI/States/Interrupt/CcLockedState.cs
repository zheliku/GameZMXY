using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI;

namespace GameLogic.Entity.Monsters.AI.States
{
	/// <summary>
	/// 受控期间不做决策（击退速度由实体物理保持）；解除后再僵直 CalmTime 秒（旧 behit_calmtime），
	/// 然后有目标 → WalkToTarget，否则 → Wander。被打会锁定攻击者（MonsterEntity.OnHurt），所以挨打后通常直接反击。
	/// </summary>
	public class CcLockedState : MonsterAiState
	{
		/// <summary>受控解除后已僵直的秒数</summary>
		private float m_CalmElapsed;

		protected override void Enter(IMonsterAiAgent agent)
		{
			base.Enter(agent);
			m_CalmElapsed = 0f;
		}

		protected override void Tick(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds)
		{
			if (agent.IsCcLocked)
			{
				m_CalmElapsed = 0f;
				return;
			}

			m_CalmElapsed += elapseSeconds;
			if (m_CalmElapsed >= agent.Params.CalmTime)
			{
				if (agent.TargetBox.IsEmpty)
				{
					ChangeState<WanderState>(fsm);
				}
				else
				{
					ChangeState<WalkToTargetState>(fsm);
				}
			}
		}
	}
}
