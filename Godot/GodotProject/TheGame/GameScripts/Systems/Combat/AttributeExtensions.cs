using System;
using GameConfig.Attributes;

namespace GameLogic.Battle.Attributes
{
    /// <summary>
    /// 读取生成的基础属性，不修改 Luban 生成类型。
    /// </summary>
    public static class AttributeExtensions
    {
        /// <summary>
        /// 按配置属性种类读取对应的具名字段。
        /// </summary>
        /// <param name = "attributes">只读基础属性。</param>
        extension(ActorBaseAttributes attributes)
        {
            /// <summary>
            /// 获取指定基础值。
            /// </summary>
            /// <param name = "attribute">属性种类。</param>
            /// <returns>配置值；概率使用 0..1。</returns>
            public float Get(EAttributeType attribute)
            {
                return attribute switch
                {
                    EAttributeType.MaxHp => attributes.MaxHp,
                    EAttributeType.MaxMp => attributes.MaxMp,
                    EAttributeType.Power => attributes.Power,
                    EAttributeType.Def => attributes.Def,
                    EAttributeType.Mdef => attributes.Mdef,
                    EAttributeType.Crit => attributes.Crit,
                    EAttributeType.Miss => attributes.Miss,
                    EAttributeType.Lucky => attributes.Lucky,
                    EAttributeType.Toughness => attributes.Toughness,
                    EAttributeType.Htarget => attributes.Htarget,
                    EAttributeType.CritReduce => attributes.CritReduce,
                    EAttributeType.Ar => attributes.Ar,
                    EAttributeType.Sp => attributes.Sp,
                    EAttributeType.Vampirism => attributes.Vampirism,
                    EAttributeType.HpRegen => attributes.HpRegen,
                    EAttributeType.MpRegen => attributes.MpRegen,
                    _ => throw new ArgumentOutOfRangeException(nameof(attribute))
                };
            }
        }

        /// <summary>
        /// 属性单位与整数、概率两类取值的共同约定。
        /// </summary>
        /// <param name = "attribute">统一属性种类。</param>
        extension(EAttributeType attribute)
        {
            /// <summary>
            /// 是否使用 0..1 的直接概率；命中与抗暴以相同单位抵消对手。
            /// </summary>
            public bool IsProbability
            {
                get
                {
                    return attribute is EAttributeType.Crit or EAttributeType.Miss or EAttributeType.Htarget or EAttributeType.CritReduce;
                }
            }
        }
    }
}
