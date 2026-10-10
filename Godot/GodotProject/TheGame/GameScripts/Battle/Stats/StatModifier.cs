using GameConfig.Stat;

namespace GameLogic.Battle.Stats;

/// <summary>一条属性修正：作用的属性、修正方式和数值；同一来源的全部修正一起登记和移除。</summary>
public readonly struct StatModifier
{
	/// <summary>被修正的属性。</summary>
	public readonly StatType Stat;

	/// <summary>修正方式：固定值、同类百分比相加或独立百分比相乘。</summary>
	public readonly StatModifierKind Kind;

	/// <summary>修正值；百分比类以小数表示（0.1 = +10%）。</summary>
	public readonly float Value;

	/// <summary>创建一条属性修正。</summary>
	/// <param name="stat">被修正的属性。</param>
	/// <param name="kind">修正方式。</param>
	/// <param name="value">修正值；百分比类以小数表示。</param>
	public StatModifier(StatType stat, StatModifierKind kind, float value)
	{
		Stat = stat;
		Kind = kind;
		Value = value;
	}

	/// <summary>创建固定值修正。</summary>
	/// <param name="stat">被修正的属性。</param>
	/// <param name="value">叠加到基础值上的固定量。</param>
	/// <returns>固定值修正。</returns>
	public static StatModifier Flat(StatType stat, float value) => new(stat, StatModifierKind.Flat, value);

	/// <summary>创建同类百分比修正（同属性的所有此类修正先相加再统一乘）。</summary>
	/// <param name="stat">被修正的属性。</param>
	/// <param name="value">百分比小数（0.1 = +10%）。</param>
	/// <returns>同类百分比修正。</returns>
	public static StatModifier PercentAdd(StatType stat, float value) => new(stat, StatModifierKind.PercentAdd, value);

	/// <summary>创建独立百分比修正（与其他修正逐个连乘）。</summary>
	/// <param name="stat">被修正的属性。</param>
	/// <param name="value">百分比小数（0.1 = ×1.1）。</param>
	/// <returns>独立百分比修正。</returns>
	public static StatModifier PercentMult(StatType stat, float value) => new(stat, StatModifierKind.PercentMult, value);
}
