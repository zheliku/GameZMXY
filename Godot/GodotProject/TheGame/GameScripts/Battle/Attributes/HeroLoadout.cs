using System;
using GameConfig.Attributes;
using GameLogic.Equipment;

namespace GameLogic.Battle.Attributes;

/// <summary>冻结英雄本次出战的身份、等级和成长属性；实体不持有档案引用。</summary>
public sealed class HeroLoadout
{
    /// <summary>HeroConfig 主键。</summary>
    public int HeroId { get; }

    /// <summary>参与等级压制的出战等级。</summary>
    public int Level { get; }

    /// <summary>该等级的只读成长属性。</summary>
    public ActorBaseAttributes BaseAttributes { get; }

    /// <summary>冻结的武器实例或空。</summary>
    public EquipmentInstance Weapon { get; }

    /// <summary>冻结的防具实例或空。</summary>
    public EquipmentInstance Armor { get; }

    /// <summary>冻结一份英雄战斗输入。</summary>
    /// <param name="heroId">英雄配置编号。</param>
    /// <param name="level">当前等级。</param>
    /// <param name="baseAttributes">已验证的只读成长属性。</param>
    /// <param name="weapon">出战时穿戴的不可变武器实例。</param>
    /// <param name="armor">出战时穿戴的不可变防具实例。</param>
    /// <exception cref="ArgumentNullException">成长属性为空。</exception>
    public HeroLoadout(int heroId, int level, ActorBaseAttributes baseAttributes, EquipmentInstance weapon = null,
        EquipmentInstance armor = null)
    {
        HeroId = heroId;
        Level = level;
        BaseAttributes = baseAttributes ?? throw new ArgumentNullException(nameof(baseAttributes));
        Weapon = weapon;
        Armor = armor;
    }
}
