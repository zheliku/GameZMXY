namespace GameLogic.Entity.Monsters.AI
{
	/// <summary>
	/// 怪物 AI 决策参数（值快照）：MonsterEntity 从 MonsterConfig 拷入，AI 行为只读这里。
	/// 纯 C# 值类型——AI 不直接依赖 Luban 生成类，单测用对象初始化器直接构造。
	/// "多近才能打"不在这里：由攻击动画判定盒推导（<see cref="MonsterAttackSpec.Reach"/>）。
	/// 感知类参数（索敌距离、丢失目标时间）归实体，不在这里——AI 只看"有没有目标"。
	/// </summary>
	public readonly struct MonsterAiParams
	{
		/// <summary>攻击欲望 0-100：每次攻击判定的出手概率（AttackDesire）</summary>
		public int AttackDesire { get; init; }

		/// <summary>站定后攻击判定间隔秒（AttackInterval，旧 count%60）</summary>
		public float AttackInterval { get; init; }

		/// <summary>
		/// 进入攻击范围后首次出手判定的随机延迟秒（AttackFirstDelay 的 x=最短、y=最长）。
		/// 只是"反应时间"，与出手概率无关；持续交战的期望出手间隔 ≈ AttackInterval ÷ (AttackDesire/100)。
		/// </summary>
		public (float Min, float Max) AttackFirstDelay { get; init; }

		/// <summary>游荡/待机重新决策间隔秒（PatrolInterval，旧 change_state 计时器）</summary>
		public float PatrolInterval { get; init; }

		/// <summary>游荡决策时原地停留概率 0-100（PatrolIdleChance）</summary>
		public int PatrolIdleChance { get; init; }

		/// <summary>巡逻半径 px：离出生点超出即往回走（PatrolRadius；0 = 不限）</summary>
		public float PatrolRadius { get; init; }

		/// <summary>受控解除后的额外僵直秒（BehitCalmTime）</summary>
		public float CalmTime { get; init; }

		/// <summary>站定滞回 px（AttackRangeSlack）：水平间隙超过本值才回到接近；滞回区内小步贴近</summary>
		public float AttackRangeSlack { get; init; }

		/// <summary>守候踱步半幅 px（PaceRange）：目标高度够不着时，以目标 x 为中心左右来回的距离（0 = 原地等）</summary>
		public float PaceRange { get; init; }
	}
}
