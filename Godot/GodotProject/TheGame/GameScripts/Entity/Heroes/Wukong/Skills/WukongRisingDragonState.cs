using GameConfig.Skills;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Heroes.Body;
using GameLogic.Input;
using GameLogic.Skills;
using Godot;

namespace GameLogic.Heroes.Wukong;

/// <summary>升龙斩：占用空中跳跃次数，保留起手方向，松开方向后停止横移并按招式时点收招。</summary>
public sealed class WukongRisingDragonState : AbstractHeroSkillState
{
    private int m_MoveAxis; // 本次起手方向，松开后本招内不再恢复。

    /// <inheritdoc />
    protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
    {
        base.Enter(fsm, body);
        var configuration = body.GetModel<ConfigurationModel>();
        var action = configuration.Tables.TbSkillActionConfig.Get(SkillId.RisingDragon);
        m_MoveAxis = body.ActionData.MoveAxis;
        body.ActionData.LastGroundSegment = -1;
        body.ActionData.AttackReadyAt = body.ActionData.Time + action.NormalAttackBlockSeconds;
        if (action.ConsumesSecondAirJump && body.ActionData.JumpCount == 1)
            body.ActionData.JumpCount = 2;
        BeginAnimation(body, action);
        AdvanceSkill(body, 0);
    }

    /// <inheritdoc />
    protected override void AdvanceSkill(IHeroBody body, float dt)
    {
        var input = body.GetSystem<InputSystem>();
        if (!input.IsDirectionHeld(body.ActionData, m_MoveAxis) || input.WasDirectionReleased(body.ActionData, m_MoveAxis))
            m_MoveAxis = 0;
        float speed = body.ActionData.Running ? body.Params.RunSpeed : body.Params.WalkSpeed;
        body.Velocity = new Vector2(m_MoveAxis * speed * body.MoveSpeedMultiplier, body.Velocity.Y);
        var actor = (ISkillActor)body;
        foreach (var entry in AdvanceTimeline(dt, body.OnFloor))
        {
            if (entry.Kind == SkillTimelineKind.NewAttackId)
                actor.NewSkillAttack(body.GetModel<ConfigurationModel>().Tables.TbSkillActionConfig
                    .Get(SkillId.RisingDragon).BodyHitId);
            else if (entry.Kind == SkillTimelineKind.CompleteActor)
                SkillData.Completed = true;
        }
        actor.SetSkillAnimationTime(SkillTime);
    }
}
