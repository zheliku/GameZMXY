using GameLogic.Entity.Monsters.AI;

namespace GameLogic.Entity.Monsters.AI.States
{
	/// <summary>
	/// 在目标下方原地面向目标等（不踱步）：守候规则同 <see cref="PaceBelowTargetState"/>，半幅固定为 0。
	/// 适合守卫、重装怪。标准用法：Hold 槽。
	/// </summary>
	public sealed class WaitBelowTargetState : PaceBelowTargetState
	{
		protected override float PaceRange(IMonsterAiAgent agent) => 0f;
	}
}
