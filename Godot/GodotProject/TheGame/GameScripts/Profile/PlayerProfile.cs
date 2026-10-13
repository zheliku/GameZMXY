using System;
using System.Collections.Generic;

namespace GameLogic.Profile;

/// <summary>
/// 一个存档槽的运行时状态：拥有英雄成长、钱包和装备背包。
/// 从读档到返回标题期间保持同一实例；纯 C#，不负责文件读写。
/// </summary>
public sealed class PlayerProfile
{
	private readonly Dictionary<int, HeroProgression> m_Heroes = new(); // 已拥有的英雄，按 HeroId 索引。
	private readonly Dictionary<int, HeroEquipment> m_Equipment = new(); // 每名已拥有英雄的穿戴栏。

	/// <summary>当前出战英雄 ID。</summary>
	public int ActiveHeroId { get; }

	/// <summary>钱包。</summary>
	public Wallet Wallet { get; }

	/// <summary>本槽共享的装备背包。</summary>
	public Inventory Inventory { get; }

	/// <summary>已拥有的英雄记录，按 HeroId 索引。</summary>
	public IReadOnlyDictionary<int, HeroProgression> Heroes => m_Heroes;

	/// <summary>当前出战英雄的记录。</summary>
	public HeroProgression ActiveHero => m_Heroes[ActiveHeroId];

	/// <summary>当前出战英雄的穿戴栏。</summary>
	public HeroEquipment ActiveEquipment => m_Equipment[ActiveHeroId];

	/// <summary>读取已拥有英雄的穿戴栏。</summary>
	/// <param name="heroId">已拥有英雄编号。</param>
	/// <returns>该英雄权威的穿戴状态。</returns>
	public HeroEquipment GetEquipment(int heroId) => m_Equipment[heroId];

	/// <summary>创建档案并校验出战英雄已拥有。</summary>
	/// <param name="activeHeroId">出战英雄 ID。</param>
	/// <param name="heroes">已拥有的英雄记录，HeroId 不能重复。</param>
	/// <param name="wallet">钱包。</param>
	/// <param name="inventory">本槽拥有的装备背包。</param>
	/// <exception cref="ArgumentNullException">参数为空。</exception>
	/// <exception cref="ArgumentException">英雄重复或出战英雄不在列表中。</exception>
	public PlayerProfile(int activeHeroId, IEnumerable<HeroProgression> heroes, Wallet wallet, Inventory inventory)
	{
		ArgumentNullException.ThrowIfNull(heroes);
		Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
		Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));

		// 建立英雄索引；重复 ID 说明存档或建档规则有误，直接拒绝。
		foreach (HeroProgression hero in heroes)
		{
			ArgumentNullException.ThrowIfNull(hero, nameof(heroes));
			if (!m_Heroes.TryAdd(hero.HeroId, hero))
			{
				throw new ArgumentException($"英雄 {hero.HeroId} 重复。", nameof(heroes));
			}

			m_Equipment.Add(hero.HeroId, new HeroEquipment());
		}

		if (!m_Heroes.ContainsKey(activeHeroId))
		{
			throw new ArgumentException($"出战英雄 {activeHeroId} 不在已拥有列表中。", nameof(activeHeroId));
		}

		ActiveHeroId = activeHeroId;
	}
}
