using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI;

namespace GameLogic.Entity.Monsters.AI.States
{
	/// <summary>
	/// 站定出招：面向目标，每 AttackInterval 按 AttackDesire 掷一次，命中则按权重选择攻击（旧 Monster_Intelligence 近身分支）。
	/// 进入后立即掷第一次。
	///  * 水平间隙 &gt; AttackRangeSlack → WalkToTarget；滞回区内小步贴近（被击退几像素不来回切状态）；
	///  * 水平到位但高度够不着（目标在平台/头顶）→ PaceBelowTarget。
	/// </summary>
	public class StandAndStrikeState : EngageState
	{
		private float m_RollTimer; // 距下一次普攻意愿判定的剩余时间（秒）。

		/// <summary>进入站定攻击状态时立即准备进行攻击判定。</summary>
		protected override void Enter(IMonsterAiAgent agent)
		{
			base.Enter(agent);
			m_RollTimer = 0f;
		}

		/// <summary>处理范围滞回、高度踱步和普攻请求。</summary>
		protected override void Engage(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, int dir, float elapseSeconds)
		{
			float gap = agent.Attacks.BasicGapX(agent.TargetBox, dir);
			if (gap > agent.Params.AttackRangeSlack)
			{
				ChangeState<WalkToTargetState>(fsm);
				return;
			}

			agent.Face(dir);
			if (gap > -MonsterAttackBook.ReachMargin)
			{
				agent.Move(dir);   // 滞回区内：不退回接近，小步贴近
				return;
			}

			if (!agent.Attacks.BasicInReach(agent.TargetBox, dir))
			{
				ChangeState<PaceBelowTargetState>(fsm);
				return;
			}

			agent.Move(0);
			m_RollTimer -= elapseSeconds;
			if (m_RollTimer > 0f)
			{
				return;
			}

			m_RollTimer = agent.Params.AttackInterval;
			if (Chance(agent, agent.Params.AttackDesire))
			{
				agent.RequestAttack(agent.Attacks.SelectBasic(agent.TargetBox, dir, agent.NextRandom));
			}
		}
	}
}
