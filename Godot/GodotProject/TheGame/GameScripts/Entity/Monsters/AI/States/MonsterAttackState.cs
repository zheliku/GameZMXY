using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI;

namespace GameLogic.Entity.Monsters.AI.States
{
	/// <summary>
	/// 站定攻击：面向目标；技能就绪且够得着优先放技能；否则每 AttackInterval 按 AttackDesire 掷一次，
	/// 命中则从普攻池按权重抽招（旧 Monster_Intelligence 近身分支：stop_move + count%60 掷 attackDesire）。
	///
	/// "够得着"按攻击动画判定盒推导（见 <see cref="MonsterSkillBook"/>）：
	///  * 水平间隙 &gt; AttackRangeSlack → 回追击；
	///  * 水平间隙在 (−ReachMargin, AttackRangeSlack] → 留在本状态、小步贴近（滞回：被击退几像素不来回切状态）；
	///  * 水平够得着但**高度够不着**（目标跳起/在平台上）→ 站定面向目标、照常计时掷骰，但抽不到招 = 不出招，
	///    目标落回判定盒高度后自然出手。
	/// 出招中（含已请求未提交、收招硬直）不做任何决策、不离开本状态——硬直结束后再判距离/目标/转向。
	/// 进入本状态后立即掷第一次（贴身即有出手机会）。
	/// </summary>
	public class MonsterAttackState : MonsterAiState
	{
		/// <inheritdoc />
		public override MonsterAiRole Role => MonsterAiRole.Attack;

		/// <summary>距下一次普攻判定的剩余秒数</summary>
		private float m_RollTimer;

		protected internal override void OnEnter(IFsm<IMonsterAiAgent> fsm)
		{
			base.OnEnter(fsm);
			m_RollTimer = 0f;
			fsm.Owner.Move(0);
		}

		protected override void Tick(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds)
		{
			if (agent.IsAttacking)
			{
				return;
			}

			if (!agent.HasTarget)
			{
				ChangeRole(fsm, MonsterAiRole.Patrol);
				return;
			}

			int dir = MonsterAiRules.Sign(agent.TargetDeltaX);
			float gap = agent.Skills.BasicHorizontalGap(agent.TargetBox, dir);
			if (gap > agent.Params.AttackRangeSlack)
			{
				ChangeRole(fsm, MonsterAiRole.Chase);
				return;
			}

			agent.Face(dir);

			if (MonsterAiRules.TryCastSkill(agent))
			{
				return;
			}

			if (gap > -MonsterAiRules.ReachMargin)
			{
				// 滞回区内：不退回追击，小步贴近到判定盒吃进目标
				agent.Move(dir);
				return;
			}

			agent.Move(0);

			m_RollTimer -= elapseSeconds;
			if (m_RollTimer > 0f)
			{
				return;
			}

			m_RollTimer = agent.Params.AttackInterval;
			if (!MonsterAiRules.Chance(agent.NextRandom(), agent.Params.AttackDesire))
			{
				return;
			}

			// 高度够不着时抽不到招（SelectBasic 返回 -1）：本次判定作废，下个间隔再掷
			int index = agent.Skills.SelectBasic(agent.TargetBox, dir, agent.NextRandom());
			if (index >= 0)
			{
				agent.RequestAttack(index);
			}
		}
	}
}
