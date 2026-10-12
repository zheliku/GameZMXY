using GameLogic.Monsters.AI;
using GameFramework.Fsm;

namespace GameLogic.Monsters.AI
{
	/// <summary>
	/// 水平走向目标（旧 have_target）；攻击判定盒**水平**吃进目标即转站定出招——不看高度：目标在头顶也走到下面
	/// （高度由站定出招状态把关）。
	/// </summary>
	public class WalkToTargetState : EngageState
	{
		/// <summary>未进入普通攻击水平范围时追向目标。</summary>
		protected override void Engage(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, int dir, float elapseSeconds)
		{
			if (agent.Attacks.BasicGapX(agent.TargetBox, dir) <= -MonsterAttackBook.ReachMargin)
			{
				ChangeState<StandAndStrikeState>(fsm);
			}
			else
			{
				agent.Move(dir);
			}
		}
	}
}
