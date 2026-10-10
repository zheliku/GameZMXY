using System;
using System.Collections.Generic;
using GameConfig.Stat;
using GameLogic.Battle.Stats;

namespace GameLogic.Profile;

/// <summary>
/// 英雄出战装配：由档案换算出的、实体显示所需的全部持久属性输入（等级、成长基础值、持久修正）。
/// 不可变快照；升级或换装后重新构建并交给实体，实体只读取它，不反向修改档案。
/// </summary>
public sealed class HeroLoadout
{
	/// <summary>英雄 ID。</summary>
	public int HeroId { get; }

	/// <summary>等级（参与等级压制）。</summary>
	public int Level { get; }

	/// <summary>该等级的成长属性（StatSheet 的基础值）。</summary>
	public StatBlock GrowthStats { get; }

	/// <summary>持久修正（装备、法宝、被动、丹药……），按来源分组；本轮为空。</summary>
	public IReadOnlyList<KeyValuePair<StatSource, IReadOnlyList<StatModifier>>> PersistentModifiers { get; }

	/// <summary>创建出战装配。</summary>
	/// <param name="heroId">英雄 ID。</param>
	/// <param name="level">等级。</param>
	/// <param name="growthStats">该等级成长属性。</param>
	/// <param name="persistentModifiers">按来源分组的持久修正。</param>
	/// <exception cref="ArgumentNullException">成长属性或修正列表为空。</exception>
	public HeroLoadout(int heroId, int level, StatBlock growthStats,
		IReadOnlyList<KeyValuePair<StatSource, IReadOnlyList<StatModifier>>> persistentModifiers)
	{
		HeroId = heroId;
		Level = level;
		GrowthStats = growthStats ?? throw new ArgumentNullException(nameof(growthStats));
		PersistentModifiers = persistentModifiers ?? throw new ArgumentNullException(nameof(persistentModifiers));
	}

	/// <summary>把成长基础值与全部持久修正写入属性汇总；不触碰其他来源（如 Buff）。</summary>
	/// <param name="stats">实体的属性汇总。</param>
	/// <param name="previous">上一次应用的装配，用于移除已不存在的持久来源；首次应用传空。</param>
	/// <exception cref="ArgumentNullException">属性汇总为空。</exception>
	public void ApplyTo(StatSheet stats, HeroLoadout previous)
	{
		ArgumentNullException.ThrowIfNull(stats);

		// 先移除上一次装配登记的持久来源，再登记本次的，避免卸下的装备残留。
		if (previous != null)
		{
			foreach (KeyValuePair<StatSource, IReadOnlyList<StatModifier>> entry in previous.PersistentModifiers)
			{
				stats.RemoveSource(entry.Key);
			}
		}

		stats.SetBase(GrowthStats);
		foreach (KeyValuePair<StatSource, IReadOnlyList<StatModifier>> entry in PersistentModifiers)
		{
			stats.SetSource(entry.Key, entry.Value);
		}
	}
}
