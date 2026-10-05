using System;
using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI;

namespace GameLogic.Entity.Monsters.AI.States
{
	/// <summary>
	/// 在目标下方守候：攻击重新够得着（目标落回判定高度）→ 站定出招，水平走远 → 继续追击；
	/// 否则以目标 x 为中心、<see cref="PaceRange"/> 为半幅左右往返（到一端掉头；半幅 0 = 原地面向目标等）。
	/// 半幅取 MonsterConfig.PaceRange。
	/// </summary>
	public class PaceBelowTargetState : EngageState
	{
		private int m_Dir; // 当前平台下踱步方向；0 表示尚未选向。

		/// <summary>踱步半幅 px</summary>
		protected override void Enter(IMonsterAiAgent agent)
		{
			base.Enter(agent);
			m_Dir = 0;
		}

		/// <summary>目标高度暂不可达时在其下方踱步或转入追击。</summary>
		protected override void Engage(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, int dir, float elapseSeconds)
		{
			if (agent.Attacks.BasicInReach(agent.TargetBox, dir))
			{
				ChangeState<StandAndStrikeState>(fsm);
				return;
			}

			// 目标在平台上走远（普攻水平间隙超出踱步半幅 + 滞回）：交给接近
			float range = agent.Params.PaceRange;
			if (agent.Attacks.BasicGapX(agent.TargetBox, dir) > range + agent.Params.AttackRangeSlack)
			{
				ChangeState<WalkToTargetState>(fsm);
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
