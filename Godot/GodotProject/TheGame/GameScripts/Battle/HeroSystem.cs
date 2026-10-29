using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Attributes;
using GameConfig.Battle;
using GameLogic.Architecture;
using GameLogic.Battle.Attributes;
using GameLogic.Config;
using GameLogic.Equipment;
using GameLogic.Profile;
using GameLogic.Skills;
using GameConfig.Skills;

namespace GameLogic.Battle
{
    /// <summary>
    /// 英雄玩法数据入口：协调成长、穿戴、最终属性、生命魔法和无双；不持有表现节点。
    /// </summary>
    public sealed class HeroSystem : AbstractSystem
    {
        private static readonly int m_AttributeCount = Enum.GetValues<AttributeType>().Length; // 配置属性的连续数量。
        private static readonly AttributeSourceId m_WeaponSource = new("EquipmentWeapon", 0); // 当前武器的来源键。
        private static readonly AttributeSourceId m_ArmorSource = new("EquipmentArmor", 0); // 当前防具的来源键。
        private static readonly AttributeSourceId m_AccessorySource = new("EquipmentAccessory", 0); // 当前饰品的来源键。
        private static readonly AttributeSourceId m_PassiveSource = new("PassiveSkills", 0); // 当前英雄的五项被动总加成。
        private const double TickTolerance = 1e-5; // 秒数累计的浮点点数容差。
        private const double RecoveryInterval = 25d / 24d; // 官方tcount++ >= 24每25个源帧执行一次恢复。
        private const int HurtProtectionHitCount = 5; // 官方BaseAddEffect在第五次有效命中后开启保护。
        private const double HurtChainInterval = 1d; // 官方father.interval=1000ms，相邻命中达到该间隔即清零。
        private const double HurtProtectionDuration = 2d; // 官方father.time=48源帧，按24fps换算为秒。

        /// <summary>
        /// 即时借用本局英雄数值，不缓存模型引用。
        /// </summary>
        private HeroModel Hero
        {
            get
            {
                return this.GetModel<HeroModel>();
            }
        }

        /// <summary>
        /// 无双当前是否提供霸体，供身体状态机查询。
        /// </summary>
        public bool SuperArmor
        {
            get
            {
                return this.GetModel<HeroModel>().WsActive.Value;
            }
        }

        /// <summary>
        /// 当前横向移动倍率，不改写共享角色参数。
        /// </summary>
        public float MoveSpeedMultiplier
        {
            get
            {
                return SuperArmor
            ? this.GetModel<ConfigurationModel>().Tables.TbBattleConfig.WsMoveSpeedMultiplier
            : 1f;
            }
        }

        /// <summary>
        /// 当前起跳倍率。
        /// </summary>
        public float JumpSpeedMultiplier
        {
            get
            {
                return SuperArmor
            ? this.GetModel<ConfigurationModel>().Tables.TbBattleConfig.WsJumpSpeedMultiplier
            : 1f;
            }
        }

        /// <summary>
        /// 当前重力倍率。
        /// </summary>
        public float GravityMultiplier
        {
            get
            {
                return SuperArmor
            ? this.GetModel<ConfigurationModel>().Tables.TbBattleConfig.WsGravityMultiplier
            : 1f;
            }
        }

        /// <summary>
        /// 整招攻击威力倍率，包含招式固定加值。
        /// </summary>
        public float AttackPowerMultiplier
        {
            get
            {
                return SuperArmor
            ? this.GetModel<ConfigurationModel>().Tables.TbBattleConfig.WsAttackMultiplier
            : 1f;
            }
        }

        /// <summary>
        /// 绑定所属流程架构；所需模型须先注册。
        /// </summary>
        /// <param name="architecture">所属架构。</param>
        public HeroSystem(IArchitecture architecture) : base(architecture) { }

        /// <summary>
        /// 从完整档案建立本局数值，随后监听成长与穿戴。
        /// </summary>
        protected override void OnInit()
        {
            var profile = this.GetModel<ProfileModel>();
            var tables = this.GetModel<ConfigurationModel>().Tables;
            Reset(profile.ActiveHeroId, profile.Level,
                tables.TbHeroGrowthConfig.Get(profile.ActiveHeroId).Levels[profile.Level - 1].BaseAttributes,
                profile.Weapon, profile.Armor, profile.Accessory);
            var battle = tables.TbBattleConfig.Data;
            ResetWs(battle.WsMax, battle.WsDecayPerSecond, battle.WsDrainPerSecond);
            profile.Progress.Register(OnProgressChanged, priority: 100);
            profile.Slots.Register(OnEquipmentChanged, priority: 100);
            this.GetModel<SkillModel>().GetPassiveLevels(profile.ActiveHeroId).Register(OnPassiveLevelsChanged, priority: 100);
        }

        /// <summary>
        /// 移除系统时退订持久来源并结束无双；模型稍后由流程移除。
        /// </summary>
        protected override void OnDeinit()
        {
            this.GetModel<ProfileModel>().Progress.UnRegister(OnProgressChanged);
            this.GetModel<ProfileModel>().Slots.UnRegister(OnEquipmentChanged);
            this.GetModel<SkillModel>().GetPassiveLevels(Hero.HeroId).UnRegister(OnPassiveLevelsChanged);
            ClearWs();
            ClearPoison();
            Hero.FreezeSeconds = 0;
            ClearHurtProtection();
        }

        /// <summary>
        /// 应用伤害；首次归零时结束无双，死亡动作交给实体。
        /// </summary>
        /// <param name="amount">伤害值。</param>
        /// <returns>本次是否首次归零。</returns>
        public bool Damage(int amount)
        {
            if (amount <= 0 || Hero.IsDepleted)
            {
                return false;
            }

            Hero.Hp.Value = Math.Max(0, Hero.Hp.Value - amount);
            bool died = Hero.IsDepleted;
            if (died)
            {
                ClearWs();
                ClearPoison();
                Hero.FreezeSeconds = 0;
                ClearHurtProtection();
            }
            return died;
        }

        /// <summary>
        /// 恢复存活英雄生命，不超过最终上限。
        /// </summary>
        /// <param name="amount">恢复量。</param>
        public void Heal(int amount)
        {
            if (amount > 0 && !Hero.IsDepleted)
            {
                Hero.Hp.Value = (int)Math.Min(Hero.MaxHp.Value, (long)Hero.Hp.Value + amount);
            }
        }

        /// <summary>
        /// 记录一次已结算且未闪避的直接命中；第五次伤害仍生效，保护后续命中。
        /// </summary>
        public void RecordHurt()
        {
            if (Hero.IsDepleted || Hero.HurtInvulnerabilitySeconds > 0)
            {
                return;
            }

            Hero.HurtHitCount++;
            Hero.HurtChainSeconds = HurtChainInterval;
            if (Hero.HurtHitCount >= HurtProtectionHitCount)
            {
                Hero.HurtHitCount = 0;
                Hero.HurtInvulnerabilitySeconds = HurtProtectionDuration;
            }
        }

        /// <summary>
        /// 推进连续受击与保护时钟；暂停不调用，技能无敌不会覆盖这份计时。
        /// </summary>
        /// <param name="delta">本步游戏秒数。</param>
        public void AdvanceHurtProtection(float delta)
        {
            if (!float.IsFinite(delta) || delta <= 0)
            {
                return;
            }

            Hero.HurtInvulnerabilitySeconds = Math.Max(0, Hero.HurtInvulnerabilitySeconds - delta);
            if (Hero.HurtInvulnerabilitySeconds < TickTolerance)
            {
                Hero.HurtInvulnerabilitySeconds = 0;
            }

            Hero.HurtChainSeconds = Math.Max(0, Hero.HurtChainSeconds - delta);
            if (Hero.HurtChainSeconds < TickTolerance)
            {
                Hero.HurtChainSeconds = 0;
                Hero.HurtHitCount = 0;
            }
        }

        /// <summary>
        /// 新局、死亡或离关时清除命中记录与无敌，池复用不继承旧保护。
        /// </summary>
        private void ClearHurtProtection()
        {
            Hero.HurtHitCount = 0;
            Hero.HurtChainSeconds = 0;
            Hero.HurtInvulnerabilitySeconds = 0;
        }

        /// <summary>
        /// 按官方秒间隔结算装备回血回蓝；负回血交给实体提交死亡事实，不产生受击反馈。
        /// </summary>
        /// <param name="delta">本步实际推进的游戏秒数；暂停时不传正值。</param>
        /// <returns>本步到期的生命流失值；调用方立即通过持续伤害入口结算。</returns>
        public int AdvanceRecovery(float delta)
        {
            if (delta <= 0f || !float.IsFinite(delta) || Hero.IsDepleted)
            {
                return 0;
            }

            Hero.RecoveryProgress += delta;
            double ticks = Math.Floor((Hero.RecoveryProgress + TickTolerance) / RecoveryInterval);
            if (ticks < 1)
            {
                return 0;
            }

            Hero.RecoveryProgress = Math.Max(0, Hero.RecoveryProgress - ticks * RecoveryInterval);
            // 使用同一个节拍，穿卸改变下次结算值，不重新开始累计；负回蓝也保留源文件钳制规则。
            Hero.Mp.Value = (int)Math.Clamp(Hero.Mp.Value + ticks * Hero.MpRegen.Value, 0d, Hero.MaxMp.Value);
            double hp = ticks * Hero.HpRegen.Value;
            if (hp >= 0)
            {
                Heal((int)Math.Min(int.MaxValue, hp));
                return 0;
            }
            return (int)Math.Min(int.MaxValue, -hp);
        }

        /// <summary>
        /// 施加存活英雄的冰冻控制时长；同一状态的再次施加保留较长的剩余时间。
        /// </summary>
        /// <param name="seconds">有限且为正的游戏秒数。</param>
        /// <exception cref="ArgumentOutOfRangeException">持续时间无效。</exception>
        public void ApplyFreeze(float seconds)
        {
            if (!float.IsFinite(seconds) || seconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }

            if (!Hero.IsDepleted)
            {
                Hero.FreezeSeconds = Math.Max(Hero.FreezeSeconds, seconds);
            }
        }

        /// <summary>
        /// 推进冰冻状态；暂停传入零，死亡、离关与重建由所属生命周期清理。
        /// </summary>
        /// <param name="delta">非负游戏秒数。</param>
        public void AdvanceFreeze(float delta)
        {
            if (!float.IsFinite(delta) || delta <= 0)
            {
                return;
            }

            float remaining = Hero.FreezeSeconds - delta;
            Hero.FreezeSeconds = remaining > TickTolerance ? remaining : 0;
        }

        /// <summary>
        /// 施加中毒；配置给出固定每跳伤害、秒间隔和总跳数，重复命中只刷新。
        /// </summary>
        /// <param name="attack">固定伤害记录；生命尚存才生效。</param>
        /// <exception cref="ArgumentException">中毒记录不是有限正数值。</exception>
        public void ApplyPoison(AttackConfig attack)
        {
            ArgumentNullException.ThrowIfNull(attack);
            if (attack.FlatPower.X <= 0 || attack.FlatPower.X != attack.FlatPower.Y ||
                !float.IsFinite(attack.FlatPower.X) || attack.HitBudget <= 0 ||
                !float.IsFinite(attack.RepeatIntervalSeconds) || attack.RepeatIntervalSeconds <= 0)
            {
                throw new ArgumentException("中毒需要固定正伤害、正跳数和正秒间隔。", nameof(attack));
            }

            if (Hero.IsDepleted)
            {
                return;
            }

            bool alreadyPoisoned = Hero.PoisonAttack != null;
            Hero.PoisonAttack = attack;
            Hero.PoisonTicks = attack.HitBudget;
            if (!alreadyPoisoned)
            {
                Hero.PoisonTickLeft = attack.RepeatIntervalSeconds;
            }
        }

        /// <summary>
        /// 推进中毒时钟并返回本步固定扣血量；实体提交死亡与飘字，不触发受击动作。
        /// </summary>
        /// <param name="delta">游戏秒数；暂停不调用。</param>
        /// <returns>本步到期的总伤害；无到期或无状态时为零。</returns>
        public int AdvancePoison(float delta)
        {
            if (!float.IsFinite(delta) || delta <= 0 || Hero.PoisonAttack == null || Hero.IsDepleted)
            {
                return 0;
            }

            var attack = Hero.PoisonAttack;
            Hero.PoisonTickLeft -= delta;
            int damage = 0;
            while (Hero.PoisonTickLeft <= TickTolerance && Hero.PoisonTicks > 0)
            {
                damage += (int)attack.FlatPower.X;
                Hero.PoisonTicks--;
                Hero.PoisonTickLeft += attack.RepeatIntervalSeconds;
            }
            if (Hero.PoisonTicks == 0)
            {
                ClearPoison();
            }

            return damage;
        }

        /// <summary>
        /// 死亡、离关或重新初始化时移除中毒，不保留上一局时钟。
        /// </summary>
        private void ClearPoison()
        {
            Hero.PoisonAttack = null;
            Hero.PoisonTicks = 0;
            Hero.PoisonTickLeft = 0;
        }

        /// <summary>
        /// 消耗魔法；不足时保持状态。
        /// </summary>
        /// <param name="amount">消耗量。</param>
        /// <returns>是否成功。</returns>
        public bool TrySpendMp(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (Hero.Mp.Value < amount)
            {
                return false;
            }

            Hero.Mp.Value -= amount;
            return true;
        }

        /// <summary>
        /// 恢复魔法，不超过最终上限。
        /// </summary>
        /// <param name="amount">恢复量。</param>
        public void RestoreMp(int amount)
        {
            if (amount > 0)
            {
                Hero.Mp.Value = (int)Math.Min(Hero.MaxMp.Value, (long)Hero.Mp.Value + amount);
            }
        }

        /// <summary>
        /// 开始本局时清除旧来源，整体应用成长与穿戴。
        /// </summary>
        /// <param name="heroId">英雄身份。</param>
        /// <param name="level">成长等级。</param>
        /// <param name="baseAttributes">对应基础值。</param>
        /// <param name="weaponData">当前武器。</param>
        /// <param name="armorData">当前防具。</param>
        /// <param name="accessoryData">当前饰品。</param>
        internal void Reset(int heroId, int level, ActorBaseAttributes baseAttributes,
            EquipmentData weaponData = null, EquipmentData armorData = null, EquipmentData accessoryData = null)
        {
            ArgumentNullException.ThrowIfNull(baseAttributes);
            Hero.Sources.Clear();
            ReplacePassiveSource(this.GetModel<SkillModel>().GetPassiveLevels(heroId));
            Hero.RecoveryProgress = 0;
            ClearHurtProtection();
            ClearPoison();
            Hero.FreezeSeconds = 0;
            SetGrowth(baseAttributes, heroId, level, weaponData, armorData, refill: true, accessoryData);
            Hero.ControlsEnabled = true;
        }

        /// <summary>
        /// 同步当前成长与穿戴并重算属性，保留技能/Buff 来源。
        /// </summary>
        /// <param name="baseAttributes">当前基础值。</param>
        /// <param name="heroId">当前英雄。</param>
        /// <param name="level">当前等级。</param>
        /// <param name="weaponData">武器。</param>
        /// <param name="armorData">防具。</param>
        /// <param name="accessoryData">饰品。</param>
        /// <param name="refill">升级是否补满资源。</param>
        internal void SetGrowth(ActorBaseAttributes baseAttributes, int heroId, int level,
            EquipmentData weaponData, EquipmentData armorData, bool refill, EquipmentData accessoryData = null)
        {
            ArgumentNullException.ThrowIfNull(baseAttributes);
            Hero.BaseAttributes = baseAttributes;
            ReplaceEquipmentSources(weaponData, armorData, accessoryData);
            Recalculate(heroId, level, refill);
        }

        /// <summary>
        /// 登记技能/Buff 等独立来源，替换同来源修正并立即重算。
        /// </summary>
        /// <param name="source">有效来源键。</param>
        /// <param name="modifiers">完整修正集合。</param>
        /// <exception cref="ArgumentException">来源无效。</exception>
        public void SetSource(AttributeSourceId source, IReadOnlyList<AttributeModifier> modifiers)
        {
            if (source.Category == null)
            {
                throw new ArgumentException("属性来源无效。", nameof(source));
            }

            ArgumentNullException.ThrowIfNull(modifiers);
            Hero.Sources[source] = modifiers.ToArray();
            Recalculate(Hero.HeroId, Hero.Level.Value, refill: false);
        }

        /// <summary>
        /// 结束独立来源并重新发布最终值。
        /// </summary>
        /// <param name="source">来源键。</param>
        /// <returns>是否存在该来源。</returns>
        public bool RemoveSource(AttributeSourceId source)
        {
            if (!Hero.Sources.Remove(source))
            {
                return false;
            }

            Recalculate(Hero.HeroId, Hero.Level.Value, refill: false);
            return true;
        }

        /// <summary>
        /// 等级变化时使用已提交的成长和装备重算，升级补满、回滚不补满。
        /// </summary>
        /// <param name="progress">完整成长状态。</param>
        private void OnProgressChanged(HeroProgressionState progress)
        {
            if (progress.Level == Hero.Level.Value)
            {
                return;
            }

            var row = this.GetModel<ConfigurationModel>().Tables.TbHeroGrowthConfig.Get(Hero.HeroId).Levels[progress.Level - 1];
            var profile = this.GetModel<ProfileModel>();
            SetGrowth(row.BaseAttributes, Hero.HeroId, progress.Level, profile.Weapon, profile.Armor,
                refill: progress.Level > Hero.Level.Value, profile.Accessory);
        }

        /// <summary>
        /// 装备事务完成后重算，保留其余来源且不补血。
        /// </summary>
        /// <param name="slots">已经提交的穿戴。</param>
        private void OnEquipmentChanged(EquippedItems slots)
        {
            ReplaceEquipmentSources(slots.Weapon, slots.Armor, slots.Accessory);
            Recalculate(Hero.HeroId, Hero.Level.Value, refill: false);
        }

        /// <summary>
        /// 被动升级或回滚时替换总加成；增加资源上限时同时增加当前资源，保留原有缺损。
        /// </summary>
        /// <param name="levels">当前英雄已经提交的全部被动等级。</param>
        private void OnPassiveLevelsChanged(IReadOnlyDictionary<PassiveId, int> levels)
        {
            int maxHp = Hero.MaxHp.Value;
            int maxMp = Hero.MaxMp.Value;
            ReplacePassiveSource(levels);
            Recalculate(Hero.HeroId, Hero.Level.Value, refill: false);
            Heal(Hero.MaxHp.Value - maxHp);
            RestoreMp(Hero.MaxMp.Value - maxMp);
        }

        /// <summary>
        /// 各被动只读取已学习等级的总值，避免把1至当前等级重复累加。
        /// </summary>
        /// <param name="levels">本英雄的被动等级。</param>
        private void ReplacePassiveSource(IReadOnlyDictionary<PassiveId, int> levels)
        {
            var definitions = this.GetModel<ConfigurationModel>().Tables.TbPassiveConfig;
            List<AttributeModifier> modifiers = new();
            foreach (var (id, level) in levels)
            {
                if (level == 0)
                {
                    continue;
                }

                var definition = definitions.Get(id);
                AttributeType attribute = definition.Effect switch
                {
                    PassiveEffect.MaxHealth => AttributeType.MaxHp,
                    PassiveEffect.MaxMana => AttributeType.MaxMp,
                    PassiveEffect.CriticalChance => AttributeType.Crit,
                    PassiveEffect.HealthRegen => AttributeType.HpRegen,
                    PassiveEffect.ManaRegen => AttributeType.MpRegen,
                    _ => throw new InvalidOperationException($"未实现的被动效果：{definition.Effect}")
                };
                modifiers.Add(AttributeModifier.Flat(attribute, definition.Levels[level - 1].Value));
            }
            Hero.Sources[m_PassiveSource] = modifiers.ToArray();
        }

        /// <summary>
        /// 整体替换当前三个穿戴部位的属性来源。
        /// </summary>
        /// <param name="weaponData">当前武器。</param>
        /// <param name="armorData">当前防具。</param>
        /// <param name="accessoryData">当前饰品。</param>
        private void ReplaceEquipmentSources(EquipmentData weaponData, EquipmentData armorData, EquipmentData accessoryData)
        {
            Hero.Sources.Remove(m_WeaponSource);
            Hero.Sources.Remove(m_ArmorSource);
            Hero.Sources.Remove(m_AccessorySource);
            if (weaponData != null)
            {
                Hero.Sources[m_WeaponSource] = weaponData.Attributes.EnumerateModifiers().ToArray();
            }

            if (armorData != null)
            {
                Hero.Sources[m_ArmorSource] = armorData.Attributes.EnumerateModifiers().ToArray();
            }

            if (accessoryData != null)
            {
                Hero.Sources[m_AccessorySource] = accessoryData.Attributes.EnumerateModifiers().ToArray();
            }
        }

        /// <summary>
        /// 按固定、加算百分比、乘算百分比顺序汇总，再更新模型；各绑定值同步通知。
        /// </summary>
        /// <param name="heroId">当前英雄。</param>
        /// <param name="level">当前等级。</param>
        /// <param name="refill">是否补满生命魔法。</param>
        private void Recalculate(int heroId, int level, bool refill)
        {
            int count = m_AttributeCount;
            Span<float> flat = stackalloc float[count];
            Span<float> percentAdd = stackalloc float[count];
            Span<float> percentMult = stackalloc float[count];
            percentMult.Fill(1f);
            foreach (var modifiers in Hero.Sources.Values)
            {
                foreach (var modifier in modifiers)
                {
                    int index = (int)modifier.Attribute;
                    switch (modifier.Kind)
                    {
                        case AttributeModifierKind.Flat:
                            flat[index] += modifier.Value;
                            break;
                        case AttributeModifierKind.PercentAdd:
                            percentAdd[index] += modifier.Value;
                            break;
                        case AttributeModifierKind.PercentMult:
                            percentMult[index] *= 1f + modifier.Value;
                            break;
                    }
                }
            }

            Span<float> final = stackalloc float[count];
            for (int i = 0; i < count; i++)
            {
                var attribute = (AttributeType)i;
                float value = ((Hero.BaseAttributes?.Get(attribute) ?? 0) + flat[i])
                                           * (1 + percentAdd[i]) * percentMult[i];
                if (attribute is not (AttributeType.HpRegen or AttributeType.MpRegen))
                {
                    value = MathF.Max(0, value);
                }

                final[i] = attribute.IsProbability ? Math.Clamp(value, 0f, 1f)
                                : MathF.Round(value, MidpointRounding.AwayFromZero);
            }
            final[(int)AttributeType.MaxHp] = Math.Max(1, final[(int)AttributeType.MaxHp]);
            ApplyFinalAttributes(final, heroId, level, refill);
        }

        /// <summary>
        /// 按实际伤害与出招时攻击力结算命中无双；开启期间不增长。
        /// </summary>
        /// <param name="damage">实际伤害。</param>
        /// <param name="basePower">出招时基础攻击力。</param>
        public void RecordHit(int damage, int basePower)
        {
            if (Hero.IsDepleted)
            {
                return;
            }

            float coefficient = this.GetModel<ConfigurationModel>().Tables.TbBattleConfig.WsGainPerDamageRatio;
            if (damage <= 0 || basePower <= 0 || !float.IsFinite(coefficient) || coefficient <= 0f)
            {
                return;
            }

            AddWs((int)Math.Min(int.MaxValue, Math.Floor((double)damage / basePower * coefficient + .5)));
        }

        /// <summary>
        /// 系统初始化时设置本局无双参数并清零。
        /// </summary>
        /// <param name="max">无双上限，下限 0。</param>
        /// <param name="decayPerSecond">自然衰减速度，有限正点数/秒。</param>
        /// <param name="drainPerSecond">开启后额外消耗速度，有限正点数/秒。</param>
        /// <exception cref="ArgumentOutOfRangeException">衰减参数非法。</exception>
        internal void ResetWs(int max, float decayPerSecond, float drainPerSecond)
        {
            var model = this.GetModel<HeroModel>();
            if (!float.IsFinite(decayPerSecond) || decayPerSecond <= 0f
                                                || !float.IsFinite(drainPerSecond) || drainPerSecond <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(decayPerSecond), "无双每秒衰减和消耗必须为有限正数。");
            }

            model.WsDecayPerSecond = decayPerSecond;
            model.WsDecayProgress = 0;
            model.WsDrainPerSecond = drainPerSecond;
            model.WsDrainProgress = 0;
            SetWsState(0, Math.Max(0, max), false);
        }

        /// <summary>
        /// 按官方规则累计：正在开启或本次加值会超过上限时整次不增加。
        /// </summary>
        /// <param name="amount">增加量；非正数不处理，满值保持。</param>
        internal void AddWs(int amount)
        {
            var model = this.GetModel<HeroModel>();
            if (!model.WsActive.Value)
            {
                AddWsFromPickup(amount);
            }
        }

        /// <summary>
        /// 黄球增加无双；开启期间也可补充，超过上限仍整次忽略。
        /// </summary>
        /// <param name="amount">本颗黄球的配置值，通常为 5。</param>
        public void AddWsFromPickup(int amount)
        {
            var model = this.GetModel<HeroModel>();
            if (amount <= 0 || amount > model.MaxWs.Value - model.Ws.Value)
            {
                return;
            }

            SetWsState(model.Ws.Value + amount, model.MaxWs.Value, model.WsActive.Value);
        }

        /// <summary>
        /// 蓄满时开启无双；激活期间不再次开启，也不累计命中收益。
        /// </summary>
        /// <returns>是否已进入无双状态。</returns>
        public bool TryActivateWs()
        {
            var model = this.GetModel<HeroModel>();
            if (Hero.IsDepleted || !model.ControlsEnabled || model.FreezeSeconds > 0 || !model.IsWsFull)
            {
                return false;
            }

            model.WsDrainProgress = 0;
            SetWsState(model.Ws.Value, model.MaxWs.Value, true);
            return true;
        }

        /// <summary>
        /// 转场阶段关闭玩家控制但保留动作、重力与效果计时；新局模型默认恢复开放。
        /// </summary>
        /// <param name="enabled">是否允许新的玩家操作。</param>
        public void SetControlsEnabled(bool enabled)
        {
            Hero.ControlsEnabled = enabled;
        }

        /// <summary>
        /// 推进未满时的自然衰减或开启后的耗尽；满值保留，暂停时宿主不推进。
        /// </summary>
        /// <param name="delta">本次物理步长秒数。</param>
        public void AdvanceWs(float delta)
        {
            var model = this.GetModel<HeroModel>();
            if (!float.IsFinite(delta) || delta <= 0f)
            {
                return;
            }

            if (model.IsWsFull || model.WsDecayPerSecond <= 0f)
            {
                return;
            }

            int value = model.Ws.Value;
            bool wasActive = model.WsActive.Value;
            // 官方满值不推进自然衰减；首次开启扣点后，普通衰减恢复。
            double decayDelta = delta;
            if (model.WsActive.Value)
            {
                if (model.Ws.Value == model.MaxWs.Value)
                {
                    decayDelta = Math.Max(0, delta - (1 - model.WsDrainProgress) / model.WsDrainPerSecond);
                }

                model.WsDrainProgress += delta * (double)model.WsDrainPerSecond;
                double ticks = Math.Floor(model.WsDrainProgress + TickTolerance);
                model.WsDrainProgress = Math.Max(0, model.WsDrainProgress - ticks);
                value = (int)Math.Max(0, value - ticks);
            }

            model.WsDecayProgress += decayDelta * model.WsDecayPerSecond;
            double decayTicks = Math.Floor(model.WsDecayProgress + TickTolerance);
            model.WsDecayProgress = Math.Max(0, model.WsDecayProgress - decayTicks);
            value = (int)Math.Max(0, value - decayTicks);
            SetWsState(value, model.MaxWs.Value, value > 0 && wasActive);
        }

        /// <summary>
        /// 立即结束并清空进度（死亡或模型移除），不影响无双上限。
        /// </summary>
        public void ClearWs()
        {
            var model = this.GetModel<HeroModel>();
            model.WsDrainProgress = 0;
            model.WsDecayProgress = 0;
            SetWsState(0, model.MaxWs.Value, false);
        }

        /// <summary>
        /// 更新最终属性与资源；各属性由 Value 自动通知。
        /// </summary>
        /// <param name="final">按 AttributeType 排列的最终数值。</param>
        /// <param name="heroId">本局英雄身份。</param>
        /// <param name="level">对应等级。</param>
        /// <param name="refill">是否补满生命魔法。</param>
        private void ApplyFinalAttributes(ReadOnlySpan<float> final, int heroId, int level, bool refill)
        {
            Hero.HeroId = heroId;
            Hero.MaxHp.Value = (int)final[(int)AttributeType.MaxHp];
            Hero.MaxMp.Value = (int)final[(int)AttributeType.MaxMp];
            Hero.Power.Value = (int)final[(int)AttributeType.Power];
            Hero.Def.Value = (int)final[(int)AttributeType.Def];
            Hero.Mdef.Value = (int)final[(int)AttributeType.Mdef];
            Hero.Crit.Value = final[(int)AttributeType.Crit];
            Hero.Miss.Value = final[(int)AttributeType.Miss];
            Hero.Lucky.Value = (int)final[(int)AttributeType.Lucky];
            Hero.Toughness.Value = (int)final[(int)AttributeType.Toughness];
            Hero.Htarget.Value = final[(int)AttributeType.Htarget];
            Hero.CritReduce.Value = final[(int)AttributeType.CritReduce];
            Hero.Ar.Value = (int)final[(int)AttributeType.Ar];
            Hero.Sp.Value = (int)final[(int)AttributeType.Sp];
            Hero.Vampirism.Value = (int)final[(int)AttributeType.Vampirism];
            Hero.HpRegen.Value = (int)final[(int)AttributeType.HpRegen];
            Hero.MpRegen.Value = (int)final[(int)AttributeType.MpRegen];
            Hero.Hp.Value = refill ? Hero.MaxHp.Value : Math.Min(Hero.Hp.Value, Hero.MaxHp.Value);
            Hero.Mp.Value = refill ? Hero.MaxMp.Value : Math.Min(Hero.Mp.Value, Hero.MaxMp.Value);
            Hero.Level.Value = level;
        }

        /// <summary>
        /// 按上限、当前值、开启状态的业务顺序更新无双。
        /// </summary>
        /// <param name="value">当前值。</param>
        /// <param name="max">当前上限。</param>
        /// <param name="active">是否开启。</param>
        private void SetWsState(int value, int max, bool active)
        {
            Hero.MaxWs.Value = max;
            Hero.Ws.Value = value;
            Hero.WsActive.Value = active;
        }
    }
}
