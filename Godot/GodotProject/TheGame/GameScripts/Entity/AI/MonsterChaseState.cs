using GameFramework.Fsm;

namespace GameLogic.Entity.AI
{
	/// <summary>
	/// 追击：朝目标水平移动（旧 have_target）；途中技能就绪即放（远程/突进技能）；
	/// 进入站定距离转 Attack；目标失效回巡逻。出招中不做移动决策。
	/// </summary>
	public class MonsterChaseState : MonsterAiState
	{
		/// <inheritdoc />
		public override MonsterAiRole Role => MonsterAiRole.Chase;

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

			if (MonsterAiRules.InAttackRange(agent.TargetDeltaX, agent.Params.AttackRange))
			{
				ChangeRole(fsm, MonsterAiRole.Attack);
				return;
			}

			if (MonsterAiRules.TryCastSkill(agent))
			{
				return;
			}

			agent.Move(MonsterAiRules.Sign(agent.TargetDeltaX));
		}
	}
}
