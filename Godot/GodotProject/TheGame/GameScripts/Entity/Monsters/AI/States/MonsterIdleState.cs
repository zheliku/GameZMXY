using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI;

namespace GameLogic.Entity.Monsters.AI.States
{
	/// <summary>
	/// 待机：巡逻途中原地停留一个 PatrolInterval（旧 normal_state 的 stop_move 分支），到点回巡逻；有目标立刻追击。
	/// </summary>
	public class MonsterIdleState : MonsterAiState
	{
		/// <inheritdoc />
		public override MonsterAiRole Role => MonsterAiRole.Idle;

		/// <summary>剩余停留秒数</summary>
		private float m_Timer;

		protected internal override void OnEnter(IFsm<IMonsterAiAgent> fsm)
		{
			base.OnEnter(fsm);
			m_Timer = fsm.Owner.Params.PatrolInterval;
			fsm.Owner.Move(0);
		}

		protected override void Tick(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds)
		{
			if (agent.HasTarget)
			{
				ChangeRole(fsm, MonsterAiRole.Chase);
				return;
			}

			agent.Move(0);
			m_Timer -= elapseSeconds;
			if (m_Timer <= 0f)
			{
				ChangeRole(fsm, MonsterAiRole.Patrol);
			}
		}
	}
}
