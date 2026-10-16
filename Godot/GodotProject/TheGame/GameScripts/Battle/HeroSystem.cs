using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.Attributes;
using GameLogic.Architecture;
using GameLogic.Battle.Attributes;
using GameLogic.Config;
using GameLogic.Equipment;
using GameLogic.Profile;

namespace GameLogic.Battle;

/// <summary>英雄玩法数据入口：协调成长、穿戴、最终属性、生命魔法和无双；不持有表现节点。</summary>
public sealed class HeroSystem : AbstractSystem
{
    private static readonly int m_AttributeCount = Enum.GetValues<AttributeType>().Length; // 配置属性的连续数量。
    private static readonly AttributeSourceId m_WeaponSource = new("EquipmentWeapon", 0); // 当前武器的来源键。
    private static readonly AttributeSourceId m_ArmorSource = new("EquipmentArmor", 0); // 当前防具的来源键。
    private const double TickTolerance = 1e-5; // 秒数累计的浮点点数容差。

    /// <summary>即时借用本局英雄数值，不缓存模型引用。</summary>
    private HeroModel Hero => this.GetModel<HeroModel>();

    /// <summary>无双当前是否提供霸体，供身体状态机查询。</summary>
    public bool SuperArmor => this.GetModel<HeroModel>().WsActive.Value;

    /// <summary>当前横向移动倍率，不改写共享角色参数。</summary>
    public float MoveSpeedMultiplier => SuperArmor
        ? this.GetModel<ConfigurationModel>().Tables.TbBattleConfig.WsMoveSpeedMultiplier
        : 1f;

    /// <summary>当前起跳倍率。</summary>
    public float JumpSpeedMultiplier => SuperArmor
        ? this.GetModel<ConfigurationModel>().Tables.TbBattleConfig.WsJumpSpeedMultiplier
        : 1f;

    /// <summary>当前重力倍率。</summary>
    public float GravityMultiplier => SuperArmor
        ? this.GetModel<ConfigurationModel>().Tables.TbBattleConfig.WsGravityMultiplier
        : 1f;

    /// <summary>整招攻击威力倍率，包含招式固定加值。</summary>
    public float AttackPowerMultiplier => SuperArmor
        ? this.GetModel<ConfigurationModel>().Tables.TbBattleConfig.WsAttackMultiplier
        : 1f;

    /// <summary>绑定所属流程架构；所需模型须先注册。</summary>
    /// <param name="architecture">所属架构。</param>
    public HeroSystem(IArchitecture architecture) : base(architecture) { }

    /// <summary>从完整档案建立本局数值，随后监听成长与穿戴。</summary>
    protected override void OnInit()
    {
        var profile = this.GetModel<ProfileModel>();
        var tables = this.GetModel<ConfigurationModel>().Tables;
        Reset(profile.ActiveHeroId, profile.Level,
            tables.TbHeroGrowthConfig.Get(profile.ActiveHeroId, profile.Level).BaseAttributes,
            profile.Weapon, profile.Armor);
        var battle = tables.TbBattleConfig.Data;
        ResetWs(battle.WsMax, battle.WsDecayPerSecond, battle.WsDrainPerSecond);
        profile.Progress.Register(OnProgressChanged, priority: 100);
        profile.Slots.Register(OnEquipmentChanged, priority: 100);
    }

    /// <summary>移除系统时退订持久来源并结束无双；模型稍后由流程移除。</summary>
    protected override void OnDeinit()
    {
        this.GetModel<ProfileModel>().Progress.UnRegister(OnProgressChanged);
        this.GetModel<ProfileModel>().Slots.UnRegister(OnEquipmentChanged);
        ClearWs();
    }

    /// <summary>应用伤害；首次归零时结束无双，死亡动作交给实体。</summary>
    /// <param name="amount">伤害值。</param>
    /// <returns>本次是否首次归零。</returns>
    public bool Damage(int amount)
    {
        if (amount <= 0 || Hero.IsDepleted)
            return false;
        Hero.Hp.Value = Math.Max(0, Hero.Hp.Value - amount);
        bool died = Hero.IsDepleted;
        if (died)
            ClearWs();
        return died;
    }

    /// <summary>恢复存活英雄生命，不超过最终上限。</summary>
    /// <param name="amount">恢复量。</param>
    public void Heal(int amount)
    {
        if (amount > 0 && !Hero.IsDepleted)
            Hero.Hp.Value = (int)Math.Min(Hero.MaxHp.Value, (long)Hero.Hp.Value + amount);
    }

    /// <summary>消耗魔法；不足时保持状态。</summary>
    /// <param name="amount">消耗量。</param>
    /// <returns>是否成功。</returns>
    public bool TrySpendMp(int amount)
    {
        if (amount <= 0)
            return true;
        if (Hero.Mp.Value < amount)
            return false;
        Hero.Mp.Value -= amount;
        return true;
    }

    /// <summary>恢复魔法，不超过最终上限。</summary>
    /// <param name="amount">恢复量。</param>
    public void RestoreMp(int amount)
    {
        if (amount > 0)
            Hero.Mp.Value = (int)Math.Min(Hero.MaxMp.Value, (long)Hero.Mp.Value + amount);
    }

    /// <summary>开始本局时清除旧来源，整体应用成长与穿戴。</summary>
    /// <param name="heroId">英雄身份。</param>
    /// <param name="level">成长等级。</param>
    /// <param name="baseAttributes">对应基础值。</param>
    /// <param name="weapon">当前武器。</param>
    /// <param name="armor">当前防具。</param>
    internal void Reset(int heroId, int level, ActorBaseAttributes baseAttributes,
        EquipmentInstance weapon = null, EquipmentInstance armor = null)
    {
        ArgumentNullException.ThrowIfNull(baseAttributes);
        Hero.Sources.Clear();
        SetGrowth(baseAttributes, heroId, level, weapon, armor, refill: true);
    }

    /// <summary>同步当前成长与穿戴并重算属性，保留技能/Buff 来源。</summary>
    /// <param name="baseAttributes">当前基础值。</param>
    /// <param name="heroId">当前英雄。</param>
    /// <param name="level">当前等级。</param>
    /// <param name="weapon">武器。</param>
    /// <param name="armor">防具。</param>
    /// <param name="refill">升级是否补满资源。</param>
    internal void SetGrowth(ActorBaseAttributes baseAttributes, int heroId, int level,
        EquipmentInstance weapon, EquipmentInstance armor, bool refill)
    {
        ArgumentNullException.ThrowIfNull(baseAttributes);
        Hero.BaseAttributes = baseAttributes;
        ReplaceEquipmentSources(weapon, armor);
        Recalculate(heroId, level, refill);
    }

    /// <summary>登记技能/Buff 等独立来源，替换同来源修正并立即重算。</summary>
    /// <param name="source">有效来源键。</param>
    /// <param name="modifiers">完整修正集合。</param>
    /// <exception cref="ArgumentException">来源无效。</exception>
    public void SetSource(AttributeSourceId source, IReadOnlyList<AttributeModifier> modifiers)
    {
        if (source.Category == null)
            throw new ArgumentException("属性来源无效。", nameof(source));
        ArgumentNullException.ThrowIfNull(modifiers);
        Hero.Sources[source] = modifiers.ToArray();
        Recalculate(Hero.HeroId, Hero.Level.Value, refill: false);
    }

    /// <summary>结束独立来源并重新发布最终值。</summary>
    /// <param name="source">来源键。</param>
    /// <returns>是否存在该来源。</returns>
    public bool RemoveSource(AttributeSourceId source)
    {
        if (!Hero.Sources.Remove(source))
            return false;
        Recalculate(Hero.HeroId, Hero.Level.Value, refill: false);
        return true;
    }

    /// <summary>等级变化时使用已提交的成长和装备重算，升级补满、回滚不补满。</summary>
    /// <param name="progress">完整成长状态。</param>
    private void OnProgressChanged(HeroProgressionState progress)
    {
        if (progress.Level == Hero.Level.Value)
            return;
        var row = this.GetModel<ConfigurationModel>().Tables.TbHeroGrowthConfig.Get(Hero.HeroId, progress.Level);
        var profile = this.GetModel<ProfileModel>();
        SetGrowth(row.BaseAttributes, Hero.HeroId, progress.Level, profile.Weapon, profile.Armor,
            refill: progress.Level > Hero.Level.Value);
    }

    /// <summary>装备事务完成后重算，保留其余来源且不补血。</summary>
    /// <param name="slots">已经提交的穿戴。</param>
    private void OnEquipmentChanged(EquippedItems slots)
    {
        ReplaceEquipmentSources(slots.Weapon, slots.Armor);
        Recalculate(Hero.HeroId, Hero.Level.Value, refill: false);
    }

    /// <summary>整体替换两个装备来源。</summary>
    /// <param name="weapon">当前武器。</param>
    /// <param name="armor">当前防具。</param>
    private void ReplaceEquipmentSources(EquipmentInstance weapon, EquipmentInstance armor)
    {
        Hero.Sources.Remove(m_WeaponSource);
        Hero.Sources.Remove(m_ArmorSource);
        if (weapon != null)
            Hero.Sources[m_WeaponSource] = weapon.Attributes.EnumerateModifiers().ToArray();
        if (armor != null)
            Hero.Sources[m_ArmorSource] = armor.Attributes.EnumerateModifiers().ToArray();
    }

    /// <summary>按固定、加算百分比、乘算百分比顺序汇总，再原子提交模型。</summary>
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

        Span<int> final = stackalloc int[count];
        for (int i = 0; i < count; i++)
            final[i] = (int)MathF.Round(MathF.Max(0, ((Hero.BaseAttributes?.Get((AttributeType)i) ?? 0) + flat[i])
                                                     * (1 + percentAdd[i]) * percentMult[i]),
                MidpointRounding.AwayFromZero);
        final[(int)AttributeType.MaxHp] = Math.Max(1, final[(int)AttributeType.MaxHp]);
        ApplyFinalAttributes(final, heroId, level, refill);
    }

    /// <summary>按实际伤害与出招时攻击力结算命中无双；开启期间不增长。</summary>
    /// <param name="damage">实际伤害。</param>
    /// <param name="basePower">出招时基础攻击力。</param>
    public void RecordHit(int damage, int basePower)
    {
        if (Hero.IsDepleted)
            return;
        float coefficient = this.GetModel<ConfigurationModel>().Tables.TbBattleConfig.WsGainPerDamageRatio;
        if (damage <= 0 || basePower <= 0 || !float.IsFinite(coefficient) || coefficient <= 0f)
            return;
        AddWs((int)Math.Min(int.MaxValue, Math.Floor((double)damage / basePower * coefficient + .5)));
    }

    /// <summary>系统初始化时设置本局无双参数并清零。</summary>
    /// <param name="max">无双上限，下限 0。</param>
    /// <param name="decayPerSecond">自然衰减速度，有限正点数/秒。</param>
    /// <param name="drainPerSecond">开启后额外消耗速度，有限正点数/秒。</param>
    /// <exception cref="ArgumentOutOfRangeException">衰减参数非法。</exception>
    internal void ResetWs(int max, float decayPerSecond, float drainPerSecond)
    {
        var model = this.GetModel<HeroModel>();
        if (!float.IsFinite(decayPerSecond) || decayPerSecond <= 0f
                                            || !float.IsFinite(drainPerSecond) || drainPerSecond <= 0f)
            throw new ArgumentOutOfRangeException(nameof(decayPerSecond), "无双每秒衰减和消耗必须为有限正数。");
        model.WsDecayPerSecond = decayPerSecond;
        model.WsDecayProgress = 0;
        model.WsDrainPerSecond = drainPerSecond;
        model.WsDrainProgress = 0;
        SetWsState(0, Math.Max(0, max), false);
    }

    /// <summary>按官方规则累计：正在开启或本次加值会超过上限时整次不增加。</summary>
    /// <param name="amount">增加量；非正数不处理，满值保持。</param>
    internal void AddWs(int amount)
    {
        var model = this.GetModel<HeroModel>();
        if (!model.WsActive.Value)
            AddWsFromPickup(amount);
    }

    /// <summary>黄球增加无双；开启期间也可补充，超过上限仍整次忽略。</summary>
    /// <param name="amount">本颗黄球的配置值，通常为 5。</param>
    public void AddWsFromPickup(int amount)
    {
        var model = this.GetModel<HeroModel>();
        if (amount <= 0 || amount > model.MaxWs.Value - model.Ws.Value)
            return;
        SetWsState(model.Ws.Value + amount, model.MaxWs.Value, model.WsActive.Value);
    }

    /// <summary>蓄满时开启无双；激活期间不再次开启，也不累计命中收益。</summary>
    /// <returns>是否已进入无双状态。</returns>
    public bool TryActivateWs()
    {
        var model = this.GetModel<HeroModel>();
        if (Hero.IsDepleted || !model.IsWsFull)
        {
            return false;
        }

        model.WsDrainProgress = 0;
        SetWsState(model.Ws.Value, model.MaxWs.Value, true);
        return true;
    }

    /// <summary>推进未满时的自然衰减或开启后的耗尽；满值保留，暂停时宿主不推进。</summary>
    /// <param name="delta">本次物理步长秒数。</param>
    public void AdvanceWs(float delta)
    {
        var model = this.GetModel<HeroModel>();
        if (!float.IsFinite(delta) || delta <= 0f)
        {
            return;
        }

        if (model.IsWsFull || model.WsDecayPerSecond <= 0f)
            return;
        int value = model.Ws.Value;
        bool wasActive = model.WsActive.Value;
        // 官方满值不推进自然衰减；首次开启扣点后，普通衰减恢复。
        double decayDelta = delta;
        if (model.WsActive.Value)
        {
            if (model.Ws.Value == model.MaxWs.Value)
                decayDelta = Math.Max(0, delta - (1 - model.WsDrainProgress) / model.WsDrainPerSecond);
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

    /// <summary>立即结束并清空进度（死亡或模型移除），不影响无双上限。</summary>
    public void ClearWs()
    {
        var model = this.GetModel<HeroModel>();
        model.WsDrainProgress = 0;
        model.WsDecayProgress = 0;
        SetWsState(0, model.MaxWs.Value, false);
    }

    /// <summary>更新最终属性与资源；各属性由 Value 自动通知。</summary>
    /// <param name="final">按 AttributeType 排列的最终数值。</param>
    /// <param name="heroId">本局英雄身份。</param>
    /// <param name="level">对应等级。</param>
    /// <param name="refill">是否补满生命魔法。</param>
    private void ApplyFinalAttributes(ReadOnlySpan<int> final, int heroId, int level, bool refill)
    {
        Hero.HeroId = heroId;
        for (int i = 0; i < final.Length; i++)
            Hero.GetAttribute((AttributeType)i).Value = final[i];
        Hero.Hp.Value = refill ? Hero.MaxHp.Value : Math.Min(Hero.Hp.Value, Hero.MaxHp.Value);
        Hero.Mp.Value = refill ? Hero.MaxMp.Value : Math.Min(Hero.Mp.Value, Hero.MaxMp.Value);
        Hero.Level.Value = level;
    }

    /// <summary>按上限、当前值、开启状态的业务顺序更新无双。</summary>
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
