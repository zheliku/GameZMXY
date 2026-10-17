using GameConfig.Skills;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Config;
using GameLogic.Heroes.Body;
using GameLogic.Input;
using GameLogic.Skills;
using Godot;

namespace GameLogic.Heroes.Wukong;

/// <summary>火焰突刺：奔跑转突刺，四次独立命中与逐段减速，收招后衔接地面连段。</summary>
public sealed class WukongFlameThrustState : AbstractHeroSkillState
{

    /// <inheritdoc />
    protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
    {
        base.Enter(fsm, body);
        var configuration = body.GetModel<ConfigurationModel>();
        var action = configuration.Tables.TbSkillActionConfig.Get(SkillId.FlameThrust);
        body.ActionData.LastGroundSegment = 0;
        body.ActionData.Running = false;
        BeginAnimation(body, action);
        AdvanceSkill(body, 0);
    }

    /// <summary>按突刺事件开启新判定、冲刺、减速与收招，不由公共系统解释本招。</summary>
    /// <param name="body">悟空身体。</param>
    /// <param name="dt">本帧游戏秒数。</param>
    protected override void AdvanceSkill(IHeroBody body, float dt)
    {
        var actor = (ISkillActor)body;
        foreach (var entry in AdvanceTimeline(dt, body.OnFloor))
        {
            switch (entry.Kind)
            {
                case SkillTimelineKind.NewAttackId:
                    actor.NewSkillAttack(body.GetModel<ConfigurationModel>().Tables.TbSkillActionConfig
                        .Get(SkillId.FlameThrust).BodyHitId);
                    break;
                case SkillTimelineKind.SetVelocityX:
                    int direction = body.Velocity.X > 0 ? 1 : -1;
                    float speed = body.GetModel<GameLogic.Battle.HeroModel>().WsActive.Value
                        ? entry.MusouVelocity.X : entry.Velocity.X;
                    body.Velocity = new Vector2(direction * speed, body.Velocity.Y);
                    break;
                case SkillTimelineKind.MultiplyVelocityX:
                    body.Velocity = new Vector2(body.Velocity.X * entry.XMultiplier, body.Velocity.Y);
                    break;
                case SkillTimelineKind.CompleteActor:
                    SkillData.Completed = true;
                    break;
            }
            if (SkillData.Completed)
                break;
        }
        actor.SetSkillAnimationTime(SkillTime);
    }
}
