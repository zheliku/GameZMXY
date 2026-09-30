using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI;

namespace GameLogic.Entity.Monsters.AI.States
{
	/// <summary>
	/// 巡逻：左右随机走动（旧 normal_state）。每 PatrolInterval 重新决策一次：
	/// PatrolIdleChance 概率停下（→ Idle），否则左右各半；离出生点超出 PatrolRadius 时强制折返。
	/// </summary>
	public class MonsterPatrolState : MonsterAiState
	{
		/// <inheritdoc />
		public override MonsterAiRole Role => MonsterAiRole.Patrol;

		/// <summary>距下一次决策的剩余秒数</summary>
		private float m_DecideTimer;

		/// <summary>当前行走方向（-1/1）</summary>
		private int m_Dir;

		protected internal override void OnEnter(IFsm<IMonsterAiAgent> fsm)
		{
			base.OnEnter(fsm);
			IMonsterAiAgent agent = fsm.Owner;

			// 进入时只选方向，不掷停留（停留在后续决策点掷，避免 OnEnter 里嵌套切状态）
			m_Dir = PickDirection(agent);
			m_DecideTimer = agent.Params.PatrolInterval;
			agent.Move(m_Dir);
		}

		protected override void Tick(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds)
		{
			if (agent.HasTarget)
			{
				ChangeRole(fsm, MonsterAiRole.Chase);
				return;
			}

			m_DecideTimer -= elapseSeconds;
			int leash = MonsterAiRules.LeashDirection(agent.HomeDeltaX, agent.Params.PatrolRadius);
			if (m_DecideTimer <= 0f)
			{
				m_DecideTimer = agent.Params.PatrolInterval;
				if (leash == 0 && MonsterAiRules.Chance(agent.NextRandom(), agent.Params.PatrolIdleChance))
				{
					ChangeRole(fsm, MonsterAiRole.Idle);
					return;
				}

				m_Dir = PickDirection(agent);
			}
			else if (leash != 0)
			{
				// 两次决策之间走出半径：立刻折返
				m_Dir = leash;
			}

			agent.Move(m_Dir);
		}

		/// <summary>决策方向：超出半径则折返，否则左右各半。</summary>
		private static int PickDirection(IMonsterAiAgent agent)
		{
			int leash = MonsterAiRules.LeashDirection(agent.HomeDeltaX, agent.Params.PatrolRadius);
			return leash != 0 ? leash : (agent.NextRandom() < 0.5f ? -1 : 1);
		}
	}
}
