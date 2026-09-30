using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI;

namespace GameLogic.Entity.Monsters.AI.States
{
	/// <summary>原地停一个 PatrolInterval（旧 normal_state 的 stop_move 分支），到点转 Patrol。标准用法：Idle 槽。</summary>
	public class PauseState : RoamState
	{
		/// <summary>剩余停留秒数</summary>
		private float m_Timer;

		protected override void Enter(IMonsterAiAgent agent)
		{
			base.Enter(agent);
			m_Timer = agent.Params.PatrolInterval;
		}

		protected override void Roam(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds)
		{
			m_Timer -= elapseSeconds;
			if (m_Timer <= 0f)
			{
				ChangeRole(fsm, MonsterAiRole.Patrol);
			}
		}
	}
}
