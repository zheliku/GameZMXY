using System;
using GameConfig.Skills;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Battle;
using GameLogic.Heroes.Body;
using GameLogic.Skills;
using Godot;

namespace GameLogic.Heroes.Wukong;

/// <summary>火焰突刺：奔跑转突刺，四次独立命中与逐段减速，结束后衔接地面连段。</summary>
public sealed class WukongFlameThrustState : AbstractHeroSkillState
{
    private const string AnimationName = "skills/flame_thrust"; // 本招的原生动画资源名。

    /// <inheritdoc />
    protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
    {
        base.Enter(fsm, body);
        body.ActionData.LastGroundSegment = 0;
        body.ActionData.Running = false;
        BeginAnimation(body, AnimationName);
    }

    /// <summary>起手沿奔跑方向突刺；速度在动作轨道中编辑。</summary>
    /// <param name="body">悟空身体。</param>
    /// <param name="speed">普通突刺速度，px/s。</param>
    /// <param name="musouSpeed">无双突刺速度，px/s。</param>
    public void BeginThrust(IHeroBody body, float speed, float musouSpeed)
    {
        int direction = body.Velocity.X > 0 ? 1 : -1;
        body.Velocity = new Vector2(direction *
            (body.GetModel<HeroModel>().WsActive.Value ? musouSpeed : speed), body.Velocity.Y);
        BeginHit(body);
    }

    /// <summary>每个段点更换攻击包并衰减横向速度；倍率由动画键提供。</summary>
    /// <param name="body">悟空身体。</param>
    /// <param name="multiplier">本次横速倍率。</param>
    public void ContinueThrust(IHeroBody body, float multiplier)
    {
        body.Velocity = new Vector2(body.Velocity.X * multiplier, body.Velocity.Y);
        BeginHit(body);
    }

    /// <summary>先按原版四舍五入基础威力，再由实体应用无双倍率。</summary>
    /// <param name="body">悟空身体。</param>
    private static void BeginHit(IHeroBody body) => ((ISkillActor)body).NewSkillAttack(
        SkillHitId.FlameThrust, Math.Round(AttackPower(body, SkillHitId.FlameThrust), MidpointRounding.AwayFromZero));
}
