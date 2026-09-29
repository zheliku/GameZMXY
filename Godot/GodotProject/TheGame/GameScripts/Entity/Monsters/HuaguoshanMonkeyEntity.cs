namespace GameLogic.Entity.Monsters
{
	/// <summary>
	/// 花果山猴子（旧 Monster_1）。数值全部在 MonsterConfig（Id=1），招式在 AttackConfig（OwnerId=HuaguoshanMonkey）。
	/// M4 只作沙包；M5 在此挂 GF.Fsm AI（Patrol/Chase/Attack/CcLocked/Death）。
	/// </summary>
	public partial class HuaguoshanMonkeyEntity : MonsterEntity
	{
	}
}
