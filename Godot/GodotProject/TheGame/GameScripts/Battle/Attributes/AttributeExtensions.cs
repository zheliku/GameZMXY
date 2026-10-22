using System;
using GameConfig.Attributes;

namespace GameLogic.Battle.Attributes;

/// <summary>读取生成的基础属性，不修改 Luban 生成类型。</summary>
public static class AttributeExtensions
{
    /// <summary>按配置属性种类读取对应的具名字段。</summary>
    /// <param name="attributes">只读基础属性。</param>
    extension(ActorBaseAttributes attributes)
    {
        /// <summary>获取指定基础值。</summary>
        /// <param name="attribute">属性种类。</param>
        /// <returns>配置值；概率使用 0..1。</returns>
        public float Get(AttributeType attribute) => attribute switch
        {
            AttributeType.MaxHp => attributes.MaxHp,
            AttributeType.MaxMp => attributes.MaxMp,
            AttributeType.Power => attributes.Power,
            AttributeType.Def => attributes.Def,
            AttributeType.Mdef => attributes.Mdef,
            AttributeType.Crit => attributes.Crit,
            AttributeType.Miss => attributes.Miss,
            AttributeType.Lucky => attributes.Lucky,
            AttributeType.Toughness => attributes.Toughness,
            AttributeType.Htarget => attributes.Htarget,
            AttributeType.CritReduce => attributes.CritReduce,
            AttributeType.Ar => attributes.Ar,
            AttributeType.Sp => attributes.Sp,
            AttributeType.Vampirism => attributes.Vampirism,
            AttributeType.HpRegen => attributes.HpRegen,
            AttributeType.MpRegen => attributes.MpRegen,
            _ => throw new ArgumentOutOfRangeException(nameof(attribute))
        };
    }

    /// <summary>属性单位与整数、概率两类取值的共同约定。</summary>
    /// <param name="attribute">统一属性种类。</param>
    extension(AttributeType attribute)
    {
        /// <summary>是否使用 0..1 的直接概率；命中与抗暴以相同单位抵消对手。</summary>
        public bool IsProbability => attribute is AttributeType.Crit or AttributeType.Miss
            or AttributeType.Htarget or AttributeType.CritReduce;
    }

}
