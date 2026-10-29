using System;
using System.Collections.Generic;
using GameConfig.Attributes;
using GameConfig.Battle;
using GameConfig.Skills;
using GameLogic.Architecture;
using GameLogic.Battle.Attributes;
using GameLogic.Battle.Damage;
using GameLogic.Skills;

namespace GameLogic.Battle
{
    /// <summary>
    /// 本局英雄的权威属性、资源和技能状态；HeroSystem 与 SkillSystem 执行业务规则。
    /// </summary>
    public sealed class HeroModel : AbstractModel
    {
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
        public bool IsDepleted
        {
            get
            {
                return Hp.Value <= 0;
            }
        }

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
        public bool IsWsFull
        {
            get
            {
                return !WsActive.Value && MaxWs.Value > 0 && Ws.Value >= MaxWs.Value;
            }
        }

        /// <summary>
        /// 冰冻剩余游戏秒数；只禁止玩家控制，既有动作、重力和状态计时仍继续。
        /// </summary>
        public float FreezeSeconds { get; internal set; }

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
        /// 本局中毒的每跳数值，离关不保存；为空时未中毒。
        /// </summary>
        internal AttackConfig PoisonAttack { get; set; }

        /// <summary>
        /// 中毒剩余跳数，重复施加刷新时长，不叠加伤害源。
        /// </summary>
        internal int PoisonTicks { get; set; }

        /// <summary>
        /// 距下次中毒扣血的游戏秒数。
        /// </summary>
        internal double PoisonTickLeft { get; set; }

        /// <summary>
        /// 装备自然恢复的累计游戏秒数，暂停不推进，本局重建时归零。
        /// </summary>
        internal double RecoveryProgress { get; set; }

        /// <summary>
        /// 本局当前激活的技能编号；招式阶段与旧回调有效性归具体 GGF 状态。
        /// </summary>
        public SkillId? ActiveSkillId { get; internal set; }

        /// <summary>
        /// 本局角色或武器的技能登记，由 SkillSystem 校验并复制，不保存角色节点。
        /// </summary>
        internal Dictionary<SkillId, SkillDefinition> SkillDefinitions { get; } = new();

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
    }
}
