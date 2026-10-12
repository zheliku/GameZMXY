using System;
using GameConfig.Hero;
using GameLogic.Battle.Attributes;
using GameLogic.Profile;

namespace GameLogic.Equipment;

/// <summary>英雄装配入口：从档案与配置构建战斗输入；后续装备修正在这里汇入，不交给窗口或实体查档案。</summary>
public sealed class EquipmentModule
{
    private readonly TbHeroGrowthConfig m_Growth; // 已在启动阶段验证的成长配置。

    /// <summary>创建档案作用域内的装配模块。</summary>
    /// <param name="growth">完整的英雄成长表。</param>
    /// <exception cref="ArgumentNullException">成长表为空。</exception>
    public EquipmentModule(TbHeroGrowthConfig growth) =>
        m_Growth = growth ?? throw new ArgumentNullException(nameof(growth));

    /// <summary>按英雄当前成长构建独立于后续档案变化的战斗输入。</summary>
    /// <param name="hero">档案拥有的英雄成长。</param>
    /// <returns>身份、等级与成长属性的不可变输入。</returns>
    /// <exception cref="ArgumentNullException">英雄为空。</exception>
    /// <exception cref="InvalidOperationException">配置缺少对应成长行。</exception>
    public HeroLoadout CreateLoadout(HeroProgression hero)
    {
        ArgumentNullException.ThrowIfNull(hero);
        HeroGrowthConfig row = m_Growth.Get(hero.HeroId, hero.Level)
            ?? throw new InvalidOperationException($"成长表缺少英雄 {hero.HeroId} 的 {hero.Level} 级。");
        return new HeroLoadout(hero.HeroId, hero.Level, row.Stats);
    }
}
