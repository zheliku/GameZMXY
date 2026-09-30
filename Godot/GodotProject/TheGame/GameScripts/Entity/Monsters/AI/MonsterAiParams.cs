namespace GameLogic.Entity.Monsters.AI
{
	/// <summary>
	/// 怪物 AI 决策参数（值快照）：由 MonsterEntity 从 MonsterConfig 拷入，AI 状态只读这里。
	/// 纯 C# 值类型——AI 状态不直接依赖 Luban 生成类，单测可直接构造。
	/// "多近才能打"不在这里：由攻击动画判定盒推导（<see cref="MonsterSkillSpec.Reach"/>）。
	/// </summary>
	public readonly struct MonsterAiParams
	{
		/// <summary>攻击欲望 0-100：每次攻击判定的出手概率（MonsterConfig.AttackDesire）</summary>
		public readonly int AttackDesire;

		/// <summary>站定后攻击判定间隔秒（MonsterConfig.AttackInterval，旧 count%60）</summary>
		public readonly float AttackInterval;

		/// <summary>巡逻/待机重新决策间隔秒（MonsterConfig.PatrolInterval，旧 change_state 计时器）</summary>
		public readonly float PatrolInterval;

		/// <summary>巡逻决策时原地停留概率 0-100（MonsterConfig.PatrolIdleChance）</summary>
		public readonly int PatrolIdleChance;

		/// <summary>巡逻半径 px：离出生点超出即折返（MonsterConfig.PatrolRadius）</summary>
		public readonly float PatrolRadius;

		/// <summary>受控解除后的额外僵直秒（MonsterConfig.BehitCalmTime，旧 behit_calmtime）</summary>
		public readonly float CalmTime;

		/// <summary>
		/// 站定滞回 px（MonsterConfig.AttackRangeSlack）：目标离开普攻判定盒的水平间隙超过本值才回到追击；
		/// 间隙在 (0, 本值] 内时留在站定状态、小步贴近——目标被击退几像素不会在 Chase/Attack 间来回切。
		/// </summary>
		public readonly float AttackRangeSlack;

		public MonsterAiParams(int attackDesire, float attackInterval, float patrolInterval, int patrolIdleChance,
			float patrolRadius, float calmTime, float attackRangeSlack)
		{
			AttackDesire = attackDesire;
			AttackInterval = attackInterval;
			PatrolInterval = patrolInterval;
			PatrolIdleChance = patrolIdleChance;
			PatrolRadius = patrolRadius;
			CalmTime = calmTime;
			AttackRangeSlack = attackRangeSlack;
		}
	}
}
