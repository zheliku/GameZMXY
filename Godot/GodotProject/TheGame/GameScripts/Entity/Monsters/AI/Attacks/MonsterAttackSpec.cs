using System;

namespace GameLogic.Entity.Monsters.AI
{
	/// <summary>
	/// 一招的 AI 用法（值快照）：AttackConfig 的 Ai* 列 + 攻击动画判定盒推导出的够得着范围。
	///
	/// 招式按"怎么被选中"分两种（小怪通常只有第一种）：
	///  * **普攻**（<see cref="Priority"/> = 0）：站定后按 AttackDesire 掷骰出手，普攻之间按权重抽；
	///  * **优先招**（Priority &gt; 0，即旧项目的"技能"）：冷却就绪且够得着就先放，不掷骰；多招同时可用时优先级大者胜。
	/// </summary>
	public readonly struct MonsterAttackSpec
	{
		/// <summary>招式下标（= AttackSegment）</summary>
		public int Index { get; init; }

		/// <summary>0 = 普攻；&gt;0 = 优先招（大者优先）</summary>
		public int Priority { get; init; }

		/// <summary>同类招式内的抽取权重（≤0 = AI 不用这招）</summary>
		public int Weight { get; init; }

		/// <summary>
		/// 够得着的范围：攻击动画判定窗口内判定盒的并集（素材原生朝左、相对怪物原点）。
		/// 非空 = 近身招，按判定盒与目标受击盒重叠判断（含高度）；空 = 远程招，按 <see cref="Range"/> 判水平距离。
		/// </summary>
		public AiBox Reach { get; init; }

		/// <summary>远程招释放水平距离区间 px（AttackConfig.AiRange；仅 Reach 为空时使用）</summary>
		public (float Min, float Max) Range { get; init; }

		/// <summary>释放后冷却秒区间（AttackConfig.AiCooldown）</summary>
		public (float Min, float Max) Cooldown { get; init; }

		/// <summary>出生初始冷却秒区间（AttackConfig.AiInitCooldown）</summary>
		public (float Min, float Max) InitCooldown { get; init; }

		/// <summary>普攻（按攻击欲望掷骰出手）</summary>
		public bool IsBasic => Priority == 0;

		/// <summary>在区间内按 t ∈ [0,1) 插值（上限小于下限按下限）。</summary>
		public static float Roll((float Min, float Max) range, float t)
		{
			return range.Min + (Math.Max(range.Min, range.Max) - range.Min) * t;
		}
	}
}
