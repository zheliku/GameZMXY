using System;
using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI;

namespace GameLogic.Entity.Monsters.AI.States
{
	/// <summary>
	/// 左右随机游荡（旧 normal_state）。每 PatrolInterval 决策一次：PatrolIdleChance 概率停下（转 Idle），否则左右各半；
	/// 离出生点超出 PatrolRadius 时立刻往回走、期间不停留——丢失目标回到本槽时也就自然走回巡逻范围。标准用法：Patrol 槽。
	/// </summary>
	public class WanderState : RoamState
	{
		/// <summary>距下一次决策的剩余秒数</summary>
		private float m_DecideTimer;

		/// <summary>当前行走方向（-1/1）</summary>
		private int m_Dir;

		protected override void Enter(IMonsterAiAgent agent)
		{
			// 进入时只选方向，不掷停留（避免进入即切走）
			m_DecideTimer = agent.Params.PatrolInterval;
			m_Dir = Leash(agent) is var leash && leash != 0 ? leash : RandomDir(agent);
			agent.Move(m_Dir);
		}

		protected override void Roam(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds)
		{
			m_DecideTimer -= elapseSeconds;
			int leash = Leash(agent);
			if (leash != 0)
			{
				m_Dir = leash;
			}
			else if (m_DecideTimer <= 0f)
			{
				m_DecideTimer = agent.Params.PatrolInterval;
				if (Chance(agent, agent.Params.PatrolIdleChance))
				{
					ChangeRole(fsm, MonsterAiRole.Idle);
					return;
				}

				m_Dir = RandomDir(agent);
			}

			agent.Move(m_Dir);
		}

		/// <summary>折返方向：离出生点超出巡逻半径时指向出生点，否则 0（半径 ≤0 = 不限）。</summary>
		private static int Leash(IMonsterAiAgent agent)
		{
			float home = agent.HomeDeltaX;
			float radius = agent.Params.PatrolRadius;
			return radius > 0f && Math.Abs(home) > radius ? -Math.Sign(home) : 0;
		}

		private static int RandomDir(IMonsterAiAgent agent)
		{
			return agent.NextRandom() < 0.5f ? -1 : 1;
		}
	}
}
