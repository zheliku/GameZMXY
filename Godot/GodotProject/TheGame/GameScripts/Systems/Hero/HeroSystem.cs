using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig;
using GameConfig.Attributes;
using GameConfig.Battle;
using GameConfig.Hero;
using GameConfig.Skills;
using GameLogic.Architecture;
using GameLogic.Battle.Attacks;
using GameLogic.Battle.Attributes;
using GameLogic.Battle.Damage;
using GameLogic.Config;
using GameLogic.Equipment;
using GameLogic.Profile;
using GameLogic.Skills;

namespace GameLogic.Battle;

/// <summary>
/// 英雄玩法数据入口：协调成长、穿戴、最终属性、生命魔法和无双；不持有表现节点。
/// </summary>
public sealed class HeroSystem : AbstractSystem
{
    private static readonly int s_attributeCount = Enum.GetValues<AttributeType>().Length; // 配置属性的连续数量。
    private static readonly AttributeSourceId s_weaponSource = new("EquipmentWeapon", 0); // 当前武器的来源键。
    private static readonly AttributeSourceId s_armorSource = new("EquipmentArmor", 0); // 当前防具的来源键。
    private static readonly AttributeSourceId s_accessorySource = new("EquipmentAccessory", 0); // 当前饰品的来源键。
    private static readonly AttributeSourceId s_treasureSource = new("EquipmentTreasure", 0); // 当前法宝的来源键。
    private static readonly AttributeSourceId s_passiveSource = new("PassiveSkills", 0); // 当前英雄的五项被动总加成。
    private readonly Dictionary<int, (Action<HeroProgressionState> Progress, Action<EquippedItems> Slots,
        Action<IReadOnlyDictionary<PassiveId, int>> Passives)> _subscriptions = new(); // 每位参战英雄实际注册的回调，用于对称退订。
    private const double TickTolerance = 1e-5; // 秒数累计的浮点点数容差。
    private const double RecoveryInterval = 25d / 24d; // 官方tcount++ >= 24每25个源帧执行一次恢复。
    private const int HurtProtectionHitCount = 5; // 官方BaseAddEffect在第五次有效命中后开启保护。
    private const double HurtChainInterval = 1d; // 官方father.interval=1000ms，相邻命中达到该间隔即清零。
    private const double HurtProtectionDuration = 2d; // 官方father.time=48源帧，按24fps换算为秒。

    /// <summary>
    /// 当前横向移动倍率，不改写共享角色参数。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <returns>该英雄当前的倍率。</returns>
    public float GetMoveSpeedMultiplier(int heroId)
    {
        return this.GetModel<HeroModel>().GetState(heroId).WsActive.Value
        ? this.GetModel<ConfigurationModel>().Tables.TbBattleConfig.WsMoveSpeedMultiplier
        : 1f;
    }

    /// <summary>
    /// 当前起跳倍率。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <returns>该英雄当前的倍率。</returns>
    public float GetJumpSpeedMultiplier(int heroId)
    {
        return this.GetModel<HeroModel>().GetState(heroId).WsActive.Value
        ? this.GetModel<ConfigurationModel>().Tables.TbBattleConfig.WsJumpSpeedMultiplier
        : 1f;
    }

    /// <summary>
    /// 当前重力倍率。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <returns>该英雄当前的倍率。</returns>
    public float GetGravityMultiplier(int heroId)
    {
        return this.GetModel<HeroModel>().GetState(heroId).WsActive.Value
        ? this.GetModel<ConfigurationModel>().Tables.TbBattleConfig.WsGravityMultiplier
        : 1f;
    }

    /// <summary>
    /// 整招攻击威力倍率，包含招式固定加值。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <returns>该英雄当前的倍率。</returns>
    public float GetAttackPowerMultiplier(int heroId)
    {
        return this.GetModel<HeroModel>().GetState(heroId).WsActive.Value
        ? this.GetModel<ConfigurationModel>().Tables.TbBattleConfig.WsAttackMultiplier
        : 1f;
    }

    /// <summary>
    /// 绑定所属流程架构；所需模型须先注册。
    /// </summary>
    /// <param name="architecture">所属架构。</param>
    public HeroSystem(IArchitecture architecture) : base(architecture)
    {
    }

    /// <summary>
    /// 从完整档案建立本局数值，随后监听成长与穿戴。
    /// </summary>
    protected override void OnInit()
    {
        ProfileModel profile = this.GetModel<ProfileModel>();
        Tables tables = this.GetModel<ConfigurationModel>().Tables;
        BattleConfig battle = tables.TbBattleConfig.Data;
        // 每位英雄先建立状态，再按同一顺序初始化属性和无双，最后注册本人的三个来源。
        foreach (int heroId in profile.HeroIds)
        {
            this.GetModel<HeroModel>().CreateState(heroId);
            HeroProgressionState progress = profile.GetProgress(heroId).Value;
            EquippedItems slots = profile.GetSlots(heroId).Value;
            Reset(heroId, progress.Level,
                tables.TbHeroGrowthConfig.Get(heroId).Levels[progress.Level - 1].BaseAttributes,
                slots.Weapon, slots.Armor, slots.Accessory, slots.Treasure);
            ResetWs(heroId, battle.WsMax, battle.WsDecayPerSecond, battle.WsDrainPerSecond);
            Action<HeroProgressionState> onProgress = progress => OnProgressChanged(heroId, progress);
            Action<EquippedItems> onSlots = items => OnEquipmentChanged(heroId, items);
            Action<IReadOnlyDictionary<PassiveId, int>> onPassives = levels => OnPassiveLevelsChanged(heroId, levels);
            _subscriptions.Add(heroId, (onProgress, onSlots, onPassives));
            profile.GetProgress(heroId).Register(onProgress, priority: 100);
            profile.GetSlots(heroId).Register(onSlots, priority: 100);
            this.GetModel<SkillModel>().GetPassiveLevels(heroId).Register(onPassives, priority: 100);
        }
    }

    /// <summary>
    /// 移除系统时退订持久来源并结束无双；模型稍后由流程移除。
    /// </summary>
    protected override void OnDeinit()
    {
        // 只退订本周期实际登记的闭包，不能用另一英雄的状态推断订阅身份。
        foreach (var (heroId, callbacks) in _subscriptions)
        {
            this.GetModel<ProfileModel>().GetProgress(heroId).UnRegister(callbacks.Progress);
            this.GetModel<ProfileModel>().GetSlots(heroId).UnRegister(callbacks.Slots);
            this.GetModel<SkillModel>().GetPassiveLevels(heroId).UnRegister(callbacks.Passives);
        }
        _subscriptions.Clear();
        foreach (HeroState state in this.GetModel<HeroModel>().Heroes.Values)
        {
            ClearWs(state.HeroId);
            ClearHurtProtection(state);
        }
        this.GetSystem<HeroStatusSystem>().ClearAll();
        this.GetModel<HeroModel>().ClearStates();
    }

    /// <summary>
    /// 应用伤害；首次归零时结束无双，死亡动作交给实体。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <param name="amount">伤害值。</param>
    /// <returns>本次是否首次归零。</returns>
    public bool Damage(int heroId, int amount)
    {
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        if (amount <= 0 || state.IsDepleted)
        {
            return false;
        }

        state.Hp.Value = Math.Max(0, state.Hp.Value - amount);
        bool died = state.IsDepleted;
        if (died)
        {
            ClearWs(heroId);
            this.GetSystem<HeroStatusSystem>().ClearAll(heroId);
            ClearHurtProtection(state);
        }
        return died;
    }

    /// <summary>
    /// 恢复存活英雄生命，不超过最终上限。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <param name="amount">恢复量。</param>
    public void Heal(int heroId, int amount)
    {
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        if (amount > 0 && !state.IsDepleted)
        {
            state.Hp.Value = (int)Math.Min(state.MaxHp.Value, (long)state.Hp.Value + amount);
        }
    }

    /// <summary>
    /// 记录一次已结算且未闪避的直接命中；第五次伤害仍生效，保护后续命中。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    public void RecordHurt(int heroId)
    {
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        if (state.IsDepleted || state.HurtInvulnerabilitySeconds > 0)
        {
            return;
        }

        state.HurtHitCount++;
        state.HurtChainSeconds = HurtChainInterval;
        if (state.HurtHitCount >= HurtProtectionHitCount)
        {
            state.HurtHitCount = 0;
            state.HurtInvulnerabilitySeconds = HurtProtectionDuration;
        }
    }

    /// <summary>
    /// 推进连续受击与保护时钟；暂停不调用，技能无敌不会覆盖这份计时。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <param name="delta">本步游戏秒数。</param>
    public void AdvanceHurtProtection(int heroId, float delta)
    {
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        if (!float.IsFinite(delta) || delta <= 0)
        {
            return;
        }

        state.HurtInvulnerabilitySeconds = Math.Max(0, state.HurtInvulnerabilitySeconds - delta);
        if (state.HurtInvulnerabilitySeconds < TickTolerance)
        {
            state.HurtInvulnerabilitySeconds = 0;
        }

        state.HurtChainSeconds = Math.Max(0, state.HurtChainSeconds - delta);
        if (state.HurtChainSeconds < TickTolerance)
        {
            state.HurtChainSeconds = 0;
            state.HurtHitCount = 0;
        }
    }

    /// <summary>
    /// 新局、死亡或离关时清除命中记录与无敌，池复用不继承旧保护。
    /// </summary>
    /// <param name="state">本次处理的英雄状态。</param>
    private void ClearHurtProtection(HeroState state)
    {
        state.HurtHitCount = 0;
        state.HurtChainSeconds = 0;
        state.HurtInvulnerabilitySeconds = 0;
    }

    /// <summary>
    /// 按官方秒间隔结算装备回血回蓝；负回血交给实体提交死亡事实，不产生受击反馈。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <param name="delta">本步实际推进的游戏秒数；暂停时不传正值。</param>
    /// <returns>本步到期的生命流失值；调用方立即通过持续伤害入口结算。</returns>
    public int AdvanceRecovery(int heroId, float delta)
    {
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        if (delta <= 0f || !float.IsFinite(delta) || state.IsDepleted)
        {
            return 0;
        }

        state.RecoveryProgress += delta;
        double ticks = Math.Floor((state.RecoveryProgress + TickTolerance) / RecoveryInterval);
        if (ticks < 1)
        {
            return 0;
        }

        state.RecoveryProgress = Math.Max(0, state.RecoveryProgress - ticks * RecoveryInterval);
        // 使用同一个节拍，穿卸改变下次结算值，不重新开始累计；负回蓝也保留源文件钳制规则。
        state.Mp.Value = (int)Math.Clamp(state.Mp.Value + ticks * state.MpRegen.Value, 0d, state.MaxMp.Value);
        double hp = ticks * state.HpRegen.Value;
        if (hp >= 0)
        {
            Heal(heroId, (int)Math.Min(int.MaxValue, hp));
            return 0;
        }
        return (int)Math.Min(int.MaxValue, -hp);
    }

    /// <summary>
    /// 消耗魔法；不足时保持状态。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <param name="amount">消耗量。</param>
    /// <returns>是否成功。</returns>
    public bool TrySpendMp(int heroId, int amount)
    {
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        if (amount <= 0)
        {
            return true;
        }

        if (state.Mp.Value < amount)
        {
            return false;
        }

        state.Mp.Value -= amount;
        return true;
    }

    /// <summary>
    /// 恢复魔法，不超过最终上限。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <param name="amount">恢复量。</param>
    public void RestoreMp(int heroId, int amount)
    {
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        if (amount > 0)
        {
            state.Mp.Value = (int)Math.Min(state.MaxMp.Value, (long)state.Mp.Value + amount);
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
    /// <param name="treasureData">当前法宝。</param>
    internal void Reset(int heroId, int level, ActorBaseAttributes baseAttributes,
        EquipmentData weaponData = null, EquipmentData armorData = null, EquipmentData accessoryData = null,
        EquipmentData treasureData = null)
    {
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        ArgumentNullException.ThrowIfNull(baseAttributes);
        state.Sources.Clear();
        ReplacePassiveSource(state, this.GetModel<SkillModel>().GetPassiveLevels(heroId));
        state.RecoveryProgress = 0;
        ClearHurtProtection(state);
        this.GetSystem<HeroStatusSystem>().ClearAll(heroId);
        SetGrowth(heroId, baseAttributes, level, weaponData, armorData, refill: true, accessoryData, treasureData);
        state.ControlsEnabled = true;
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
    /// <param name="treasureData">法宝。</param>
    internal void SetGrowth(int heroId, ActorBaseAttributes baseAttributes, int level,
        EquipmentData weaponData, EquipmentData armorData, bool refill, EquipmentData accessoryData = null,
        EquipmentData treasureData = null)
    {
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        ArgumentNullException.ThrowIfNull(baseAttributes);
        state.BaseAttributes = baseAttributes;
        ReplaceEquipmentSources(state, weaponData, armorData, accessoryData, treasureData);
        Recalculate(heroId, level, refill);
    }

    /// <summary>
    /// 登记技能/Buff 等独立来源，替换同来源修正并立即重算。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <param name="source">有效来源键。</param>
    /// <param name="modifiers">完整修正集合。</param>
    /// <exception cref="ArgumentException">来源无效。</exception>
    public void SetSource(int heroId, AttributeSourceId source, IReadOnlyList<AttributeModifier> modifiers)
    {
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        if (source.Category == null)
        {
            throw new ArgumentException("属性来源无效。", nameof(source));
        }

        ArgumentNullException.ThrowIfNull(modifiers);
        state.Sources[source] = modifiers.ToArray();
        Recalculate(state.HeroId, state.Level.Value, refill: false);
    }

    /// <summary>
    /// 结束独立来源并重新发布最终值。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <param name="source">来源键。</param>
    /// <returns>是否存在该来源。</returns>
    public bool RemoveSource(int heroId, AttributeSourceId source)
    {
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        if (!state.Sources.Remove(source))
        {
            return false;
        }

        Recalculate(state.HeroId, state.Level.Value, refill: false);
        return true;
    }

    /// <summary>
    /// 等级变化时使用已提交的成长和装备重算，升级补满、回滚不补满。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <param name="progress">完整成长状态。</param>
    private void OnProgressChanged(int heroId, HeroProgressionState progress)
    {
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        if (progress.Level == state.Level.Value)
        {
            return;
        }

        HeroGrowthLevel row = this.GetModel<ConfigurationModel>().Tables.TbHeroGrowthConfig.Get(state.HeroId).Levels[progress.Level - 1];
        EquippedItems slots = this.GetModel<ProfileModel>().GetSlots(state.HeroId).Value;
        SetGrowth(heroId, row.BaseAttributes, progress.Level, slots.Weapon, slots.Armor,
            refill: progress.Level > state.Level.Value, slots.Accessory, slots.Treasure);
    }

    /// <summary>
    /// 装备事务完成后重算，保留其余来源且不补血。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <param name="slots">已经提交的穿戴。</param>
    private void OnEquipmentChanged(int heroId, EquippedItems slots)
    {
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        ReplaceEquipmentSources(state, slots.Weapon, slots.Armor, slots.Accessory, slots.Treasure);
        Recalculate(state.HeroId, state.Level.Value, refill: false);
    }

    /// <summary>
    /// 被动升级或回滚时替换总加成；增加资源上限时同时增加当前资源，保留原有缺损。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <param name="levels">当前英雄已经提交的全部被动等级。</param>
    private void OnPassiveLevelsChanged(int heroId, IReadOnlyDictionary<PassiveId, int> levels)
    {
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        int maxHp = state.MaxHp.Value;
        int maxMp = state.MaxMp.Value;
        ReplacePassiveSource(state, levels);
        Recalculate(state.HeroId, state.Level.Value, refill: false);
        Heal(heroId, state.MaxHp.Value - maxHp);
        RestoreMp(heroId, state.MaxMp.Value - maxMp);
    }

    /// <summary>
    /// 各被动只读取已学习等级的总值，避免把1至当前等级重复累加。
    /// </summary>
    /// <param name="state">本次处理的英雄状态。</param>
    /// <param name="levels">本英雄的被动等级。</param>
    private void ReplacePassiveSource(HeroState state, IReadOnlyDictionary<PassiveId, int> levels)
    {
        TbPassiveConfig definitions = this.GetModel<ConfigurationModel>().Tables.TbPassiveConfig;
        List<AttributeModifier> modifiers = new();
        foreach ((PassiveId id, int level) in levels)
        {
            if (level == 0)
            {
                continue;
            }

            PassiveConfig definition = definitions.Get(id);
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
        state.Sources[s_passiveSource] = modifiers.ToArray();
    }

    /// <summary>
    /// 整体替换当前穿戴部位的属性来源。
    /// </summary>
    /// <param name="state">本次处理的英雄状态。</param>
    /// <param name="weaponData">当前武器。</param>
    /// <param name="armorData">当前防具。</param>
    /// <param name="accessoryData">当前饰品。</param>
    /// <param name="treasureData">当前法宝。</param>
    private void ReplaceEquipmentSources(HeroState state, EquipmentData weaponData, EquipmentData armorData,
        EquipmentData accessoryData, EquipmentData treasureData)
    {
        state.Sources.Remove(s_weaponSource);
        state.Sources.Remove(s_armorSource);
        state.Sources.Remove(s_accessorySource);
        state.Sources.Remove(s_treasureSource);
        if (weaponData != null)
        {
            state.Sources[s_weaponSource] = weaponData.Attributes.EnumerateModifiers().ToArray();
        }

        if (armorData != null)
        {
            state.Sources[s_armorSource] = armorData.Attributes.EnumerateModifiers().ToArray();
        }

        if (accessoryData != null)
        {
            state.Sources[s_accessorySource] = accessoryData.Attributes.EnumerateModifiers().ToArray();
        }

        if (treasureData != null)
        {
            state.Sources[s_treasureSource] = treasureData.Attributes.EnumerateModifiers().ToArray();
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
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        int count = s_attributeCount;
        Span<float> flat = stackalloc float[count];
        Span<float> percentAdd = stackalloc float[count];
        Span<float> percentMult = stackalloc float[count];
        percentMult.Fill(1f);
        foreach (AttributeModifier[] modifiers in state.Sources.Values)
        {
            foreach (AttributeModifier modifier in modifiers)
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
            float value = ((state.BaseAttributes?.GetValue(attribute) ?? 0) + flat[i])
                                       * (1 + percentAdd[i]) * percentMult[i];
            if (attribute is not (AttributeType.HpRegen or AttributeType.MpRegen))
            {
                value = MathF.Max(0, value);
            }

            final[i] = attribute.IsProbability ? Math.Clamp(value, 0f, 1f)
                            : MathF.Round(value, MidpointRounding.AwayFromZero);
        }
        final[(int)AttributeType.MaxHp] = Math.Max(1, final[(int)AttributeType.MaxHp]);
        ApplyFinalAttributes(state, final, level, refill);
    }

    /// <summary>
    /// 接受战斗系统已经结算的命中收益；死亡或开启无双期间不接受增长。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <param name="result">同一次命中的结算结果。</param>
    public void RecordHit(int heroId, DamageResult result)
    {
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        if (state.IsDepleted || state.WsActive.Value || result.IsMiss)
        {
            return;
        }

        AddWs(heroId, result.AttackerWsGain);
    }

    /// <summary>
    /// 系统初始化时设置本局无双参数并清零。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <param name="max">无双上限，下限 0。</param>
    /// <param name="decayPerSecond">自然衰减速度，有限正点数/秒。</param>
    /// <param name="drainPerSecond">开启后额外消耗速度，有限正点数/秒。</param>
    /// <exception cref="ArgumentOutOfRangeException">衰减参数非法。</exception>
    internal void ResetWs(int heroId, int max, float decayPerSecond, float drainPerSecond)
    {
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        if (!float.IsFinite(decayPerSecond) || decayPerSecond <= 0f
                                            || !float.IsFinite(drainPerSecond) || drainPerSecond <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(decayPerSecond), "无双每秒衰减和消耗必须为有限正数。");
        }

        state.WsDecayPerSecond = decayPerSecond;
        state.WsDecayProgress = 0;
        state.WsDrainPerSecond = drainPerSecond;
        state.WsDrainProgress = 0;
        SetWsState(state, 0, Math.Max(0, max), false);
    }

    /// <summary>
    /// 按官方规则累计：正在开启或本次加值会超过上限时整次不增加。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <param name="amount">增加量；非正数不处理，满值保持。</param>
    internal void AddWs(int heroId, int amount)
    {
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        if (!state.WsActive.Value)
        {
            AddWsFromPickup(heroId, amount);
        }
    }

    /// <summary>
    /// 黄球增加无双；开启期间也可补充，超过上限仍整次忽略。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <param name="amount">本颗黄球的配置值，通常为 5。</param>
    public void AddWsFromPickup(int heroId, int amount)
    {
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        if (amount <= 0 || amount > state.MaxWs.Value - state.Ws.Value)
        {
            return;
        }

        SetWsState(state, state.Ws.Value + amount, state.MaxWs.Value, state.WsActive.Value);
    }

    /// <summary>
    /// 蓄满时开启无双；激活期间不再次开启，也不累计命中收益。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <returns>是否已进入无双状态。</returns>
    public bool TryActivateWs(int heroId)
    {
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        if (state.IsDepleted || !state.ControlsEnabled || this.GetSystem<HeroStatusSystem>().IsFrozen(heroId) || !state.IsWsFull)
        {
            return false;
        }

        state.WsDrainProgress = 0;
        SetWsState(state, state.Ws.Value, state.MaxWs.Value, true);
        return true;
    }

    /// <summary>
    /// 转场阶段关闭玩家控制但保留动作、重力与效果计时；新局模型默认恢复开放。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <param name="enabled">是否允许新的玩家操作。</param>
    public void SetControlsEnabled(int heroId, bool enabled)
    {
        this.GetModel<HeroModel>().GetState(heroId).ControlsEnabled = enabled;
    }

    /// <summary>
    /// 推进未满时的自然衰减或开启后的耗尽；满值保留，暂停时宿主不推进。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <param name="delta">本次物理步长秒数。</param>
    public void AdvanceWs(int heroId, float delta)
    {
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        if (!float.IsFinite(delta) || delta <= 0f)
        {
            return;
        }

        if (state.IsWsFull || state.WsDecayPerSecond <= 0f)
        {
            return;
        }

        int value = state.Ws.Value;
        bool wasActive = state.WsActive.Value;
        // 官方满值不推进自然衰减；首次开启扣点后，普通衰减恢复。
        double decayDelta = delta;
        if (state.WsActive.Value)
        {
            if (state.Ws.Value == state.MaxWs.Value)
            {
                decayDelta = Math.Max(0, delta - (1 - state.WsDrainProgress) / state.WsDrainPerSecond);
            }

            state.WsDrainProgress += delta * (double)state.WsDrainPerSecond;
            double ticks = Math.Floor(state.WsDrainProgress + TickTolerance);
            state.WsDrainProgress = Math.Max(0, state.WsDrainProgress - ticks);
            value = (int)Math.Max(0, value - ticks);
        }

        state.WsDecayProgress += decayDelta * state.WsDecayPerSecond;
        double decayTicks = Math.Floor(state.WsDecayProgress + TickTolerance);
        state.WsDecayProgress = Math.Max(0, state.WsDecayProgress - decayTicks);
        value = (int)Math.Max(0, value - decayTicks);
        SetWsState(state, value, state.MaxWs.Value, value > 0 && wasActive);
    }

    /// <summary>
    /// 立即结束并清空进度（死亡或模型移除），不影响无双上限。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    public void ClearWs(int heroId)
    {
        HeroState state = this.GetModel<HeroModel>().GetState(heroId);
        state.WsDrainProgress = 0;
        state.WsDecayProgress = 0;
        SetWsState(state, 0, state.MaxWs.Value, false);
    }

    /// <summary>
    /// 更新最终属性与资源；各属性由 Value 自动通知。
    /// </summary>
    /// <param name="state">本次处理的英雄状态。</param>
    /// <param name="final">按 AttributeType 排列的最终数值。</param>
    /// <param name="level">对应等级。</param>
    /// <param name="refill">是否补满生命魔法。</param>
    private void ApplyFinalAttributes(HeroState state, ReadOnlySpan<float> final, int level, bool refill)
    {
        state.MaxHp.Value = (int)final[(int)AttributeType.MaxHp];
        state.MaxMp.Value = (int)final[(int)AttributeType.MaxMp];
        state.Power.Value = (int)final[(int)AttributeType.Power];
        state.Def.Value = (int)final[(int)AttributeType.Def];
        state.Mdef.Value = (int)final[(int)AttributeType.Mdef];
        state.Crit.Value = final[(int)AttributeType.Crit];
        state.Miss.Value = final[(int)AttributeType.Miss];
        state.Lucky.Value = (int)final[(int)AttributeType.Lucky];
        state.Toughness.Value = (int)final[(int)AttributeType.Toughness];
        state.Htarget.Value = final[(int)AttributeType.Htarget];
        state.CritReduce.Value = final[(int)AttributeType.CritReduce];
        state.Ar.Value = (int)final[(int)AttributeType.Ar];
        state.Sp.Value = (int)final[(int)AttributeType.Sp];
        state.Vampirism.Value = (int)final[(int)AttributeType.Vampirism];
        state.HpRegen.Value = (int)final[(int)AttributeType.HpRegen];
        state.MpRegen.Value = (int)final[(int)AttributeType.MpRegen];
        state.Hp.Value = refill ? state.MaxHp.Value : Math.Min(state.Hp.Value, state.MaxHp.Value);
        state.Mp.Value = refill ? state.MaxMp.Value : Math.Min(state.Mp.Value, state.MaxMp.Value);
        state.Level.Value = level;
    }

    /// <summary>
    /// 按上限、当前值、开启状态的业务顺序更新无双。
    /// </summary>
    /// <param name="state">本次处理的英雄状态。</param>
    /// <param name="value">当前值。</param>
    /// <param name="max">当前上限。</param>
    /// <param name="active">是否开启。</param>
    private void SetWsState(HeroState state, int value, int max, bool active)
    {
        state.MaxWs.Value = max;
        state.Ws.Value = value;
        state.WsActive.Value = active;
    }
}
