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

		private readonly MonsterAttackSpec[] m_Specs;
		private readonly float[] m_Cooldowns;

		public MonsterAttackBook(IReadOnlyList<MonsterAttackSpec> specs)
		{
			m_Specs = [.. specs];
			m_Cooldowns = new float[m_Specs.Length];
		}

		/// <summary>按招式下标取判定盒范围（调试观测；不存在返回空盒）。</summary>
		public AiBox ReachOf(int index)
		{
			int i = Find(index);
			return i < 0 ? default : m_Specs[i].Reach;
		}

		public void Reset(Func<float> random)
		{
			for (int i = 0; i < m_Specs.Length; i++)
			{
				m_Cooldowns[i] = Roll(m_Specs[i].InitCooldown, random);
			}
		}

		public void Tick(float elapseSeconds)
		{
			for (int i = 0; i < m_Cooldowns.Length; i++)
			{
				m_Cooldowns[i] = Math.Max(0f, m_Cooldowns[i] - elapseSeconds);
			}
		}

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

		public int SelectPriority(AiBox target, int dir, int priority, float roll)
		{
			return priority <= 0 ? -1 : PickWeighted(target, dir, roll, priority);
		}

		/// <summary>从可用普攻按权重抽取；无候选不取随机值。</summary>
		public int SelectBasic(AiBox target, int dir, Func<float> random)
		{
			return PickWeighted(target, dir, random, 0);
		}

		private int PickWeighted(AiBox target, int dir, float roll, int priority)
		{
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

		private int PickWeighted(AiBox target, int dir, Func<float> random, int priority)
		{
			for (int i = 0; i < m_Specs.Length; i++)
			{
				if (m_Specs[i].Priority == priority && IsUsable(i, target, dir))
				{
					return PickWeighted(target, dir, random(), priority);
				}
			}

			return -1;
		}

		private bool IsUsable(int i, AiBox target, int dir)
		{
			return m_Specs[i].Weight > 0 && m_Cooldowns[i] <= 0f && HasReachOrRange(m_Specs[i]) &&
			       InReach(m_Specs[i], target, dir);
		}

		private static bool HasReachOrRange(MonsterAttackSpec spec)
		{
			return !spec.Reach.IsEmpty || spec.Range.Max > 0f;
		}

		private static bool InReach(MonsterAttackSpec spec, AiBox target, int dir)
		{
			return !target.IsEmpty && GapX(spec, target, dir) <= -ReachMargin &&
			       (spec.Reach.IsEmpty || spec.Reach.Facing(dir).GapY(target) < 0f);
		}

		/// <summary>近战盒间隙；远程招测量到水平距离区间的距离。</summary>
		private static float GapX(MonsterAttackSpec spec, AiBox target, int dir)
		{
			if (!spec.Reach.IsEmpty)
			{
				return spec.Reach.Facing(dir).GapX(target);
			}

			float distance = Math.Abs(target.CenterX);
			return distance < spec.Range.Min ? spec.Range.Min - distance :
				distance > spec.Range.Max ? distance - spec.Range.Max : float.NegativeInfinity;
		}

		private int Find(int index)
		{
			return Array.FindIndex(m_Specs, spec => spec.Index == index);
		}

		private static float Roll((float Min, float Max) range, Func<float> random)
		{
			return MonsterAttackSpec.Roll(range, range.Max > range.Min ? random() : 0f);
		}
	}
}
