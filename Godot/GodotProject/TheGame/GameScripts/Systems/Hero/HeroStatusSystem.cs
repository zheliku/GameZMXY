using System;
using System.Collections.Generic;
using GameConfig.Battle;
using GameLogic.Architecture;

namespace GameLogic.Battle;

/// <summary>
/// 英雄冻结、中毒的刷新、推进与清理规则；和 HeroSystem 共用同一 HeroModel。
/// </summary>
public sealed class HeroStatusSystem : AbstractSystem
{
    private const double StatusTickTolerance = 1e-5; // 秒制累计的浮点边界容差。

    /// <summary>
    /// 绑定所属本局架构，英雄模型必须已注册。
    /// </summary>
    /// <param name="architecture">所属本局架构。</param>
    public HeroStatusSystem(IArchitecture architecture) : base(architecture)
    {
    }

    /// <summary>
    /// 指定英雄是否被冻结。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <returns>冻结剩余时间为正时为 true。</returns>
    public bool IsFrozen(int heroId)
    {
        return this.GetModel<HeroModel>().GetState(heroId).GetStatusEffect(HeroState.StatusEffectKind.Freeze)?.RemainingSeconds > 0;
    }

    /// <summary>
    /// 指定英雄是否中毒。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <returns>存在中毒效果时为 true。</returns>
    public bool IsPoisoned(int heroId)
    {
        return this.GetModel<HeroModel>().GetState(heroId).GetStatusEffect(HeroState.StatusEffectKind.Poison) != null;
    }

    /// <summary>
    /// 施加冻结；重复施加只保留更长的剩余时间。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <param name="seconds">有限且为正的游戏秒数。</param>
    public void ApplyFreeze(int heroId, float seconds)
    {
        HeroState hero = this.GetModel<HeroModel>().GetState(heroId);
        if (!float.IsFinite(seconds) || seconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seconds));
        }

        if (hero.IsDepleted)
        {
            return;
        }

        HeroState.StatusEffectState state = hero.GetStatusEffect(HeroState.StatusEffectKind.Freeze)
            ?? new HeroState.StatusEffectState(HeroState.StatusEffectKind.Freeze);
        state.RemainingSeconds = Math.Max(state.RemainingSeconds, seconds);
        hero.SetStatusEffect(state);
    }

    /// <summary>
    /// 推进冻结计时；暂停传入零。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <param name="delta">非负游戏秒数。</param>
    public void AdvanceFreeze(int heroId, float delta)
    {
        HeroState hero = this.GetModel<HeroModel>().GetState(heroId);
        if (!float.IsFinite(delta) || delta <= 0)
        {
            return;
        }

        HeroState.StatusEffectState state = hero.GetStatusEffect(HeroState.StatusEffectKind.Freeze);
        if (state == null)
        {
            return;
        }

        state.RemainingSeconds = Math.Max(0, state.RemainingSeconds - delta);
        if (state.RemainingSeconds <= StatusTickTolerance)
        {
            hero.RemoveStatusEffect(HeroState.StatusEffectKind.Freeze);
        }
    }

    /// <summary>
    /// 施加中毒；重复命中刷新伤害和跳数，但保留当前下一跳相位。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <param name="attack">固定伤害、跳数和秒间隔配置。</param>
    public void ApplyPoison(int heroId, AttackConfig attack)
    {
        HeroState hero = this.GetModel<HeroModel>().GetState(heroId);
        ArgumentNullException.ThrowIfNull(attack);
        if (attack.FlatPower.X <= 0 || attack.FlatPower.X != attack.FlatPower.Y ||
            !float.IsFinite(attack.FlatPower.X) || attack.HitBudget <= 0 ||
            !float.IsFinite(attack.RepeatIntervalSeconds) || attack.RepeatIntervalSeconds <= 0)
        {
            throw new ArgumentException("中毒需要固定正伤害、正跳数和正秒间隔。", nameof(attack));
        }

        if (hero.IsDepleted)
        {
            return;
        }

        HeroState.StatusEffectState state = hero.GetStatusEffect(HeroState.StatusEffectKind.Poison);
        bool firstApplication = state == null;
        state ??= new HeroState.StatusEffectState(HeroState.StatusEffectKind.Poison);
        state.Damage = (int)attack.FlatPower.X;
        state.RemainingTicks = attack.HitBudget;
        state.TickIntervalSeconds = attack.RepeatIntervalSeconds;
        if (firstApplication)
        {
            state.TickLeftSeconds = attack.RepeatIntervalSeconds;
        }

        hero.SetStatusEffect(state);
    }

    /// <summary>
    /// 推进中毒并返回本步应结算的持续伤害。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    /// <param name="delta">游戏秒数；暂停不推进。</param>
    /// <returns>本步到期的固定伤害总和。</returns>
    public int AdvancePoison(int heroId, float delta)
    {
        HeroState hero = this.GetModel<HeroModel>().GetState(heroId);
        if (!float.IsFinite(delta) || delta <= 0 || hero.IsDepleted)
        {
            return 0;
        }

        HeroState.StatusEffectState state = hero.GetStatusEffect(HeroState.StatusEffectKind.Poison);
        if (state == null)
        {
            return 0;
        }

        state.TickLeftSeconds -= delta;
        int damage = 0;
        while (state.TickLeftSeconds <= StatusTickTolerance && state.RemainingTicks > 0)
        {
            damage += state.Damage;
            state.RemainingTicks--;
            state.TickLeftSeconds += state.TickIntervalSeconds;
        }

        if (state.RemainingTicks <= 0)
        {
            hero.RemoveStatusEffect(HeroState.StatusEffectKind.Poison);
        }

        return damage;
    }

    /// <summary>
    /// 离关时清空全部参战英雄的效果。
    /// </summary>
    public void ClearAll()
    {
        foreach (HeroState hero in this.GetModel<HeroModel>().Heroes.Values)
        {
            hero.ClearStatusEffects();
        }
    }

    /// <summary>
    /// 死亡或重建时仅清空指定英雄的效果。
    /// </summary>
    /// <param name="heroId">参战英雄编号。</param>
    public void ClearAll(int heroId)
    {
        this.GetModel<HeroModel>().GetState(heroId).ClearStatusEffects();
    }

    /// <inheritdoc />
    protected override void OnDeinit()
    {
        ClearAll();
    }
}
