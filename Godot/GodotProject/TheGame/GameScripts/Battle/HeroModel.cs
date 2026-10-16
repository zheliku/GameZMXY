using System;
using System.Collections.Generic;
using GameConfig.Attributes;
using GameLogic.Architecture;
using GameLogic.Battle.Attributes;
using GameLogic.Battle.Damage;

namespace GameLogic.Battle;

/// <summary>本局英雄的权威数值；业务规则与完整提交由 HeroSystem 执行。</summary>
public sealed class HeroModel : AbstractModel
{
    /// <summary>本局英雄身份。</summary>
    public int HeroId { get; internal set; }

    /// <summary>当前成长等级。</summary>
    public BindableProperty<int> Level { get; } = new(1);

    /// <summary>当前生命。</summary>
    public BindableProperty<int> Hp { get; } = new();

    /// <summary>当前魔法。</summary>
    public BindableProperty<int> Mp { get; } = new();

    /// <summary>生命已归零。</summary>
    public bool IsDepleted => Hp.Value <= 0;

    /// <summary>最终生命上限。</summary>
    public BindableProperty<int> MaxHp { get; } = new();

    /// <summary>最终魔法上限。</summary>
    public BindableProperty<int> MaxMp { get; } = new();

    /// <summary>最终攻击。</summary>
    public BindableProperty<int> Power { get; } = new();

    /// <summary>最终物防。</summary>
    public BindableProperty<int> Def { get; } = new();

    /// <summary>最终魔防。</summary>
    public BindableProperty<int> Mdef { get; } = new();

    /// <summary>最终暴击。</summary>
    public BindableProperty<int> Crit { get; } = new();

    /// <summary>最终闪避。</summary>
    public BindableProperty<int> Miss { get; } = new();

    /// <summary>最终幸运。</summary>
    public BindableProperty<int> Lucky { get; } = new();

    /// <summary>最终韧性。</summary>
    public BindableProperty<int> Toughness { get; } = new();

    /// <summary>最终命中。</summary>
    public BindableProperty<int> Htarget { get; } = new();

    /// <summary>最终抗暴。</summary>
    public BindableProperty<int> CritReduce { get; } = new();

    /// <summary>最终破甲。</summary>
    public BindableProperty<int> Ar { get; } = new();

    /// <summary>最终破魔。</summary>
    public BindableProperty<int> Sp { get; } = new();

    /// <summary>最终吸血百分点。</summary>
    public BindableProperty<int> Vampirism { get; } = new();

    /// <summary>最终每秒回血。</summary>
    public BindableProperty<int> HpRegen { get; } = new();

    /// <summary>最终每秒回蓝。</summary>
    public BindableProperty<int> MpRegen { get; } = new();

    /// <summary>当前无双值。</summary>
    public BindableProperty<int> Ws { get; } = new();

    /// <summary>本局无双上限。</summary>
    public BindableProperty<int> MaxWs { get; } = new();

    /// <summary>是否已开启无双。</summary>
    public BindableProperty<bool> WsActive { get; } = new();

    /// <summary>尚未开启且已经蓄满。</summary>
    public bool IsWsFull => !WsActive.Value && MaxWs.Value > 0 && Ws.Value >= MaxWs.Value;

    /// <summary>当前等级的只读基础记录，由英雄系统替换。</summary>
    internal ActorBaseAttributes BaseAttributes { get; set; }

    /// <summary>本局修正来源，系统存入副本；不向 UI 借出可写集合。</summary>
    internal Dictionary<AttributeSourceId, AttributeModifier[]> Sources { get; } = new();

    /// <summary>开启消耗不足一点的累计量。</summary>
    internal double WsDrainProgress { get; set; }

    /// <summary>开启后的额外点数/秒。</summary>
    internal float WsDrainPerSecond { get; set; }

    /// <summary>自然衰减不足一点的累计量。</summary>
    internal double WsDecayProgress { get; set; }

    /// <summary>未满时的自然衰减点数/秒。</summary>
    internal float WsDecayPerSecond { get; set; }

    /// <summary>读取指定属性的同一个绑定对象，不复制数据。</summary>
    /// <param name="attribute">属性种类。</param>
    /// <returns>对应的只读属性。</returns>
    public BindableProperty<int> GetAttribute(AttributeType attribute) => attribute switch
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

    /// <summary>冻结出招/受击时的最终值，已发出的攻击不随后续属性变化。</summary>
    /// <param name="actorId">来源实体编号。</param>
    /// <returns>伤害公式使用的不可变快照。</returns>
    public CombatantAttributes CaptureCombatAttributes(int? actorId = null) => new(CombatSide.Hero,
        Level.Value, Power.Value, Def.Value, Mdef.Value, Crit.Value, Miss.Value, Lucky.Value,
        Toughness.Value, Htarget.Value, CritReduce.Value, Ar.Value, Sp.Value, actorId);
}
