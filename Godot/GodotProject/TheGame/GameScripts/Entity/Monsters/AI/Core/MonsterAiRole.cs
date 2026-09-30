namespace GameLogic.Entity.Monsters.AI
{
	/// <summary>
	/// AI 的**角色槽**：状态机的骨架（处在哪个阶段），与"具体怎么做"无关。行为由大脑原型
	/// <c>MonsterAiStateSet.Bind(role, 行为)</c> 放进来；行为之间按角色跳转，所以换掉某个槽的行为，整张图其余部分不用改。
	/// </summary>
	public enum MonsterAiRole
	{
		/// <summary>无目标：游荡间歇</summary>
		Idle = 0,

		/// <summary>无目标：游荡（丢失目标后也回到这里，离家太远会先走回巡逻范围）</summary>
		Patrol = 1,

		/// <summary>有目标、还够不着：接近（途中可放技能）</summary>
		Chase = 2,

		/// <summary>有目标、够得着：出招（技能优先，其次普攻池）</summary>
		Attack = 3,

		/// <summary>有目标、水平到位但**高度够不着**（目标在平台/头顶）：守候，等目标下来</summary>
		Hold = 4,

		/// <summary>受控（受击硬直；后续冰冻/眩晕等控制 Buff）——由基类打断进入</summary>
		CcLocked = 5,

		/// <summary>死亡（终态）——由基类打断进入</summary>
		Death = 6,
	}
}
