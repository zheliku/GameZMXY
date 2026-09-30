using System;
using System.Collections.Generic;

namespace GameLogic.Entity.AI
{
	/// <summary>
	/// 一招的 AI 用法（值快照，来自 AttackConfig 的 Ai* 列）。
	/// </summary>
	public readonly struct MonsterSkillSpec
	{
		/// <summary>招式下标（= AttackSegment = 动画 attack_(Index+1)）</summary>
		public readonly int Index;

		/// <summary>0 = 普攻池；&gt;0 = 技能（冷却就绪且距离满足即放，大者优先）</summary>
		public readonly int Priority;

		/// <summary>同池/同优先级内的抽取权重（≤0 不参与）</summary>
		public readonly int Weight;

		/// <summary>释放距离下限 px（水平）</summary>
		public readonly float MinRange;

		/// <summary>释放距离上限 px（水平）</summary>
		public readonly float MaxRange;

		/// <summary>释放后冷却秒区间</summary>
		public readonly float CooldownMin;
		public readonly float CooldownMax;

		/// <summary>出生初始冷却秒区间</summary>
		public readonly float InitCooldownMin;
		public readonly float InitCooldownMax;

		public MonsterSkillSpec(int index, int priority, int weight, float minRange, float maxRange,
			float cooldownMin, float cooldownMax, float initCooldownMin, float initCooldownMax)
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
		}

		/// <summary>是否技能（有优先级、走冷却），否则属于普攻池</summary>
		public bool IsSkill => Priority > 0;
	}

	/// <summary>
	/// 怪物技能书：一只怪全部招式的**冷却与选招**（纯 C#，随机数由调用方传入，可精确单测）。
	///
	/// 选招规则（对应旧 Monster_103 的 Skill_N_CD + 距离门槛 + 普攻 attackDesire 掷骰）：
	///  * <see cref="SelectSkill"/>：技能（Priority&gt;0）中取"冷却就绪 且 距离在区间内"的，
	///    优先级最高者胜；同优先级按权重抽。追击与站定都会调用——所以远程/突进技能在接近途中就会释放；
	///  * <see cref="SelectBasic"/>：普攻池（Priority=0）中取距离满足、冷却就绪的，按权重抽。
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

		/// <summary>该招此刻能否释放（冷却就绪、距离满足、权重有效）。</summary>
		public bool IsUsable(int position, float distance)
		{
			MonsterSkillSpec spec = m_Specs[position];
			return spec.Weight > 0 && m_Cooldowns[position] <= 0f && distance >= spec.MinRange && distance <= spec.MaxRange;
		}

		/// <summary>技能选招：可用技能中最高优先级者，同级按权重抽；无可用返回 -1。</summary>
		public int SelectSkill(float distance, float roll)
		{
			int best = 0;
			for (int i = 0; i < m_Specs.Length; i++)
			{
				if (m_Specs[i].IsSkill && IsUsable(i, distance) && m_Specs[i].Priority > best)
				{
					best = m_Specs[i].Priority;
				}
			}

			return best <= 0 ? -1 : PickWeighted(distance, roll, best);
		}

		/// <summary>普攻池选招：可用普攻按权重抽；无可用返回 -1。</summary>
		public int SelectBasic(float distance, float roll)
		{
			return PickWeighted(distance, roll, 0);
		}

		/// <summary>普攻池里是否有此距离可用的招（AI 用来判断"站定是否有意义"）。</summary>
		public bool HasBasicInRange(float distance)
		{
			for (int i = 0; i < m_Specs.Length; i++)
			{
				if (!m_Specs[i].IsSkill && m_Specs[i].Weight > 0 && distance >= m_Specs[i].MinRange &&
				    distance <= m_Specs[i].MaxRange)
				{
					return true;
				}
			}

			return false;
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
		private int PickWeighted(float distance, float roll, int priority)
		{
			int total = 0;
			for (int i = 0; i < m_Specs.Length; i++)
			{
				if (m_Specs[i].Priority == priority && IsUsable(i, distance))
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
				if (m_Specs[i].Priority != priority || !IsUsable(i, distance))
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
