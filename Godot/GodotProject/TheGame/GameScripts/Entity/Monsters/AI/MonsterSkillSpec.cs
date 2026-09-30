using System;

namespace GameLogic.Entity.Monsters.AI
{
	/// <summary>
	/// 一招的 AI 用法（值快照）：来自 AttackConfig 的 Ai* 列 + 攻击动画判定盒推导出的够得着范围。
	/// </summary>
	public readonly struct MonsterSkillSpec
	{
		/// <summary>招式下标（= AttackSegment）</summary>
		public readonly int Index;

		/// <summary>0 = 普攻池；&gt;0 = 技能（冷却就绪且够得着即放，大者优先）</summary>
		public readonly int Priority;

		/// <summary>同池/同优先级内的抽取权重（≤0 不参与）</summary>
		public readonly int Weight;

		/// <summary>远程招释放距离下限 px（水平；仅 <see cref="Reach"/> 为空时使用，AttackConfig.AiRange.X）</summary>
		public readonly float MinRange;

		/// <summary>远程招释放距离上限 px（水平；仅 <see cref="Reach"/> 为空时使用，AttackConfig.AiRange.Y）</summary>
		public readonly float MaxRange;

		/// <summary>释放后冷却秒区间</summary>
		public readonly float CooldownMin;
		public readonly float CooldownMax;

		/// <summary>出生初始冷却秒区间</summary>
		public readonly float InitCooldownMin;
		public readonly float InitCooldownMax;

		/// <summary>
		/// 够得着的范围：攻击动画判定窗口内判定盒的并集（素材原生朝左、相对怪物原点）。
		/// 非空 = 近身招，按"判定盒与目标受击盒是否重叠"判断（含高度）；空 = 远程招，按 MinRange/MaxRange 判水平距离。
		/// </summary>
		public readonly AiBox Reach;

		public MonsterSkillSpec(int index, int priority, int weight, float minRange, float maxRange,
			float cooldownMin, float cooldownMax, float initCooldownMin, float initCooldownMax, AiBox reach = default)
		{
			Index = index;
			Priority = priority;
			Weight = weight;
			MinRange = minRange;
			MaxRange = maxRange;
			CooldownMin = cooldownMin;
			CooldownMax = Math.Max(cooldownMin, cooldownMax);
			InitCooldownMin = initCooldownMin;
			InitCooldownMax = Math.Max(initCooldownMin, initCooldownMax);
			Reach = reach;
		}

		/// <summary>是否技能（有优先级、走冷却），否则属于普攻池</summary>
		public bool IsSkill => Priority > 0;

		/// <summary>是否近身招（有判定盒范围）</summary>
		public bool HasReach => !Reach.IsEmpty;
	}
}
