using System;
using System.Collections.Generic;

namespace GameLogic.Profile;

/// <summary>
/// 玩家档案：一个存档槽内全部持久状态的运行时所有者（英雄记录、钱包，以后的背包与世界进度）。
/// 由 <see cref="GameLogic.Session.GameContext"/> 持有，从读档到回到标题期间存在；
/// 纯 C#，不引用节点与框架服务，写盘只经 <see cref="GameLogic.Save.SaveService"/>。
/// </summary>
public sealed class PlayerProfile
{
	private readonly Dictionary<int, HeroRecord> m_Heroes = new(); // 已拥有的英雄，按 HeroId 索引。

	/// <summary>当前出战英雄 ID。</summary>
	public int ActiveHeroId { get; private set; }

	/// <summary>钱包。</summary>
	public Wallet Wallet { get; }

	/// <summary>已拥有的英雄记录，按 HeroId 索引。</summary>
	public IReadOnlyDictionary<int, HeroRecord> Heroes => m_Heroes;

	/// <summary>当前出战英雄的记录。</summary>
	public HeroRecord ActiveHero => m_Heroes[ActiveHeroId];

	/// <summary>创建档案并校验出战英雄已拥有。</summary>
	/// <param name="activeHeroId">出战英雄 ID。</param>
	/// <param name="heroes">已拥有的英雄记录，HeroId 不能重复。</param>
	/// <param name="wallet">钱包。</param>
	/// <exception cref="ArgumentNullException">参数为空。</exception>
	/// <exception cref="ArgumentException">英雄重复或出战英雄不在列表中。</exception>
	public PlayerProfile(int activeHeroId, IEnumerable<HeroRecord> heroes, Wallet wallet)
	{
		ArgumentNullException.ThrowIfNull(heroes);
		Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));

		// 建立英雄索引；重复 ID 说明存档或建档规则有误，直接拒绝。
		foreach (HeroRecord hero in heroes)
		{
			ArgumentNullException.ThrowIfNull(hero, nameof(heroes));
			if (!m_Heroes.TryAdd(hero.HeroId, hero))
			{
				throw new ArgumentException($"英雄 {hero.HeroId} 重复。", nameof(heroes));
			}
		}

		if (!m_Heroes.ContainsKey(activeHeroId))
		{
			throw new ArgumentException($"出战英雄 {activeHeroId} 不在已拥有列表中。", nameof(activeHeroId));
		}

		ActiveHeroId = activeHeroId;
	}
}
