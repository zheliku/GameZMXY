using GameConfig.Skills;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Heroes.Body;
using GameLogic.Input;
using GameLogic.Skills;
using Godot;

namespace GameLogic.Heroes.Wukong;

/// <summary>火魔斩：起跳、寻敌连斩或直接下冲、落地特效等待与收招由本状态完成。</summary>
public sealed class WukongFireDemonState : AbstractHeroSkillState
{
    private SkillTimeline m_Timeline; // 本次连斩的秒制游标，可在寻敌后冻结或跳转。
    private Vector2? m_Target; // 本次寻敌得到的位置，不保存怪物节点。
    private bool m_LandingSpawned; // 本次最多进入一次落地等待。

    /// <inheritdoc />
    protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
    {
        base.Enter(fsm, body);
        var configuration = body.GetModel<ConfigurationModel>();
        var action = configuration.Tables.TbSkillActionConfig.Get(SkillId.FireDemonSlash);
        m_Target = null;
        m_LandingSpawned = false;
        body.ActionData.LastGroundSegment = -1;
        body.ActionData.AttackReadyAt = body.ActionData.Time + action.NormalAttackBlockSeconds;
        m_Timeline = new(configuration.GetSkillTimeline(SkillId.FireDemonSlash, SkillEffectId.None));
        BeginAnimation(body, action);
        AdvanceSkill(body, 0);
    }

    /// <inheritdoc />
    protected override void AdvanceSkill(IHeroBody body, float dt)
    {
        foreach (var entry in m_Timeline.Advance(dt, body.OnFloor))
        {
            if (Matches(entry.Condition, body))
                ProcessEvent(entry, body);
            if (Execution.Completed)
                break;
        }
        ((ISkillActor)body).SetSkillAnimationTime(m_Timeline.Elapsed);
    }

    /// <summary>只解释火魔斩的寻敌、落地与兜底条件。</summary>
    /// <param name="condition">本条事件条件。</param>
    /// <param name="body">悟空身体。</param>
    /// <returns>满足本招当前阶段时为真。</returns>
    private bool Matches(SkillTimelineCondition condition, IHeroBody body) => condition switch
    {
        SkillTimelineCondition.Always => true,
        SkillTimelineCondition.TargetFound => m_Target.HasValue,
        SkillTimelineCondition.TargetMissing => !m_Target.HasValue,
        SkillTimelineCondition.OnFloor => body.OnFloor,
        SkillTimelineCondition.LandingMissing => !m_LandingSpawned,
        _ => false
    };

    /// <summary>执行本招的阶段转换与运动；公共时间游标不负责这些规则。</summary>
    /// <param name="entry">到期事件。</param>
    /// <param name="body">悟空身体。</param>
    private void ProcessEvent(SkillTimelineConfig entry, IHeroBody body)
    {
        var actor = (ISkillActor)body;
        switch (entry.Kind)
        {
            case SkillTimelineKind.SuspendGravityAndInput:
                Execution.GravitySuspended = true;
                Execution.InputLocked = true;
                break;
            case SkillTimelineKind.StopMotion:
                body.Velocity = Vector2.Zero;
                break;
            case SkillTimelineKind.SpawnEffect:
                actor.SpawnSkillEffect(entry.EffectId, Execution,
                    entry.Anchor == SkillEffectAnchor.SelectedTarget ? m_Target.Value : actor.SkillPosition);
                break;
            case SkillTimelineKind.SetVelocity:
                var velocity = body.GetModel<GameLogic.Battle.HeroModel>().WsActive.Value ? entry.MusouVelocity : entry.Velocity;
                velocity.X *= actor.Facing;
                body.Velocity = velocity;
                break;
            case SkillTimelineKind.SearchTarget:
                m_Target = actor.FindSkillTarget(entry.TargetRange, entry.ViewportInset);
                break;
            case SkillTimelineKind.TeleportToTarget:
                actor.SkillPosition = m_Target.Value;
                m_Timeline.Freeze(entry.ActionLockSeconds);
                break;
            case SkillTimelineKind.SeekActor:
                m_Timeline.Seek(entry.SeekSeconds);
                break;
            case SkillTimelineKind.SpawnLandingAndPauseActor:
                // 本次等待只捕获本次游标；旧特效回调不能改变池复用后的新招式。
                m_LandingSpawned = true;
                m_Timeline.Paused = true;
                body.Velocity = Vector2.Zero;
                var timeline = m_Timeline;
                float recoveryAt = 0;
                foreach (var effectEvent in body.GetModel<ConfigurationModel>()
                    .GetSkillTimeline(SkillId.FireDemonSlash, entry.EffectId))
                    if (effectEvent.Kind == SkillTimelineKind.FinishLandingEffect)
                        recoveryAt = effectEvent.SeekSeconds;
                actor.SpawnSkillEffect(entry.EffectId, Execution, actor.SkillPosition, () =>
                {
                    timeline.Seek(recoveryAt);
                    timeline.Paused = false;
                });
                break;
            case SkillTimelineKind.RestoreGravityAndInput:
                Execution.GravitySuspended = false;
                Execution.InputLocked = false;
                break;
            case SkillTimelineKind.CompleteActor:
                Execution.Completed = true;
                break;
        }
    }
}
