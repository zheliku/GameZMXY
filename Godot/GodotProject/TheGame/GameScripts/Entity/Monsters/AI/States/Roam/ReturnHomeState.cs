using System;
using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI;

namespace GameLogic.Entity.Monsters.AI.States
{
	/// <summary>
	/// 回岗：走回出生点，到了就原地站岗（面朝原来的方向不再游荡）。丢失目标后回到本槽即"回去守着"。标准用法：Patrol 槽（守卫类）。
	/// </summary>
	public class ReturnHomeState : RoamState
	{
		/// <summary>到岗判定距离 px（工程常数：移动一帧的步长量级，免得在出生点附近左右抖动）</summary>
		private static readonly float ArriveDistance = 4f;

		protected override void Roam(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds)
		{
			float home = agent.HomeDeltaX;
			agent.Move(Math.Abs(home) > ArriveDistance ? -Math.Sign(home) : 0);
		}
	}
}
