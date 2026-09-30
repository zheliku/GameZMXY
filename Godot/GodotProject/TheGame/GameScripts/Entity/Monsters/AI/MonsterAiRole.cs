namespace GameLogic.Entity.Monsters.AI
{
	/// <summary>
	/// AI 状态的**角色**（逻辑槽位）：状态之间按角色跳转，不按具体类型跳转——
	/// 所以子类怪物可以把某个角色换成自己的实现（如飞行怪换掉 Chase），整张图其余部分不用改。
	/// 名称即 Entity/AGENTS.md 的怪物状态命名标准。
	/// </summary>
	public enum MonsterAiRole
	{
		/// <summary>原地待机（巡逻间歇）</summary>
		Idle = 0,

		/// <summary>无目标时巡逻</summary>
		Patrol = 1,

		/// <summary>追击目标（途中可放技能）</summary>
		Chase = 2,

		/// <summary>站定攻击（技能优先，其次普攻池）</summary>
		Attack = 3,

		/// <summary>受控（受击硬直；后续冰冻/眩晕等控制 Buff）</summary>
		CcLocked = 4,

		/// <summary>死亡（终态）</summary>
		Death = 5,
	}
}
