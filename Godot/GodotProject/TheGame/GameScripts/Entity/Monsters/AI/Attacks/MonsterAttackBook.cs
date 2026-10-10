using System;
using System.Collections.Generic;

namespace GameLogic.Entity.Monsters.AI
{
	/// <summary>
	/// 怪物攻击范围与加权选招（纯 C#，随机数由调用方传入，可精确单测）。
	/// 普攻站位由判定盒决定；优先招可使用远程区间与逐招冷却。
	/// </summary>
	public sealed class MonsterAttackBook
	{
		/// <summary>
		/// 近身招"够得着"所需的最小水平重叠深度 px（工程常数）：判定盒只擦边时物理重叠不稳定，吃进几像素再出招。
		/// </summary>
		public static readonly float ReachMargin = 4f;

		/// <summary>空攻击集（只会走动、不会出招）</summary>
		public static readonly MonsterAttackBook Empty = new([]);

		private readonly MonsterAttackSpec[] m_Specs; // 按攻击下标保存的招式规格。
		private readonly float[] m_Cooldowns; // 每个招式剩余的冷却秒数。

		/// <summary>创建攻击集并复制招式规格。</summary>
		/// <param name="specs">招式规格列表。</param>
		public MonsterAttackBook(IReadOnlyList<MonsterAttackSpec> specs)
		{
			m_Specs = [.. specs];
			m_Cooldowns = new float[m_Specs.Length];
		}

		/// <summary>重置所有招式，并按初始冷却区间采样冷却。</summary>
		public void Reset(Func<float> random)
		{
			for (int i = 0; i < m_Specs.Length; i++)
			{
				m_Cooldowns[i] = Roll(m_Specs[i].InitCooldown, random);
			}
		}

		/// <summary>推进招式冷却并截断到零。</summary>
		public void Tick(float elapseSeconds)
		{
			for (int i = 0; i < m_Cooldowns.Length; i++)
			{
				m_Cooldowns[i] = Math.Max(0f, m_Cooldowns[i] - elapseSeconds);
			}
		}

		/// <summary>标记招式已使用，并按其冷却区间重新计时。</summary>
		public void MarkUsed(int index, Func<float> random)
		{
			int i = Find(index);
			if (i >= 0)
			{
				m_Cooldowns[i] = Roll(m_Specs[i].Cooldown, random);
			}
		}

		/// <summary>
		/// 所有可用攻击中最近一招的水平间隙（&gt;0 = 还差多少 px，≤ -ReachMargin = 水平够得着）。
		/// 不看高度；无有效攻击/无目标返回正无穷。dir 为面向目标的方向。
		/// </summary>
		public float BasicGapX(AiBox target, int dir)
		{
			float best = float.PositiveInfinity;
			for (int i = 0; i < m_Specs.Length && !target.IsEmpty; i++)
			{
				if (m_Specs[i].IsBasic && m_Specs[i].Weight > 0 && HasReachOrRange(m_Specs[i]))
				{
					best = Math.Min(best, GapX(m_Specs[i], target, dir));
				}
			}

			return best;
		}

		/// <summary>有没有一招此刻够得着目标（含高度）。</summary>
		public bool BasicInReach(AiBox target, int dir)
		{
			foreach (MonsterAttackSpec spec in m_Specs)
			{
				if (spec.IsBasic && spec.Weight > 0 && HasReachOrRange(spec) && InReach(spec, target, dir))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>返回当前目标可用的最高优先级，没招可用时返回 -1。</summary>
		public int HighestPriority(AiBox target, int dir)
		{
			int best = 0;
			for (int i = 0; i < m_Specs.Length; i++)
			{
				if (m_Specs[i].Priority > best && IsUsable(i, target, dir))
				{
					best = m_Specs[i].Priority;
				}
			}

			return best == 0 ? -1 : best;
		}

		/// <summary>按给定优先级和随机值加权选择招式。</summary>
		public int SelectPriority(AiBox target, int dir, int priority, float roll)
		{
			return priority <= 0 ? -1 : PickWeighted(target, dir, roll, priority);
		}

		/// <summary>从可用普攻按权重抽取；无候选不取随机值。</summary>
		public int SelectBasic(AiBox target, int dir, Func<float> random)
		{
			return PickWeighted(target, dir, random, 0);
		}

		private int PickWeighted(AiBox target, int dir, float roll, int priority) // 从指定优先级的可用招式中按权重选择。
		{
			// 先计算候选总权重，避免没有候选时消费随机值。
			int total = 0;
			for (int i = 0; i < m_Specs.Length; i++)
			{
				total += m_Specs[i].Priority == priority && IsUsable(i, target, dir)
					? m_Specs[i].Weight
					: 0;
			}

			if (total == 0)
			{
				return -1;
			}

			// 按随机落点扣减权重，落入的最后一个候选即为选择结果。
			float pick = roll * total;
			int last = -1;
			for (int i = 0; i < m_Specs.Length; i++)
			{
				if (m_Specs[i].Priority != priority || !IsUsable(i, target, dir))
				{
					continue;
				}

				last = m_Specs[i].Index;
				pick -= m_Specs[i].Weight;
				if (pick < 0f)
				{
					break;
				}
			}

			return last;   // roll 恰为 1 的浮点边界落到最后一个候选
		}

		private int PickWeighted(AiBox target, int dir, Func<float> random, int priority) // 确认有候选后取一次随机值并执行加权选择。
		{
			// 先确认候选存在，保证空候选不调用随机源。
			for (int i = 0; i < m_Specs.Length; i++)
			{
				if (m_Specs[i].Priority == priority && IsUsable(i, target, dir))
				{
					return PickWeighted(target, dir, random(), priority);
				}
			}

			return -1;
		}

		private bool IsUsable(int i, AiBox target, int dir) // 判断招式是否有权重、冷却结束且当前可命中。
		{
			return m_Specs[i].Weight > 0 && m_Cooldowns[i] <= 0f && HasReachOrRange(m_Specs[i]) &&
			       InReach(m_Specs[i], target, dir);
		}

		private static bool HasReachOrRange(MonsterAttackSpec spec) // 判断招式是否配置了判定盒或远程范围。
		{
			return !spec.Reach.IsEmpty || spec.Range.Max > 0f;
		}

		private static bool InReach(MonsterAttackSpec spec, AiBox target, int dir) // 判断目标是否同时满足水平重叠和近战高度条件。
		{
			return !target.IsEmpty && GapX(spec, target, dir) <= -ReachMargin &&
			       (spec.Reach.IsEmpty || spec.Reach.Facing(dir).GapY(target) < 0f);
		}

		private static float GapX(MonsterAttackSpec spec, AiBox target, int dir) // 计算近战盒间隙或远程范围距离。
		{
			if (!spec.Reach.IsEmpty)
			{
				return spec.Reach.Facing(dir).GapX(target);
			}

			float distance = Math.Abs(target.CenterX);
			return distance < spec.Range.Min ? spec.Range.Min - distance :
				distance > spec.Range.Max ? distance - spec.Range.Max : float.NegativeInfinity;
		}

		private int Find(int index) // 按外部攻击下标查找规格数组位置。
		{
			return Array.FindIndex(m_Specs, spec => spec.Index == index);
		}

		private static float Roll((float Min, float Max) range, Func<float> random) // 按区间配置采样冷却，定值区间不消费随机源。
		{
			return MonsterAttackSpec.Roll(range, range.Max > range.Min ? random() : 0f);
		}
	}
}
