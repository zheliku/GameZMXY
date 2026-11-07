using System.Collections.Generic;
using GameConfig.Attributes;
using GameConfig.Skills;
using GameLogic.Architecture;
using GameLogic.Battle.Attributes;
using GameLogic.Battle.Damage;

namespace GameLogic.Battle;

/// <summary>
/// 本局英雄唯一的数值与状态；HeroSystem、HeroStatusSystem 和 SkillSystem 分别维护各自规则。
/// </summary>
public sealed class HeroModel : AbstractModel
{
    /// <summary>
    /// 当前英雄独占的状态效果；HeroStatusSystem 维护，同英雄模型一起创建和结束。
    /// </summary>
    private readonly Dictionary<StatusEffectKind, StatusEffectState> _statusEffects = new(); // 英雄效果运行时数据，由 HeroStatusSystem 维护。

    /// <summary>
    /// 当前英雄的效果数据只读视图。
    /// </summary>
    public IReadOnlyDictionary<StatusEffectKind, StatusEffectState> StatusEffects => _statusEffects;

    /// <summary>
    /// 读取一种当前英雄效果。
    /// </summary>
    /// <param name="kind">效果类别。</param>
    /// <returns>效果状态；未施加时为 null。</returns>
    public StatusEffectState GetStatusEffect(StatusEffectKind kind) => _statusEffects.GetValueOrDefault(kind);

    /// <summary>
    /// 写入已经由 HeroStatusSystem 校验的效果状态。
    /// </summary>
    /// <param name="state">效果状态。</param>
    internal void SetStatusEffect(StatusEffectState state) => _statusEffects[state.Kind] = state;

    /// <summary>
    /// 移除一种效果。
    /// </summary>
    /// <param name="kind">效果类别。</param>
    internal void RemoveStatusEffect(StatusEffectKind kind) => _statusEffects.Remove(kind);

    /// <summary>
    /// 清空本局英雄效果。
    /// </summary>
    internal void ClearStatusEffects() => _statusEffects.Clear();

    /// <summary>
    /// 本局英雄身份。
    /// </summary>
    public int HeroId { get; internal set; }

    /// <summary>
    /// 当前成长等级。
    /// </summary>
    public BindableProperty<int> Level { get; } = new(1);

    /// <summary>
    /// 当前生命。
    /// </summary>
    public BindableProperty<int> Hp { get; } = new();

    /// <summary>
    /// 当前魔法。
    /// </summary>
    public BindableProperty<int> Mp { get; } = new();

    /// <summary>
    /// 生命已归零。
    /// </summary>
    public bool IsDepleted => Hp.Value <= 0;

    /// <summary>
    /// 最终生命上限。
    /// </summary>
    public BindableProperty<int> MaxHp { get; } = new();

    /// <summary>
    /// 最终魔法上限。
    /// </summary>
    public BindableProperty<int> MaxMp { get; } = new();

    /// <summary>
    /// 最终攻击。
    /// </summary>
    public BindableProperty<int> Power { get; } = new();

    /// <summary>
    /// 最终物防。
    /// </summary>
    public BindableProperty<int> Def { get; } = new();

    /// <summary>
    /// 最终魔防。
    /// </summary>
    public BindableProperty<int> Mdef { get; } = new();

    /// <summary>
    /// 最终暴击。
    /// </summary>
    public BindableProperty<float> Crit { get; } = new();

    /// <summary>
    /// 最终闪避。
    /// </summary>
    public BindableProperty<float> Miss { get; } = new();

    /// <summary>
    /// 最终幸运。
    /// </summary>
    public BindableProperty<int> Lucky { get; } = new();

    /// <summary>
    /// 最终韧性。
    /// </summary>
    public BindableProperty<int> Toughness { get; } = new();

    /// <summary>
    /// 最终命中。
    /// </summary>
    public BindableProperty<float> Htarget { get; } = new();

    /// <summary>
    /// 最终抗暴。
    /// </summary>
    public BindableProperty<float> CritReduce { get; } = new();

    /// <summary>
    /// 最终破甲。
    /// </summary>
    public BindableProperty<int> Ar { get; } = new();

    /// <summary>
    /// 最终破魔。
    /// </summary>
    public BindableProperty<int> Sp { get; } = new();

    /// <summary>
    /// 最终吸血百分点。
    /// </summary>
    public BindableProperty<int> Vampirism { get; } = new();

    /// <summary>
    /// 最终每秒回血。
    /// </summary>
    public BindableProperty<int> HpRegen { get; } = new();

    /// <summary>
    /// 最终每秒回蓝。
    /// </summary>
    public BindableProperty<int> MpRegen { get; } = new();

    /// <summary>
    /// 当前无双值。
    /// </summary>
    public BindableProperty<int> Ws { get; } = new();

    /// <summary>
    /// 本局无双上限。
    /// </summary>
    public BindableProperty<int> MaxWs { get; } = new();

    /// <summary>
    /// 是否已开启无双。
    /// </summary>
    public BindableProperty<bool> WsActive { get; } = new();

    /// <summary>
    /// 尚未开启且已经蓄满。
    /// </summary>
    public bool IsWsFull => !WsActive.Value && MaxWs.Value > 0 && Ws.Value >= MaxWs.Value;

    /// <summary>
    /// 连续命中的累计次数；只由英雄受击保护规则维护，不写入存档。
    /// </summary>
    internal int HurtHitCount { get; set; }

    /// <summary>
    /// 连续命中记录距清零的剩余游戏秒数。
    /// </summary>
    internal double HurtChainSeconds { get; set; }

    /// <summary>
    /// 连续受击保护的剩余游戏秒数，与技能无敌区间独立。
    /// </summary>
    internal double HurtInvulnerabilitySeconds { get; set; }

    /// <summary>
    /// 流程转场等场景阶段是否开放玩家控制；动作、重力与状态时钟仍继续。
    /// </summary>
    public bool ControlsEnabled { get; internal set; } = true;

    /// <summary>
    /// 装备自然恢复的累计游戏秒数，暂停不推进，本局重建时归零。
    /// </summary>
    internal double RecoveryProgress { get; set; }

    /// <summary>
    /// 本局当前激活的技能编号；招式阶段与旧回调有效性归具体 GGF 状态。
    /// </summary>
    public SkillId? ActiveSkillId { get; internal set; }

    /// <summary>
    /// 技能无敌区间起点，使用本英雄的游戏时钟，单位秒。
    /// </summary>
    internal double SkillInvulnerableFrom { get; set; } = double.PositiveInfinity;

    /// <summary>
    /// 技能无敌区间终点，单位秒，独立于动画是否跳帧。
    /// </summary>
    internal double SkillInvulnerableUntil { get; set; }

    /// <summary>
    /// 当前等级的只读基础记录，由英雄系统替换。
    /// </summary>
    internal ActorBaseAttributes BaseAttributes { get; set; }

    /// <summary>
    /// 本局修正来源，系统存入副本；不向 UI 借出可写集合。
    /// </summary>
    internal Dictionary<AttributeSourceId, AttributeModifier[]> Sources { get; } = new();

    /// <summary>
    /// 开启消耗不足一点的累计量。
    /// </summary>
    internal double WsDrainProgress { get; set; }

    /// <summary>
    /// 开启后的额外点数/秒。
    /// </summary>
    internal float WsDrainPerSecond { get; set; }

    /// <summary>
    /// 自然衰减不足一点的累计量。
    /// </summary>
    internal double WsDecayProgress { get; set; }

    /// <summary>
    /// 未满时的自然衰减点数/秒。
    /// </summary>
    internal float WsDecayPerSecond { get; set; }

    /// <summary>
    /// 冻结出招/受击时的最终值，已发出的攻击不随后续属性变化。
    /// </summary>
    /// <param name="actorId">来源实体编号。</param>
    /// <returns>伤害公式使用的不可变快照。</returns>
    public CombatantAttributes CaptureCombatAttributes(int? actorId = null)
    {
        return new(CombatSide.Hero,
        Level.Value, Power.Value, Def.Value, Mdef.Value, Crit.Value, Miss.Value, Lucky.Value,
        Toughness.Value, Htarget.Value, CritReduce.Value, Ar.Value, Sp.Value, actorId);
    }

    /// <summary>
    /// 状态效果类别；动作状态仍由英雄身体 FSM 表达。
    /// </summary>
    public enum StatusEffectKind
    {
        /// <summary>
        /// 按间隔结算固定伤害的中毒效果。
        /// </summary>
        Poison,

        /// <summary>
        /// 按剩余秒数限制角色控制的冻结效果。
        /// </summary>
        Freeze
    }

    /// <summary>
    /// 单个英雄状态效果的可变运行时数据；只由 HeroStatusSystem 修改。
    /// </summary>
    public sealed class StatusEffectState
    {
        /// <summary>
        /// 建立指定类别的效果状态。
        /// </summary>
        /// <param name="kind">效果类别。</param>
        public StatusEffectState(StatusEffectKind kind)
        {
            Kind = kind;
        }

        /// <summary>
        /// 效果类别。
        /// </summary>
        public StatusEffectKind Kind { get; }

        /// <summary>
        /// 固定每跳伤害；非持续伤害效果为零。
        /// </summary>
        public int Damage { get; internal set; }

        /// <summary>
        /// 剩余触发次数。
        /// </summary>
        public int RemainingTicks { get; internal set; }

        /// <summary>
        /// 两次触发之间的秒数。
        /// </summary>
        public double TickIntervalSeconds { get; internal set; }

        /// <summary>
        /// 距离下一次触发的秒数。
        /// </summary>
        public double TickLeftSeconds { get; internal set; }

        /// <summary>
        /// 控制效果剩余秒数。
        /// </summary>
        public float RemainingSeconds { get; internal set; }
    }
}
