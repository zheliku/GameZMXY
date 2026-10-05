using System;
using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI;

namespace GameLogic.Entity.Monsters.AI.States
{
	/// <summary>
	/// 左右随机游荡（旧 normal_state）。每 PatrolInterval 决策一次：PatrolIdleChance 概率停下，否则左右各半；
	/// 离出生点超出 PatrolRadius 时立刻往回走、期间不停留。
	/// </summary>
	public class WanderState : RoamState
	{
		private float m_DecideTimer; // 距下一次巡逻停留/转向决策的剩余时间（秒）。

		private int m_Dir; // 当前游荡方向；-1 向左，1 向右。

		/// <summary>初始化巡逻决策计时，并选择初始方向。</summary>
		protected override void Enter(IMonsterAiAgent agent)
		{
			// 进入时只选方向，不掷停留（避免进入即切走）
			m_DecideTimer = agent.Params.PatrolInterval;
			m_Dir = Leash(agent) is var leash && leash != 0 ? leash : RandomDir(agent);
			agent.Move(m_Dir);
		}

		/// <summary>按巡逻范围与停留概率更新方向或切换暂停状态。</summary>
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
					ChangeState<PauseState>(fsm);
					return;
				}

				m_Dir = RandomDir(agent);
			}

			agent.Move(m_Dir);
		}

		private static int Leash(IMonsterAiAgent agent) // 超出出生点巡逻半径时返回朝家的折返方向。
		{
			float home = agent.HomeDeltaX;
			float radius = agent.Params.PatrolRadius;
			return radius > 0f && Math.Abs(home) > radius ? -Math.Sign(home) : 0;
		}

		private static int RandomDir(IMonsterAiAgent agent) // 使用怪物随机源等概率选取左右方向。
		{
			return agent.NextRandom() < 0.5f ? -1 : 1;
		}
	}
}
