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

	/// <summary>持久修正（装备、法宝、被动、丹药……）的只读快照，按来源分组。</summary>
	public IReadOnlyList<KeyValuePair<StatSource, IReadOnlyList<StatModifier>>> PersistentModifiers { get; }

	/// <summary>创建出战装配。</summary>
	/// <param name="heroId">英雄 ID。</param>
	/// <param name="level">等级。</param>
	/// <param name="growthStats">该等级成长属性。</param>
	/// <param name="persistentModifiers">按来源分组的持久修正。</param>
	/// <exception cref="ArgumentNullException">成长属性或修正列表为空。</exception>
	/// <exception cref="ArgumentException">修正来源无效或其中一组修正为空。</exception>
	public HeroLoadout(int heroId, int level, StatBlock growthStats,
		IReadOnlyList<KeyValuePair<StatSource, IReadOnlyList<StatModifier>>> persistentModifiers)
	{
		HeroId = heroId;
		Level = level;
		GrowthStats = growthStats ?? throw new ArgumentNullException(nameof(growthStats));
		ArgumentNullException.ThrowIfNull(persistentModifiers);

		// 装配一旦构建就不受外部列表修改影响，连同每个来源下的修正一起复制。
		var entries = new KeyValuePair<StatSource, IReadOnlyList<StatModifier>>[persistentModifiers.Count];
		for (int i = 0; i < entries.Length; i++)
		{
			var (source, modifiers) = persistentModifiers[i];
			if (source.Category == null || modifiers == null)
			{
				throw new ArgumentException("持久修正必须包含有效来源和修正集合。", nameof(persistentModifiers));
			}

			StatModifier[] copy = new StatModifier[modifiers.Count];
			for (int j = 0; j < copy.Length; j++)
			{
				copy[j] = modifiers[j];
			}

			entries[i] = new(source, Array.AsReadOnly(copy));
		}

		PersistentModifiers = Array.AsReadOnly(entries);
	}

	/// <summary>把成长基础值与全部持久修正写入属性汇总；不触碰其他来源（如 Buff）。</summary>
	/// <param name="stats">实体的属性汇总。</param>
	/// <param name="previousSources">上次登记的持久来源；首次应用可传空，无需保留上一份装配。</param>
	/// <returns>本次登记的来源标识，交给调用方持有，下次替换装配时传回。</returns>
	/// <exception cref="ArgumentNullException">属性汇总为空。</exception>
	public StatSource[] ApplyTo(StatSheet stats, IReadOnlyList<StatSource> previousSources)
	{
		ArgumentNullException.ThrowIfNull(stats);

		// 先移除上一次装配登记的持久来源，再登记本次的，避免卸下的装备残留。
		if (previousSources != null)
		{
			foreach (StatSource source in previousSources)
			{
				stats.RemoveSource(source);
			}
		}

		stats.SetBase(GrowthStats);
		StatSource[] sources = PersistentModifiers.Count == 0 ? [] : new StatSource[PersistentModifiers.Count];
		for (int i = 0; i < sources.Length; i++)
		{
			KeyValuePair<StatSource, IReadOnlyList<StatModifier>> entry = PersistentModifiers[i];
			stats.SetSource(entry.Key, entry.Value);
			sources[i] = entry.Key;
		}

		return sources;
	}
}
