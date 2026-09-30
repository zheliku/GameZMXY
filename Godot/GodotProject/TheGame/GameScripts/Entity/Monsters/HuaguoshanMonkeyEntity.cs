namespace GameLogic.Entity.Monsters
{
	/// <summary>
	/// 花果山猴子（旧 Monster_1）。数值全部在 MonsterConfig（Id=1），招式在 AttackConfig（OwnerId=HuaguoshanMonkey）。
	/// 标准小怪：完全使用 MonsterEntity 的默认 AI（巡逻/追击/攻击/受控/死亡）与数据驱动的普攻池，本类无需覆写——
	/// 这正是"扩展分层"第 1 层（纯数据）的样例；需要特殊行为的怪才覆写 ConfigureAi 等钩子（见 MonsterEntity）。
	/// </summary>
	public partial class HuaguoshanMonkeyEntity : MonsterEntity
	{
	}
}
