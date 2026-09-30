using System;
using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI;

namespace GameLogic.Entity.Monsters.AI.States
{
	/// <summary>
	/// 在目标下方守候：普攻重新够得着（目标落回判定高度）→ Attack，水平走远 → Chase；
	/// 否则以目标 x 为中心、<see cref="PaceRange"/> 为半幅左右往返（到一端掉头；半幅 0 = 原地面向目标等）。
	/// 标准用法：Hold 槽。默认半幅取 MonsterConfig.PaceRange；守卫类用 <see cref="WaitBelowTargetState"/>（固定 0）。
	/// </summary>
	public class PaceBelowTargetState : EngageState
	{
		/// <summary>当前往返方向（-1/1；0 = 未开始）</summary>
		private int m_Dir;

		/// <summary>踱步半幅 px</summary>
		protected virtual float PaceRange(IMonsterAiAgent agent) => agent.Params.PaceRange;

		protected override void Enter(IMonsterAiAgent agent)
		{
			base.Enter(agent);
			m_Dir = 0;
		}

		protected override void Engage(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, int dir, float elapseSeconds)
		{
			if (agent.Attacks.BasicInReach(agent.TargetBox, dir))
			{
				ChangeRole(fsm, MonsterAiRole.Attack);
				return;
			}

			// 目标在平台上走远（普攻水平间隙超出踱步半幅 + 滞回）：交给接近
			float range = PaceRange(agent);
			if (agent.Attacks.BasicGapX(agent.TargetBox, dir) > range + agent.Params.AttackRangeSlack)
			{
				ChangeRole(fsm, MonsterAiRole.Chase);
				return;
			}

			if (range <= 0f)
			{
				agent.Move(0);
				agent.Face(dir);
				return;
			}

			// 走到半幅外（且还在往外走）就掉头
			float fromTarget = -agent.TargetBox.CenterX;   // 自己相对目标的水平偏移
			if (m_Dir == 0 || (Math.Abs(fromTarget) >= range && Math.Sign(fromTarget) == m_Dir))
			{
				m_Dir = fromTarget > 0f ? -1 : 1;
			}

			agent.Move(m_Dir);
		}
	}
}
