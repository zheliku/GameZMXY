using System;

namespace GameLogic.Entity.Monsters.AI
{
	/// <summary>
	/// 一招的 AI 用法：优先级、权重、释放距离与攻击动画判定盒。
	/// </summary>
	public readonly struct MonsterAttackSpec
	{
		/// <summary>招式下标（= AttackSegment）</summary>
		public int Index { get; init; }

		/// <summary>抽选权重（≤0 = AI 不用这招）</summary>
		public int Weight { get; init; }

		/// <summary>0 = 普攻；&gt;0 = 可在追击/守候时释放的优先招（大者优先）</summary>
		public int Priority { get; init; }

		/// <summary>近战攻击动画判定盒的并集（素材原生朝左）。</summary>
		public AiBox Reach { get; init; }

		/// <summary>无近战判定盒招式的水平释放距离区间（远程/冲锋）</summary>
		public (float Min, float Max) Range { get; init; }

		/// <summary>释放后冷却区间</summary>
		public (float Min, float Max) Cooldown { get; init; }

		/// <summary>出生时初始冷却区间</summary>
		public (float Min, float Max) InitCooldown { get; init; }

		public bool IsBasic => Priority == 0;

		public static float Roll((float Min, float Max) range, float t)
		{
			return range.Min + (Math.Max(range.Min, range.Max) - range.Min) * t;
		}
	}
}
