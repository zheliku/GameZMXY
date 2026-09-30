using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI;

namespace GameLogic.Entity.Monsters.AI.States
{
	/// <summary>
	/// 追击：朝目标水平移动（旧 have_target）；途中技能就绪且够得着即放（远程/突进技能）；
	/// 普攻判定盒**水平上**够得着目标（重叠 ≥ ReachMargin）即转 Attack——不看高度：目标在头顶时
	/// 也站到正下方等它落地（由 Attack 状态负责"高度够不着不出招"）。目标失效回巡逻。出招中不做移动决策。
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

			int dir = MonsterAiRules.Sign(agent.TargetDeltaX);
			if (agent.Skills.BasicHorizontalGap(agent.TargetBox, dir) <= -MonsterAiRules.ReachMargin)
			{
				ChangeRole(fsm, MonsterAiRole.Attack);
				return;
			}

			if (MonsterAiRules.TryCastSkill(agent))
			{
				return;
			}

			agent.Move(dir);
		}
	}
}
