using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI;

namespace GameLogic.Entity.Monsters.AI.States
{
	/// <summary>
	/// 水平走向目标（旧 have_target）；普攻判定盒**水平**吃进目标即转 Attack——不看高度：目标在头顶也走到下面
	/// （高度由 Attack 槽把关）。标准用法：Chase 槽。
	/// </summary>
	public class WalkToTargetState : EngageState
	{
		protected override void Engage(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, int dir, float elapseSeconds)
		{
			if (agent.Attacks.BasicGapX(agent.TargetBox, dir) <= -MonsterAttackBook.ReachMargin)
			{
				ChangeRole(fsm, MonsterAiRole.Attack);
			}
			else
			{
				agent.Move(dir);
			}
		}
	}
}
