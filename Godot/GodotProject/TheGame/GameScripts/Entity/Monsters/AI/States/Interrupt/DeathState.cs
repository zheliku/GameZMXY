using GameLogic.Monsters.AI;
using GameLogic.Monsters;
using GameFramework.Fsm;

namespace GameLogic.Monsters.AI
{
	/// <summary>
	/// 终态：停止一切意图。死亡动画与回收由 MonsterEntity 负责，实体隐藏时状态机随之销毁。
	/// </summary>
	public class DeathState : MonsterAiState
	{
		/// <summary>死亡 AI 状态不再产生意图。</summary>
		protected override void Tick(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds)
		{
		}
	}
}
