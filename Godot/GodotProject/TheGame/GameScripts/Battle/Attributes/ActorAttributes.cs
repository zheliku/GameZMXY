using System;
using System.Collections.Generic;
using GameConfig.Attributes;

namespace GameLogic.Battle.Attributes;

/// <summary>
/// 一名角色的属性汇总：基础值（成长表或怪物配置）+ 按来源登记的修正（装备、法宝、被动、丹药、Buff……）。
/// 最终值 = (基础 + Σ固定值) × (1 + Σ同类百分比) × Π(1 + 独立百分比)。
/// 修正按来源整体登记和移除，卸下装备或 Buff 结束时不会残留；读取时按脏标记惰性重算。
/// 纯 C#，不引用节点和框架服务；由实体持有，生命周期与实体显示一致。
/// </summary>
public sealed class ActorAttributes
{
	private static readonly int AttributeCount = Enum.GetValues<AttributeType>().Length; // 属性种类数，枚举值从 0 连续。

	private readonly float[] m_Base = new float[AttributeCount]; // 基础值，按 AttributeType 索引。

	private readonly float[] m_Final = new float[AttributeCount]; // 最近一次重算的最终值缓存。

	private readonly Dictionary<AttributeSourceId, AttributeModifier[]> m_Sources = new(); // 来源到其全部修正的登记表。

	private bool m_Dirty = true; // 基础值或修正变化后需要重算。

	/// <summary>最终生命上限。</summary>
	public int MaxHp => GetInt(AttributeType.MaxHp);

	/// <summary>最终魔法上限。</summary>
	public int MaxMp => GetInt(AttributeType.MaxMp);

	/// <summary>最终攻击。</summary>
	public int Power => GetInt(AttributeType.Power);

	/// <summary>最终防御。</summary>
	public int Def => GetInt(AttributeType.Def);

	/// <summary>最终魔抗。</summary>
	public int Mdef => GetInt(AttributeType.Mdef);

	/// <summary>最终暴击点数。</summary>
	public int Crit => GetInt(AttributeType.Crit);

	/// <summary>最终闪避点数。</summary>
	public int Miss => GetInt(AttributeType.Miss);

	/// <summary>最终幸运。</summary>
	public int Lucky => GetInt(AttributeType.Lucky);

	/// <summary>最终韧性。</summary>
	public int Toughness => GetInt(AttributeType.Toughness);

	/// <summary>最终命中。</summary>
	public int Htarget => GetInt(AttributeType.Htarget);

	/// <summary>最终抗暴。</summary>
	public int CritReduce => GetInt(AttributeType.CritReduce);

	/// <summary>最终破甲。</summary>
	public int Ar => GetInt(AttributeType.Ar);

	/// <summary>最终破魔。</summary>
	public int Sp => GetInt(AttributeType.Sp);

	/// <summary>最终吸血百分点。</summary>
	public int Vampirism => GetInt(AttributeType.Vampirism);

	/// <summary>最终每秒回血。</summary>
	public int HpRegen => GetInt(AttributeType.HpRegen);

	/// <summary>最终每秒回蓝。</summary>
	public int MpRegen => GetInt(AttributeType.MpRegen);

	/// <summary>用一组完整属性替换基础值；不影响已登记的修正。</summary>
	/// <param name="block">成长表或怪物配置中的属性块。</param>
	/// <exception cref="ArgumentNullException">属性块为空。</exception>
	public void SetBase(ActorBaseAttributes block)
	{
		ArgumentNullException.ThrowIfNull(block);

		// 按枚举顺序整体覆盖，生成类型的字段与枚举一一对应。
		m_Base[(int)AttributeType.MaxHp] = block.MaxHp;
		m_Base[(int)AttributeType.MaxMp] = block.MaxMp;
		m_Base[(int)AttributeType.Power] = block.Power;
		m_Base[(int)AttributeType.Def] = block.Def;
		m_Base[(int)AttributeType.Mdef] = block.Mdef;
		m_Base[(int)AttributeType.Crit] = block.Crit;
		m_Base[(int)AttributeType.Miss] = block.Miss;
		m_Base[(int)AttributeType.Lucky] = block.Lucky;
		m_Base[(int)AttributeType.Toughness] = block.Toughness;
		m_Base[(int)AttributeType.Htarget] = block.Htarget;
		m_Base[(int)AttributeType.CritReduce] = block.CritReduce;
		m_Base[(int)AttributeType.Ar] = block.Ar;
		m_Base[(int)AttributeType.Sp] = block.Sp;
		m_Base[(int)AttributeType.Vampirism] = block.Vampirism;
		m_Base[(int)AttributeType.HpRegen] = block.HpRegen;
		m_Base[(int)AttributeType.MpRegen] = block.MpRegen;
		m_Dirty = true;
	}

	/// <summary>登记一个来源的全部修正；同一来源已存在时整体替换。</summary>
	/// <param name="source">修正来源。</param>
	/// <param name="modifiers">该来源提供的修正，可为空集合。</param>
	/// <exception cref="ArgumentException">来源无效。</exception>
	/// <exception cref="ArgumentNullException">修正集合为空。</exception>
	public void SetSource(AttributeSourceId source, IReadOnlyList<AttributeModifier> modifiers)
	{
		if (source.Category == null)
		{
			throw new ArgumentException("属性来源无效。", nameof(source));
		}

		ArgumentNullException.ThrowIfNull(modifiers);

		// 复制调用方集合，之后外部修改不会影响已登记的修正。
		AttributeModifier[] copy = new AttributeModifier[modifiers.Count];
		for (int i = 0; i < copy.Length; i++)
		{
			copy[i] = modifiers[i];
		}

		m_Sources[source] = copy;
		m_Dirty = true;
	}

	/// <summary>移除一个来源的全部修正。</summary>
	/// <param name="source">修正来源。</param>
	/// <returns>来源是否存在并已移除。</returns>
	public bool RemoveSource(AttributeSourceId source)
	{
		if (!m_Sources.Remove(source))
		{
			return false;
		}

		m_Dirty = true;
		return true;
	}

	/// <summary>清空基础值和全部修正（池化实体复用前调用）。</summary>
	public void Clear()
	{
		Array.Clear(m_Base);
		m_Sources.Clear();
		m_Dirty = true;
	}

	/// <summary>读取最终值（浮点）。</summary>
	/// <param name="attribute">属性种类。</param>
	/// <returns>汇总后的最终值，下限为 0。</returns>
	public float Get(AttributeType attribute)
	{
		if (m_Dirty)
		{
			Recalculate();
		}

		return m_Final[(int)attribute];
	}

	/// <summary>读取最终值并四舍五入为整数（生命、攻防等整数属性）。</summary>
	/// <param name="attribute">属性种类。</param>
	/// <returns>四舍五入（远离零）后的最终值。</returns>
	public int GetInt(AttributeType attribute) => (int)MathF.Round(Get(attribute), MidpointRounding.AwayFromZero);

	/// <summary>按修正方式分组汇总全部来源，刷新最终值缓存。</summary>
	private void Recalculate()
	{
		// 分三类累加：固定值相加、同类百分比相加、独立百分比连乘。
		Span<float> flat = stackalloc float[AttributeCount];
		Span<float> percentAdd = stackalloc float[AttributeCount];
		Span<float> percentMult = stackalloc float[AttributeCount];
		percentMult.Fill(1f);
		foreach (AttributeModifier[] modifiers in m_Sources.Values)
		{
			foreach (AttributeModifier modifier in modifiers)
			{
				int index = (int)modifier.Attribute;
				switch (modifier.Kind)
				{
					case AttributeModifierKind.Flat:
						flat[index] += modifier.Value;
						break;
					case AttributeModifierKind.PercentAdd:
						percentAdd[index] += modifier.Value;
						break;
					case AttributeModifierKind.PercentMult:
						percentMult[index] *= 1f + modifier.Value;
						break;
				}
			}
		}

		// 属性没有负值语义，最终值统一下限为 0。
		for (int i = 0; i < AttributeCount; i++)
		{
			m_Final[i] = MathF.Max(0f, (m_Base[i] + flat[i]) * (1f + percentAdd[i]) * percentMult[i]);
		}

		m_Dirty = false;
	}
}
