using System;
using System.Collections.Generic;
using GameConfig.Hero;
using GameLogic.Battle.Stats;

namespace GameLogic.Profile;

/// <summary>
/// 把档案中的英雄记录换算为出战装配：查逐级成长表得到基础属性，汇总装备、法宝、被动、丹药等持久修正。
/// 纯函数式规则，不持有状态；新的持久系统只需在这里登记自己的来源，不需要改实体。
/// </summary>
public sealed class HeroStatBuilder
{
	private static readonly IReadOnlyList<KeyValuePair<StatSource, IReadOnlyList<StatModifier>>> NoModifiers =
		Array.Empty<KeyValuePair<StatSource, IReadOnlyList<StatModifier>>>(); // 本轮尚无持久修正来源。

	private readonly TbHeroGrowthConfig m_Growth; // 逐级成长表（启动期已校验覆盖全部等级）。

	/// <summary>创建换算规则。</summary>
	/// <param name="growth">逐级成长表。</param>
	/// <exception cref="ArgumentNullException">成长表为空。</exception>
	public HeroStatBuilder(TbHeroGrowthConfig growth)
	{
		m_Growth = growth ?? throw new ArgumentNullException(nameof(growth));
	}

	/// <summary>按英雄当前等级构建出战装配。</summary>
	/// <param name="hero">档案中的英雄记录。</param>
	/// <returns>实体显示所需的不可变装配。</returns>
	/// <exception cref="ArgumentNullException">英雄记录为空。</exception>
	/// <exception cref="InvalidOperationException">成长表缺少该英雄该等级（启动校验应已拦截）。</exception>
	public HeroLoadout Build(HeroRecord hero)
	{
		ArgumentNullException.ThrowIfNull(hero);
		int level = hero.Progression.Level;
		HeroGrowthConfig growth = m_Growth.Get(hero.HeroId, level)
			?? throw new InvalidOperationException($"成长表缺少英雄 {hero.HeroId} 的 {level} 级。");
		return new HeroLoadout(hero.HeroId, level, growth.Stats, NoModifiers);
	}
}
