using System;
using System.Collections.Generic;
using GameConfig.Stat;

namespace GameLogic.Battle.Stats;

/// <summary>
/// 一名角色的属性汇总：基础值（成长表或怪物配置）+ 按来源登记的修正（装备、法宝、被动、丹药、Buff……）。
/// 最终值 = (基础 + Σ固定值) × (1 + Σ同类百分比) × Π(1 + 独立百分比)。
/// 修正按来源整体登记和移除，卸下装备或 Buff 结束时不会残留；读取时按脏标记惰性重算。
/// 纯 C#，不引用节点和框架服务；由实体持有，生命周期与实体显示一致。
/// </summary>
public sealed class StatSheet
{
	private static readonly int StatCount = Enum.GetValues<StatType>().Length; // 属性种类数，枚举值从 0 连续。

	private readonly float[] m_Base = new float[StatCount]; // 基础值，按 StatType 索引。

	private readonly float[] m_Final = new float[StatCount]; // 最近一次重算的最终值缓存。

	private readonly Dictionary<StatSource, StatModifier[]> m_Sources = new(); // 来源到其全部修正的登记表。

	private bool m_Dirty = true; // 基础值或修正变化后需要重算。

	/// <summary>基础值或修正发生变化；订阅方重新读取需要的最终值。</summary>
	public event Action Changed;

	/// <summary>用一组完整属性替换基础值；不影响已登记的修正。</summary>
	/// <param name="block">成长表或怪物配置中的属性块。</param>
	/// <exception cref="ArgumentNullException">属性块为空。</exception>
	public void SetBase(StatBlock block)
	{
		ArgumentNullException.ThrowIfNull(block);

		// 按枚举顺序整体覆盖，生成类型的字段与枚举一一对应。
		m_Base[(int)StatType.MaxHp] = block.MaxHp;
		m_Base[(int)StatType.MaxMp] = block.MaxMp;
		m_Base[(int)StatType.Power] = block.Power;
		m_Base[(int)StatType.Def] = block.Def;
		m_Base[(int)StatType.Mdef] = block.Mdef;
		m_Base[(int)StatType.Crit] = block.Crit;
		m_Base[(int)StatType.Miss] = block.Miss;
		m_Base[(int)StatType.Lucky] = block.Lucky;
		m_Base[(int)StatType.Toughness] = block.Toughness;
		m_Base[(int)StatType.Htarget] = block.Htarget;
		m_Base[(int)StatType.CritReduce] = block.CritReduce;
		m_Base[(int)StatType.Ar] = block.Ar;
		m_Base[(int)StatType.Sp] = block.Sp;
		m_Base[(int)StatType.Vampirism] = block.Vampirism;
		m_Base[(int)StatType.HpRegen] = block.HpRegen;
		m_Base[(int)StatType.MpRegen] = block.MpRegen;
		MarkDirty();
	}

	/// <summary>登记一个来源的全部修正；同一来源已存在时整体替换。</summary>
	/// <param name="source">修正来源。</param>
	/// <param name="modifiers">该来源提供的修正，可为空集合。</param>
	/// <exception cref="ArgumentException">来源无效。</exception>
	/// <exception cref="ArgumentNullException">修正集合为空。</exception>
	public void SetSource(StatSource source, IReadOnlyList<StatModifier> modifiers)
	{
		if (source.Category == null)
		{
			throw new ArgumentException("属性来源无效。", nameof(source));
		}

		ArgumentNullException.ThrowIfNull(modifiers);

		// 复制调用方集合，之后外部修改不会影响已登记的修正。
		StatModifier[] copy = new StatModifier[modifiers.Count];
		for (int i = 0; i < copy.Length; i++)
		{
			copy[i] = modifiers[i];
		}

		m_Sources[source] = copy;
		MarkDirty();
	}

	/// <summary>移除一个来源的全部修正。</summary>
	/// <param name="source">修正来源。</param>
	/// <returns>来源是否存在并已移除。</returns>
	public bool RemoveSource(StatSource source)
	{
		if (!m_Sources.Remove(source))
		{
			return false;
		}

		MarkDirty();
		return true;
	}

	/// <summary>清空基础值和全部修正（池化实体复用前调用）。</summary>
	public void Clear()
	{
		Array.Clear(m_Base);
		m_Sources.Clear();
		MarkDirty();
	}

	/// <summary>读取最终值（浮点）。</summary>
	/// <param name="stat">属性种类。</param>
	/// <returns>汇总后的最终值，下限为 0。</returns>
	public float Get(StatType stat)
	{
		if (m_Dirty)
		{
			Recalculate();
		}

		return m_Final[(int)stat];
	}

	/// <summary>读取最终值并四舍五入为整数（生命、攻防等整数属性）。</summary>
	/// <param name="stat">属性种类。</param>
	/// <returns>四舍五入（远离零）后的最终值。</returns>
	public int GetInt(StatType stat) => (int)MathF.Round(Get(stat), MidpointRounding.AwayFromZero);

	/// <summary>标记需要重算并通知订阅者。</summary>
	private void MarkDirty()
	{
		m_Dirty = true;
		Changed?.Invoke();
	}

	/// <summary>按修正方式分组汇总全部来源，刷新最终值缓存。</summary>
	private void Recalculate()
	{
		// 分三类累加：固定值相加、同类百分比相加、独立百分比连乘。
		Span<float> flat = stackalloc float[StatCount];
		Span<float> percentAdd = stackalloc float[StatCount];
		Span<float> percentMult = stackalloc float[StatCount];
		percentMult.Fill(1f);
		foreach (StatModifier[] modifiers in m_Sources.Values)
		{
			foreach (StatModifier modifier in modifiers)
			{
				int index = (int)modifier.Stat;
				switch (modifier.Kind)
				{
					case StatModifierKind.Flat:
						flat[index] += modifier.Value;
						break;
					case StatModifierKind.PercentAdd:
						percentAdd[index] += modifier.Value;
						break;
					case StatModifierKind.PercentMult:
						percentMult[index] *= 1f + modifier.Value;
						break;
				}
			}
		}

		// 属性没有负值语义，最终值统一下限为 0。
		for (int i = 0; i < StatCount; i++)
		{
			m_Final[i] = MathF.Max(0f, (m_Base[i] + flat[i]) * (1f + percentAdd[i]) * percentMult[i]);
		}

		m_Dirty = false;
	}
}
