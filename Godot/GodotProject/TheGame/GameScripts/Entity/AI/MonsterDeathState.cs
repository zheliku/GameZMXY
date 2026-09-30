using GameFramework.Fsm;

namespace GameLogic.Entity.AI
{
	/// <summary>
	/// 死亡：终态，停止一切意图。死亡动画与回收由 MonsterEntity 负责，实体隐藏时状态机随之销毁。
	/// 子类可覆写 OnEnter 做死亡演出决策（如 Boss 死亡召唤），但不得离开本状态。
	/// </summary>
	public class MonsterDeathState : MonsterAiState
	{
		/// <inheritdoc />
		public override MonsterAiRole Role => MonsterAiRole.Death;

		protected internal override void OnEnter(IFsm<IMonsterAiAgent> fsm)
		{
			base.OnEnter(fsm);
			fsm.Owner.Move(0);
		}

		protected override void Tick(IFsm<IMonsterAiAgent> fsm, IMonsterAiAgent agent, float elapseSeconds)
		{
		}
	}
}
