using GameConfig.Skills;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Heroes.Body;
using GameLogic.Input;
using GameLogic.Skills;
using Godot;

namespace GameLogic.Heroes.Wukong;

/// <summary>烈焰风暴：地面定身、占用空中跳跃次数，并逐段更新旋转攻击判定。</summary>
public sealed class WukongFlameStormState : AbstractHeroSkillState
{

    /// <inheritdoc />
    protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
    {
        base.Enter(fsm, body);
        var configuration = body.GetModel<ConfigurationModel>();
        var action = configuration.Tables.TbSkillActionConfig.Get(SkillId.FlameStorm);
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
        if (body.OnFloor)
            Stand(body);
        var actor = (ISkillActor)body;
        foreach (var entry in AdvanceTimeline(dt, body.OnFloor))
        {
            if (entry.Kind == SkillTimelineKind.NewAttackId)
                actor.NewSkillAttack(body.GetModel<ConfigurationModel>().Tables.TbSkillActionConfig
                    .Get(SkillId.FlameStorm).BodyHitId);
            else if (entry.Kind == SkillTimelineKind.CompleteActor)
                SkillData.Completed = true;
        }
        actor.SetSkillAnimationTime(SkillTime);
    }
}
