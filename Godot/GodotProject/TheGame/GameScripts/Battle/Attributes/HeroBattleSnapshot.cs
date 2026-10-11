using System;
using GameConfig.Hero;
using GameConfig.Stat;

namespace GameLogic.Battle.Attributes;

/// <summary>冻结英雄本次出战的身份、等级和成长属性；实体不持有档案引用。</summary>
public sealed class HeroBattleSnapshot
{
    /// <summary>HeroConfig 主键。</summary>
    public int HeroId { get; }

    /// <summary>参与等级压制的出战等级。</summary>
    public int Level { get; }

    /// <summary>该等级的只读成长属性。</summary>
    public StatBlock BaseAttributes { get; }

    /// <summary>冻结一份英雄战斗输入。</summary>
    /// <param name="heroId">英雄配置编号。</param>
    /// <param name="level">当前等级。</param>
    /// <param name="baseAttributes">已验证的只读成长属性。</param>
    /// <exception cref="ArgumentNullException">成长属性为空。</exception>
    public HeroBattleSnapshot(int heroId, int level, StatBlock baseAttributes)
    {
        HeroId = heroId;
        Level = level;
        BaseAttributes = baseAttributes ?? throw new ArgumentNullException(nameof(baseAttributes));
    }

    /// <summary>按英雄与等级读取成长表，创建本次战斗的输入快照。</summary>
    /// <param name="heroId">英雄配置编号。</param>
    /// <param name="level">当前等级。</param>
    /// <param name="growth">启动期已验证的成长表。</param>
    /// <returns>独立于档案后续变化的战斗输入。</returns>
    /// <exception cref="ArgumentNullException">成长表为空。</exception>
    /// <exception cref="InvalidOperationException">成长行不存在。</exception>
    public static HeroBattleSnapshot Create(int heroId, int level, TbHeroGrowthConfig growth)
    {
        ArgumentNullException.ThrowIfNull(growth);
        HeroGrowthConfig row = growth.Get(heroId, level)
            ?? throw new InvalidOperationException($"成长表缺少英雄 {heroId} 的 {level} 级。");
        return new HeroBattleSnapshot(heroId, level, row.Stats);
    }
}
