using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI;

namespace GameLogic.Entity.Monsters.AI.States
{
	/// <summary>按巡逻停留概率原地暂停，到点恢复游荡。</summary>
	public class PauseState : RoamState
	{
		/// <summary>剩余停留秒数</summary>
		private float m_Timer;

		/// <summary>重置暂停时长为巡逻间隔。</summary>
		protected override void Enter(IMonsterAiAgent agent)
		{
			base.Enter(agent);
			m_Timer = agent.Params.PatrolInterval;
		}

		/// <summary>倒计时结束后恢复游荡。</summary>
		protected override void Roam(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds)
		{
			m_Timer -= elapseSeconds;
			if (m_Timer <= 0f)
			{
				ChangeState<WanderState>(fsm);
			}
		}
	}
}
