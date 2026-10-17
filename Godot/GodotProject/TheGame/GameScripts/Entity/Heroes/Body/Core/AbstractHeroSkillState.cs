using System;
using System.Collections.Generic;
using GameConfig.Skills;
using GameLogic.Config;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Input;
using GameLogic.Skills;

namespace GameLogic.Heroes.Body;

/// <summary>具名技能状态共用的身体驱动；具体技能负责自己的进入规则、阶段和移动。</summary>
public abstract class AbstractHeroSkillState : AbstractHeroBodyState
{
    private IReadOnlyList<SkillTimelineConfig> m_Events; // 本招本次使用的只读事件序列。
    private int m_NextEvent; // 下一条尚未交给具体状态的事件。
    private double m_FreezeRemaining; // 剩余冻结游戏秒数。

    /// <summary>本次动画局部秒数。</summary>
    protected double SkillTime { get; private set; }

    /// <summary>具体状态等待独立效果完成时暂停局部时间。</summary>
    protected bool TimelinePaused { get; set; }

    /// <summary>借用模型拥有的本次施放数据，只在本状态活动期间有效。</summary>
    protected SkillData SkillData { get; set; }

    /// <summary>旧状态退出后借用本次施放数据，具体状态负责起手。</summary>
    /// <param name="fsm">身体状态机。</param>
    /// <param name="body">英雄表现。</param>
    protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body) =>
        SkillData = body.GetSystem<SkillSystem>().BodySkillData ??
            throw new InvalidOperationException("进入技能状态前必须先成功激活技能。");

    /// <summary>由 GGF 物理帧推进当前招式，完成后回到移动状态；后续输入由具体技能状态读取。</summary>
    /// <param name="fsm">身体状态机。</param>
    /// <param name="body">英雄表现。</param>
    /// <param name="dt">游戏秒数。</param>
    protected override void Tick(IFsm<IHeroBody> fsm, IHeroBody body, float dt)
    {
        if (SkillData.Completed)
        {
            ChangeToLocomotion(fsm, body);
            return;
        }
        body.GetSystem<InputSystem>().ConsumeAttack(body.ActionData);
        if (!SkillData.GravitySuspended)
            ApplyGravity(body, dt);
        AdvanceSkill(body, dt);
        if (SkillData.Completed)
            ChangeToLocomotion(fsm, body);
    }

    /// <summary>离开身体状态时释放本次表现与施放数据；多阶段技能转交数据时由具体状态处理。</summary>
    /// <param name="fsm">身体状态机。</param>
    /// <param name="body">英雄表现。</param>
    /// <param name="isShutdown">框架是否关停。</param>
    protected override void Leave(IFsm<IHeroBody> fsm, IHeroBody body, bool isShutdown)
    {
        body.GetSystem<SkillSystem>().ReleaseBody(SkillData, (ISkillActor)body);
        SkillData = null;
    }

    /// <summary>开始动画技能共用的表现与秒制无敌，不处理具体招式规则。</summary>
    /// <param name="body">本次身体。</param>
    /// <param name="action">共享动作定义。</param>
    protected void BeginAnimation(IHeroBody body, SkillActionConfig action)
    {
        m_Events = body.GetModel<ConfigurationModel>().GetSkillTimeline(SkillData.Id, SkillEffectId.None);
        m_NextEvent = 0;
        m_FreezeRemaining = SkillTime = 0;
        TimelinePaused = false;
        if (action.InvincibleDurationSeconds > 0)
            body.GetSystem<SkillSystem>().GrantInvulnerability(body.ActionData.Time + action.InvincibleDelaySeconds,
                action.InvincibleDurationSeconds);
        ((ISkillActor)body).BeginSkillAnimation(action);
    }

    /// <summary>推进本招的具体流程和后续输入；独立特效由自己的节点推进。</summary>
    /// <param name="body">本次身体。</param>
    /// <param name="dt">本帧游戏秒数。</param>
    protected virtual void AdvanceSkill(IHeroBody body, float dt) { }

    /// <summary>产出本帧到期事件；处理者可在事件之间暂停、冻结或跳转。</summary>
    /// <param name="dt">本次游戏秒数。</param>
    /// <param name="onFloor">本帧落地事实，用于有截止时间的落地事件。</param>
    /// <returns>到期事件，由招式状态解释。</returns>
    protected IEnumerable<SkillTimelineConfig> AdvanceTimeline(float dt, bool onFloor)
    {
        double remaining = dt;
        while (m_NextEvent < m_Events.Count && !TimelinePaused)
        {
            var entry = m_Events[m_NextEvent];
            // 同时点先全部产出，再消耗冻结时间；低帧率也保留事件顺序。
            if (entry.AtSeconds > SkillTime + .0001)
            {
                if (m_FreezeRemaining > 0)
                {
                    double frozen = Math.Min(remaining, m_FreezeRemaining);
                    m_FreezeRemaining -= frozen;
                    remaining -= frozen;
                }
                if (remaining <= 0)
                    yield break;
                double step = Math.Min(remaining, entry.AtSeconds - SkillTime);
                SkillTime += step;
                remaining -= step;
                if (entry.AtSeconds > SkillTime + .0001)
                    yield break;
            }
            if (entry.Condition == SkillTimelineCondition.OnFloor && !onFloor && SkillTime <= entry.UntilSeconds + .0001)
            {
                SkillTime += remaining;
                remaining = 0;
                if (SkillTime <= entry.UntilSeconds + .0001)
                    yield break;
            }
            m_NextEvent++;
            yield return entry;
        }
    }

    /// <summary>冻结后续时间，同时点其他事件仍会产出。</summary>
    /// <param name="seconds">冻结的游戏秒数。</param>
    protected void FreezeTimeline(float seconds) => m_FreezeRemaining = seconds;

    /// <summary>跳过目标时点之前的事件，保留目标时点自身的事件。</summary>
    /// <param name="seconds">目标局部秒数。</param>
    protected void SeekTimeline(float seconds)
    {
        SkillTime = seconds;
        while (m_NextEvent < m_Events.Count && m_Events[m_NextEvent].AtSeconds < seconds - .0001)
            m_NextEvent++;
    }
}
