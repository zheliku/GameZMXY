using GameLogic.Entity.Monsters.AI.States;

namespace GameLogic.Entity.Monsters.AI
{
	/// <summary>
	/// 可复用的"大脑原型"——**看 AI 先看这里**。三层结构（同 tModLoader aiStyle / Unity Game Kit / Hollow Knight）：
	/// <code>
	///   身体   MonsterEntity                      血量/物理/受击/出招节奏/感知（所有怪共用）
	///   大脑   MonsterBrains.Xxx()                一类怪共用的 AI：把"行为"装进"角色槽"
	///   种类   Monsters/&lt;种类&gt;/&lt;种类&gt;Entity    选大脑 + 数值（表）+ 真正独有的行为（同文件夹）
	/// </code>
	/// 状态类是**可复用的行为**（States/Roam、States/Engage、States/Interrupt），只描述"做什么"、不写死角色；
	/// 原型用 <see cref="MonsterAiStateSet.Bind"/> 把行为放进角色槽。于是：
	///  * 同一个行为可以被任何原型、任何怪复用，也可以放进不同的槽；
	///  * 某怪要改一个环节，只在自己的 CreateBrain 里 <c>.Bind(MonsterAiRole.Attack, new XxxState())</c> 换掉那个槽；
	///  * 某怪独有的行为放它自己的文件夹，按"种类 + 行为"命名（HuaguoshanMonkeyBackstepState）——不会与公共行为重名。
	/// 原型按整套打法命名，与小怪/精英/Boss 无关（阶级是数据）。每次调用返回全新状态集（GF.Fsm：状态实例不跨状态机共享）。
	///
	/// | 槽 | Brawler 肉搏 | Sentry 守卫 |
	/// |----|-------------|-------------|
	/// | Idle / Patrol | 原地停 / 左右游荡 | 原地站岗（不游荡） |
	/// | Chase | 走向目标 | 走向目标 |
	/// | Attack | 站定出招 | 站定出招 |
	/// | Hold（够不着） | 在目标下方来回踱步 | 原地面向目标等 |
	/// 丢失目标（MonsterConfig.LoseTargetTime）后都回 Patrol 槽：Brawler 走回巡逻范围继续游荡，Sentry 走回岗位。
	/// </summary>
	public static class MonsterBrains
	{
		/// <summary>近身肉搏（花果山猴子等标准小怪）：游荡 → 走向目标 → 站定出招；目标在平台上时在下面来回踱步。</summary>
		public static MonsterAiStateSet Brawler()
		{
			return Melee()
				.Bind(MonsterAiRole.Idle, new PauseState())
				.Bind(MonsterAiRole.Patrol, new WanderState())
				.Bind(MonsterAiRole.Hold, new PaceBelowTargetState());
		}

		/// <summary>
		/// 守卫：没有目标时回到出生点站岗（PatrolRadius 视为 0），有目标同样近身出招；够不着时原地等。
		/// 适合守门/守宝箱的怪、固定刷怪点的精英。
		/// </summary>
		public static MonsterAiStateSet Sentry()
		{
			return Melee()
				.Bind(MonsterAiRole.Idle, new PauseState())
				.Bind(MonsterAiRole.Patrol, new ReturnHomeState())
				.Bind(MonsterAiRole.Hold, new WaitBelowTargetState());
		}

		/// <summary>近身交战的公共部分：走向目标 + 站定出招 + 打断槽。</summary>
		private static MonsterAiStateSet Melee()
		{
			return new MonsterAiStateSet()
				.Bind(MonsterAiRole.Chase, new WalkToTargetState())
				.Bind(MonsterAiRole.Attack, new StandAndStrikeState())
				.Bind(MonsterAiRole.CcLocked, new CcLockedState())
				.Bind(MonsterAiRole.Death, new DeathState());
		}
	}
}
