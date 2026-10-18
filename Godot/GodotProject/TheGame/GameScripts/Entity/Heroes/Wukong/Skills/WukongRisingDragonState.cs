using GameConfig.Skills;
using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Heroes.Body;
using GameLogic.Input;
using GameLogic.Skills;
using Godot;

namespace GameLogic.Heroes.Wukong;

/// <summary>升龙斩：占用空中跳跃，保留起手方向；判定与收招时点由本招动画调用。</summary>
public sealed class WukongRisingDragonState : AbstractHeroSkillState
{
    private const string AnimationName = "skills/rising_dragon"; // 本招的原生动画资源名。

    private int m_MoveAxis; // 松开后本次出招不再恢复横移。

    /// <inheritdoc />
    protected override void Enter(IFsm<IHeroBody> fsm, IHeroBody body)
    {
        base.Enter(fsm, body);
        m_MoveAxis = body.ActionData.MoveAxis;
        body.ActionData.LastGroundSegment = -1;
        if (body.ActionData.JumpCount == 1)
            body.ActionData.JumpCount = 2;
        BeginAnimation(body, AnimationName);
    }

    /// <summary>原生方法轨道开启本招的新判定，保留原版不预先取整的威力。</summary>
    /// <param name="body">悟空身体。</param>
    public void BeginHit(IHeroBody body) => ((ISkillActor)body).NewSkillAttack(
        SkillHitId.RisingDragon, AttackPower(body, SkillHitId.RisingDragon));

    /// <inheritdoc />
    protected override void UpdateSkill(IHeroBody body, float dt)
    {
        var input = body.GetSystem<InputSystem>();
        if (!input.IsDirectionHeld(body.ActionData, m_MoveAxis) || input.WasDirectionReleased(body.ActionData, m_MoveAxis))
            m_MoveAxis = 0;
        float speed = body.ActionData.Running ? body.Params.RunSpeed : body.Params.WalkSpeed;
        body.Velocity = new Vector2(m_MoveAxis * speed * body.MoveSpeedMultiplier, body.Velocity.Y);
    }
}
