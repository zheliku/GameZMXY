using System;
using System.Collections.Generic;

namespace GameLogic.Entity.Monsters.AI
{
	/// <summary>
	/// 怪物技能书：一只怪全部招式的**冷却与选招**（纯 C#，随机数由调用方传入，可精确单测）。
	///
	/// "够不够得着"（<see cref="InReach"/>）：
	///  * 近身招（<see cref="MonsterSkillSpec.HasReach"/>）：招式判定盒（按朝向镜像）与目标受击盒**水平与垂直都重叠**，
	///    且水平重叠深度 ≥ <see cref="MonsterAiRules.ReachMargin"/>——目标跳到头顶时水平够得着但高度不够，照样不出招；
	///  * 远程招（无判定盒）：只比水平距离 |dx| ∈ [MinRange, MaxRange]。
	///
	/// 选招规则（对应旧 Monster_103 的 Skill_N_CD + 距离门槛 + 普攻 attackDesire 掷骰）：
	///  * <see cref="SelectSkill"/>：技能（Priority&gt;0）中取"冷却就绪 且 够得着"的，优先级最高者胜；同优先级按权重抽。
	///    追击与站定都会调用——远程/突进技能在接近途中就会释放；
	///  * <see cref="SelectBasic"/>：普攻池（Priority=0）中取够得着、冷却就绪的，按权重抽。
	///    出手与否由 AI 状态按 AttackDesire 掷骰决定，技能书只负责"抽哪一招"。
	/// 冷却只在真正出招时（实体提交出招请求那一刻）通过 <see cref="MarkUsed"/> 计入，请求被受击打断不消耗冷却。
	/// </summary>
	public sealed class MonsterSkillBook
	{
		private readonly MonsterSkillSpec[] m_Specs;
		private readonly float[] m_Cooldowns;

		/// <summary>空技能书（没有任何招式的怪：只会走动，不会出招）</summary>
		public static readonly MonsterSkillBook Empty = new MonsterSkillBook(Array.Empty<MonsterSkillSpec>());

		public MonsterSkillBook(IReadOnlyList<MonsterSkillSpec> specs)
		{
			m_Specs = new MonsterSkillSpec[specs.Count];
			for (int i = 0; i < specs.Count; i++)
			{
				m_Specs[i] = specs[i];
			}

			m_Cooldowns = new float[m_Specs.Length];
		}

		/// <summary>招式数</summary>
		public int Count => m_Specs.Length;

		/// <summary>按位置取招式用法</summary>
		public MonsterSkillSpec this[int position] => m_Specs[position];

		/// <summary>某招剩余冷却秒（按招式下标；不存在返回 0）</summary>
		public float GetCooldown(int index)
		{
			int position = Find(index);
			return position < 0 ? 0f : m_Cooldowns[position];
		}

		/// <summary>出生/复用时重置：每招按初始冷却区间掷一次（random 返回 [0,1)）。</summary>
		public void Reset(Func<float> random)
		{
			for (int i = 0; i < m_Specs.Length; i++)
			{
				m_Cooldowns[i] = Lerp(m_Specs[i].InitCooldownMin, m_Specs[i].InitCooldownMax, random());
			}
		}

		/// <summary>冷却推进（秒）。</summary>
		public void Tick(float elapseSeconds)
		{
			for (int i = 0; i < m_Cooldowns.Length; i++)
			{
				if (m_Cooldowns[i] > 0f)
				{
					m_Cooldowns[i] = Math.Max(0f, m_Cooldowns[i] - elapseSeconds);
				}
			}
		}

		/// <summary>
		/// 某招与目标的水平间隙（带符号：&gt;0 = 还差多少 px，≤0 = 已水平重叠的深度取负）。
		/// 远程招：距离在区间内返回负无穷（水平上算完全够得着），否则返回到区间边缘的距离。
		/// </summary>
		/// <param name="dir">怪物出招朝向（-1 左 / 1 右；出招时本来就会转向目标，调用方传"面向目标"的方向）</param>
		public float HorizontalGap(int position, AiBox target, int dir)
		{
			MonsterSkillSpec spec = m_Specs[position];
			if (spec.HasReach)
			{
				return MonsterAiRules.HorizontalGap(MonsterAiRules.ToFacing(spec.Reach, dir), target);
			}

			float distance = Math.Abs(target.CenterX);
			if (distance < spec.MinRange)
			{
				return spec.MinRange - distance;
			}

			if (distance > spec.MaxRange)
			{
				return distance - spec.MaxRange;
			}

			return float.NegativeInfinity;
		}

		/// <summary>该招此刻是否够得着目标（近身招含高度判断，见类注释）。</summary>
		public bool InReach(int position, AiBox target, int dir)
		{
			if (target.IsEmpty || HorizontalGap(position, target, dir) > -MonsterAiRules.ReachMargin)
			{
				return false;
			}

			MonsterSkillSpec spec = m_Specs[position];
			return !spec.HasReach || MonsterAiRules.VerticalGap(MonsterAiRules.ToFacing(spec.Reach, dir), target) < 0f;
		}

		/// <summary>该招此刻能否释放（冷却就绪、够得着、权重有效）。</summary>
		public bool IsUsable(int position, AiBox target, int dir)
		{
			return m_Specs[position].Weight > 0 && m_Cooldowns[position] <= 0f && InReach(position, target, dir);
		}

		/// <summary>
		/// 普攻池里**水平上**最近的一招与目标的间隙（不看冷却与高度；无有效普攻返回正无穷）。
		/// AI 用它决定站定/追击：水平够得着就站定，高度够不着的在站定状态里等（不出招）。
		/// </summary>
		public float BasicHorizontalGap(AiBox target, int dir)
		{
			float best = float.PositiveInfinity;
			if (target.IsEmpty)
			{
				return best;
			}

			for (int i = 0; i < m_Specs.Length; i++)
			{
				if (!m_Specs[i].IsSkill && m_Specs[i].Weight > 0)
				{
					best = Math.Min(best, HorizontalGap(i, target, dir));
				}
			}

			return best;
		}

		/// <summary>技能选招：可用技能中最高优先级者，同级按权重抽；无可用返回 -1。</summary>
		public int SelectSkill(AiBox target, int dir, float roll)
		{
			int best = 0;
			for (int i = 0; i < m_Specs.Length; i++)
			{
				if (m_Specs[i].IsSkill && m_Specs[i].Priority > best && IsUsable(i, target, dir))
				{
					best = m_Specs[i].Priority;
				}
			}

			return best <= 0 ? -1 : PickWeighted(target, dir, roll, best);
		}

		/// <summary>普攻池选招：可用普攻按权重抽；无可用（含高度够不着）返回 -1。</summary>
		public int SelectBasic(AiBox target, int dir, float roll)
		{
			return PickWeighted(target, dir, roll, 0);
		}

		/// <summary>出招计入冷却（roll ∈ [0,1) 在冷却区间内插值）。</summary>
		public void MarkUsed(int index, float roll)
		{
			int position = Find(index);
			if (position >= 0)
			{
				m_Cooldowns[position] = Lerp(m_Specs[position].CooldownMin, m_Specs[position].CooldownMax, roll);
			}
		}

		/// <summary>在"可用 且 优先级 == priority"的招式中按权重抽取，返回招式下标。</summary>
		private int PickWeighted(AiBox target, int dir, float roll, int priority)
		{
			int total = 0;
			for (int i = 0; i < m_Specs.Length; i++)
			{
				if (m_Specs[i].Priority == priority && IsUsable(i, target, dir))
				{
					total += m_Specs[i].Weight;
				}
			}

			if (total <= 0)
			{
				return -1;
			}

			float pick = roll * total;
			int acc = 0;
			int last = -1;
			for (int i = 0; i < m_Specs.Length; i++)
			{
				if (m_Specs[i].Priority != priority || !IsUsable(i, target, dir))
				{
					continue;
				}

				acc += m_Specs[i].Weight;
				last = m_Specs[i].Index;
				if (pick < acc)
				{
					return m_Specs[i].Index;
				}
			}

			return last;   // roll 恰为 1 的浮点边界兜底
		}

		private int Find(int index)
		{
			for (int i = 0; i < m_Specs.Length; i++)
			{
				if (m_Specs[i].Index == index)
				{
					return i;
				}
			}

			return -1;
		}

		private static float Lerp(float a, float b, float t)
		{
			return a + (b - a) * t;
		}
	}
}
