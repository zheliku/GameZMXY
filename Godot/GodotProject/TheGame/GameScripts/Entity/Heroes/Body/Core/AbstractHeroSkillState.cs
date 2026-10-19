using System;
using GameConfig.Skills;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Config;
using GameLogic.Input;
using GameLogic.Skills;

namespace GameLogic.Heroes.Body;

/// <summary>技能共用的身体生命周期；原生动画决定时点，具体状态决定招式规则。</summary>
public abstract class AbstractHeroSkillState : AbstractHeroBodyState
{
    private SkillId m_SkillId; // 本状态进入时的技能，取消后不借用下一招的编号。
    private bool m_AnimationCompleted; // 方法轨道只登记完成，退出在动画调用返回后执行。

    /// <summary>当前阶段是否暂停身体重力。</summary>
    protected bool GravitySuspended { get; set; }
    /// <summary>等待独立效果时暂停本体动画；不暂停效果或游戏。</summary>
    protected bool AnimationPaused { get; set; }

    /// <inheritdoc />
    protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
    {
        m_SkillId = body.GetModel<HeroModel>().ActiveSkillId ??
            throw new InvalidOperationException("进入技能状态前必须先成功激活技能。");
        m_AnimationCompleted = GravitySuspended = AnimationPaused = false;
    }

    /// <inheritdoc />
    protected override void Tick(IFsm<IHeroBody> fsm, IHeroBody body, float dt)
    {
        if (body.GetModel<HeroModel>().ActiveSkillId != m_SkillId)
        {
            ChangeToLocomotion(fsm, body);
            return;
        }
        body.GetSystem<InputSystem>().ConsumeAttack(body.ActionData);
        if (!GravitySuspended)
            ApplyGravity(body, dt);
        UpdateSkill(body, dt);
    }

    /// <summary>状态决策完成后每物理帧推进一次，避免 GGF 同帧状态跳转重复推进动画。</summary>
    /// <param name="fsm">所属身体状态机。</param>
    /// <param name="body">英雄表现。</param>
    /// <param name="dt">游戏秒数。</param>
    public void AdvanceAnimation(IFsm<IHeroBody> fsm, IHeroBody body, float dt)
    {
        if (body.GetModel<HeroModel>().ActiveSkillId != m_SkillId)
            return;
        if (!AnimationPaused)
            ((ISkillActor)body).AdvanceSkillAnimation(dt);
        // 原生方法回调不能在 AnimationPlayer 遍历轨道时更换动画或释放状态。
        AfterAnimation(body);
        if (m_AnimationCompleted)
            ChangeToLocomotion(fsm, body);
    }

    /// <summary>由动画末尾的方法键请求收招；实际迁移在推进返回后执行。</summary>
    public void CompleteAnimation() => m_AnimationCompleted = true;

    /// <inheritdoc />
    protected override void Leave(IFsm<IHeroBody> fsm, IHeroBody body, bool isShutdown)
    {
        ((ISkillActor)body).EndSkillAnimation();
        body.GetSystem<SkillSystem>().EndSkill(m_SkillId);
        m_AnimationCompleted = GravitySuspended = AnimationPaused = false;
    }

    /// <summary>应用数值表中的普攻冷却后播放本招的原生动画。</summary>
    /// <param name="body">英雄身体。</param>
    /// <param name="animation">具体角色的动画名称。</param>
    protected void BeginAnimation(IHeroBody body, string animation)
    {
        var definition = body.GetModel<ConfigurationModel>().Tables.TbSkillConfig.Get(m_SkillId);
        if (definition.NormalAttackBlockSeconds > 0)
            body.ActionData.AttackReadyAt = body.ActionData.Time + definition.NormalAttackBlockSeconds;
        ((ISkillActor)body).BeginSkillAnimation(animation);
    }

    /// <summary>读取本招固定数值，具体状态决定其取整顺序与击退方向。</summary>
    /// <param name="body">英雄身体。</param>
    /// <param name="hit">伤害阶段。</param>
    /// <returns>乘无双之前的威力。</returns>
    protected static double AttackPower(IHeroBody body, SkillHitId hit)
    {
        var definition = body.GetModel<ConfigurationModel>().Tables.TbAttackConfig.Get((int)hit);
        return body.GetModel<HeroModel>().Power.Value * (double)definition.PowerScale.X + definition.FlatPower.X;
    }

    /// <summary>具体招式逐帧运动和阶段条件。</summary>
    /// <param name="body">英雄身体。</param>
    /// <param name="dt">游戏秒数。</param>
    protected virtual void UpdateSkill(IHeroBody body, float dt) { }

    /// <summary>轨道回调返回后执行阶段切换。</summary>
    /// <param name="body">英雄身体。</param>
    protected virtual void AfterAnimation(IHeroBody body) { }
}
