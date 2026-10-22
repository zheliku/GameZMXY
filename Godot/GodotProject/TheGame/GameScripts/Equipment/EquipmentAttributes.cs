using System;
using System.Collections.Generic;
using GameConfig.Attributes;
using GameLogic.Battle.Attributes;
using System.Runtime.Serialization;

namespace GameLogic.Equipment;

/// <summary>一件装备已经抽出的基础属性；字段名就是属性含义，与表格行序、JSON 字段顺序无关。</summary>
[DataContract]
public sealed record EquipmentAttributes
{
    /// <summary>生命上限。</summary>
    [DataMember(IsRequired = true)]
    public int MaxHp { get; init; }

    /// <summary>魔法上限。</summary>
    [DataMember(IsRequired = true)]
    public int MaxMp { get; init; }

    /// <summary>攻击。</summary>
    [DataMember(IsRequired = true)]
    public int Power { get; init; }

    /// <summary>防御。</summary>
    [DataMember(IsRequired = true)]
    public int Def { get; init; }

    /// <summary>魔抗。</summary>
    [DataMember(IsRequired = true)]
    public int Mdef { get; init; }

    /// <summary>暴击。</summary>
    [DataMember(IsRequired = true)]
    public float Crit { get; init; }

    /// <summary>闪避。</summary>
    [DataMember(IsRequired = true)]
    public float Miss { get; init; }

    /// <summary>幸运。</summary>
    [DataMember(IsRequired = true)]
    public int Lucky { get; init; }

    /// <summary>韧性。</summary>
    [DataMember(IsRequired = true)]
    public int Toughness { get; init; }

    /// <summary>命中。</summary>
    [DataMember(IsRequired = true)]
    public float Htarget { get; init; }

    /// <summary>抗暴。</summary>
    [DataMember(IsRequired = true)]
    public float CritReduce { get; init; }

    /// <summary>破甲。</summary>
    [DataMember(IsRequired = true)]
    public int Ar { get; init; }

    /// <summary>破魔。</summary>
    [DataMember(IsRequired = true)]
    public int Sp { get; init; }

    /// <summary>吸血。</summary>
    [DataMember(IsRequired = true)]
    public int Vampirism { get; init; }

    /// <summary>回血。</summary>
    [DataMember(IsRequired = true)]
    public int HpRegen { get; init; }

    /// <summary>回蓝。</summary>
    [DataMember(IsRequired = true)]
    public int MpRegen { get; init; }

    /// <summary>按属性类型读取具名字段；不使用列表下标。</summary>
    /// <param name="attribute">统一的战斗属性类型。</param>
    /// <returns>该字段的实际值；概率使用 0..1。</returns>
    /// <exception cref="ArgumentOutOfRangeException">类型未定义。</exception>
    public float Get(AttributeType attribute) => attribute switch
    {
        AttributeType.MaxHp => MaxHp,
        AttributeType.MaxMp => MaxMp,
        AttributeType.Power => Power,
        AttributeType.Def => Def,
        AttributeType.Mdef => Mdef,
        AttributeType.Crit => Crit,
        AttributeType.Miss => Miss,
        AttributeType.Lucky => Lucky,
        AttributeType.Toughness => Toughness,
        AttributeType.Htarget => Htarget,
        AttributeType.CritReduce => CritReduce,
        AttributeType.Ar => Ar,
        AttributeType.Sp => Sp,
        AttributeType.Vampirism => Vampirism,
        AttributeType.HpRegen => HpRegen,
        AttributeType.MpRegen => MpRegen,
        _ => throw new ArgumentOutOfRangeException(nameof(attribute))
    };

    /// <summary>抽取时生成替换指定字段的新值对象，已生成的实例始终不可变。</summary>
    /// <param name="attribute">当前配置行指定的属性。</param>
    /// <param name="value">实际抽取值。</param>
    /// <returns>不修改原对象的新属性。</returns>
    /// <exception cref="ArgumentOutOfRangeException">类型未定义。</exception>
    internal EquipmentAttributes WithValue(AttributeType attribute, float value) => attribute switch
    {
        AttributeType.MaxHp => this with { MaxHp = (int)value },
        AttributeType.MaxMp => this with { MaxMp = (int)value },
        AttributeType.Power => this with { Power = (int)value },
        AttributeType.Def => this with { Def = (int)value },
        AttributeType.Mdef => this with { Mdef = (int)value },
        AttributeType.Crit => this with { Crit = value },
        AttributeType.Miss => this with { Miss = value },
        AttributeType.Lucky => this with { Lucky = (int)value },
        AttributeType.Toughness => this with { Toughness = (int)value },
        AttributeType.Htarget => this with { Htarget = value },
        AttributeType.CritReduce => this with { CritReduce = value },
        AttributeType.Ar => this with { Ar = (int)value },
        AttributeType.Sp => this with { Sp = (int)value },
        AttributeType.Vampirism => this with { Vampirism = (int)value },
        AttributeType.HpRegen => this with { HpRegen = (int)value },
        AttributeType.MpRegen => this with { MpRegen = (int)value },
        _ => throw new ArgumentOutOfRangeException(nameof(attribute))
    };

    /// <summary>把非零具名字段接入已有战斗修正管线，装备本身不保存这份派生列表。</summary>
    /// <returns>带明确类型的固定值修正。</returns>
    public IEnumerable<AttributeModifier> EnumerateModifiers()
    {
        foreach (AttributeType attribute in Enum.GetValues<AttributeType>())
        {
            float value = Get(attribute);
            if (value != 0)
                yield return new AttributeModifier(attribute, AttributeModifierKind.Flat, value);
        }
    }
}
