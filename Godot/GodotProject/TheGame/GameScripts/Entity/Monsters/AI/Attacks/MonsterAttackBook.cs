using System;
using System.Collections.Generic;

namespace GameLogic.Entity.Monsters.AI
{
	/// <summary>
	/// 怪物招式书：一只怪全部招式的**冷却与选招**（纯 C#，随机数由调用方传入，可精确单测）。
	/// 普攻与优先招的区别见 <see cref="MonsterAttackSpec"/>。
	///
	/// 够不够得着：
	///  * 近身招：判定盒（按朝向）与目标受击盒**水平、垂直都重叠**，且水平吃进 ≥ <see cref="ReachMargin"/>；
	///  * 远程招：只比水平距离 |dx| ∈ Range。
	/// AI 用两个问题驱动站位：<see cref="BasicGapX"/>（普攻水平上还差多远，决定接近/站定）与
	/// <see cref="BasicInReach"/>（算上高度够不够得着，决定出手/守候）。
	/// 冷却只在真正出招时（宿主提交请求那一刻）<see cref="MarkUsed"/> 计入。
	/// </summary>
	public sealed class MonsterAttackBook
	{
		/// <summary>
		/// 近身招"够得着"所需的最小水平重叠深度 px（工程常数）：判定盒只擦边时物理重叠不稳定，吃进几像素再出招。
		/// </summary>
		public static readonly float ReachMargin = 4f;

		/// <summary>空招式书（只会走动、不会出招）</summary>
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

		/// <summary>出生/复用时重置：每招按初始冷却区间掷一次。</summary>
		public void Reset(Func<float> random)
		{
			for (int i = 0; i < m_Specs.Length; i++)
			{
				m_Cooldowns[i] = MonsterAttackSpec.Roll(m_Specs[i].InitCooldown, random());
			}
		}

		/// <summary>冷却推进（秒）。</summary>
		public void Tick(float elapseSeconds)
		{
			for (int i = 0; i < m_Cooldowns.Length; i++)
			{
				m_Cooldowns[i] = Math.Max(0f, m_Cooldowns[i] - elapseSeconds);
			}
		}

		/// <summary>出招计入冷却（roll ∈ [0,1) 在冷却区间内插值）。</summary>
		public void MarkUsed(int index, float roll)
		{
			int i = Find(index);
			if (i >= 0)
			{
				m_Cooldowns[i] = MonsterAttackSpec.Roll(m_Specs[i].Cooldown, roll);
			}
		}

		/// <summary>
		/// 普攻里**水平上**最近一招与目标的间隙（带符号：&gt;0 = 还差多少 px，≤ -ReachMargin = 水平够得着）。
		/// 不看冷却与高度；无普攻/无目标返回正无穷。dir = 出招朝向（出招时会转向目标，传"面向目标"）。
		/// </summary>
		public float BasicGapX(AiBox target, int dir)
		{
			float best = float.PositiveInfinity;
			for (int i = 0; i < m_Specs.Length && !target.IsEmpty; i++)
			{
				if (m_Specs[i].IsBasic && m_Specs[i].Weight > 0)
				{
					best = Math.Min(best, GapX(m_Specs[i], target, dir));
				}
			}

			return best;
		}

		/// <summary>普攻里有没有一招此刻够得着目标（含高度，不看冷却）。</summary>
		public bool BasicInReach(AiBox target, int dir)
		{
			foreach (MonsterAttackSpec spec in m_Specs)
			{
				if (spec.IsBasic && spec.Weight > 0 && InReach(spec, target, dir))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>优先招选招：可用优先招中优先级最高者，同级按权重抽；无可用返回 -1。</summary>
		public int SelectPriority(AiBox target, int dir, float roll)
		{
			int best = 0;
			for (int i = 0; i < m_Specs.Length; i++)
			{
				if (m_Specs[i].Priority > best && IsUsable(i, target, dir))
				{
					best = m_Specs[i].Priority;
				}
			}

			return best == 0 ? -1 : PickWeighted(target, dir, roll, best);
		}

		/// <summary>普攻选招：可用普攻按权重抽；无可用（含高度够不着）返回 -1。</summary>
		public int SelectBasic(AiBox target, int dir, float roll)
		{
			return PickWeighted(target, dir, roll, 0);
		}

		/// <summary>在"可用且优先级 == priority"的招式中按权重抽取，返回招式下标（无候选 -1）。</summary>
		private int PickWeighted(AiBox target, int dir, float roll, int priority)
		{
			int total = 0;
			for (int i = 0; i < m_Specs.Length; i++)
			{
				total += m_Specs[i].Priority == priority && IsUsable(i, target, dir) ? m_Specs[i].Weight : 0;
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

		private bool IsUsable(int i, AiBox target, int dir)
		{
			return m_Specs[i].Weight > 0 && m_Cooldowns[i] <= 0f && InReach(m_Specs[i], target, dir);
		}

		private static bool InReach(MonsterAttackSpec spec, AiBox target, int dir)
		{
			return !target.IsEmpty && GapX(spec, target, dir) <= -ReachMargin &&
			       (spec.Reach.IsEmpty || spec.Reach.Facing(dir).GapY(target) < 0f);
		}

		/// <summary>近身招：判定盒与受击盒的水平间隙；远程招：到距离区间的距离（区间内为负无穷）。</summary>
		private static float GapX(MonsterAttackSpec spec, AiBox target, int dir)
		{
			if (!spec.Reach.IsEmpty)
			{
				return spec.Reach.Facing(dir).GapX(target);
			}

			float d = Math.Abs(target.CenterX);
			return d < spec.Range.Min ? spec.Range.Min - d : d > spec.Range.Max ? d - spec.Range.Max : float.NegativeInfinity;
		}

		private int Find(int index)
		{
			return Array.FindIndex(m_Specs, s => s.Index == index);
		}
	}
}
