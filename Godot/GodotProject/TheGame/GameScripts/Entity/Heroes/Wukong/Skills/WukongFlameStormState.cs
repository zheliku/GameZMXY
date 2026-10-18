using System;
using GameConfig.Skills;
using GameFramework.Fsm;
using GameLogic.Heroes.Body;
using GameLogic.Skills;

namespace GameLogic.Heroes.Wukong;

/// <summary>烈焰风暴：地面定身、占用空中跳跃，三段旋转攻击向悟空两侧击退。</summary>
public sealed class WukongFlameStormState : AbstractHeroSkillState
{
    private const string AnimationName = "skills/flame_storm"; // 本招的原生动画资源名。

    /// <inheritdoc />
    protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
    {
        base.Enter(fsm, body);
        body.ActionData.LastGroundSegment = -1;
        if (body.ActionData.JumpCount == 1)
            body.ActionData.JumpCount = 2;
        BeginAnimation(body, AnimationName);
    }

    /// <summary>动画每段创建独立判定；中心击退与取整是本招规则。</summary>
    /// <param name="body">悟空身体。</param>
    public void BeginHit(IHeroBody body)
    {
        var actor = (ISkillActor)body;
        actor.NewSkillAttack(SkillHitId.FlameStorm,
            Math.Round(AttackPower(body, SkillHitId.FlameStorm), MidpointRounding.AwayFromZero), actor.SkillPosition.X);
    }

    /// <inheritdoc />
    protected override void UpdateSkill(IHeroBody body, float dt)
    {
        if (body.OnFloor)
            Stand(body);
    }
}
