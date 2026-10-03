using GameFramework.Fsm;
using GameLogic.Entity.Monsters.AI;

namespace GameLogic.Entity.Monsters.AI.States
{
	/// <summary>
	/// 终态：停止一切意图。死亡动画与回收由 MonsterEntity 负责，实体隐藏时状态机随之销毁。
	/// </summary>
	public class DeathState : MonsterAiState
	{
		protected override void Tick(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds)
		{
		}
	}
}
