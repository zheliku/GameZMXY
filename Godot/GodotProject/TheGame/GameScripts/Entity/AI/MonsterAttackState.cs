using GameFramework.Fsm;

namespace GameLogic.Entity.AI
{
	/// <summary>
	/// 站定攻击：面向目标；技能就绪优先放技能；否则每 AttackInterval 按 AttackDesire 掷一次，
	/// 命中则从普攻池按权重抽招（旧 Monster_Intelligence 近身分支：stop_move + count%60 掷 attackDesire）。
	/// 出招中（含已请求未提交）不做任何决策、不离开本状态——收招后再判距离/目标。
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

			// 滞回：离开站定的门槛比进入宽 AttackRangeSlack
			if (!MonsterAiRules.InAttackRange(agent.TargetDeltaX, agent.Params.AttackRange + agent.Params.AttackRangeSlack))
			{
				ChangeRole(fsm, MonsterAiRole.Chase);
				return;
			}

			agent.Move(0);
			agent.Face(MonsterAiRules.Sign(agent.TargetDeltaX));

			if (MonsterAiRules.TryCastSkill(agent))
			{
				return;
			}

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

			float distance = System.Math.Abs(agent.TargetDeltaX);
			int index = agent.Skills.SelectBasic(distance, agent.NextRandom());
			if (index >= 0)
			{
				agent.RequestAttack(index);
			}
		}
	}
}
