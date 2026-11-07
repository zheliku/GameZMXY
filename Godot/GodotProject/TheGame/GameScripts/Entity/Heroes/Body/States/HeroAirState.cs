using GameFramework.Fsm;
using GameLogic.Architecture;
using GameLogic.Heroes.Body;
using GameLogic.Input;

namespace GameLogic.Heroes.Body;

/// <summary>
/// 空中：起跳上升 / 二段跳 / 下落。空中保留横向控制、可二段跳、可起手角色指定的空中普攻；
/// 落地进 Ground；仅空中普攻清空地面连段，普通跳跃不清空。
/// 二段跳按原生结束标记完成翻身再切下落，不在顶点截断翻身。
/// </summary>
public sealed class HeroAirState : AbstractHeroBodyState
{
    /// <summary>
    /// 处理空中攻击、二段跳、转向与跳跃/下落动画。
    /// </summary>
    /// <param name="fsm">所属身体状态机。</param>
    /// <param name="body">英雄宿主。</param>
    /// <param name="dt">游戏物理帧秒数。</param>
    protected override void Tick(IFsm<IHeroBody> fsm, IHeroBody body, float dt)
    {
        if (TryStartSkill(fsm, body))
        {
            return;
        }

        HandleJumpKeyCombinations(body);
        if (body.ActionData.AttackRequested)
        {
            body.ActionData.AttackRequested = false;
            ChangeState<HeroAttackState>(fsm);
            return;
        }

        if (TryJump(body, dt))
        {
            body.RestartAnim(HeroAnims.Jump2);
        }
        if (!IsAirborne(body))
        {
            ChangeState<HeroGroundState>(fsm);
            return;
        }

        ApplyGravity(body, dt);
        ApplyHorizontalMovement(body);
        body.PlayAnim(body.ActionData.JumpCount >= 2
            ? body.IsAnimationComplete(HeroAnims.Jump2) ? HeroAnims.Fall : HeroAnims.Jump2
            : SelectAirAnimation(body));
    }

    /// <inheritdoc />
    public override bool TryGetActionInput(IHeroBody body, out bool attacking, out bool airborne)
    {
        attacking = false;
        airborne = IsAirborne(body);
        return true;
    }
}
