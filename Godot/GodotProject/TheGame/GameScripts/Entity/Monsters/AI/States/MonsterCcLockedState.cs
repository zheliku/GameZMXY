using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI;

namespace GameLogic.Entity.Monsters.AI.States
{
	/// <summary>
	/// 受控：受控期间不做决策（击退速度由实体物理保持）。解除后再僵直 CalmTime 秒
	/// （旧 behit_calmtime），然后有目标 → 追击，否则 → 巡逻。
	/// 被打会锁定攻击者为目标（MonsterEntity.OnHurt），所以挨打后通常直接反击。
	/// </summary>
	public class MonsterCcLockedState : MonsterAiState
	{
		/// <inheritdoc />
		public override MonsterAiRole Role => MonsterAiRole.CcLocked;

		/// <summary>受控解除后已僵直的秒数</summary>
		private float m_CalmElapsed;

		protected internal override void OnEnter(IFsm<IMonsterAiAgent> fsm)
		{
			base.OnEnter(fsm);
			m_CalmElapsed = 0f;
			fsm.Owner.Move(0);
		}

		protected override void Tick(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds)
		{
			agent.Move(0);
			if (agent.IsCcLocked)
			{
				m_CalmElapsed = 0f;
				return;
			}

			m_CalmElapsed += elapseSeconds;
			if (m_CalmElapsed < agent.Params.CalmTime)
			{
				return;
			}

			ChangeRole(fsm, agent.HasTarget ? MonsterAiRole.Chase : MonsterAiRole.Patrol);
		}
	}
}
