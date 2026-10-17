using GameConfig.Skills;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Heroes.Body;
using GameLogic.Input;
using GameLogic.Skills;
using Godot;

namespace GameLogic.Heroes.Wukong;

/// <summary>烈焰闪：起手移动、生成火焰拖尾、定向闪身、停止与收招均由本状态管理。</summary>
public sealed class WukongBlazingFlashState : AbstractHeroSkillState
{
    private int m_MoveAxis; // 闪身前保留的起手方向。
    private SkillTimeline m_Timeline; // 闪身各阶段的秒制事件。

    /// <inheritdoc />
    protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
    {
        base.Enter(fsm, body);
        var configuration = body.GetModel<ConfigurationModel>();
        var action = configuration.Tables.TbSkillActionConfig.Get(SkillId.BlazingFlash);
        m_MoveAxis = body.ActionData.MoveAxis;
        body.ActionData.LastGroundSegment = -1;
        body.ActionData.AttackReadyAt = body.ActionData.Time + action.NormalAttackBlockSeconds;
        m_Timeline = new(configuration.GetSkillTimeline(SkillId.BlazingFlash, SkillEffectId.None));
        BeginAnimation(body, action);
        AdvanceSkill(body, 0);
    }

    /// <inheritdoc />
    protected override void AdvanceSkill(IHeroBody body, float dt)
    {
        var action = body.GetModel<ConfigurationModel>().Tables.TbSkillActionConfig.Get(SkillId.BlazingFlash);
        if (m_Timeline.Elapsed < action.InvincibleDelaySeconds)
        {
            var input = body.GetSystem<InputSystem>();
            if (!input.IsDirectionHeld(body.ActionData, m_MoveAxis) || input.WasDirectionReleased(body.ActionData, m_MoveAxis))
                m_MoveAxis = 0;
            float speed = body.ActionData.Running ? body.Params.RunSpeed : body.Params.WalkSpeed;
            body.Velocity = new Vector2(m_MoveAxis * speed * body.MoveSpeedMultiplier, body.Velocity.Y);
        }
        var actor = (ISkillActor)body;
        foreach (var entry in m_Timeline.Advance(dt, body.OnFloor))
        {
            switch (entry.Kind)
            {
                case SkillTimelineKind.NewAttackId:
                    actor.NewSkillAttack(action.BodyHitId);
                    break;
                case SkillTimelineKind.SpawnEffect:
                    actor.SpawnSkillEffect(entry.EffectId, Execution, actor.SkillPosition);
                    break;
                case SkillTimelineKind.SetVelocityX:
                    float speed = body.GetModel<GameLogic.Battle.HeroModel>().WsActive.Value
                        ? entry.MusouVelocity.X : entry.Velocity.X;
                    body.Velocity = new Vector2(actor.Facing * speed, body.Velocity.Y);
                    break;
                case SkillTimelineKind.StopMotion:
                    body.Velocity = Vector2.Zero;
                    break;
                case SkillTimelineKind.CompleteActor:
                    Execution.Completed = true;
                    break;
            }
        }
        actor.SetSkillAnimationTime(m_Timeline.Elapsed);
    }
}
